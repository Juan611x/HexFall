using System;
using System.Collections.Generic;
using HexFall.Player.Spells;

namespace HexFall.Player.Actions
{
    // The wizard's action state: Ready, Attack (until the last shot has left and the aim hold is over),
    // Charging or Channelling (a held cast), Parry (while the shield guards) or ParryEmpty (a parry
    // pressed with no charges: the same pose for the guard time, but no shield and no guard).
    // The active spell's SpellCaster decides what a press, a release and the passing time produce; the
    // machine adds the wind-up before a shot leaves and the aim hold after it. It reuses ShieldState for
    // the shield rules and reports the moments the rest of the player reacts to: state changes, shots,
    // channel ticks, hits.
    // A parry press interrupts any cast (parry is defensive; shots not yet fired and held charges or
    // channels are cancelled), while an attack press is refused during a parry (the guard has to finish).
    public sealed class ActionMachine
    {
        private readonly ActionSettings _settings;
        private readonly ShieldState _shield;
        private readonly Queue<(float Due, ProjectileShot Shot)> _pending = new Queue<(float, ProjectileShot)>();
        private SpellCaster _caster;
        private float _enteredAt;
        private float _attackEndsAt;
        private float _now;
        private bool _releasing;

        // Without a caster the wizard casts the Base spell (a single 18 damage bolt, 0.5 s cooldown).
        public ActionMachine(ActionSettings settings, SpellCaster caster = null)
        {
            _settings = settings;
            _shield = new ShieldState(settings.shieldCharges, settings.chargeRechargeSeconds,
                settings.perfectParrySeconds, settings.guardSeconds, settings.partialBlock);
            _caster = caster ?? new SpellCaster(new CastConfig
            {
                Pattern = CastPattern.Single, Damage = 18, CooldownSeconds = 0.5f,
            });
            Subscribe(_caster);
        }

        public ActionStateId Current { get; private set; } = ActionStateId.Ready;

        public bool IsActing => Current != ActionStateId.Ready;

        private bool InParry => Current == ActionStateId.Parry || Current == ActionStateId.ParryEmpty;

        // True while the wizard turns to the crosshair.
        public bool FacesAim =>
            Current == ActionStateId.Attack || Current == ActionStateId.Charging || Current == ActionStateId.Channelling;

        public int MaxShieldCharges => _shield.MaxCharges;

        public event Action<ActionStateId, ActionStateId> StateChanged;

        // A shot leaves the staff, once its wind-up is over.
        public event Action<ProjectileShot> ShotFired;

        public event Action ChannelStarted;

        // Damage of one channel tick.
        public event Action<int> ChannelTicked;

        // Seconds the channel actually ran, whether it ended on release, duration or a cancel.
        public event Action<float> ChannelEnded;

        // Raised for every press the tick sees, whether or not the press is accepted.
        public event Action AttackPressed;

        public event Action ParryPressed;

        public event Action<HitResult> HitResolved;

        public int ShieldCharges(float now) => _shield.Charges(now);

        public bool RefillShield(float now) => _shield.Refill(now);

        // Swaps the spell. A cast in progress is cancelled (its pending shots are dropped).
        public void SetCaster(SpellCaster caster, float now)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            _now = now;
            var casting = Current == ActionStateId.Attack || Current == ActionStateId.Charging
                || Current == ActionStateId.Channelling;
            CancelCast(now);
            Unsubscribe(_caster);
            _caster = caster;
            Subscribe(_caster);
            if (casting) Enter(ActionStateId.Ready, now);
        }

        public void Tick(ActionIntent intent, float now)
        {
            _now = now;
            Expire(now);
            if (intent.ParryPressed) ParryPressed?.Invoke();
            if (intent.AttackPressed) AttackPressed?.Invoke();
            if (intent.ParryPressed && !InParry && _shield.TryRaise(now))
            {
                CancelCast(now);
                Enter(ActionStateId.Parry, now);
            }
            else if (intent.ParryPressed && !InParry && !_shield.IsGuarding(now) && !_shield.HasCharge(now))
            {
                CancelCast(now);
                Enter(ActionStateId.ParryEmpty, now);
            }
            else if (intent.AttackPressed && Current == ActionStateId.Ready && _caster.Press(now))
            {
                Enter(StateFor(_caster.Pattern), now);
            }
            if (intent.AttackReleased) ReleaseCast(now);
        }

        public HitResult ResolveHit(int damage, float now)
        {
            var hit = _shield.Resolve(damage, now);
            HitResolved?.Invoke(hit);
            return hit;
        }

        private static ActionStateId StateFor(CastPattern pattern)
        {
            switch (pattern)
            {
                case CastPattern.Charged: return ActionStateId.Charging;
                case CastPattern.Channel: return ActionStateId.Channelling;
                default: return ActionStateId.Attack;
            }
        }

        // Lets the caster emit what is due, fires the shots whose wind-up is over and leaves the state
        // whose time is over.
        private void Expire(float now)
        {
            _caster.Tick(now);
            DeliverDue(now);
            if (Current == ActionStateId.Attack)
            {
                if (_pending.Count == 0 && !_caster.IsBusy && now >= _attackEndsAt)
                    Enter(ActionStateId.Ready, now);
            }
            else if (Current == ActionStateId.Channelling)
            {
                if (!_caster.IsBusy) Enter(ActionStateId.Ready, now);
            }
            else if (Current == ActionStateId.ParryEmpty && now >= _enteredAt + _settings.guardSeconds)
            {
                Enter(ActionStateId.Ready, now);
            }
            else if (Current == ActionStateId.Parry && !_shield.IsGuarding(now))
            {
                Enter(ActionStateId.Ready, now);
            }
        }

        // A charge leaves at once on release, with no wind-up; a channel simply ends.
        private void ReleaseCast(float now)
        {
            if (Current == ActionStateId.Charging)
            {
                _releasing = true;
                _caster.Release(now);
                _releasing = false;
                Enter(ActionStateId.Attack, now);
                DeliverDue(now);
            }
            else if (Current == ActionStateId.Channelling)
            {
                _caster.Release(now);
                if (!_caster.IsBusy) Enter(ActionStateId.Ready, now);
            }
        }

        private void CancelCast(float now)
        {
            _caster.Cancel(now);
            _pending.Clear();
        }

        // The caster emits a shot at its scheduled time; the machine holds it back for the wind-up.
        private void OnShot(ProjectileShot shot)
        {
            var due = _now + (_releasing ? 0f : _settings.boltDelaySeconds);
            _pending.Enqueue((due, shot));
            _attackEndsAt = due + _settings.faceAimHoldSeconds;
        }

        // A shot leaves the queue before its event, so a throwing listener cannot fire it twice.
        private void DeliverDue(float now)
        {
            // Dequeue outside the ?.Invoke: with no listener the argument would never be evaluated and the
            // loop would spin forever.
            while (_pending.Count > 0 && now >= _pending.Peek().Due)
            {
                var shot = _pending.Dequeue().Shot;
                ShotFired?.Invoke(shot);
            }
        }

        private void OnChannelStarted() => ChannelStarted?.Invoke();

        private void OnChannelTicked(int damage) => ChannelTicked?.Invoke(damage);

        private void OnChannelEnded(float seconds) => ChannelEnded?.Invoke(seconds);

        private void Subscribe(SpellCaster caster)
        {
            caster.ProjectileFired += OnShot;
            caster.ChannelStarted += OnChannelStarted;
            caster.ChannelTicked += OnChannelTicked;
            caster.ChannelEnded += OnChannelEnded;
        }

        private void Unsubscribe(SpellCaster caster)
        {
            caster.ProjectileFired -= OnShot;
            caster.ChannelStarted -= OnChannelStarted;
            caster.ChannelTicked -= OnChannelTicked;
            caster.ChannelEnded -= OnChannelEnded;
        }

        private void Enter(ActionStateId next, float now)
        {
            var from = Current;
            Current = next;
            _enteredAt = now;
            StateChanged?.Invoke(from, next);
        }
    }
}

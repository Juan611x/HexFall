using System;

namespace HexFall.Player.Actions
{
    // The wizard's action state: Ready, Attack (until the bolt has left and the aim hold is over) or
    // Parry (while the shield guards) or ParryEmpty (a parry pressed with no charges: the same pose for
    // the guard time, but no shield and no guard). It reuses AttackCooldown and ShieldState for the rules and
    // reports the moments the rest of the player reacts to: state changes, the bolt leaving, hits.
    // A parry press interrupts an attack (parry is defensive; a bolt not yet fired is cancelled),
    // while an attack press is refused during a parry (the guard has to finish).
    public sealed class ActionMachine
    {
        private readonly ActionSettings _settings;
        private readonly AttackCooldown _cooldown;
        private readonly ShieldState _shield;
        private float _enteredAt;
        private bool _boltFired;

        public ActionMachine(ActionSettings settings)
        {
            _settings = settings;
            _cooldown = new AttackCooldown(settings.attackCooldownSeconds);
            _shield = new ShieldState(settings.shieldCharges, settings.chargeRechargeSeconds,
                settings.perfectParrySeconds, settings.guardSeconds, settings.partialBlock);
        }

        public ActionStateId Current { get; private set; } = ActionStateId.Ready;

        public bool IsActing => Current != ActionStateId.Ready;

        private bool InParry => Current == ActionStateId.Parry || Current == ActionStateId.ParryEmpty;

        // True while the wizard turns to the crosshair.
        public bool FacesAim => Current == ActionStateId.Attack;

        public int MaxShieldCharges => _shield.MaxCharges;

        public event Action<ActionStateId, ActionStateId> StateChanged;

        public event Action BoltFired;

        // Raised for every press the tick sees, whether or not the press is accepted.
        public event Action AttackPressed;

        public event Action ParryPressed;

        public event Action<HitResult> HitResolved;

        public int ShieldCharges(float now) => _shield.Charges(now);

        public bool RefillShield(float now) => _shield.Refill(now);

        public void Tick(ActionIntent intent, float now)
        {
            Expire(now);
            if (intent.ParryPressed) ParryPressed?.Invoke();
            if (intent.AttackPressed) AttackPressed?.Invoke();
            if (intent.ParryPressed && !InParry && _shield.TryRaise(now))
                Enter(ActionStateId.Parry, now);
            else if (intent.ParryPressed && !InParry && !_shield.IsGuarding(now) && !_shield.HasCharge(now))
                Enter(ActionStateId.ParryEmpty, now);
            else if (intent.AttackPressed && Current == ActionStateId.Ready && _cooldown.TryAttack(now))
                Enter(ActionStateId.Attack, now);
        }

        public HitResult ResolveHit(int damage, float now)
        {
            var hit = _shield.Resolve(damage, now);
            HitResolved?.Invoke(hit);
            return hit;
        }

        // Fires the bolt once it is due and leaves the state whose time is over.
        private void Expire(float now)
        {
            if (Current == ActionStateId.Attack)
            {
                if (!_boltFired && now >= _enteredAt + _settings.boltDelaySeconds)
                {
                    _boltFired = true;
                    BoltFired?.Invoke();
                }
                if (now >= _enteredAt + _settings.boltDelaySeconds + _settings.faceAimHoldSeconds)
                    Enter(ActionStateId.Ready, now);
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

        private void Enter(ActionStateId next, float now)
        {
            var from = Current;
            Current = next;
            _enteredAt = now;
            _boltFired = false;
            StateChanged?.Invoke(from, next);
        }
    }
}

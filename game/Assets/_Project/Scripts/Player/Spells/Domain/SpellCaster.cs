using System;

namespace HexFall.Player.Spells
{
    // Per-spell runtime state: the active pattern plus its cooldown. Driven purely by timestamps.
    public sealed class SpellCaster : ICastSink
    {
        private readonly ICastPattern _pattern;
        private float _readyAt = float.NegativeInfinity;
        private float _cooldownStart = float.NegativeInfinity;

        public SpellCaster(CastConfig config)
        {
            Pattern = config.Pattern;
            _pattern = CastPatternFactory.Create(config);
        }

        public CastPattern Pattern { get; }

        public event Action<ProjectileShot> ProjectileFired;
        public event Action ChannelStarted;
        public event Action<int> ChannelTicked;
        public event Action<float> ChannelEnded;

        public bool IsBusy => _pattern.IsBusy;

        // A held cast that waits for its pose (a channel with a wind-up): nothing has been cast yet.
        public bool IsWindingUp => _pattern.IsWindingUp;

        // The adapter reports the cast pose reached: a channel that is winding up starts now.
        public void PoseReached(float now)
        {
            if (Valid(now)) _pattern.PoseReached(now, this);
        }

        // 0..1 progress of the charge in progress; 0 when nothing is charging or the time is not valid.
        public float ChargeLevel(float now) => Valid(now) ? _pattern.ChargeLevel(now) : 0f;

        // When the cooldown of the last cast ends (-Infinity before the first cast).
        public float ReadyAt => _readyAt;

        // When the cooldown of the last cast began (-Infinity before the first cast).
        public float CooldownStart => _cooldownStart;

        // 1 when a cooldown has just begun, draining to 0 when it ends; 0 when none runs or the time is not valid.
        public float CooldownFraction(float now)
        {
            if (!Valid(now)) return 0f;
            var total = _readyAt - _cooldownStart;
            if (float.IsNaN(total) || float.IsInfinity(total) || total <= 0f) return 0f;
            return Math.Min(1f, Math.Max(0f, (_readyAt - now) / total));
        }

        // 1 when a running channel has just begun, draining to 0 at its end; 0 when no channel is running
        // (idle, winding up or not a channel).
        public float ChannelRemainingFraction(float now) => Valid(now) ? _pattern.ChannelRemaining(now) : 0f;

        // A spell swap keeps the cooldown that was running: this caster is not ready before the given time
        // either. Never shortens its own cooldown; a non-finite time is ignored. The start of that cooldown keeps
        // the cooldown fraction right (unknown start: the fraction reads 0 until it ends).
        public void InheritCooldown(float readyAt, float cooldownStart = float.NegativeInfinity)
        {
            if (float.IsNaN(readyAt) || float.IsInfinity(readyAt)) return;
            if (readyAt <= _readyAt) return;
            _readyAt = readyAt;
            _cooldownStart = float.IsNaN(cooldownStart) ? float.NegativeInfinity : cooldownStart;
        }

        public bool IsReady(float now) => !IsBusy && now >= _readyAt - CastTime.Eps;

        // Returns whether the press started a cast.
        public bool Press(float now)
        {
            if (!Valid(now) || !IsReady(now)) return false;
            _pattern.Press(now, this);
            return true;
        }

        public void Release(float now)
        {
            if (Valid(now)) _pattern.Release(now, this);
        }

        public void Tick(float now)
        {
            if (Valid(now)) _pattern.Tick(now, this);
        }

        public void Cancel(float now)
        {
            if (Valid(now)) _pattern.Cancel(now, this);
        }

        private static bool Valid(float t) => !float.IsNaN(t) && !float.IsInfinity(t) && t >= 0f;

        void ICastSink.Fire(ProjectileShot shot) => ProjectileFired?.Invoke(shot);
        void ICastSink.ChannelStart() => ChannelStarted?.Invoke();
        void ICastSink.ChannelTick(int damage) => ChannelTicked?.Invoke(damage);
        void ICastSink.ChannelEnd(float channelledSeconds) => ChannelEnded?.Invoke(channelledSeconds);
        void ICastSink.StartCooldown(float from, float seconds)
        {
            _cooldownStart = from;
            _readyAt = from + seconds;
        }
    }
}

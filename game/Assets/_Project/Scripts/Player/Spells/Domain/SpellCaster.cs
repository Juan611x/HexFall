using System;

namespace HexFall.Player.Spells
{
    // Per-spell runtime state: the active pattern plus its cooldown. Driven purely by timestamps.
    public sealed class SpellCaster : ICastSink
    {
        private readonly ICastPattern _pattern;
        private float _readyAt = float.NegativeInfinity;

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

        // A spell swap keeps the cooldown that was running: this caster is not ready before the given time
        // either. Never shortens its own cooldown; a non-finite time is ignored.
        public void InheritCooldown(float readyAt)
        {
            if (float.IsNaN(readyAt) || float.IsInfinity(readyAt)) return;
            if (readyAt > _readyAt) _readyAt = readyAt;
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
        void ICastSink.StartCooldown(float from, float seconds) => _readyAt = from + seconds;
    }
}

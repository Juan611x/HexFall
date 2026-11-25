using System;

namespace HexFall.Player.Spells
{
    // Per-spell runtime state: the active pattern plus its cooldown. Driven purely by timestamps.
    public sealed class SpellCaster : ICastSink
    {
        private readonly ICastPattern _pattern;
        private float _readyAt = float.NegativeInfinity;

        public SpellCaster(CastConfig config) => _pattern = CastPatternFactory.Create(config);

        public event Action<ProjectileShot> ProjectileFired;
        public event Action ChannelStarted;
        public event Action<int> ChannelTicked;
        public event Action<float> ChannelEnded;

        public bool IsBusy => _pattern.IsBusy;

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

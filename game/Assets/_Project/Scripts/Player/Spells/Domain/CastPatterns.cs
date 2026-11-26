using System;

namespace HexFall.Player.Spells
{
    // Tolerance for float time comparisons (1f + 0.08f vs a 1.08f timestamp).
    internal static class CastTime
    {
        public const float Eps = 1e-4f;
    }

    internal sealed class SinglePattern : ICastPattern
    {
        private readonly CastConfig _c;
        public SinglePattern(CastConfig c) => _c = c;

        public bool IsBusy => false;

        public float ChargeLevel(float now) => 0f;

        public void Press(float now, ICastSink sink)
        {
            sink.StartCooldown(now, _c.CooldownSeconds);
            sink.Fire(new ProjectileShot(0, _c.Damage, 0f, 0f));
        }

        public void Release(float now, ICastSink sink) { }
        public void Tick(float now, ICastSink sink) { }
        public void Cancel(float now, ICastSink sink) { }
    }

    internal sealed class ChargedPattern : ICastPattern
    {
        private readonly CastConfig _c;
        private bool _charging;
        private float _start;

        public ChargedPattern(CastConfig c) => _c = c;

        public bool IsBusy => _charging;

        public float ChargeLevel(float now) =>
            _charging ? ChargeFeedback.Level(now - _start, _c.MaxChargeSeconds) : 0f;

        public void Press(float now, ICastSink sink)
        {
            _charging = true;
            _start = now;
        }

        public void Release(float now, ICastSink sink)
        {
            if (!_charging) return;
            _charging = false;
            var charge = ChargeFeedback.Level(now - _start, _c.MaxChargeSeconds);
            var damage = ChargeDamage.Damage(_c, charge);
            // State and cooldown advance before the event so a throwing subscriber cannot re-fire or skip it.
            sink.StartCooldown(now, _c.CooldownSeconds);
            sink.Fire(new ProjectileShot(0, damage, 0f, charge));
        }

        public void Tick(float now, ICastSink sink) { }

        // A parried charge is lost with no cooldown penalty.
        public void Cancel(float now, ICastSink sink) => _charging = false;
    }

    internal sealed class BurstPattern : ICastPattern
    {
        private readonly CastConfig _c;
        private bool _active;
        private float _start;
        private int _next;

        public BurstPattern(CastConfig c) => _c = c;

        public bool IsBusy => _active;

        public float ChargeLevel(float now) => 0f;

        public void Press(float now, ICastSink sink)
        {
            _active = true;
            _start = now;
            _next = 0;
            Advance(now, sink);
        }

        public void Tick(float now, ICastSink sink)
        {
            if (_active) Advance(now, sink);
        }

        public void Release(float now, ICastSink sink) { }

        public void Cancel(float now, ICastSink sink)
        {
            if (!_active) return;
            _active = false;
            sink.StartCooldown(now, _c.CooldownSeconds);
        }

        // Fires every shot whose scheduled time has passed, so a long frame never skips or doubles one.
        private void Advance(float now, ICastSink sink)
        {
            while (_next < _c.BurstCount)
            {
                var due = _start + _next * _c.BurstIntervalSeconds;
                if (now < due - CastTime.Eps) return;
                var spread = (_next - (_c.BurstCount - 1) * 0.5f) * _c.BurstSpreadDegrees;
                var shot = new ProjectileShot(_next, _c.Damage, spread, 0f);
                _next++;
                if (_next == _c.BurstCount)
                {
                    _active = false;
                    sink.StartCooldown(due, _c.CooldownSeconds);
                }
                sink.Fire(shot);
            }
        }
    }

    // The first tick lands after 1/rate (no instant damage on press), so a full channel
    // deals duration * rate ticks.
    internal sealed class ChannelPattern : ICastPattern
    {
        private readonly CastConfig _c;
        private bool _windingUp;
        private float _pressedAt;
        private bool _active;
        private float _start;
        private int _ticks;

        public ChannelPattern(CastConfig c) => _c = c;

        public bool IsBusy => _active || _windingUp;

        public bool IsWindingUp => _windingUp;

        public float ChargeLevel(float now) => 0f;

        public float ChannelRemaining(float now)
        {
            if (!_active || _c.ChannelDurationSeconds <= 0f) return 0f;
            return Math.Min(1f, Math.Max(0f, 1f - (now - _start) / _c.ChannelDurationSeconds));
        }

        // With a wind-up the press only starts the pose: nothing is cast and no cooldown runs until the pose
        // is reached (or the fallback timeout passes), so an early release or cancel costs nothing.
        public void Press(float now, ICastSink sink)
        {
            if (_c.ChannelWindUpTimeoutSeconds > 0f)
            {
                _windingUp = true;
                _pressedAt = now;
                return;
            }
            Begin(now, sink);
        }

        public void PoseReached(float now, ICastSink sink)
        {
            if (_windingUp) Begin(now, sink);
        }

        private void Begin(float now, ICastSink sink)
        {
            _windingUp = false;
            _active = true;
            _start = now;
            _ticks = 0;
            sink.ChannelStart();
        }

        public void Tick(float now, ICastSink sink)
        {
            if (_windingUp)
            {
                if (now - _pressedAt >= _c.ChannelWindUpTimeoutSeconds - CastTime.Eps) Begin(now, sink);
                return;
            }
            if (!_active) return;
            var elapsed = now - _start;
            EmitTicks(elapsed, sink);
            if (elapsed >= _c.ChannelDurationSeconds - CastTime.Eps)
                End(_start + _c.ChannelDurationSeconds, _c.ChannelDurationSeconds, sink);
        }

        public void Release(float now, ICastSink sink)
        {
            if (_windingUp)
            {
                _windingUp = false; // released before the pose: nothing was cast, nothing to cool down
                return;
            }
            if (!_active) return;
            Tick(now, sink);
            if (_active) End(now, now - _start, sink);
        }

        // Interrupted channels drop their pending ticks.
        public void Cancel(float now, ICastSink sink)
        {
            if (_windingUp)
            {
                _windingUp = false;
                return;
            }
            if (_active) End(now, now - _start, sink);
        }

        private void EmitTicks(float elapsed, ICastSink sink)
        {
            var capped = Math.Min(elapsed, _c.ChannelDurationSeconds);
            var due = (int)Math.Floor(capped * _c.ChannelTicksPerSecond + CastTime.Eps);
            while (_ticks < due)
            {
                _ticks++;
                sink.ChannelTick(_c.ChannelDamagePerTick);
            }
        }

        private void End(float at, float channelled, ICastSink sink)
        {
            _active = false;
            channelled = Math.Max(0f, Math.Min(channelled, _c.ChannelDurationSeconds));
            sink.StartCooldown(at, _c.CooldownSeconds + channelled * _c.ChannelCooldownPerSecond);
            sink.ChannelEnd(channelled);
        }
    }

    public static class CastPatternFactory
    {
        public static ICastPattern Create(CastConfig config)
        {
            var c = config.Sanitized();
            switch (c.Pattern)
            {
                case CastPattern.Charged: return new ChargedPattern(c);
                case CastPattern.Burst: return new BurstPattern(c);
                case CastPattern.Channel: return new ChannelPattern(c);
                default: return new SinglePattern(c);
            }
        }
    }
}

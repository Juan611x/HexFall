using System;
using UnityEngine;

namespace HexFall.Player.Spells
{
    // The flight of one projectile, as plain maths: gravity pulls the velocity down, the curve yaws it
    // around world up, and every step reports the segment the hit sweep has to cover. Semi-implicit
    // Euler (velocity first, then position), so zero gravity and zero curve is a straight line. Drag is
    // exponential, v *= exp(-drag x dt): the fraction of speed lost per second is the same whatever the
    // frame rate, and it can never reverse the velocity. The gravity is an acceleration in m/s^2 (the
    // adapter passes weight x world gravity), so this class never reads the physics settings.
    public sealed class ProjectileFlight
    {
        private const float DegToRad = 0.017453292519943295f;
        // Absorbs float rounding when many small steps add up to the lifetime.
        private const float ExpiryTolerance = 1e-5f;

        private readonly float _gravity;
        private readonly float _drag;
        private readonly float _curveRadiansPerSecond;
        private readonly float _lifetime;

        public ProjectileFlight(Vector3 position, Vector3 velocity, float gravity, float curveDegreesPerSecond, float lifetimeSeconds,
            float drag = 0f)
        {
            if (!IsFinite(position)) throw new ArgumentException("Position must be finite.", nameof(position));
            if (!IsFinite(velocity)) throw new ArgumentException("Velocity must be finite.", nameof(velocity));
            if (!IsFinite(gravity)) throw new ArgumentException("Gravity must be finite.", nameof(gravity));
            if (!IsFinite(curveDegreesPerSecond)) throw new ArgumentException("Curve must be finite.", nameof(curveDegreesPerSecond));
            if (!IsFinite(lifetimeSeconds)) throw new ArgumentException("Lifetime must be finite.", nameof(lifetimeSeconds));
            if (!IsFinite(drag)) throw new ArgumentException("Drag must be finite.", nameof(drag));
            Position = position;
            Velocity = velocity;
            _gravity = gravity;
            _drag = Math.Max(0f, drag);
            _curveRadiansPerSecond = curveDegreesPerSecond * DegToRad;
            _lifetime = Math.Max(0f, lifetimeSeconds);
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        // Seconds flown so far.
        public float Age { get; private set; }

        public bool IsExpired => Age >= _lifetime - ExpiryTolerance;

        // Advances the flight and returns the segment it covered. A step past the lifetime is cut short at
        // it; an expired flight returns an empty segment.
        public (Vector3 From, Vector3 To) Step(float dt)
        {
            if (!IsFinite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt), "dt must be finite and not negative.");
            var from = Position;
            dt = Math.Min(dt, _lifetime - Age);
            if (dt <= 0f) return (from, from);

            var velocity = Velocity;
            if (_drag > 0f) velocity *= (float)Math.Exp(-_drag * dt);
            velocity.y -= _gravity * dt;
            if (_curveRadiansPerSecond != 0f)
            {
                // Yaw by +angle around up, the same sense as Quaternion.AngleAxis(angle, Vector3.up).
                var angle = _curveRadiansPerSecond * dt;
                var cos = (float)Math.Cos(angle);
                var sin = (float)Math.Sin(angle);
                velocity = new Vector3(velocity.x * cos + velocity.z * sin, velocity.y, -velocity.x * sin + velocity.z * cos);
            }

            Velocity = velocity;
            Position = from + velocity * dt;
            Age += dt;
            return (from, Position);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }
}

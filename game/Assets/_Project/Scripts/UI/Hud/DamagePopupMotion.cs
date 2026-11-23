using UnityEngine;

namespace HexFall.UI
{
    // How a damage number moves over its short life: it punches in large and settles, climbs with an
    // ease-out, and fades out at the end. Consecutive hits on the same target drift centre, right, left
    // so their numbers do not stack on top of each other.
    public sealed class DamagePopupMotion
    {
        private readonly float _lifetime;
        private readonly float _riseHeight;
        private readonly float _popScale;
        private readonly float _popSeconds;
        private readonly float _fadeSeconds;
        private readonly float _driftSpacing;

        public DamagePopupMotion(float lifetimeSeconds, float riseHeight, float popScale, float popSeconds,
            float fadeSeconds, float driftSpacing)
        {
            _lifetime = lifetimeSeconds;
            _riseHeight = riseHeight;
            _popScale = popScale;
            _popSeconds = popSeconds;
            _fadeSeconds = fadeSeconds;
            _driftSpacing = driftSpacing;
        }

        public float Rise(float age)
        {
            var left = 1f - Mathf.Clamp01(age / _lifetime);
            return _riseHeight * (1f - left * left);
        }

        public float Scale(float age) =>
            _popSeconds <= 0f ? 1f : Mathf.Lerp(_popScale, 1f, Mathf.Clamp01(age / _popSeconds));

        public float Alpha(float age)
        {
            var fadeFrom = _lifetime - _fadeSeconds;
            if (age <= fadeFrom) return 1f;
            return _fadeSeconds <= 0f ? 0f : 1f - Mathf.Clamp01((age - fadeFrom) / _fadeSeconds);
        }

        public bool IsDone(float age) => age >= _lifetime;

        // Sideways offset for the n-th hit: 0, +spacing, -spacing, then again.
        public float Drift(int sequence)
        {
            switch (sequence % 3)
            {
                case 1: return _driftSpacing;
                case 2: return -_driftSpacing;
                default: return 0f;
            }
        }
    }
}

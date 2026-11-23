using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Maps the vertical speed of a landing to a 0..1 loudness. Landings slower than the minimum speed
    // (a small step, a stair) are silent; from the minimum up the loudness rises linearly from
    // SoftestLoudness (so the softest audible landing is still heard) to 1 at the full speed.
    public static class LandingLoudness
    {
        public const float SoftestLoudness = 0.35f;

        public static float For(float landingSpeed, float minSpeed, float fullSpeed)
        {
            if (float.IsNaN(landingSpeed) || float.IsNaN(minSpeed) || float.IsNaN(fullSpeed)) return 0f;
            if (landingSpeed < minSpeed) return 0f;
            if (fullSpeed <= minSpeed) return 1f;
            var t = Mathf.Clamp01((landingSpeed - minSpeed) / (fullSpeed - minSpeed));
            return SoftestLoudness + (1f - SoftestLoudness) * t;
        }
    }
}

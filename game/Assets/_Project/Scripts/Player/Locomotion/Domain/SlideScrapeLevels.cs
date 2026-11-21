using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Pure helpers for the slide scrape: how loud it starts for a given entry speed, and how it
    // fades out once the slide ends.
    public static class SlideScrapeLevels
    {
        // 0..1 from the planar speed on entering the slide: linear up to fullSpeed. A NaN speed is
        // silent; a fullSpeed without span (zero, negative, NaN) means full loudness.
        public static float SpeedFactor(float entrySpeed, float fullSpeed)
        {
            if (float.IsNaN(entrySpeed)) return 0f;
            if (float.IsNaN(fullSpeed) || fullSpeed <= 0f) return 1f;
            return Mathf.Clamp01(entrySpeed / fullSpeed);
        }

        // Gain multiplier 1..0 while fading out: linear over fadeSeconds. A non-positive duration or a
        // NaN time cuts at once.
        public static float FadeGain(float elapsedSeconds, float fadeSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsNaN(fadeSeconds) || fadeSeconds <= 0f) return 0f;
            return 1f - Mathf.Clamp01(elapsedSeconds / fadeSeconds);
        }
    }
}

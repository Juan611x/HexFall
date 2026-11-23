using UnityEngine;

namespace HexFall.Surfaces
{
    // Deterministic selection helpers: callers inject the random values.
    public static class SurfaceEffectPicker
    {
        // Maps a 0..1 random value to a clip index, or -1 when there are no clips.
        public static int PickIndex(int clipCount, float random01)
        {
            if (clipCount <= 0) return -1;
            if (float.IsNaN(random01)) random01 = 0f;
            int index = (int)(Mathf.Clamp01(random01) * clipCount);
            return Mathf.Min(index, clipCount - 1);
        }

        // Interpolates a range with a 0..1 random value.
        public static float Lerp(Vector2 range, float random01)
        {
            if (float.IsNaN(random01)) return range.x;
            return range.x + (range.y - range.x) * Mathf.Clamp01(random01);
        }
    }
}

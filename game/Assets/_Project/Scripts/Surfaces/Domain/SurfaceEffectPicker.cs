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

        // Like PickIndex but never returns `previous` when there is another clip: the random value is
        // mapped over the remaining clips, uniformly. A previous index outside the range is ignored.
        public static int PickIndexAvoiding(int clipCount, int previous, float random01)
        {
            if (clipCount <= 1 || previous < 0 || previous >= clipCount) return PickIndex(clipCount, random01);
            int slot = PickIndex(clipCount - 1, random01);
            return slot >= previous ? slot + 1 : slot;
        }

        // Interpolates a range with a 0..1 random value.
        public static float Lerp(Vector2 range, float random01)
        {
            if (float.IsNaN(random01)) return range.x;
            return range.x + (range.y - range.x) * Mathf.Clamp01(random01);
        }
    }
}

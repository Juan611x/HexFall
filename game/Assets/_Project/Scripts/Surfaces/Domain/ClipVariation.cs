using System.Collections.Generic;

namespace HexFall.Surfaces
{
    // Remembers the last clip played per effect so the next pick differs from it.
    public sealed class ClipVariation
    {
        private readonly Dictionary<SurfaceEffect, int> _last = new Dictionary<SurfaceEffect, int>();

        // Index of the clip to play, or -1 when the effect has no clips.
        public int Pick(SurfaceEffect effect, float random01)
        {
            if (effect == null || effect.clips == null || effect.clips.Length == 0) return -1;
            _last.TryGetValue(effect, out var previous);
            if (!_last.ContainsKey(effect)) previous = -1;
            var index = SurfaceEffectPicker.PickIndexAvoiding(effect.clips.Length, previous, random01);
            _last[effect] = index;
            return index;
        }
    }
}

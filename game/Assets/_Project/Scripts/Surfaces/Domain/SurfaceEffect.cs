using System;
using UnityEngine;

namespace HexFall.Surfaces
{
    // Clips and randomization ranges for one impact type on a surface.
    [Serializable]
    public class SurfaceEffect
    {
        public SurfaceImpact impact;
        public AudioClip[] clips = new AudioClip[0];
        public Vector2 volumeRange = new Vector2(0.8f, 1f);
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    }
}

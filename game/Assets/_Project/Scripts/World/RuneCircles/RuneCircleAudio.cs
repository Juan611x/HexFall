using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // Layer 1 of a circle's sound: an optional ambient loop emitted by the circle itself (3D, effects
    // volume). The clip is left empty until one is chosen, so the circle is silent. The sound each
    // effect makes when the circle fires belongs to the effect (layer 2).
    public sealed class RuneCircleAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip ambientClip;
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.5f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 20f;

        public AudioClip AmbientClip => ambientClip;

        internal void ConfigureAmbient(AudioClip clip, float volume)
        {
            ambientClip = clip;
            ambientVolume = volume;
        }

        // Creates the looping source; null when there is no ambient clip.
        internal AudioSource Build()
        {
            if (ambientClip == null) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = ambientClip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = SpatialSfx.Scaled(ambientVolume, SpatialSfx.EffectsLevel());
            return source;
        }

        private void Start()
        {
            var source = Build();
            if (source != null) source.Play();
        }
    }
}

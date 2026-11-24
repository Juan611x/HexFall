using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // Layer 1 of a circle's sound: an optional ambient loop emitted by the circle itself (3D, effects
    // volume, linear rolloff so it is only heard close to the circle). With no clip the circle is silent.
    // The loop starts at a random point so several circles of one kind do not play in phase. The sound
    // each effect makes when the circle fires belongs to the effect (layer 2).
    public sealed class RuneCircleAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip ambientClip;
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.5f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 12f;

        public AudioClip AmbientClip => ambientClip;
        public float AmbientVolume => ambientVolume;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        // Where in a clip of the given length the loop starts, for a random value in [0, 1).
        internal static float StartOffset(float clipLength, float random01) =>
            clipLength > 0f && random01 >= 0f && random01 < 1f ? clipLength * random01 : 0f;

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
            if (source == null) return;
            source.time = StartOffset(source.clip.length, Random.value);
            source.Play();
        }
    }
}

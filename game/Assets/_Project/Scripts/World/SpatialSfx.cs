using System;
using HexFall.UI;
using UnityEngine;

namespace HexFall.World
{
    // Fire-and-forget 3D one-shots for world objects (rune circles, pickups). The level is the base
    // volume of the component times the effects level the player saved in the menu, like the wizard SFX.
    // Requested is raised before anything plays (with the unscaled volume) so tests need no audio device.
    public static class SpatialSfx
    {
        public static event Action<AudioClip, Vector3, float> Requested;

        public const float DefaultMinDistance = 4f;
        public const float DefaultMaxDistance = 40f;

        // The effects level the player saved in the menu's sound settings.
        public static float EffectsLevel() => new SoundSettings(new PlayerPrefsSoundSettingsStore().Load()).EffectsMultiplier;

        public static float Scaled(float volume, float effectsLevel) => Mathf.Clamp01(volume * effectsLevel);

        public static void Play(AudioClip clip, Vector3 position, float volume,
            float minDistance = DefaultMinDistance, float maxDistance = DefaultMaxDistance)
        {
            if (clip == null) return;
            Requested?.Invoke(clip, position, volume);
            if (!Application.isPlaying) return;

            var go = new GameObject("SpatialSfx(" + clip.name + ")");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = Scaled(volume, EffectsLevel());
            source.Play();
            UnityEngine.Object.Destroy(go, clip.length + 0.1f);
        }
    }
}

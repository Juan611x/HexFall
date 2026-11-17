using System;
using UnityEngine;

namespace HexFall.UI
{
    [CreateAssetMenu(fileName = "MenuAudioLibrary", menuName = "HexFall/Menu Audio Library")]
    public sealed class MenuAudioLibrary : ScriptableObject
    {
        [Serializable]
        public struct Sound
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        public Sound hover = new Sound { volume = 0.35f };
        public Sound click = new Sound { volume = 0.6f };
        public Sound back = new Sound { volume = 0.6f };
        public AudioClip[] spellClips = new AudioClip[0];
        [Range(0f, 1f)] public float spellVolume = 0.8f;
    }
}

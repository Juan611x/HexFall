using System;
using HexFall.Player.Actions;
using UnityEngine;

namespace HexFall.Player
{
    [CreateAssetMenu(fileName = "WizardSfxLibrary", menuName = "HexFall/Wizard Sfx Library")]
    public sealed class WizardSfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public WizardSfxCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public bool TryGet(WizardSfxCue cue, out AudioClip clip, out float volume)
        {
            foreach (var entry in entries)
            {
                if (entry.cue != cue || entry.clip == null) continue;
                clip = entry.clip;
                volume = entry.volume;
                return true;
            }
            clip = null;
            volume = 0f;
            return false;
        }
    }
}

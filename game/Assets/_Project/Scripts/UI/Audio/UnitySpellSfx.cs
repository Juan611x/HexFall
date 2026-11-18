using System;
using UnityEngine;

namespace HexFall.UI
{
    public sealed class UnitySpellSfx : ISpellSfx
    {
        private readonly AudioSource _source;
        private readonly MenuAudioLibrary _library;
        private readonly SpellPicker _picker;
        private readonly Func<float> _scale;

        public UnitySpellSfx(AudioSource source, MenuAudioLibrary library, SpellPicker picker, Func<float> scale = null)
        {
            _scale = scale ?? (() => 1f);
            _source = source;
            _library = library;
            _picker = picker;
        }

        public void PlayCast()
        {
            var clips = _library.spellClips;
            var index = _picker.Next(clips == null ? 0 : clips.Length);
            if (index < 0 || clips[index] == null)
            {
                return;
            }

            _source.PlayOneShot(clips[index], _library.spellVolume * _scale());
        }
    }
}

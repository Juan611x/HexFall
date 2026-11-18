using System;
using UnityEngine;

namespace HexFall.UI
{
    public sealed class UnityMenuSfx : IMenuSfx
    {
        private readonly AudioSource _source;
        private readonly MenuAudioLibrary _library;
        private readonly Func<float> _scale;

        public UnityMenuSfx(AudioSource source, MenuAudioLibrary library, Func<float> scale = null)
        {
            _source = source;
            _library = library;
            _scale = scale ?? (() => 1f);
        }

        public void PlayHover() => Play(_library.hover);

        public void PlayClick() => Play(_library.click);

        public void PlayBack() => Play(_library.back);

        private void Play(MenuAudioLibrary.Sound sound)
        {
            if (sound.clip == null)
            {
                return;
            }

            _source.PlayOneShot(sound.clip, sound.volume * _scale());
        }
    }
}

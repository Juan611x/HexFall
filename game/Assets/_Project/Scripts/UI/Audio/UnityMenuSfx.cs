using UnityEngine;

namespace HexFall.UI
{
    public sealed class UnityMenuSfx : IMenuSfx
    {
        private readonly AudioSource _source;
        private readonly MenuAudioLibrary _library;

        public UnityMenuSfx(AudioSource source, MenuAudioLibrary library)
        {
            _source = source;
            _library = library;
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

            _source.PlayOneShot(sound.clip, sound.volume);
        }
    }
}

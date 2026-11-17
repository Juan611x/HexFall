using UnityEngine;

namespace HexFall.UI
{
    public sealed class UnityMusicLayers : IMusicLayers
    {
        // Lead time so every layer is scheduled before the shared start time passes.
        private const double StartDelaySeconds = 0.1;

        private readonly AudioSource[] _sources;

        public UnityMusicLayers(params AudioSource[] sources) => _sources = sources;

        public int LayerCount => _sources.Length;

        public void StartAll()
        {
            var startTime = AudioSettings.dspTime + StartDelaySeconds;
            foreach (var source in _sources)
            {
                source.volume = 0f;
                source.PlayScheduled(startTime);
            }
        }

        public void SetVolume(int layer, float volume) => _sources[layer].volume = volume;
    }
}

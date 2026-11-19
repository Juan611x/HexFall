using System;

namespace HexFall.UI
{
    // Fades every layer in together once started; start can be gated on a user gesture (browser autoplay policy).
    public sealed class MenuMusicPlayer : ITickable
    {
        private readonly IMusicLayers _layers;
        private readonly float[] _targets;
        private readonly float _fadeSeconds;
        private readonly bool _requireGesture;
        private readonly Func<float> _scale;
        private bool _begun;
        private float _elapsed;

        // The scale is read on every tick, so a mixer change lands live, mid-fade included.
        public MenuMusicPlayer(IMusicLayers layers, float[] targetVolumes, float fadeSeconds, bool requireGesture, Func<float> scale = null)
        {
            _scale = scale ?? (() => 1f);
            _layers = layers;
            _targets = (float[])targetVolumes.Clone();
            _fadeSeconds = fadeSeconds;
            _requireGesture = requireGesture;
        }

        public bool IsStarted { get; private set; }

        public void Begin()
        {
            _begun = true;
            if (!_requireGesture)
            {
                Start();
            }
        }

        public void OnUserGesture()
        {
            if (_begun && _requireGesture)
            {
                Start();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsStarted)
            {
                return;
            }

            _elapsed += deltaSeconds;
            Apply();
        }

        private void Start()
        {
            if (IsStarted)
            {
                return;
            }

            IsStarted = true;
            _elapsed = 0f;
            _layers.StartAll();
            Apply();
        }

        private void Apply()
        {
            var progress = _fadeSeconds <= 0f ? 1f : Math.Min(1f, _elapsed / _fadeSeconds);
            var scale = _scale();
            var count = Math.Min(_layers.LayerCount, _targets.Length);
            for (var i = 0; i < count; i++)
            {
                _layers.SetVolume(i, _targets[i] * progress * scale);
            }
        }
    }
}

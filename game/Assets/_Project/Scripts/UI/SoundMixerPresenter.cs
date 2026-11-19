using System;

namespace HexFall.UI
{
    public sealed class SoundMixerPresenter : IDisposable
    {
        private readonly ISoundMixerView _view;
        private readonly SoundSettings _settings;
        private bool _isOpen;

        public SoundMixerPresenter(ISoundMixerView view, SoundSettings settings)
        {
            _view = view;
            _settings = settings;
            _view.ToggleRequested += OnToggle;
            _view.CloseRequested += OnClose;
            _view.MasterMuteToggled += OnMasterMuteToggled;
            _view.ChannelMuteToggled += OnChannelMuteToggled;
            _view.ChannelLevelChanged += OnChannelLevelChanged;
            _settings.Changed += Render;
            Render();
        }

        public void Dispose()
        {
            _view.ToggleRequested -= OnToggle;
            _view.CloseRequested -= OnClose;
            _view.MasterMuteToggled -= OnMasterMuteToggled;
            _view.ChannelMuteToggled -= OnChannelMuteToggled;
            _view.ChannelLevelChanged -= OnChannelLevelChanged;
            _settings.Changed -= Render;
        }

        private void OnToggle()
        {
            _isOpen = !_isOpen;
            Render();
        }

        private void OnClose()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            Render();
        }

        private void OnMasterMuteToggled() => _settings.ToggleMasterMuted();

        private void OnChannelMuteToggled(SoundChannel channel) => _settings.ToggleChannelMuted(channel);

        private void OnChannelLevelChanged(SoundChannel channel, int level) => _settings.SetLevel(channel, level);

        private void Render() => _view.Render(new SoundMixerState(
            _isOpen,
            _settings.MasterMuted,
            _settings.MasterMuted ? "UNMUTE ALL" : "MUTE ALL",
            _settings.IsSilent,
            ChannelState(SoundChannel.Music),
            ChannelState(SoundChannel.Effects)));

        private SoundChannelState ChannelState(SoundChannel channel)
        {
            var dimmed = _settings.IsChannelEffectivelyMuted(channel);
            var level = _settings.GetLevel(channel);
            return new SoundChannelState(level, _settings.IsChannelMuted(channel), dimmed, dimmed ? "Off" : level + "%");
        }
    }
}

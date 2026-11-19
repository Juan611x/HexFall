using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SoundMixerPresenterTests
    {
        private sealed class FakeMixerView : ISoundMixerView
        {
            public event Action ToggleRequested;
            public event Action CloseRequested;
            public event Action MasterMuteToggled;
            public event Action<SoundChannel> ChannelMuteToggled;
            public event Action<SoundChannel, int> ChannelLevelChanged;

            public SoundMixerState Last;
            public int Renders;

            public void Render(SoundMixerState state)
            {
                Last = state;
                Renders++;
            }

            public void RaiseToggle() => ToggleRequested?.Invoke();
            public void RaiseClose() => CloseRequested?.Invoke();
            public void RaiseMaster() => MasterMuteToggled?.Invoke();
            public void RaiseChannelMute(SoundChannel channel) => ChannelMuteToggled?.Invoke(channel);
            public void RaiseLevel(SoundChannel channel, int level) => ChannelLevelChanged?.Invoke(channel, level);
        }

        private FakeMixerView _view;
        private SoundSettings _settings;
        private SoundMixerPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeMixerView();
            _settings = new SoundSettings();
            _presenter = new SoundMixerPresenter(_view, _settings);
        }

        [Test]
        public void RendersTheInitialStateOnConstruction()
        {
            Assert.AreEqual(1, _view.Renders);
            Assert.IsFalse(_view.Last.IsOpen);
            Assert.AreEqual("MUTE ALL", _view.Last.MasterLabel);
            Assert.AreEqual("100%", _view.Last.Music.ValueText);
            Assert.AreEqual(100, _view.Last.Effects.Level);
            Assert.IsFalse(_view.Last.IsSilent);
        }

        [Test]
        public void ToggleOpensThenClosesThePanel()
        {
            _view.RaiseToggle();
            Assert.IsTrue(_view.Last.IsOpen);
            _view.RaiseToggle();
            Assert.IsFalse(_view.Last.IsOpen);
        }

        [Test]
        public void CloseRequestedClosesAnOpenPanel()
        {
            _view.RaiseToggle();
            _view.RaiseClose();
            Assert.IsFalse(_view.Last.IsOpen);
        }

        [Test]
        public void CloseRequestedOnAClosedPanelDoesNotRender()
        {
            var renders = _view.Renders;
            _view.RaiseClose();
            Assert.AreEqual(renders, _view.Renders);
        }

        [Test]
        public void LevelChangeUpdatesSettingsAndValueText()
        {
            _view.RaiseLevel(SoundChannel.Music, 70);
            Assert.AreEqual(70, _settings.GetLevel(SoundChannel.Music));
            Assert.AreEqual("70%", _view.Last.Music.ValueText);
            Assert.AreEqual(70, _view.Last.Music.Level);
        }

        [Test]
        public void LevelChangeUnmutesTheChannel()
        {
            _view.RaiseChannelMute(SoundChannel.Effects);
            Assert.IsTrue(_view.Last.Effects.Muted);
            _view.RaiseLevel(SoundChannel.Effects, 50);
            Assert.IsFalse(_view.Last.Effects.Muted);
            Assert.AreEqual("50%", _view.Last.Effects.ValueText);
        }

        [Test]
        public void ChannelMuteShowsOffAndDimsOnlyThatChannel()
        {
            _view.RaiseChannelMute(SoundChannel.Music);
            Assert.IsTrue(_view.Last.Music.Muted);
            Assert.IsTrue(_view.Last.Music.Dimmed);
            Assert.AreEqual("Off", _view.Last.Music.ValueText);
            Assert.IsFalse(_view.Last.Effects.Dimmed);
            Assert.AreEqual("100%", _view.Last.Effects.ValueText);
        }

        [Test]
        public void ChannelMuteKeepsTheSliderLevel()
        {
            _view.RaiseLevel(SoundChannel.Music, 40);
            _view.RaiseChannelMute(SoundChannel.Music);
            Assert.AreEqual(40, _view.Last.Music.Level);
        }

        [Test]
        public void MasterMuteShowsOffOnBothChannelsWithoutChangingTheirFlags()
        {
            _view.RaiseMaster();
            Assert.IsTrue(_view.Last.MasterMuted);
            Assert.AreEqual("UNMUTE ALL", _view.Last.MasterLabel);
            Assert.AreEqual("Off", _view.Last.Music.ValueText);
            Assert.AreEqual("Off", _view.Last.Effects.ValueText);
            Assert.IsTrue(_view.Last.Music.Dimmed);
            Assert.IsFalse(_view.Last.Music.Muted);
            Assert.IsFalse(_view.Last.Effects.Muted);
            Assert.IsTrue(_view.Last.IsSilent);
        }

        [Test]
        public void UnmuteAllRestoresTheChannels()
        {
            _view.RaiseLevel(SoundChannel.Music, 30);
            _view.RaiseMaster();
            _view.RaiseMaster();
            Assert.AreEqual("MUTE ALL", _view.Last.MasterLabel);
            Assert.AreEqual("30%", _view.Last.Music.ValueText);
        }

        [Test]
        public void TriggerIsSilentWhenBothChannelsAreMuted()
        {
            _view.RaiseChannelMute(SoundChannel.Music);
            Assert.IsFalse(_view.Last.IsSilent);
            _view.RaiseChannelMute(SoundChannel.Effects);
            Assert.IsTrue(_view.Last.IsSilent);
        }

        [Test]
        public void ExternalSettingsChangesAreRendered()
        {
            _settings.SetLevel(SoundChannel.Effects, 15);
            Assert.AreEqual("15%", _view.Last.Effects.ValueText);
        }

        [Test]
        public void DisposeStopsReactingToViewAndSettings()
        {
            _presenter.Dispose();
            var renders = _view.Renders;

            _view.RaiseToggle();
            _view.RaiseMaster();
            _view.RaiseChannelMute(SoundChannel.Music);
            _view.RaiseLevel(SoundChannel.Music, 10);
            _settings.SetLevel(SoundChannel.Effects, 10);

            Assert.AreEqual(renders, _view.Renders);
            Assert.IsFalse(_settings.MasterMuted);
            Assert.AreEqual(100, _settings.GetLevel(SoundChannel.Music));
        }
    }
}

using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SoundSettingsTests
    {
        private SoundSettings _settings;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _settings = new SoundSettings();
            _changes = 0;
            _settings.Changed += () => _changes++;
        }

        [Test]
        public void DefaultsKeepTodaysMix()
        {
            Assert.IsFalse(_settings.MasterMuted);
            Assert.AreEqual(100, _settings.GetLevel(SoundChannel.Music));
            Assert.AreEqual(100, _settings.GetLevel(SoundChannel.Effects));
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Music));
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Effects));
            Assert.AreEqual(1f, _settings.MusicMultiplier, 1e-5f);
            Assert.AreEqual(1f, _settings.EffectsMultiplier, 1e-5f);
            Assert.IsFalse(_settings.IsSilent);
        }

        [Test]
        public void SetLevelChangesTheMultiplierOfThatChannelOnly()
        {
            _settings.SetLevel(SoundChannel.Music, 70);
            Assert.AreEqual(0.7f, _settings.MusicMultiplier, 1e-5f);
            Assert.AreEqual(1f, _settings.EffectsMultiplier, 1e-5f);
            Assert.AreEqual(1, _changes);
        }

        [TestCase(-20, 0)]
        [TestCase(250, 100)]
        [TestCase(72, 70)]
        [TestCase(73, 75)]
        public void SetLevelClampsAndSnapsToSteps(int requested, int expected)
        {
            _settings.SetLevel(SoundChannel.Effects, requested);
            Assert.AreEqual(expected, _settings.GetLevel(SoundChannel.Effects));
        }

        [Test]
        public void SetLevelUnmutesTheChannel()
        {
            _settings.SetChannelMuted(SoundChannel.Music, true);
            _settings.SetLevel(SoundChannel.Music, 40);
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Music));
            Assert.AreEqual(0.4f, _settings.MusicMultiplier, 1e-5f);
        }

        [Test]
        public void SetLevelOnAMutedChannelNotifiesEvenWhenTheLevelIsTheSame()
        {
            _settings.SetChannelMuted(SoundChannel.Music, true);
            _changes = 0;
            _settings.SetLevel(SoundChannel.Music, 100);
            Assert.AreEqual(1, _changes);
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Music));
        }

        [Test]
        public void SetLevelToTheCurrentValueDoesNotNotify()
        {
            _settings.SetLevel(SoundChannel.Music, 100);
            Assert.AreEqual(0, _changes);
        }

        [Test]
        public void MutedChannelHasZeroMultiplierButKeepsItsLevel()
        {
            _settings.SetLevel(SoundChannel.Effects, 60);
            _settings.SetChannelMuted(SoundChannel.Effects, true);
            Assert.AreEqual(0f, _settings.EffectsMultiplier);
            Assert.AreEqual(60, _settings.GetLevel(SoundChannel.Effects));
            Assert.IsTrue(_settings.IsChannelEffectivelyMuted(SoundChannel.Effects));
        }

        [Test]
        public void ToggleChannelMutedFlipsTheFlag()
        {
            _settings.ToggleChannelMuted(SoundChannel.Music);
            Assert.IsTrue(_settings.IsChannelMuted(SoundChannel.Music));
            _settings.ToggleChannelMuted(SoundChannel.Music);
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Music));
        }

        [Test]
        public void MutingAnAlreadyMutedChannelDoesNotNotify()
        {
            _settings.SetChannelMuted(SoundChannel.Music, true);
            _settings.SetChannelMuted(SoundChannel.Music, true);
            Assert.AreEqual(1, _changes);
        }

        [Test]
        public void MasterMuteSilencesBothChannelsWithoutTouchingTheirState()
        {
            _settings.SetLevel(SoundChannel.Music, 30);
            _settings.SetMasterMuted(true);
            Assert.AreEqual(0f, _settings.MusicMultiplier);
            Assert.AreEqual(0f, _settings.EffectsMultiplier);
            Assert.AreEqual(30, _settings.GetLevel(SoundChannel.Music));
            Assert.IsFalse(_settings.IsChannelMuted(SoundChannel.Music));
            Assert.IsTrue(_settings.IsChannelEffectivelyMuted(SoundChannel.Music));
        }

        [Test]
        public void UnmutingMasterRestoresTheChannelMix()
        {
            _settings.SetLevel(SoundChannel.Music, 30);
            _settings.SetMasterMuted(true);
            _settings.SetMasterMuted(false);
            Assert.AreEqual(0.3f, _settings.MusicMultiplier, 1e-5f);
            Assert.AreEqual(1f, _settings.EffectsMultiplier, 1e-5f);
        }

        [Test]
        public void ToggleMasterMutedFlipsTheFlag()
        {
            _settings.ToggleMasterMuted();
            Assert.IsTrue(_settings.MasterMuted);
            _settings.ToggleMasterMuted();
            Assert.IsFalse(_settings.MasterMuted);
        }

        [Test]
        public void SilentWhenMasterIsMuted()
        {
            _settings.SetMasterMuted(true);
            Assert.IsTrue(_settings.IsSilent);
        }

        [Test]
        public void SilentWhenBothChannelsAreMuted()
        {
            _settings.SetChannelMuted(SoundChannel.Music, true);
            Assert.IsFalse(_settings.IsSilent);
            _settings.SetChannelMuted(SoundChannel.Effects, true);
            Assert.IsTrue(_settings.IsSilent);
        }

        [Test]
        public void StateRoundTripsThroughSnapshot()
        {
            _settings.SetLevel(SoundChannel.Music, 35);
            _settings.SetChannelMuted(SoundChannel.Effects, true);
            _settings.SetMasterMuted(true);

            var copy = new SoundSettings(_settings.ToState());

            Assert.AreEqual(35, copy.GetLevel(SoundChannel.Music));
            Assert.IsTrue(copy.IsChannelMuted(SoundChannel.Effects));
            Assert.IsTrue(copy.MasterMuted);
        }

        [Test]
        public void LoadedStateIsClampedAndSnapped()
        {
            var state = new SoundSettingsState(false, 900, false, -5, true);
            var settings = new SoundSettings(state);
            Assert.AreEqual(100, settings.GetLevel(SoundChannel.Music));
            Assert.AreEqual(0, settings.GetLevel(SoundChannel.Effects));
            Assert.IsTrue(settings.IsChannelMuted(SoundChannel.Effects));
        }

        [Test]
        public void DefaultStateMatchesDefaultSettings()
        {
            var settings = new SoundSettings(SoundSettingsState.Default);
            Assert.AreEqual(100, settings.GetLevel(SoundChannel.Music));
            Assert.IsFalse(settings.IsSilent);
        }
    }

    public class SoundSettingsSaverTests
    {
        private sealed class FakeStore : ISoundSettingsStore
        {
            public int Saves;
            public SoundSettingsState Last;
            public SoundSettingsState Load() => Last;

            public void Save(SoundSettingsState state)
            {
                Saves++;
                Last = state;
            }
        }

        [Test]
        public void EveryChangeIsSaved()
        {
            var store = new FakeStore();
            var settings = new SoundSettings();
            using (new SoundSettingsSaver(settings, store))
            {
                settings.SetLevel(SoundChannel.Music, 55);
                settings.SetMasterMuted(true);
            }

            Assert.AreEqual(2, store.Saves);
            Assert.AreEqual(55, store.Last.MusicLevel);
            Assert.IsTrue(store.Last.MasterMuted);
        }

        [Test]
        public void DisposeStopsSaving()
        {
            var store = new FakeStore();
            var settings = new SoundSettings();
            var saver = new SoundSettingsSaver(settings, store);
            saver.Dispose();
            settings.SetLevel(SoundChannel.Music, 55);
            Assert.AreEqual(0, store.Saves);
        }
    }
}

using System;

namespace HexFall.UI
{
    public enum SoundChannel { Music, Effects }

    public readonly struct SoundSettingsState
    {
        public SoundSettingsState(bool masterMuted, int musicLevel, bool musicMuted, int effectsLevel, bool effectsMuted)
        {
            MasterMuted = masterMuted;
            MusicLevel = musicLevel;
            MusicMuted = musicMuted;
            EffectsLevel = effectsLevel;
            EffectsMuted = effectsMuted;
        }

        public static SoundSettingsState Default =>
            new SoundSettingsState(false, SoundSettings.MaxLevel, false, SoundSettings.MaxLevel, false);

        public bool MasterMuted { get; }
        public int MusicLevel { get; }
        public bool MusicMuted { get; }
        public int EffectsLevel { get; }
        public bool EffectsMuted { get; }
    }

    // Levels are multipliers on the current mix, so the defaults (100%, unmuted) sound exactly like before.
    public sealed class SoundSettings
    {
        public const int MaxLevel = 100;
        public const int LevelStep = 5;

        private readonly int[] _levels = new int[2];
        private readonly bool[] _muted = new bool[2];

        public SoundSettings() : this(SoundSettingsState.Default)
        {
        }

        public SoundSettings(SoundSettingsState state)
        {
            MasterMuted = state.MasterMuted;
            _levels[(int)SoundChannel.Music] = Normalize(state.MusicLevel);
            _muted[(int)SoundChannel.Music] = state.MusicMuted;
            _levels[(int)SoundChannel.Effects] = Normalize(state.EffectsLevel);
            _muted[(int)SoundChannel.Effects] = state.EffectsMuted;
        }

        public event Action Changed;

        public bool MasterMuted { get; private set; }

        public float MusicMultiplier => Multiplier(SoundChannel.Music);

        public float EffectsMultiplier => Multiplier(SoundChannel.Effects);

        public bool IsSilent =>
            MasterMuted || (_muted[(int)SoundChannel.Music] && _muted[(int)SoundChannel.Effects]);

        public int GetLevel(SoundChannel channel) => _levels[(int)channel];

        public bool IsChannelMuted(SoundChannel channel) => _muted[(int)channel];

        public bool IsChannelEffectivelyMuted(SoundChannel channel) => MasterMuted || _muted[(int)channel];

        public float Multiplier(SoundChannel channel) =>
            IsChannelEffectivelyMuted(channel) ? 0f : _levels[(int)channel] / (float)MaxLevel;

        public void SetLevel(SoundChannel channel, int level)
        {
            var index = (int)channel;
            var normalized = Normalize(level);
            if (_levels[index] == normalized && !_muted[index])
            {
                return;
            }

            _levels[index] = normalized;
            _muted[index] = false;
            Changed?.Invoke();
        }

        public void SetChannelMuted(SoundChannel channel, bool muted)
        {
            var index = (int)channel;
            if (_muted[index] == muted)
            {
                return;
            }

            _muted[index] = muted;
            Changed?.Invoke();
        }

        public void ToggleChannelMuted(SoundChannel channel) => SetChannelMuted(channel, !_muted[(int)channel]);

        public void SetMasterMuted(bool muted)
        {
            if (MasterMuted == muted)
            {
                return;
            }

            MasterMuted = muted;
            Changed?.Invoke();
        }

        public void ToggleMasterMuted() => SetMasterMuted(!MasterMuted);

        public SoundSettingsState ToState() => new SoundSettingsState(
            MasterMuted,
            _levels[(int)SoundChannel.Music],
            _muted[(int)SoundChannel.Music],
            _levels[(int)SoundChannel.Effects],
            _muted[(int)SoundChannel.Effects]);

        private static int Normalize(int level)
        {
            var clamped = Math.Max(0, Math.Min(MaxLevel, level));
            return (int)Math.Round(clamped / (double)LevelStep, MidpointRounding.AwayFromZero) * LevelStep;
        }
    }
}

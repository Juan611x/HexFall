using System;

namespace HexFall.UI
{
    public readonly struct SoundChannelState
    {
        public SoundChannelState(int level, bool muted, bool dimmed, string valueText)
        {
            Level = level;
            Muted = muted;
            Dimmed = dimmed;
            ValueText = valueText;
        }

        public int Level { get; }
        public bool Muted { get; }
        public bool Dimmed { get; }
        public string ValueText { get; }
    }

    public readonly struct SoundMixerState
    {
        public SoundMixerState(bool isOpen, bool masterMuted, string masterLabel, bool isSilent, SoundChannelState music, SoundChannelState effects)
        {
            IsOpen = isOpen;
            MasterMuted = masterMuted;
            MasterLabel = masterLabel;
            IsSilent = isSilent;
            Music = music;
            Effects = effects;
        }

        public bool IsOpen { get; }
        public bool MasterMuted { get; }
        public string MasterLabel { get; }
        public bool IsSilent { get; }
        public SoundChannelState Music { get; }
        public SoundChannelState Effects { get; }
    }

    public interface ISoundMixerView
    {
        event Action ToggleRequested;
        event Action CloseRequested;
        event Action MasterMuteToggled;
        event Action<SoundChannel> ChannelMuteToggled;
        event Action<SoundChannel, int> ChannelLevelChanged;

        void Render(SoundMixerState state);
    }
}

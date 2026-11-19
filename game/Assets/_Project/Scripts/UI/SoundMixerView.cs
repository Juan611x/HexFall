using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class SoundMixerView : ISoundMixerView
    {
        private const string OpenClass = "sound__trigger--open";
        private const string MutedClass = "sound__mute--on";
        private const string OffClass = "sound__channel--off";

        private readonly VisualElement _root;
        private readonly Button _trigger;
        private readonly IconElement _triggerIcon;
        private readonly VisualElement _panel;
        private readonly Button _master;
        private readonly ChannelParts _music;
        private readonly ChannelParts _effects;

        public event Action ToggleRequested;
        public event Action CloseRequested;
        public event Action MasterMuteToggled;
        public event Action<SoundChannel> ChannelMuteToggled;
        public event Action<SoundChannel, int> ChannelLevelChanged;

        public SoundMixerView(VisualElement root)
        {
            _root = root;
            _trigger = root.Q<Button>("sound-trigger");
            _triggerIcon = root.Q<IconElement>("sound-trigger-icon");
            _panel = root.Q<VisualElement>("sound-panel");
            _master = root.Q<Button>("sound-master");
            _music = new ChannelParts(root, "music", SoundChannel.Music, this);
            _effects = new ChannelParts(root, "effects", SoundChannel.Effects, this);

            _trigger.clicked += () => ToggleRequested?.Invoke();
            _master.clicked += () => MasterMuteToggled?.Invoke();
            _root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void Render(SoundMixerState state)
        {
            _panel.SetVisible(state.IsOpen);
            _trigger.EnableInClassList(OpenClass, state.IsOpen);
            _triggerIcon.kind = state.IsSilent ? IconKind.SpeakerOff : IconKind.Speaker;
            _master.text = state.MasterLabel;
            _master.EnableInClassList(MutedClass, state.MasterMuted);
            _music.Render(state.Music);
            _effects.Render(state.Effects);
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            CloseRequested?.Invoke();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            CloseRequested?.Invoke();
            evt.StopPropagation();
        }

        private sealed class ChannelParts
        {
            private readonly SoundMixerView _owner;
            private readonly SoundChannel _channel;
            private readonly VisualElement _row;
            private readonly Label _value;
            private readonly Button _mute;
            private readonly Slider _slider;
            private readonly VisualElement _fill;

            public ChannelParts(VisualElement root, string prefix, SoundChannel channel, SoundMixerView owner)
            {
                _owner = owner;
                _channel = channel;
                _row = root.Q<VisualElement>("sound-" + prefix);
                _value = root.Q<Label>("sound-" + prefix + "-value");
                _mute = root.Q<Button>("sound-" + prefix + "-mute");
                _slider = root.Q<Slider>("sound-" + prefix + "-slider");
                _slider.pageSize = SoundSettings.LevelStep;
                _fill = new VisualElement { pickingMode = PickingMode.Ignore };
                _fill.AddToClassList("sound__fill");
                _slider.Q<VisualElement>(className: "unity-base-slider__tracker").Add(_fill);

                _mute.clicked += () => _owner.ChannelMuteToggled?.Invoke(_channel);
                _slider.RegisterValueChangedCallback(OnSliderChanged);
            }

            public void Render(SoundChannelState state)
            {
                _value.text = state.ValueText;
                _row.EnableInClassList(OffClass, state.Dimmed);
                _mute.EnableInClassList(MutedClass, state.Muted);
                _slider.SetValueWithoutNotify(state.Level);
                SetFill(state.Level);
            }

            private void OnSliderChanged(ChangeEvent<float> evt)
            {
                var step = SoundSettings.LevelStep;
                var snapped = Mathf.Round(evt.newValue / step) * step;
                if (!Mathf.Approximately(snapped, evt.newValue))
                {
                    _slider.SetValueWithoutNotify(snapped);
                }

                SetFill(snapped);
                _owner.ChannelLevelChanged?.Invoke(_channel, (int)snapped);
            }

            private void SetFill(float level) => _fill.style.width = Length.Percent(level);
        }
    }
}

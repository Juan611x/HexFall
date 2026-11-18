using System;

namespace HexFall.UI
{
    public sealed class SoundSettingsSaver : IDisposable
    {
        private readonly SoundSettings _settings;
        private readonly ISoundSettingsStore _store;

        public SoundSettingsSaver(SoundSettings settings, ISoundSettingsStore store)
        {
            _settings = settings;
            _store = store;
            _settings.Changed += OnChanged;
        }

        public void Dispose() => _settings.Changed -= OnChanged;

        private void OnChanged() => _store.Save(_settings.ToState());
    }
}

using UnityEngine;

namespace HexFall.UI
{
    // PlayerPrefs is backed by IndexedDB on WebGL, so the mix survives browser sessions. Values are validated by SoundSettings.
    public sealed class PlayerPrefsSoundSettingsStore : ISoundSettingsStore
    {
        private const string Prefix = "hexfall.sound.";
        private const string MasterMutedKey = Prefix + "master_muted";
        private const string MusicLevelKey = Prefix + "music_level";
        private const string MusicMutedKey = Prefix + "music_muted";
        private const string EffectsLevelKey = Prefix + "effects_level";
        private const string EffectsMutedKey = Prefix + "effects_muted";

        public SoundSettingsState Load() => new SoundSettingsState(
            PlayerPrefs.GetInt(MasterMutedKey, 0) != 0,
            PlayerPrefs.GetInt(MusicLevelKey, SoundSettings.MaxLevel),
            PlayerPrefs.GetInt(MusicMutedKey, 0) != 0,
            PlayerPrefs.GetInt(EffectsLevelKey, SoundSettings.MaxLevel),
            PlayerPrefs.GetInt(EffectsMutedKey, 0) != 0);

        public void Save(SoundSettingsState state)
        {
            PlayerPrefs.SetInt(MasterMutedKey, state.MasterMuted ? 1 : 0);
            PlayerPrefs.SetInt(MusicLevelKey, state.MusicLevel);
            PlayerPrefs.SetInt(MusicMutedKey, state.MusicMuted ? 1 : 0);
            PlayerPrefs.SetInt(EffectsLevelKey, state.EffectsLevel);
            PlayerPrefs.SetInt(EffectsMutedKey, state.EffectsMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

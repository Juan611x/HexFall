namespace HexFall.UI
{
    public interface ISoundSettingsStore
    {
        SoundSettingsState Load();
        void Save(SoundSettingsState state);
    }
}

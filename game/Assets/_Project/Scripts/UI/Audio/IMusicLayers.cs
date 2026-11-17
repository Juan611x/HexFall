namespace HexFall.UI
{
    public interface IMusicLayers
    {
        int LayerCount { get; }
        void StartAll();
        void SetVolume(int layer, float volume);
    }
}

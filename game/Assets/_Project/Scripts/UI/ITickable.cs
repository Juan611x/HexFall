namespace HexFall.UI
{
    // Implemented by presenters that need per-frame time; the composition root forwards deltas.
    public interface ITickable
    {
        void Tick(float deltaSeconds);
    }
}

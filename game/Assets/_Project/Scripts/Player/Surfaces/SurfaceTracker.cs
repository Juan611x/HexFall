using HexFall.Surfaces;

namespace HexFall.Player
{
    // Remembers the current surface and tells whether an update changed it.
    public sealed class SurfaceTracker
    {
        public SurfaceDefinition Current { get; private set; }
        public SurfaceDefinition Previous { get; private set; }

        // Returns true when next differs from the current surface; a null surface is ignored.
        public bool Update(SurfaceDefinition next)
        {
            if (next == null || next == Current) return false;
            Previous = Current;
            Current = next;
            return true;
        }
    }
}

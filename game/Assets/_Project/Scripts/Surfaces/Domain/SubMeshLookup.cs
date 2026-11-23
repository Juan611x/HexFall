using System.Collections.Generic;

namespace HexFall.Surfaces
{
    // Maps a triangle index to the submesh whose index range contains it (triangle topology only).
    public static class SubMeshLookup
    {
        // Returns the submesh index, or -1 when the triangle is negative or outside every range.
        public static int IndexOf(int triangleIndex, IReadOnlyList<(int indexStart, int indexCount)> ranges)
        {
            if (triangleIndex < 0) return -1;
            int first = triangleIndex * 3;
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                if (first >= range.indexStart && first < range.indexStart + range.indexCount) return i;
            }
            return -1;
        }
    }
}

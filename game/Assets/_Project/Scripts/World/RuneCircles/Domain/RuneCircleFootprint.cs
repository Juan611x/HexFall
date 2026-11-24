using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // The real footprint of a rune circle: a disc around its centre, in the circle's own plane, and a
    // height band along the circle's up axis. Replaces an AABB test, whose corners stick out of the ring.
    public static class RuneCircleFootprint
    {
        // point is the player's feet. minHeight/maxHeight bound the offset along the circle's up axis.
        public static bool Contains(Vector3 centre, Quaternion rotation, Vector3 point, float radius, float minHeight, float maxHeight)
        {
            var local = Quaternion.Inverse(rotation) * (point - centre);
            if (local.y < minHeight || local.y > maxHeight) return false;
            return local.x * local.x + local.z * local.z <= radius * radius;
        }
    }
}

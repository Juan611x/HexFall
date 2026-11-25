using System.Collections.Generic;
using UnityEngine;

namespace HexFall.Player.Spells
{
    // Which of several pickups in reach Interact works on.
    public static class PickupTargeting
    {
        // Index of the point nearest to the origin; the earliest wins a tie. -1 when there are none.
        public static int Nearest(Vector3 origin, IReadOnlyList<Vector3> points)
        {
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < points.Count; i++)
            {
                var d = points[i] - origin;
                var squared = d.x * d.x + d.y * d.y + d.z * d.z;
                if (squared >= bestDistance) continue;
                best = i;
                bestDistance = squared;
            }
            return best;
        }
    }
}

using UnityEngine;

namespace HexFall.Player.Spells
{
    // Where a channelled beam ends: from the muzzle along a direction, capped at the range, stopped by the
    // first surface within it. Plain maths; the physics query that finds the surface lives in SpellBeam.
    public readonly struct BeamSegment
    {
        public readonly Vector3 Origin;
        public readonly Vector3 End;
        public readonly float Length;
        // True when a surface stopped the beam inside its range; the only case that can deal damage.
        public readonly bool Hit;

        public BeamSegment(Vector3 origin, Vector3 end, float length, bool hit)
        {
            Origin = origin;
            End = end;
            Length = length;
            Hit = hit;
        }
    }

    public static class BeamTrace
    {
        // Unit direction from the muzzle to the aim point; zero when they coincide or are not finite.
        public static Vector3 DirectionTo(Vector3 origin, Vector3 aimPoint)
        {
            var delta = aimPoint - origin;
            var squared = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;
            if (float.IsNaN(squared) || float.IsInfinity(squared) || squared < 1e-12f) return Vector3.zero;
            return delta * (1f / Mathf.Sqrt(squared));
        }

        // hitDistance is how far along the direction the first surface is, or null when nothing was hit.
        // A degenerate direction or range gives an empty beam at the origin.
        public static BeamSegment Resolve(Vector3 origin, Vector3 direction, float rangeMeters, float? hitDistance)
        {
            var empty = new BeamSegment(origin, origin, 0f, false);
            if (!IsFinite(rangeMeters) || rangeMeters <= 0f) return empty;
            if (!IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z)) return empty;
            if (direction.x == 0f && direction.y == 0f && direction.z == 0f) return empty;

            var hit = hitDistance.HasValue && IsFinite(hitDistance.Value)
                && hitDistance.Value >= 0f && hitDistance.Value <= rangeMeters;
            var length = hit ? hitDistance.Value : rangeMeters;
            return new BeamSegment(origin, origin + direction * length, length, hit);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}

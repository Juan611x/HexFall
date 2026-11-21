using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Measures the ledge in front of the body with physics casts, ignoring its own colliders and
    // triggers. Every distance comes from ClimbSettings; only tiny skin offsets are fixed here.
    //
    // 1. Wall: horizontal rays at knee and chest height along the wanted direction find the face and its
    //    normal (ApproachDirection = -normal); a ray just above climbMaxHeight that hits the same wall
    //    means it is too high and there is no ledge.
    // 2. Top: a downward ray just past the face, started above climbMaxHeight, gives TopPoint and
    //    TopHeight; tops steeper than the slope limit are rejected.
    // 3. Depth: downward rays march along the approach while the surface stays within
    //    topSurfaceTolerance of the top, then the end is bisected; beyond what the classifier needs
    //    the depth is reported as infinite.
    // 4. Headroom: rays up from where the body stands on the top.
    // 5. Landing (thin tops only): ground behind the far edge within maxLandingDrop, and a free body
    //    capsule over it.
    public sealed class LedgeProbe
    {
        private const float Skin = 0.02f;
        // Half-width of the headroom ray pattern as a share of the radius.
        private const float RayInset = 0.7f;
        private const int Refinements = 6;
        private const int HitCapacity = 16;

        private readonly LayerMask _layers;
        private readonly float _radius;
        private readonly float _walkableMinY;
        private readonly ClimbSettings _climb;
        private readonly float _bodyHeight;
        private readonly Transform _ignoreRoot;
        private readonly RaycastHit[] _hits = new RaycastHit[HitCapacity];
        private readonly Collider[] _overlaps = new Collider[8];

        public LedgeProbe(LayerMask layers, float controllerRadius, float slopeLimit, ClimbSettings climb,
            float bodyHeight, Transform ignoreRoot)
        {
            _layers = layers;
            _radius = Mathf.Max(0.01f, controllerRadius - Skin);
            _walkableMinY = Mathf.Cos(slopeLimit * Mathf.Deg2Rad);
            _climb = climb;
            _bodyHeight = bodyHeight;
            _ignoreRoot = ignoreRoot;
        }

        // feet is the transform position; direction is the wanted horizontal direction.
        public LedgeMeasurement Measure(Vector3 feet, Vector3 direction)
        {
            var dir = new Vector3(direction.x, 0f, direction.z);
            if (dir.sqrMagnitude <= 0.0001f) return default;
            dir.Normalize();

            if (!FindFace(feet, dir, out var face, out var approach)) return default;

            var margin = _climb.topSurfaceTolerance;
            var topOrigin = new Vector3(face.x, feet.y + _climb.climbMaxHeight + margin, face.z) + approach * Skin;
            if (!Cast(topOrigin, Vector3.down, _climb.climbMaxHeight + margin, out var top)) return default;
            if (top.normal.y < _walkableMinY) return default;

            var edge = new Vector3(face.x, top.point.y, face.z);
            var topHeight = top.point.y - feet.y;
            if (topHeight < _climb.climbMinHeight || topHeight > _climb.climbMaxHeight)
                return new LedgeMeasurement(true, topHeight, edge, approach, 0f, 0f, false, 0f);

            var depth = MeasureDepth(edge, approach);
            var headroom = MeasureHeadroom(edge, approach, depth);
            var landingClear = MeasureLanding(feet, edge, approach, depth, out var landingHeight);
            return new LedgeMeasurement(true, topHeight, edge, approach, depth, headroom, landingClear, landingHeight);
        }

        private bool FindFace(Vector3 feet, Vector3 dir, out Vector3 face, out Vector3 approach)
        {
            face = default;
            approach = default;
            var reach = _radius + Skin + _climb.detectionDistance;
            var knee = _climb.climbMinHeight * 0.5f;
            var chest = (_climb.climbMinHeight + _climb.climbMaxHeight) * 0.5f;

            RaycastHit best = default;
            var found = false;
            for (var i = 0; i < 2; i++)
            {
                var height = i == 0 ? knee : chest;
                if (!Cast(feet + Vector3.up * height, dir, reach, out var hit)) continue;
                if (hit.normal.y >= _walkableMinY) continue;
                if (found && hit.distance >= best.distance) continue;
                best = hit;
                found = true;
            }
            if (!found) return false;

            var flat = new Vector3(best.normal.x, 0f, best.normal.z);
            if (flat.sqrMagnitude <= 0.0001f) return false;
            approach = -flat.normalized;
            face = best.point;

            // A wall that is still there above the climbable range cannot be climbed.
            var upper = feet + Vector3.up * (_climb.climbMaxHeight + Skin);
            if (Cast(upper, dir, reach, out var high) && high.distance <= best.distance + _climb.topSurfaceTolerance)
                return false;
            return true;
        }

        private float MeasureDepth(Vector3 edge, Vector3 approach)
        {
            var step = Mathf.Max(0.01f, _climb.probeStep);
            var limit = Mathf.Max(_climb.standDepth, _climb.overMaxThickness) + step;
            var ok = 0f;
            var fail = -1f;
            for (var d = step; d <= limit; d += step)
            {
                if (OnTop(edge, approach, d)) ok = d;
                else { fail = d; break; }
            }
            if (fail < 0f) return float.PositiveInfinity;

            for (var i = 0; i < Refinements; i++)
            {
                var mid = (ok + fail) * 0.5f;
                if (OnTop(edge, approach, mid)) ok = mid;
                else fail = mid;
            }
            return ok;
        }

        private bool OnTop(Vector3 edge, Vector3 approach, float distance)
        {
            var tolerance = _climb.topSurfaceTolerance;
            var origin = edge + approach * distance + Vector3.up * tolerance;
            return Cast(origin, Vector3.down, 2f * tolerance, out var hit) && hit.normal.y >= _walkableMinY;
        }

        // Free height above the top where the body would stand; infinite when nothing is within twice
        // the needed headroom.
        private float MeasureHeadroom(Vector3 edge, Vector3 approach, float depth)
        {
            var forward = Mathf.Min(_climb.forwardOntoTop, depth * 0.5f);
            var stand = edge + approach * Mathf.Max(0f, forward) + Vector3.up * Skin;
            var side = Vector3.Cross(Vector3.up, approach) * (_radius * RayInset);
            var reach = _climb.minHeadroom * 2f;

            var headroom = float.PositiveInfinity;
            for (var i = 0; i < 5; i++)
            {
                var offset = i switch { 1 => side, 2 => -side, 3 => approach * (_radius * RayInset), 4 => -approach * (_radius * RayInset), _ => Vector3.zero };
                if (Cast(stand + offset, Vector3.up, reach, out var hit))
                    headroom = Mathf.Min(headroom, hit.point.y - edge.y);
            }
            return headroom;
        }

        private bool MeasureLanding(Vector3 feet, Vector3 edge, Vector3 approach, float depth, out float landingHeight)
        {
            landingHeight = edge.y - feet.y;
            if (float.IsInfinity(depth)) return false;

            var spot = edge + approach * (depth + _climb.landingClearDepth);
            var originY = feet.y + _climb.climbMaxHeight + _climb.topSurfaceTolerance;
            var reach = originY - (edge.y - _climb.maxLandingDrop);
            if (!Cast(new Vector3(spot.x, originY, spot.z), Vector3.down, reach, out var ground)) return false;
            if (ground.normal.y < _walkableMinY) return false;
            landingHeight = ground.point.y - feet.y;

            var bottom = new Vector3(spot.x, ground.point.y + Skin + _radius, spot.z);
            var topY = Mathf.Max(edge.y, ground.point.y) + _bodyHeight - _radius;
            var upper = new Vector3(spot.x, Mathf.Max(bottom.y, topY), spot.z);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, upper, _radius, _overlaps, _layers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (!IsOwn(_overlaps[i])) return false;
            }
            return true;
        }

        // Nearest hit that is not one of the body's own colliders.
        private bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            var count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, _layers, QueryTriggerInteraction.Ignore);
            var found = false;
            for (var i = 0; i < count; i++)
            {
                if (IsOwn(_hits[i].collider)) continue;
                if (found && _hits[i].distance >= nearest.distance) continue;
                nearest = _hits[i];
                found = true;
            }
            return found;
        }

        private bool IsOwn(Collider collider) => _ignoreRoot != null && collider.transform.IsChildOf(_ignoreRoot);
    }
}

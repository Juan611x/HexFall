using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Answers whether the body has room to stand up, ignoring its own colliders.
    public sealed class StandClearance
    {
        private const float RadiusMargin = 0.02f;

        private readonly LayerMask _layers;
        private readonly float _radius;
        private readonly float _standHeight;
        private readonly float _feetOffset;
        private readonly Transform _ignoreRoot;
        private readonly Collider[] _hits = new Collider[8];

        public StandClearance(LayerMask layers, float controllerRadius, float standHeight, float feetOffset, Transform ignoreRoot)
        {
            _layers = layers;
            _radius = Mathf.Max(0.01f, controllerRadius - RadiusMargin);
            _standHeight = standHeight;
            _feetOffset = feetOffset;
            _ignoreRoot = ignoreRoot;
        }

        // The capsule probed covers only the part of the standing body above the current top, so the
        // floor and the body itself are never counted.
        public bool CanStand(Vector3 feet, float currentHeight)
        {
            if (currentHeight >= _standHeight - 0.001f) return true;

            var bottomY = feet.y + _feetOffset + currentHeight;
            var topY = feet.y + _feetOffset + _standHeight;
            var low = new Vector3(feet.x, bottomY + _radius, feet.z);
            var high = new Vector3(feet.x, topY - _radius, feet.z);
            // Spheres sit one radius inside the span; keep them from inverting on a short gap.
            if (high.y < low.y) high.y = low.y;

            var count = Physics.OverlapCapsuleNonAlloc(low, high, _radius, _hits, _layers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i].transform;
                if (_ignoreRoot != null && hit.IsChildOf(_ignoreRoot)) continue;
                return false;
            }
            return true;
        }
    }
}

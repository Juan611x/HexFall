using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Answers whether the body has room to stand up, ignoring its own colliders.
    public sealed class StandClearance
    {
        private const float RadiusMargin = 0.02f;
        // Half-width of the narrow-gap box as a share of the radius, so its corners stay inside the body's circle.
        private const float BoxInset = 0.7f;

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

        // Only the part of the standing body above the current top is probed, so the floor and the body
        // itself are never counted. A gap of two radii or more takes a capsule; a narrower one (the body
        // is almost upright) takes a box, because a capsule's spheres would poke above the standing top.
        public bool CanStand(Vector3 feet, float currentHeight)
        {
            if (currentHeight >= _standHeight - 0.001f) return true;

            var bottomY = feet.y + _feetOffset + currentHeight;
            var topY = feet.y + _feetOffset + _standHeight;
            var count = topY - bottomY >= 2f * _radius
                ? Physics.OverlapCapsuleNonAlloc(
                    new Vector3(feet.x, bottomY + _radius, feet.z),
                    new Vector3(feet.x, topY - _radius, feet.z),
                    _radius, _hits, _layers, QueryTriggerInteraction.Ignore)
                : Physics.OverlapBoxNonAlloc(
                    new Vector3(feet.x, (bottomY + topY) * 0.5f, feet.z),
                    new Vector3(_radius * BoxInset, (topY - bottomY) * 0.5f, _radius * BoxInset),
                    _hits, Quaternion.identity, _layers, QueryTriggerInteraction.Ignore);
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

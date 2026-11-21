using HexFall.Surfaces;
using UnityEngine;

namespace HexFall.Player
{
    // Raycasts down from a point and resolves the surface it lands on, ignoring the owner's own colliders.
    public sealed class GroundSurfaceProbe
    {
        private readonly ISurfaceResolver _resolver;
        private readonly SurfaceRegistry _registry;
        private readonly LayerMask _layers;
        private readonly float _startHeight;
        private readonly float _length;
        private readonly Transform _ignoreRoot;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public GroundSurfaceProbe(ISurfaceResolver resolver, SurfaceRegistry registry, LayerMask layers,
            float startHeight, float length, Transform ignoreRoot)
        {
            _resolver = resolver;
            _registry = registry;
            _layers = layers;
            _startHeight = startHeight;
            _length = length;
            _ignoreRoot = ignoreRoot;
        }

        public SurfaceDefinition SurfaceAt(Vector3 point)
        {
            var origin = point + Vector3.up * _startHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, _length, _layers, QueryTriggerInteraction.Ignore);

            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hitCollider = _hits[i].collider;
                if (_ignoreRoot != null && hitCollider.transform.IsChildOf(_ignoreRoot)) continue;
                if (_hits[i].distance >= nearestDistance) continue;
                nearest = i;
                nearestDistance = _hits[i].distance;
            }

            return nearest < 0 ? _registry.DefaultSurface : _resolver.Resolve(_hits[nearest]);
        }
    }
}

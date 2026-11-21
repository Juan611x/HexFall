using UnityEngine;

namespace HexFall.Surfaces
{
    // Finds the surface under a collider hit; terrain support can plug in here later.
    public interface ISurfaceResolver
    {
        SurfaceDefinition Resolve(Collider collider, int triangleIndex);
    }

    public static class SurfaceResolverExtensions
    {
        // Adapter for physics queries that return a RaycastHit.
        public static SurfaceDefinition Resolve(this ISurfaceResolver resolver, RaycastHit hit)
        {
            return resolver.Resolve(hit.collider, hit.triangleIndex);
        }
    }
}

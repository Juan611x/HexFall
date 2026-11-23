using System.Collections.Generic;
using UnityEngine;

namespace HexFall.Surfaces
{
    // Resolves surfaces from the renderer material under a collider, using submesh ranges for mesh colliders.
    public class MeshSurfaceResolver : ISurfaceResolver
    {
        readonly SurfaceRegistry registry;
        readonly Dictionary<Mesh, (int indexStart, int indexCount)[]> rangesByMesh = new Dictionary<Mesh, (int indexStart, int indexCount)[]>();
        readonly Dictionary<Material, Texture> textureByMaterial = new Dictionary<Material, Texture>();
        readonly List<Material> materials = new List<Material>();

        public MeshSurfaceResolver(SurfaceRegistry registry)
        {
            this.registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
        }

        // Call after rebuilding meshes or swapping material textures at runtime.
        public void Clear()
        {
            rangesByMesh.Clear();
            textureByMaterial.Clear();
        }

        public SurfaceDefinition Resolve(Collider collider, int triangleIndex)
        {
            if (collider == null) return registry.DefaultSurface;

            var renderer = collider.GetComponent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
            if (renderer == null) return registry.DefaultSurface;

            Material material;
            if (collider is MeshCollider meshCollider && triangleIndex >= 0
                && meshCollider.sharedMesh != null && meshCollider.sharedMesh.subMeshCount > 1)
            {
                int subMesh = SubMeshLookup.IndexOf(triangleIndex, GetRanges(meshCollider.sharedMesh));
                if (subMesh < 0) return registry.DefaultSurface;
                renderer.GetSharedMaterials(materials);
                material = subMesh < materials.Count ? materials[subMesh] : null;
            }
            else
            {
                material = renderer.sharedMaterial;
            }

            if (material == null) return registry.DefaultSurface;
            return registry.Resolve(GetTexture(material));
        }

        // Reads the submesh descriptors once per mesh.
        (int indexStart, int indexCount)[] GetRanges(Mesh mesh)
        {
            if (rangesByMesh.TryGetValue(mesh, out var ranges)) return ranges;
            ranges = new (int indexStart, int indexCount)[mesh.subMeshCount];
            for (int i = 0; i < ranges.Length; i++)
            {
                var descriptor = mesh.GetSubMesh(i);
                ranges[i] = (descriptor.indexStart, descriptor.indexCount);
            }
            rangesByMesh.Add(mesh, ranges);
            return ranges;
        }

        // Caches the main texture per material.
        Texture GetTexture(Material material)
        {
            if (textureByMaterial.TryGetValue(material, out var texture)) return texture;
            texture = material.mainTexture;
            textureByMaterial.Add(material, texture);
            return texture;
        }
    }
}

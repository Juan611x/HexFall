using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexFall.Surfaces
{
    // One texture to surface mapping.
    [Serializable]
    public struct SurfaceTextureEntry
    {
        public Texture texture;
        public SurfaceDefinition surface;
    }

    // Maps textures to surfaces with a default fallback.
    [CreateAssetMenu(menuName = "HexFall/Surfaces/Surface Registry", fileName = "SurfaceRegistry")]
    public class SurfaceRegistry : ScriptableObject
    {
        [SerializeField] SurfaceTextureEntry[] entries = new SurfaceTextureEntry[0];
        [SerializeField] SurfaceDefinition defaultSurface;

        Dictionary<Texture, SurfaceDefinition> cache;

        public SurfaceDefinition DefaultSurface => defaultSurface;

        // Returns the mapped surface, else the default (which may be null).
        public SurfaceDefinition Resolve(Texture texture)
        {
            if (texture == null) return defaultSurface;
            if (cache == null) BuildCache();
            return cache.TryGetValue(texture, out var surface) ? surface : defaultSurface;
        }

        void OnEnable() => cache = null;

        void OnValidate() => cache = null;

        void BuildCache()
        {
            cache = new Dictionary<Texture, SurfaceDefinition>();
            bool warned = false;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.texture == null || entry.surface == null) continue;
                if (cache.ContainsKey(entry.texture))
                {
                    if (!warned)
                    {
                        Debug.LogWarning($"Duplicate texture '{entry.texture.name}' in surface registry '{name}'; the first entry wins.", this);
                        warned = true;
                    }
                    continue;
                }
                cache.Add(entry.texture, entry.surface);
            }
        }

        // Test and editor hook.
        internal void SetEntries(SurfaceTextureEntry[] value, SurfaceDefinition fallback)
        {
            entries = value;
            defaultSurface = fallback;
            cache = null;
        }
    }
}

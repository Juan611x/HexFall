using UnityEngine;

namespace HexFall.Surfaces
{
    // A walkable material kind and the effects it plays per impact type.
    [CreateAssetMenu(menuName = "HexFall/Surfaces/Surface Definition", fileName = "Surface_New")]
    public class SurfaceDefinition : ScriptableObject
    {
        [SerializeField] string displayName;
        [SerializeField] SurfaceEffect[] effects = new SurfaceEffect[0];

        public string DisplayName => displayName;

        // Finds the effect for an impact type; false when the surface has none.
        public bool TryGetEffect(SurfaceImpact impact, out SurfaceEffect effect)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].impact == impact)
                {
                    effect = effects[i];
                    return true;
                }
            }
            effect = null;
            return false;
        }

        // Test and editor hook.
        internal void SetEffects(SurfaceEffect[] value) => effects = value;
    }
}

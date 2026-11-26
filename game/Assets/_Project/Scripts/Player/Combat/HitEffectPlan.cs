using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // Which of a spell's optional effect prefabs a hit spawns. Unset slots are skipped, so a spell with no
    // effects spawns nothing.
    public static class HitEffectPlan
    {
        private static readonly GameObject[] None = new GameObject[0];

        // The impact effect always; then the enemy-hit effect on something damageable, or the surface
        // particles and decal on anything else.
        public static GameObject[] Select(bool damageable, SpellDefinition.SpellEffects effects)
        {
            var list = new System.Collections.Generic.List<GameObject>(3);
            Add(list, effects.impactEffect);
            if (damageable)
            {
                Add(list, effects.enemyHitEffect);
            }
            else
            {
                Add(list, effects.surfaceImpactParticles);
                Add(list, effects.surfaceDecal);
            }
            return list.Count == 0 ? None : list.ToArray();
        }

        private static void Add(System.Collections.Generic.List<GameObject> list, GameObject prefab)
        {
            if (prefab != null) list.Add(prefab);
        }
    }
}

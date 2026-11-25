using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // Everything a target is told about a hit. Spell is null for sources that are not spells
    // (a training dummy's shooter).
    public readonly struct HitInfo
    {
        public readonly int Damage;
        public readonly GameObject Source;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly SpellDefinition Spell;

        public HitInfo(int damage, GameObject source, Vector3 point, Vector3 normal, SpellDefinition spell = null)
        {
            Damage = damage;
            Source = source;
            Point = point;
            Normal = normal;
            Spell = spell;
        }
    }
}

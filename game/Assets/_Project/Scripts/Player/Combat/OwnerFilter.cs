using UnityEngine;

namespace HexFall.Player
{
    // Which colliders a projectile or beam ignores: the owner's own hierarchy. A sibling of the owner under
    // a shared scene container is not the owner's.
    internal static class OwnerFilter
    {
        public static bool Owns(GameObject owner, Transform candidate) =>
            owner != null && candidate != null && candidate.IsChildOf(owner.transform);
    }
}

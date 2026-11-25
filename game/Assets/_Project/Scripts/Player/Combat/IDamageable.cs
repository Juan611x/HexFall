using UnityEngine;

namespace HexFall.Player
{
    public interface IDamageable
    {
        void TakeHit(HitInfo hit);
    }
}

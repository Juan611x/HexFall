using UnityEngine;

namespace HexFall.Player
{
    public interface IDamageable
    {
        void TakeHit(int damage, GameObject source);
    }
}

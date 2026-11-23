using UnityEngine;

namespace HexFall.Player
{
    // Restores every shield charge of the wizard that touches it. Ignored while the shield is full.
    public class ShieldRefillPickup : Pickup
    {
        protected override bool TryApply(GameObject collector)
        {
            var combat = collector.GetComponentInParent<PlayerCombat>();
            return combat != null && combat.RefillShield();
        }
    }
}

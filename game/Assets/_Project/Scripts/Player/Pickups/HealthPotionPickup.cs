using UnityEngine;

namespace HexFall.Player
{
    // Heals the wizard that touches it. Ignored while their health is full.
    public class HealthPotionPickup : Pickup
    {
        [SerializeField] private int healAmount = 100;

        protected override bool TryApply(GameObject collector)
        {
            var vitals = collector.GetComponentInParent<PlayerVitals>();
            return vitals != null && vitals.Heal(healAmount);
        }
    }
}

using UnityEngine;

namespace HexFall.UI
{
    public enum SealState { Intact, Spent }

    public readonly struct PlayerStatus
    {
        public readonly string Name;
        public readonly int Health;
        public readonly int MaxHealth;
        public readonly int ShieldCharges;
        public readonly int MaxShieldCharges;

        public PlayerStatus(string name, int health, int maxHealth, int shieldCharges, int maxShieldCharges)
        {
            Name = name;
            Health = health;
            MaxHealth = maxHealth;
            ShieldCharges = shieldCharges;
            MaxShieldCharges = maxShieldCharges;
        }
    }

    public static class PlayerHudFormat
    {
        public static float HealthFraction(int health, int maxHealth) =>
            maxHealth <= 0 ? 0f : Mathf.Clamp01((float)health / maxHealth);

        public static string HealthLabel(int health, int maxHealth) =>
            $"{Mathf.Clamp(health, 0, Mathf.Max(0, maxHealth))} / {maxHealth}";

        public static SealState[] Seals(int charges, int maxCharges)
        {
            var seals = new SealState[Mathf.Max(0, maxCharges)];
            var intact = Mathf.Clamp(charges, 0, seals.Length);
            for (var i = 0; i < seals.Length; i++) seals[i] = i < intact ? SealState.Intact : SealState.Spent;
            return seals;
        }

        public static string ShieldLabel(int charges, int maxCharges) =>
            $"Shield {Mathf.Clamp(charges, 0, Mathf.Max(0, maxCharges))} / {maxCharges}";
    }
}

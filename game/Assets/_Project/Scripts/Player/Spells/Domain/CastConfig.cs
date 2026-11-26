using System;

namespace HexFall.Player.Spells
{
    public enum CastPattern { Single, Charged, Burst, Channel }

    // Plain-data mirror of SpellDefinition so the domain never depends on the ScriptableObject.
    public struct CastConfig
    {
        public CastPattern Pattern;
        public int Damage;
        public float CooldownSeconds;

        public float MaxChargeSeconds;
        public int MinDamage;
        public int MaxDamage;

        public int BurstCount;
        public float BurstIntervalSeconds;
        public float BurstSpreadDegrees;

        public float ChannelDurationSeconds;
        public float ChannelTicksPerSecond;
        public int ChannelDamagePerTick;
        public float ChannelCooldownPerSecond;

        // Clamped copy so patterns can trust their inputs (bad asset data must not crash a cast).
        public CastConfig Sanitized()
        {
            var c = this;
            c.Damage = Math.Max(0, c.Damage);
            c.CooldownSeconds = NonNegative(c.CooldownSeconds);
            c.MaxChargeSeconds = NonNegative(c.MaxChargeSeconds);
            c.MinDamage = Math.Max(0, c.MinDamage);
            c.MaxDamage = Math.Max(c.MinDamage, c.MaxDamage);
            c.BurstCount = Math.Max(1, c.BurstCount);
            c.BurstIntervalSeconds = NonNegative(c.BurstIntervalSeconds);
            c.BurstSpreadDegrees = NonNegative(c.BurstSpreadDegrees);
            c.ChannelDurationSeconds = NonNegative(c.ChannelDurationSeconds);
            c.ChannelTicksPerSecond = float.IsNaN(c.ChannelTicksPerSecond) || c.ChannelTicksPerSecond <= 0f
                ? 1f : c.ChannelTicksPerSecond;
            c.ChannelDamagePerTick = Math.Max(0, c.ChannelDamagePerTick);
            c.ChannelCooldownPerSecond = NonNegative(c.ChannelCooldownPerSecond);
            return c;
        }

        private static float NonNegative(float v) => float.IsNaN(v) || v < 0f ? 0f : v;
    }
}

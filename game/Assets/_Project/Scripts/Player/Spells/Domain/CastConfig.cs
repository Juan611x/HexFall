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
        // Charged damage = round(BaseDamage x multiplier): Constant applies ChargeMultiplier at full charge only,
        // Curve samples ChargeCurve (multipliers over charge 0..1, linearly interpolated).
        public int BaseDamage;
        public ChargeDamageMode ChargeMode;
        public float ChargeMultiplier;
        public float[] ChargeCurve;

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
            c.BaseDamage = Math.Max(0, c.BaseDamage);
            c.ChargeMultiplier = ChargeDamage.ConstantMultiplier(c.ChargeMultiplier);
            c.ChargeCurve = SanitizedCurve(c.ChargeCurve);
            c.BurstCount = Math.Max(1, c.BurstCount);
            c.BurstIntervalSeconds = NonNegative(c.BurstIntervalSeconds);
            c.BurstSpreadDegrees = NonNegative(c.BurstSpreadDegrees);
            c.ChannelDurationSeconds = NonNegative(c.ChannelDurationSeconds);
            c.ChannelTicksPerSecond = !IsFinite(c.ChannelTicksPerSecond) || c.ChannelTicksPerSecond <= 0f
                ? 1f : c.ChannelTicksPerSecond;
            c.ChannelDamagePerTick = Math.Max(0, c.ChannelDamagePerTick);
            c.ChannelCooldownPerSecond = NonNegative(c.ChannelCooldownPerSecond);
            return c;
        }

        // NaN and +/-Infinity fall back to 0: an infinite interval or cooldown would lock the spell.
        private static float NonNegative(float v) => !IsFinite(v) || v < 0f ? 0f : v;

        // A bad multiplier (NaN, infinite, negative) falls back to the neutral x1 rather than zeroing or inverting damage.
        private static float Multiplier(float v) => !IsFinite(v) || v < 0f ? 1f : v;

        // Copy with bad samples replaced by x1; a missing or empty table becomes a flat x1 one.
        private static float[] SanitizedCurve(float[] curve)
        {
            if (curve == null || curve.Length == 0) return new[] { 1f, 1f };
            var copy = new float[curve.Length];
            for (var i = 0; i < curve.Length; i++) copy[i] = Multiplier(curve[i]);
            return copy;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}

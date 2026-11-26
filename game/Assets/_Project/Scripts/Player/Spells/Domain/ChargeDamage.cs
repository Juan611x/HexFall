using System;

namespace HexFall.Player.Spells
{
    // How a Charged spell turns its charge level into a damage multiplier.
    public enum ChargeDamageMode
    {
        // A fixed multiplier, only when released at full charge; any lesser charge deals the base damage.
        Constant,
        // A curve over the charge level (0..1) giving the multiplier, baked into a table for the domain.
        Curve,
    }

    // Pure damage maths for charged casts: damage = round(baseDamage x multiplier), halves away from zero.
    public static class ChargeDamage
    {
        // Samples (including both ends) of a baked curve; 33 = 32 equal steps of 1/32.
        public const int DefaultSamples = 33;

        // A charge this close to 1 counts as full (same tolerance as the cast timing).
        public const float FullChargeEpsilon = 1e-4f;

        // Samples f over x = 0..1 into a table (at least 2 samples).
        public static float[] Bake(Func<float, float> f, int samples = DefaultSamples)
        {
            samples = Math.Max(2, samples);
            var table = new float[samples];
            for (var i = 0; i < samples; i++) table[i] = f(i / (float)(samples - 1));
            return table;
        }

        // Linear interpolation inside the table; x is clamped to 0..1. No usable table means no change (x1).
        public static float Sample(float[] table, float x)
        {
            if (table == null || table.Length == 0) return 1f;
            if (table.Length == 1) return table[0];
            if (float.IsNaN(x) || x <= 0f) return table[0];
            if (x >= 1f) return table[table.Length - 1];
            var pos = x * (table.Length - 1);
            var i = (int)pos;
            var t = pos - i;
            return table[i] + (table[i + 1] - table[i]) * t;
        }

        public static float Multiplier(CastConfig config, float charge)
        {
            if (config.ChargeMode == ChargeDamageMode.Curve) return Sample(config.ChargeCurve, charge);
            return charge >= 1f - FullChargeEpsilon ? config.ChargeMultiplier : 1f;
        }

        public static int Damage(CastConfig config, float charge)
        {
            var scaled = config.BaseDamage * (double)Multiplier(config, charge);
            return (int)Math.Max(0.0, Math.Round(scaled, MidpointRounding.AwayFromZero));
        }
    }
}

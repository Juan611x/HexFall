using System;

namespace HexFall.Player.Spells
{
    // A colour as plain floats, so the feedback maths needs no Unity types.
    public readonly struct Rgb
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    // How a charged spell looks as it charges: projectile and orb size, how far the colour travels toward
    // the full-charge colour, and the speed gained at full charge. Plain-data mirror of the SpellDefinition
    // charge feedback settings.
    public struct ChargeFeedbackConfig
    {
        public float MinScale;
        public float MaxScale;
        public float ColorBlend;
        public Rgb FullColor;
        public float SpeedScaleAtMax;

        // Same size, colour and speed at every charge.
        public static ChargeFeedbackConfig Neutral => new ChargeFeedbackConfig
        {
            MinScale = 1f, MaxScale = 1f, ColorBlend = 0f, FullColor = new Rgb(1f, 1f, 1f), SpeedScaleAtMax = 1f,
        };
    }

    // The look at one charge level.
    public readonly struct ChargeLook
    {
        public readonly float Scale;
        public readonly Rgb Color;
        public readonly float SpeedScale;

        public ChargeLook(float scale, Rgb color, float speedScale)
        {
            Scale = scale;
            Color = color;
            SpeedScale = speedScale;
        }
    }

    public static class ChargeFeedback
    {
        private const float MinScale = 0.01f;
        private const float TwoPi = 6.2831855f;

        // How far a hold has charged, 0..1. A max charge that is not positive (or not a number) is full at once.
        public static float Level(float heldSeconds, float maxChargeSeconds)
        {
            if (float.IsNaN(heldSeconds)) return 0f;
            if (float.IsNaN(maxChargeSeconds) || maxChargeSeconds <= 0f) return 1f;
            return Clamp01(Math.Max(0f, heldSeconds) / maxChargeSeconds);
        }

        // Size, colour and speed at the given level (clamped to 0..1) for a spell of the given base colour.
        public static ChargeLook Evaluate(ChargeFeedbackConfig config, Rgb baseColor, float level)
        {
            level = Clamp01(level);
            var blend = Clamp01(config.ColorBlend) * level;
            var scale = Lerp(Scale(config.MinScale), Scale(config.MaxScale), level);
            var speed = Lerp(1f, Scale(config.SpeedScaleAtMax), level);
            var color = new Rgb(
                Lerp(baseColor.R, config.FullColor.R, blend),
                Lerp(baseColor.G, config.FullColor.G, blend),
                Lerp(baseColor.B, config.FullColor.B, blend));
            return new ChargeLook(scale, color, speed);
        }

        // Size multiplier of the small throb of a fully charged orb: 1 until full, then a sine around 1.
        public static float Pulse(float level, float secondsAtFull, float frequencyHz = 6f, float amplitude = 0.08f)
        {
            if (level < 1f || !IsFinite(secondsAtFull) || secondsAtFull < 0f) return 1f;
            if (!IsFinite(frequencyHz) || !IsFinite(amplitude) || amplitude <= 0f) return 1f;
            return 1f + amplitude * (float)Math.Sin(TwoPi * frequencyHz * secondsAtFull);
        }

        // Whether a wind-up animation has played up to its hold point (0..1 of the clip; a hold time that
        // is not a number holds at the start).
        public static bool WindUpReached(float normalizedTime, float holdNormalized)
        {
            if (float.IsNaN(normalizedTime)) return false;
            return normalizedTime >= Clamp01(holdNormalized) - 1e-4f;
        }

        private static float Scale(float v) => IsFinite(v) ? Math.Max(MinScale, v) : 1f;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}

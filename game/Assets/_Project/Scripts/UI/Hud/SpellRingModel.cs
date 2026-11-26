using System;

namespace HexFall.UI
{
    // What the ring around the crosshair is doing: Ready (idle, full), Cooldown (the remaining cooldown
    // drains), Charging (the charge fills), Channelling (the remaining channel time drains) or WindingUp
    // (a channel is waiting for its cast pose: nothing is cast yet).
    public enum SpellRingMode { Ready, Cooldown, Charging, Channelling, WindingUp }

    // One frame of the ring: the mode, how much of the ring is lit (0..1) and whether a charge is full.
    public readonly struct SpellRingState
    {
        public readonly SpellRingMode Mode;
        public readonly float Fraction;
        public readonly bool Full;

        public SpellRingState(SpellRingMode mode, float fraction, bool full)
        {
            Mode = mode;
            Fraction = fraction;
            Full = full;
        }
    }

    // What the player side knows at one moment; every number is a 0..1 fraction.
    public struct SpellRingInput
    {
        public bool Charging;
        public bool Channelling;
        public bool WindingUp;
        public float ChargeLevel;
        public float ChannelRemaining;
        public float CooldownFraction;
    }

    // Pure rule for the spell ring. A held cast (charge, channel) wins over the cooldown; a spell that is
    // neither held nor cooling down shows the idle ring.
    public static class SpellRingModel
    {
        public static SpellRingState Evaluate(SpellRingInput input)
        {
            if (input.Charging)
            {
                var level = Clamp01(input.ChargeLevel);
                return new SpellRingState(SpellRingMode.Charging, level, level >= 1f);
            }
            if (input.Channelling)
            {
                return input.WindingUp
                    ? new SpellRingState(SpellRingMode.WindingUp, 0f, false)
                    : new SpellRingState(SpellRingMode.Channelling, Clamp01(input.ChannelRemaining), false);
            }
            var cooldown = Clamp01(input.CooldownFraction);
            return cooldown > 0f
                ? new SpellRingState(SpellRingMode.Cooldown, cooldown, false)
                : new SpellRingState(SpellRingMode.Ready, 1f, false);
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
    }
}

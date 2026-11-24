using System;

namespace HexFall.Player
{
    // Where a wind current's visible arc is: head and tail in turns around the sphere (unwrapped, the shader wraps
    // them), and the life fade.
    public readonly struct WindArc
    {
        public readonly float Head;
        public readonly float Tail;
        public readonly float Fade;

        public float Length => Head - Tail;

        public WindArc(float head, float tail, float fade)
        {
            Head = head;
            Tail = tail;
            Fade = fade;
        }
    }

    // Pure shape of one wind current over its life (progress 0..1): it enters fast (the arc grows out of nothing and
    // is fully opaque within the first ~12 % of life while its head sweeps most of the way round), then dissipates
    // slowly (the head keeps drifting, the arc shortens and the opacity fades over the remaining ~70 %). Also the
    // gate that opens the currents early in the shield build and the ring size that hugs the sphere at a height.
    public static class WindCurrentArc
    {
        private const float GrowEnd = 0.2f;      // life progress at which the arc has its full length
        private const float ShrinkStart = 0.4f;  // progress at which the arc starts to shorten
        private const float FadeInEnd = 0.12f;   // fast entry
        private const float FadeOutStart = 0.3f; // long dissipation: 70 % of the life
        private const float GateStart = 0.1f;    // build progress at which the currents start to show

        public static WindArc At(float progress, float startTurn, float sweepTurns = 1.1f, float maxLength = 0.4f)
        {
            float p = float.IsNaN(progress) ? 0f : Clamp01(progress);
            float grow = Smooth(p / GrowEnd);
            float shrink = 1f - 0.6f * Smooth((p - ShrinkStart) / (1f - ShrinkStart));
            float length = maxLength * grow * shrink;
            float fade = Smooth(p / FadeInEnd) * (1f - Smooth((p - FadeOutStart) / (1f - FadeOutStart)));
            float rest = 1f - p;
            float head = startTurn + sweepTurns * (1f - rest * rest * rest); // ease-out: fast sweep, slow drift
            return new WindArc(head, head - length, fade);
        }

        // 0 until the shield has started to build, 1 once it is complete; NaN closes the gate.
        public static float Gate(float buildProgress) =>
            float.IsNaN(buildProgress) ? 0f : Smooth((Clamp01(buildProgress) - GateStart) / (1f - GateStart));

        // Radius of a ring at `normalizedHeight` (fraction of the sphere radius), slightly outside the sphere surface.
        public static float RingRadius(float normalizedHeight, float sphereRadius = 0.5f, float margin = 1.06f)
        {
            float y = normalizedHeight * sphereRadius;
            return (float)Math.Sqrt(Math.Max(0f, sphereRadius * sphereRadius - y * y)) * margin;
        }

        private static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}

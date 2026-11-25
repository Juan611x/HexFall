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

    // Pure shape of one wind current over its life (progress 0..1): the arc grows out of nothing while its head
    // sweeps around the sphere, stays at full length, then shrinks and fades out. Also the gate that ties the
    // currents to the shield build and the ring size that hugs the sphere at a height.
    public static class WindCurrentArc
    {
        private const float GrowEnd = 0.35f;     // life progress at which the arc has its full length
        private const float ShrinkStart = 0.75f; // progress at which the arc starts to shorten
        private const float FadeInEnd = 0.2f;
        private const float FadeOutStart = 0.7f;
        private const float GateStart = 0.4f;    // build progress at which the currents start to show

        public static WindArc At(float progress, float startTurn, float sweepTurns = 1.1f, float maxLength = 0.4f)
        {
            float p = float.IsNaN(progress) ? 0f : Clamp01(progress);
            float grow = Smooth(p / GrowEnd);
            float shrink = 1f - 0.6f * Smooth((p - ShrinkStart) / (1f - ShrinkStart));
            float length = maxLength * grow * shrink;
            float fade = Smooth(p / FadeInEnd) * (1f - Smooth((p - FadeOutStart) / (1f - FadeOutStart)));
            float head = startTurn + sweepTurns * p;
            return new WindArc(head, head - length, fade);
        }

        // 0 while the shield is still mostly unbuilt, 1 once it is complete; NaN closes the gate.
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

using UnityEngine;

namespace HexFall.UI
{
    // Geometry of the "hex ring" crosshair (HUD option C), in UI Toolkit coordinates (y grows down).
    // Proportions come from the 48 px design: ring radius 13, colour stroke 2, black outline 5,
    // centre dot 4 px with a 1 px outline.
    public readonly struct CrosshairLayout
    {
        public const float DesignSize = 48f;
        private const float RingRadius = 13f;
        private const float RingStroke = 2f;
        private const float OutlineStroke = 5f;
        private const float DotSide = 4f;
        private const float DotOutlineSide = 6f;

        public readonly Vector2[] Ring;
        public readonly float RingWidth;
        public readonly float OutlineWidth;
        public readonly Rect Dot;
        public readonly Rect DotOutline;

        private CrosshairLayout(Vector2[] ring, float ringWidth, float outlineWidth, Rect dot, Rect dotOutline)
        {
            Ring = ring;
            RingWidth = ringWidth;
            OutlineWidth = outlineWidth;
            Dot = dot;
            DotOutline = dotOutline;
        }

        public static CrosshairLayout Compute(Vector2 centre, float size)
        {
            var scale = Mathf.Max(0f, size) / DesignSize;
            var radius = RingRadius * scale;
            var ring = new Vector2[6];
            for (var i = 0; i < 6; i++)
            {
                var angle = Mathf.Deg2Rad * (60f * i - 90f);
                ring[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return new CrosshairLayout(
                ring,
                RingStroke * scale,
                OutlineStroke * scale,
                Square(centre, DotSide * scale),
                Square(centre, DotOutlineSide * scale));
        }

        private static Rect Square(Vector2 centre, float side) =>
            new Rect(centre.x - side / 2f, centre.y - side / 2f, side, side);
    }
}

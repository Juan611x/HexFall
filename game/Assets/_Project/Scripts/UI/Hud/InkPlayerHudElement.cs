using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Player cluster of the HUD, "ink and runes" style: ink-blot backing, gold hex portrait frame with a
    // rune ring, crimson brush stroke for health and hex seals for shield charges. Drawn in a 400x130
    // design space and scaled to the element's size; labels are separate child elements.
    [UxmlElement]
    public partial class InkPlayerHudElement : VisualElement
    {
        private const float DesignWidth = 400f;

        private static readonly Color Ink = new Color(0.055f, 0.04f, 0.095f, 0.82f);
        private static readonly Color InkBleed = new Color(0.055f, 0.04f, 0.095f, 0.35f);
        private static readonly Color Gold = new Color32(201, 151, 63, 255);
        private static readonly Color GoldFaint = new Color32(201, 151, 63, 170);
        private static readonly Color PortraitFill = new Color32(19, 15, 34, 255);
        private static readonly Color HealthTrack = new Color32(120, 96, 60, 70);
        private static readonly Color Health = new Color32(190, 18, 60, 255);
        private static readonly Color HealthShine = new Color32(255, 228, 230, 90);
        private static readonly Color SealFill = new Color32(17, 94, 89, 255);
        private static readonly Color SealGlyph = new Color32(153, 246, 228, 255);

        private static readonly Vector2 PortraitCentre = new Vector2(58f, 66f);
        private const float PortraitRadius = 44f;
        private static readonly float[] SealX = { 136f, 166f, 196f, 226f, 256f };
        private const float SealY = 94f;
        private const float SealRadius = 13f;
        private const float HealthStartX = 124f;
        private const float HealthEndX = 376f;
        private const float HealthY = 62f;

        private float _health = 1f;
        private SealState[] _seals = { SealState.Intact, SealState.Intact, SealState.Intact };

        public InkPlayerHudElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetStatus(float healthFraction, SealState[] seals)
        {
            _health = Mathf.Clamp01(healthFraction);
            _seals = seals ?? new SealState[0];
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var s = r.width / DesignWidth;
            Vector2 P(float x, float y) => new Vector2(r.xMin + x * s, r.yMin + y * s);
            var p = ctx.painter2D;

            FillPolygon(p, InkOutline.Blob(new Rect(r.xMin + 2f * s, r.yMin + 20f * s, 396f * s, 104f * s), 64, 8f * s, 11), InkBleed);
            FillPolygon(p, InkOutline.Blob(new Rect(r.xMin + 6f * s, r.yMin + 24f * s, 388f * s, 96f * s), 64, 5f * s, 7), Ink);

            var portrait = P(PortraitCentre.x, PortraitCentre.y);
            DrawRuneRing(p, portrait, (PortraitRadius + 6f) * s, s);
            var outer = Hex(portrait, PortraitRadius * s);
            FillPolygon(p, outer, PortraitFill);
            StrokePolygon(p, outer, Gold, 2.5f * s);
            StrokePolygon(p, Hex(portrait, (PortraitRadius - 7f) * s), GoldFaint, 1f * s);

            p.lineCap = LineCap.Round;
            StrokeLine(p, P(HealthStartX - 2f, HealthY), P(HealthEndX + 2f, HealthY), HealthTrack, 11f * s);
            if (_health > 0f)
            {
                var end = Mathf.Lerp(HealthStartX, HealthEndX, _health);
                StrokeLine(p, P(HealthStartX, HealthY), P(end, HealthY), Health, 9f * s);
                if (end - HealthStartX > 10f)
                    StrokeLine(p, P(HealthStartX + 4f, HealthY - 2.5f), P(end - 4f, HealthY - 2.5f), HealthShine, 1.8f * s);
            }

            for (var i = 0; i < _seals.Length && i < SealX.Length; i++)
                DrawSeal(p, P(SealX[i], SealY), SealRadius * s, _seals[i], s);
        }

        private static Vector2[] Hex(Vector2 centre, float radius)
        {
            var points = new Vector2[6];
            for (var i = 0; i < 6; i++)
            {
                var a = Mathf.Deg2Rad * (60f * i - 90f);
                points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return points;
        }

        private static void DrawRuneRing(Painter2D p, Vector2 centre, float radius, float s)
        {
            p.fillColor = GoldFaint;
            const int dots = 32;
            for (var i = 0; i < dots; i++)
            {
                var a = 2f * Mathf.PI * i / dots;
                p.BeginPath();
                p.Arc(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 0.9f * s, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            p.lineCap = LineCap.Round;
            for (var i = 0; i < 8; i++)
            {
                var a = Mathf.PI / 4f * i;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var length = i % 2 == 0 ? 6f : 4f;
                StrokeLine(p, centre + dir * (radius + 2f * s), centre + dir * (radius + (2f + length) * s), Gold, 1.6f * s);
            }
        }

        private static void DrawSeal(Painter2D p, Vector2 centre, float radius, SealState state, float s)
        {
            var hex = Hex(centre, radius);
            if (state == SealState.Intact)
            {
                FillPolygon(p, hex, SealFill);
                StrokePolygon(p, hex, Gold, 2f * s);
                p.lineCap = LineCap.Round;
                StrokeLine(p, centre + new Vector2(0f, -6f * s), centre + new Vector2(0f, 7f * s), SealGlyph, 1.5f * s);
                p.strokeColor = SealGlyph;
                p.lineWidth = 1.5f * s;
                p.BeginPath();
                p.MoveTo(centre + new Vector2(-4f * s, -2f * s));
                p.LineTo(centre + new Vector2(0f, -6f * s));
                p.LineTo(centre + new Vector2(4f * s, -2f * s));
                p.Stroke();
                return;
            }

            // Spent: a dashed gold outline (Painter2D has no dash pattern, so draw the dashes).
            for (var i = 0; i < 6; i++)
            {
                var a = hex[i];
                var b = hex[(i + 1) % 6];
                for (var t = 0f; t < 1f; t += 0.34f)
                    StrokeLine(p, Vector2.Lerp(a, b, t), Vector2.Lerp(a, b, Mathf.Min(1f, t + 0.2f)), GoldFaint, 1.2f * s);
            }
        }

        private static void FillPolygon(Painter2D p, Vector2[] points, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
        }

        private static void StrokePolygon(Painter2D p, Vector2[] points, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Stroke();
        }

        private static void StrokeLine(Painter2D p, Vector2 from, Vector2 to, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(from);
            p.LineTo(to);
            p.Stroke();
        }
    }
}

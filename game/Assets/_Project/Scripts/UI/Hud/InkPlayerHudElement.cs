using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Player cluster of the HUD, "ink and runes" style: ink-blot backing, gold hex portrait frame with a
    // rune ring, crimson brush stroke for health and hex seals for shield charges. Drawn in a 580x200
    // design space and scaled to the element's size; labels are separate child elements.
    [UxmlElement]
    public partial class InkPlayerHudElement : VisualElement
    {
        private const float DesignWidth = 580f;

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

        private static readonly Vector2 PortraitCentre = new Vector2(86f, 104f);
        private static readonly float[] SealX = { 206f, 250f, 294f, 338f, 382f };
        private const float SealY = 145f;
        private const float HealthStartX = 184f;
        private const float HealthEndX = 548f;
        private const float HealthY = 104f;

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

            FillPolygon(p, InkOutline.Blob(new Rect(r.xMin + 2f * s, r.yMin + 34f * s, 572f * s, 160f * s), 72, 12f * s, 11), InkBleed);
            FillPolygon(p, InkOutline.Blob(new Rect(r.xMin + 8f * s, r.yMin + 40f * s, 560f * s, 148f * s), 72, 8f * s, 7), Ink);

            DrawRuneRing(p, P(PortraitCentre.x, PortraitCentre.y), 76f * s, s);
            var outer = Hex(P(PortraitCentre.x, PortraitCentre.y), 66f * s);
            FillPolygon(p, outer, PortraitFill);
            StrokePolygon(p, outer, Gold, 3f * s);
            StrokePolygon(p, Hex(P(PortraitCentre.x, PortraitCentre.y), 56f * s), GoldFaint, 1f * s);

            p.lineCap = LineCap.Round;
            StrokeLine(p, P(HealthStartX - 2f, HealthY), P(HealthEndX + 2f, HealthY), HealthTrack, 16f * s);
            if (_health > 0f)
            {
                var end = Mathf.Lerp(HealthStartX, HealthEndX, _health);
                StrokeLine(p, P(HealthStartX, HealthY), P(end, HealthY), Health, 13f * s);
                if (end - HealthStartX > 12f)
                    StrokeLine(p, P(HealthStartX + 6f, HealthY - 4f), P(end - 6f, HealthY - 4f), HealthShine, 2.5f * s);
            }

            for (var i = 0; i < _seals.Length && i < SealX.Length; i++)
                DrawSeal(p, P(SealX[i], SealY), 21f * s, _seals[i], s);
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
            const int dots = 44;
            for (var i = 0; i < dots; i++)
            {
                var a = 2f * Mathf.PI * i / dots;
                p.BeginPath();
                p.Arc(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, 1.1f * s, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            p.lineCap = LineCap.Round;
            for (var i = 0; i < 8; i++)
            {
                var a = Mathf.PI / 4f * i;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var length = i % 2 == 0 ? 8f : 6f;
                StrokeLine(p, centre + dir * (radius + 2f * s), centre + dir * (radius + (2f + length) * s), Gold, 2f * s);
            }
        }

        private static void DrawSeal(Painter2D p, Vector2 centre, float radius, SealState state, float s)
        {
            var hex = Hex(centre, radius);
            if (state == SealState.Intact)
            {
                FillPolygon(p, hex, SealFill);
                StrokePolygon(p, hex, Gold, 2.5f * s);
                p.lineCap = LineCap.Round;
                StrokeLine(p, centre + new Vector2(0f, -9f * s), centre + new Vector2(0f, 11f * s), SealGlyph, 1.8f * s);
                p.strokeColor = SealGlyph;
                p.lineWidth = 1.8f * s;
                p.BeginPath();
                p.MoveTo(centre + new Vector2(-6f * s, -3f * s));
                p.LineTo(centre + new Vector2(0f, -9f * s));
                p.LineTo(centre + new Vector2(6f * s, -3f * s));
                p.Stroke();
                return;
            }

            // Spent: a dashed gold outline (Painter2D has no dash pattern, so draw the dashes).
            for (var i = 0; i < 6; i++)
            {
                var a = hex[i];
                var b = hex[(i + 1) % 6];
                for (var t = 0f; t < 1f; t += 0.34f)
                    StrokeLine(p, Vector2.Lerp(a, b, t), Vector2.Lerp(a, b, Mathf.Min(1f, t + 0.2f)), GoldFaint, 1.5f * s);
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

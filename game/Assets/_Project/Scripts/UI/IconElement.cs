using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public enum IconKind { Eye, EyeOff, Dice, Envelope, Gear, Speaker, SpeakerOff }

    // Line icons painted with the element's `color`; avoids glyphs the pixel fonts do not ship.
    [UxmlElement]
    public partial class IconElement : VisualElement
    {
        private IconKind _kind;

        public IconElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        [UxmlAttribute]
        public IconKind kind
        {
            get => _kind;
            set
            {
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            var color = resolvedStyle.color;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = Mathf.Max(2f, Mathf.Min(r.width, r.height) / 12f);
            switch (_kind)
            {
                case IconKind.Eye: DrawEye(p, r, false); break;
                case IconKind.EyeOff: DrawEye(p, r, true); break;
                case IconKind.Dice: DrawDice(p, r); break;
                case IconKind.Envelope: DrawEnvelope(p, r); break;
                case IconKind.Gear: DrawGear(p, r); break;
                case IconKind.Speaker: DrawSpeaker(p, r, false); break;
                case IconKind.SpeakerOff: DrawSpeaker(p, r, true); break;
            }
        }

        private static void DrawEye(Painter2D p, Rect r, bool slashed)
        {
            var c = r.center;
            var hw = r.width * 0.5f;
            var hh = r.height * 0.28f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - hw, c.y));
            p.QuadraticCurveTo(new Vector2(c.x, c.y - hh * 2f), new Vector2(c.x + hw, c.y));
            p.QuadraticCurveTo(new Vector2(c.x, c.y + hh * 2f), new Vector2(c.x - hw, c.y));
            p.Stroke();
            p.BeginPath();
            p.Arc(c, r.width * 0.13f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
            if (!slashed) return;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + 2f, r.yMax - 2f));
            p.LineTo(new Vector2(r.xMax - 2f, r.yMin + 2f));
            p.Stroke();
        }

        private static void DrawDice(Painter2D p, Rect r)
        {
            var s = Mathf.Min(r.width, r.height) - 2f;
            var box = new Rect(r.center.x - s / 2f, r.center.y - s / 2f, s, s);
            p.BeginPath();
            p.MoveTo(new Vector2(box.xMin, box.yMin));
            p.LineTo(new Vector2(box.xMax, box.yMin));
            p.LineTo(new Vector2(box.xMax, box.yMax));
            p.LineTo(new Vector2(box.xMin, box.yMax));
            p.ClosePath();
            p.Stroke();
            var pip = s * 0.1f;
            foreach (var (fx, fy) in new[] { (0.27f, 0.27f), (0.73f, 0.27f), (0.5f, 0.5f), (0.27f, 0.73f), (0.73f, 0.73f) })
            {
                p.BeginPath();
                p.Arc(new Vector2(box.xMin + fx * s, box.yMin + fy * s), pip, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }

        private static void DrawEnvelope(Painter2D p, Rect r)
        {
            var w = r.width - 2f;
            var h = r.height * 0.7f;
            var box = new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h);
            p.BeginPath();
            p.MoveTo(new Vector2(box.xMin, box.yMin));
            p.LineTo(new Vector2(box.xMax, box.yMin));
            p.LineTo(new Vector2(box.xMax, box.yMax));
            p.LineTo(new Vector2(box.xMin, box.yMax));
            p.ClosePath();
            p.Stroke();
            p.BeginPath();
            p.MoveTo(new Vector2(box.xMin, box.yMin));
            p.LineTo(new Vector2(box.center.x, box.center.y + h * 0.1f));
            p.LineTo(new Vector2(box.xMax, box.yMin));
            p.Stroke();
        }

        private static void DrawSpeaker(Painter2D p, Rect r, bool crossed)
        {
            Vector2 At(float fx, float fy) => new Vector2(r.xMin + fx * r.width, r.yMin + fy * r.height);

            p.BeginPath();
            p.MoveTo(At(0.08f, 0.38f));
            p.LineTo(At(0.28f, 0.38f));
            p.LineTo(At(0.5f, 0.18f));
            p.LineTo(At(0.5f, 0.82f));
            p.LineTo(At(0.28f, 0.62f));
            p.LineTo(At(0.08f, 0.62f));
            p.ClosePath();
            p.Fill();
            if (crossed)
            {
                p.BeginPath();
                p.MoveTo(At(0.64f, 0.36f));
                p.LineTo(At(0.92f, 0.64f));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(At(0.92f, 0.36f));
                p.LineTo(At(0.64f, 0.64f));
                p.Stroke();
                return;
            }

            var c = At(0.5f, 0.5f);
            foreach (var radius in new[] { 0.22f * r.width, 0.4f * r.width })
            {
                p.BeginPath();
                p.Arc(c, radius, Angle.Degrees(-45f), Angle.Degrees(45f));
                p.Stroke();
            }
        }

        private static void DrawGear(Painter2D p, Rect r)
        {
            var c = r.center;
            var outer = Mathf.Min(r.width, r.height) * 0.46f;
            p.BeginPath();
            p.Arc(c, outer * 0.62f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
            var prev = p.lineWidth;
            p.lineWidth = prev * 1.6f;
            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + dir * outer * 0.72f);
                p.LineTo(c + dir * outer);
                p.Stroke();
            }

            p.lineWidth = prev;
        }
    }
}

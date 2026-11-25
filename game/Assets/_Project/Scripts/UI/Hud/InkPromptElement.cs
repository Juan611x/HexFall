using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // The Interact prompt of the HUD, in the same "ink and runes" style as the player cluster: an ink blot
    // backing with a gold hex key badge. Drawn in a 260x60 design space scaled to the element size; the
    // key ("interact-key") and the text ("interact-text") are child labels. Hidden until Show is called.
    [UxmlElement]
    public partial class InkPromptElement : VisualElement
    {
        private const float DesignWidth = 260f;

        private static readonly Color Ink = new Color(0.055f, 0.04f, 0.095f, 0.82f);
        private static readonly Color InkBleed = new Color(0.055f, 0.04f, 0.095f, 0.35f);
        private static readonly Color Gold = new Color32(201, 151, 63, 255);
        private static readonly Color BadgeFill = new Color32(19, 15, 34, 255);
        private static readonly Vector2 BadgeCentre = new Vector2(34f, 30f);
        private const float BadgeRadius = 20f;

        public InkPromptElement()
        {
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
            generateVisualContent += OnGenerateVisualContent;
        }

        public bool IsShown => style.display == DisplayStyle.Flex;

        public void Show(string key, string text)
        {
            var keyLabel = this.Q<Label>("interact-key");
            if (keyLabel != null) keyLabel.text = key;
            var textLabel = this.Q<Label>("interact-text");
            if (textLabel != null) textLabel.text = text;
            style.display = DisplayStyle.Flex;
            MarkDirtyRepaint();
        }

        public void Hide() => style.display = DisplayStyle.None;

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var s = r.width / DesignWidth;
            var p = ctx.painter2D;

            Fill(p, InkOutline.Blob(new Rect(r.xMin + 1f * s, r.yMin + 4f * s, 258f * s, 52f * s), 48, 5f * s, 21), InkBleed);
            Fill(p, InkOutline.Blob(new Rect(r.xMin + 4f * s, r.yMin + 7f * s, 252f * s, 46f * s), 48, 3f * s, 5), Ink);

            var centre = new Vector2(r.xMin + BadgeCentre.x * s, r.yMin + BadgeCentre.y * s);
            var hex = new Vector2[6];
            for (var i = 0; i < 6; i++)
            {
                var a = Mathf.Deg2Rad * (60f * i - 90f);
                hex[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (BadgeRadius * s);
            }
            Fill(p, hex, BadgeFill);
            p.strokeColor = Gold;
            p.lineWidth = 2.5f * s;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(hex[0]);
            for (var i = 1; i < 6; i++) p.LineTo(hex[i]);
            p.ClosePath();
            p.Stroke();
        }

        private static void Fill(Painter2D p, Vector2[] points, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // HUD crosshair, option C "hex ring": a hexagon outline and a centre dot in the element's `color`,
    // each drawn over a black outline so it reads on bright and dark backgrounds.
    [UxmlElement]
    public partial class HexCrosshairElement : VisualElement
    {
        public HexCrosshairElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var size = Mathf.Min(r.width, r.height);
            if (size <= 0f) return;
            var layout = CrosshairLayout.Compute(r.center, size);
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Miter;

            StrokeRing(p, layout.Ring, Color.black, layout.OutlineWidth);
            FillSquare(p, layout.DotOutline, Color.black);
            StrokeRing(p, layout.Ring, resolvedStyle.color, layout.RingWidth);
            FillSquare(p, layout.Dot, resolvedStyle.color);
        }

        private static void StrokeRing(Painter2D p, Vector2[] ring, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(ring[0]);
            for (var i = 1; i < ring.Length; i++) p.LineTo(ring[i]);
            p.ClosePath();
            p.Stroke();
        }

        private static void FillSquare(Painter2D p, Rect square, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(new Vector2(square.xMin, square.yMin));
            p.LineTo(new Vector2(square.xMax, square.yMin));
            p.LineTo(new Vector2(square.xMax, square.yMax));
            p.LineTo(new Vector2(square.xMin, square.yMax));
            p.ClosePath();
            p.Fill();
        }
    }
}

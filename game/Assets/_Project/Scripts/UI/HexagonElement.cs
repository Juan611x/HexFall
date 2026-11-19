using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Pointy-top hexagon filled with the element's `color` style; UI Toolkit has no clip-path.
    [UxmlElement]
    public partial class HexagonElement : VisualElement
    {
        public HexagonElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var p = ctx.painter2D;
            p.fillColor = resolvedStyle.color;
            p.BeginPath();
            p.MoveTo(new Vector2(r.center.x, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin + r.height * 0.25f));
            p.LineTo(new Vector2(r.xMax, r.yMin + r.height * 0.75f));
            p.LineTo(new Vector2(r.center.x, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMin + r.height * 0.75f));
            p.LineTo(new Vector2(r.xMin, r.yMin + r.height * 0.25f));
            p.ClosePath();
            p.Fill();
        }
    }
}

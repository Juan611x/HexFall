using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // PSX-style loading mark: a hexagon outline that turns in coarse steps. Painted with the element's `color`.
    // Rotation is driven by the element's own scheduler and only runs while it is attached and visible.
    public class SpinnerElement : VisualElement
    {
        private const int StepDegrees = 30;
        private const long StepMilliseconds = 80;

        private IVisualElementScheduledItem _tick;
        private int _angle;
        private bool _spinning;

        public SpinnerElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<AttachToPanelEvent>(_ => Resume());
            RegisterCallback<DetachFromPanelEvent>(_ => Pause());
        }

        // Starts or stops the rotation; a hidden spinner schedules nothing.
        public void SetSpinning(bool spinning)
        {
            _spinning = spinning;
            this.SetVisible(spinning);
            if (spinning) Resume();
            else Pause();
        }

        private void Resume()
        {
            if (!_spinning || _tick != null || panel == null) return;
            _tick = schedule.Execute(Step).Every(StepMilliseconds);
        }

        private void Pause()
        {
            _tick?.Pause();
            _tick = null;
        }

        private void Step()
        {
            _angle = (_angle + StepDegrees) % 360;
            style.rotate = new Rotate(new Angle(_angle, AngleUnit.Degree));
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            var c = r.center;
            var radius = Mathf.Min(r.width, r.height) * 0.5f - 1f;
            var p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = Mathf.Max(2f, radius / 4f);
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            for (var i = 0; i < 6; i++)
            {
                var a = Mathf.Deg2Rad * (60f * i - 90f);
                var pt = new Vector2(c.x + Mathf.Cos(a) * radius, c.y + Mathf.Sin(a) * radius);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Stroke();
        }
    }
}

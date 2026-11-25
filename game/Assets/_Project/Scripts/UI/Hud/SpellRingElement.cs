using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    // The ring around the crosshair, in the active spell's colour, drawn over a black outline like the crosshair:
    // a faint full ring when the spell is ready, an arc that drains with the cooldown, an arc that fills with a
    // charge (brighter and pulsing at full charge) and an arc that drains with a channel. While a channel waits
    // for its pose only the faint ring shows. The arc starts at the top and runs clockwise.
    [UxmlElement]
    public partial class SpellRingElement : VisualElement
    {
        private const float OutlineExtra = 2.5f;
        private const float RingWidth = 3f;
        private const float PulseHz = 2.5f;

        private SpellRingState _state = new SpellRingState(SpellRingMode.Ready, 1f, false);
        private Color _color = new Color(0.369f, 0.922f, 0.831f);

        public SpellRingElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public SpellRingState State => _state;

        public Color RingColor => _color;

        // Takes the state of this frame; repaints only when it changed (a full charge pulses, so it repaints always).
        public void SetState(SpellRingState state, Color color)
        {
            var changed = state.Mode != _state.Mode || state.Full != _state.Full || color != _color
                || !Mathf.Approximately(state.Fraction, _state.Fraction);
            _state = state;
            _color = color;
            if (changed || state.Full) MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var size = Mathf.Min(r.width, r.height);
            if (size <= 0f) return;
            var radius = size * 0.5f - (RingWidth + OutlineExtra) * 0.5f;
            if (radius <= 0f) return;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Butt;

            // The faint track is always there, so the ring reads as a gauge even when empty.
            var trackAlpha = _state.Mode == SpellRingMode.Ready ? 0.4f : 0.22f;
            Arc(p, r.center, radius, 1f, Color.black, RingWidth + OutlineExtra, 0.55f);
            Arc(p, r.center, radius, 1f, _color, RingWidth, trackAlpha);

            if (_state.Mode == SpellRingMode.Ready || _state.Mode == SpellRingMode.WindingUp) return;
            if (_state.Fraction <= 0f) return;

            var color = _color;
            var width = RingWidth;
            if (_state.Mode == SpellRingMode.Charging && _state.Full)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2f * PulseHz);
                color = Color.Lerp(_color, Color.white, 0.45f + 0.35f * pulse);
                width = RingWidth * (1.1f + 0.5f * pulse);
            }
            var alpha = _state.Mode == SpellRingMode.Cooldown ? 0.8f : 1f;
            Arc(p, r.center, radius, _state.Fraction, Color.black, width + OutlineExtra, 0.85f);
            Arc(p, r.center, radius, _state.Fraction, color, width, alpha);
        }

        private static void Arc(Painter2D p, Vector2 centre, float radius, float fraction, Color color, float width, float alpha)
        {
            color.a *= alpha;
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.Arc(centre, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Mathf.Clamp01(fraction)));
            p.Stroke();
        }
    }
}

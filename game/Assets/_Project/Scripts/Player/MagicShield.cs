using UnityEngine;

namespace HexFall.Player
{
    // Translucent shell around the wizard: pops in, holds for the parry window, fades out.
    // A parried hit makes it pulse brighter. Uses the renderer's material colour (URP Unlit, transparent).
    [RequireComponent(typeof(Renderer))]
    public class MagicShield : MonoBehaviour
    {
        [SerializeField] private float popSeconds = 0.06f;
        [SerializeField] private float fadeSeconds = 0.15f;
        [SerializeField] private float deflectBoost = 2.5f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private Vector3 _fullScale;
        private float _startedAt = float.NegativeInfinity;
        private float _holdSeconds;
        private float _boostUntil = float.NegativeInfinity;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _baseColor = _renderer.sharedMaterial.GetColor(BaseColor);
            _fullScale = transform.localScale;
            _renderer.enabled = false;
        }

        public void Flash(float holdSeconds)
        {
            _holdSeconds = holdSeconds;
            _startedAt = Time.time;
            _renderer.enabled = true;
        }

        public void Deflect() => _boostUntil = Time.time + fadeSeconds;

        private void Update()
        {
            if (!_renderer.enabled) return;
            var t = Time.time - _startedAt;
            var end = _holdSeconds + fadeSeconds;
            if (t >= end)
            {
                _renderer.enabled = false;
                return;
            }

            var grow = Mathf.Clamp01(t / popSeconds);
            var fade = t <= _holdSeconds ? 1f : 1f - (t - _holdSeconds) / fadeSeconds;
            transform.localScale = _fullScale * Mathf.Lerp(0.6f, 1f, grow);

            var color = _baseColor;
            var boost = Time.time < _boostUntil ? deflectBoost : 1f;
            color.a = Mathf.Clamp01(_baseColor.a * fade * boost);
            _block.SetColor(BaseColor, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}

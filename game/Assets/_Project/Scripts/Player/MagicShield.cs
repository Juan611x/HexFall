using UnityEngine;

namespace HexFall.Player
{
    // Air sphere around the wizard: it assembles (spiral build, bottom to top), stays built for the parry window and
    // then disassembles. The timing is a pure function (ShieldBuild) fed to the shader's _Build through a
    // MaterialPropertyBlock; the guard rules live elsewhere and never depend on this visual.
    // A parried hit makes it pulse brighter (the _BaseColor alpha is boosted for a moment).
    [RequireComponent(typeof(Renderer))]
    public class MagicShield : MonoBehaviour
    {
        [SerializeField] private float assembleSeconds = 0.12f;
        [SerializeField] private float disassembleSeconds = 0.35f;
        [SerializeField] private float deflectSeconds = 0.15f;
        [SerializeField] private float deflectBoost = 2.5f;
        [SerializeField] private ParticleSystem wisps; // looping gusts orbiting the wizard while the shield is held
        [SerializeField] private ParticleSystem burst; // one-shot ring of faster gusts on a parried hit

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Build = Shader.PropertyToID("_Build");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private ShieldBuild _shieldBuild;
        private float _startedAt = float.NegativeInfinity;
        private float _holdSeconds;
        private float _boostUntil = float.NegativeInfinity;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _baseColor = _renderer.sharedMaterial.GetColor(BaseColor);
            _renderer.enabled = false;
            if (wisps != null) wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Wisps emit while the shield is held; once it starts disassembling they stop and the live ones die out.
        public static bool WispsEmitting(float elapsed, float holdSeconds) => elapsed >= 0f && elapsed < holdSeconds;

        public void Flash(float holdSeconds)
        {
            _holdSeconds = holdSeconds;
            _shieldBuild = new ShieldBuild(holdSeconds, assembleSeconds, disassembleSeconds);
            _startedAt = Time.time;
            _renderer.enabled = true;
            if (wisps != null) wisps.Play(true);
        }

        public void Deflect()
        {
            _boostUntil = Time.time + deflectSeconds;
            if (burst != null) burst.Play(true);
        }

        private void Update()
        {
            if (!_renderer.enabled) return;
            var t = Time.time - _startedAt;
            if (wisps != null && wisps.isEmitting && !WispsEmitting(t, _holdSeconds))
                wisps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Apply(t);
        }

        // Pushes the build state for `elapsed` seconds since Flash to the renderer. Returns false (and hides the
        // renderer) once the shield has fully disassembled.
        public bool Apply(float elapsed)
        {
            if (_shieldBuild == null || _shieldBuild.IsDone(elapsed))
            {
                _renderer.enabled = false;
                return false;
            }

            var color = _baseColor;
            if (Time.time < _boostUntil) color.a = Mathf.Clamp01(_baseColor.a * deflectBoost);
            _block.SetColor(BaseColor, color);
            _block.SetFloat(Build, _shieldBuild.Progress(elapsed));
            _renderer.SetPropertyBlock(_block);
            return true;
        }
    }
}

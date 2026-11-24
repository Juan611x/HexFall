using UnityEngine;

namespace HexFall.Player
{
    // Air sphere around the wizard: it assembles (spiral build, bottom to top), stays built for the parry window and
    // then disassembles. The timing is a pure function (ShieldBuild) fed to the shader's _Build through a
    // MaterialPropertyBlock; the guard rules live elsewhere and never depend on this visual.
    // A parried hit makes it pulse brighter: the _BaseColor alpha is boosted for deflectSeconds, measured on the same
    // elapsed clock as the build. The boost is scaled by the build progress, so a hit near the end of the guard window
    // never over-brightens the shield while it is already dissolving.
    [RequireComponent(typeof(Renderer))]
    public class MagicShield : MonoBehaviour
    {
        [SerializeField] private float assembleSeconds = 0.12f;
        [SerializeField] private float disassembleSeconds = 0.35f;
        [SerializeField] private float deflectSeconds = 0.15f;
        [SerializeField] private float deflectBoost = 2.5f;
        [SerializeField] private ParticleSystem motes; // air motes: drift in while assembling, orbit while held, blow out after
        [SerializeField] private ParticleSystem burst; // one-shot outward ring of motes on a parried hit
        [SerializeField] private WindCurrents currents; // horizontal wind currents wrapping the sphere, gated by the build

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Build = Shader.PropertyToID("_Build");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _baseColor;
        private ShieldBuild _shieldBuild;
        private float _startedAt = float.NegativeInfinity;
        private float _holdSeconds;
        private float _deflectedAt = float.NegativeInfinity; // elapsed since Flash when the last parry landed
        private ShieldBuildPhase _motePhase = ShieldBuildPhase.NotStarted;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            _baseColor = _renderer.sharedMaterial.GetColor(BaseColor);
            _renderer.enabled = false;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Copies the profile of `phase` into the mote system when the phase changes: spawn radius, rate, radial and
        // orbital speed (live motes pick up the new radial speed at once, which is what blows them outward), plus one
        // outward puff when the disassembly starts. Emission stops with the profile's rate; live motes die out.
        private void DriveMotes(ShieldBuildPhase phase)
        {
            if (motes == null || phase == _motePhase) return;
            var profile = ShieldMotes.For(phase);
            var shape = motes.shape;
            shape.radius = profile.SpawnRadius;
            var velocity = motes.velocityOverLifetime;
            velocity.radial = profile.Radial;
            velocity.orbitalY = profile.OrbitalY;
            int puff = ShieldMotes.PuffCount(_motePhase, phase);
            if (puff > 0) motes.Emit(puff);
            _motePhase = phase;
            if (profile.Rate > 0f)
            {
                var emission = motes.emission;
                emission.rateOverTime = profile.Rate;
            }
            else if (motes.isEmitting)
            {
                motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void Flash(float holdSeconds)
        {
            _holdSeconds = holdSeconds;
            _shieldBuild = new ShieldBuild(holdSeconds, assembleSeconds, disassembleSeconds);
            _startedAt = Time.time;
            _deflectedAt = float.NegativeInfinity;
            _renderer.enabled = true;
            if (currents != null) currents.Restart();
            if (motes != null)
            {
                motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // a lingering earlier blow-out must not carry over
                _motePhase = ShieldBuildPhase.NotStarted;
            }
            Apply(0f); // first frame already empty: the material default (_Build 1) must never show
            if (motes != null) motes.Play(true);
        }

        public void Deflect() => Deflect(Time.time - _startedAt);

        // `elapsed` is the time since Flash at which the hit landed.
        public void Deflect(float elapsed)
        {
            _deflectedAt = elapsed;
            if (burst != null) burst.Play(true);
        }

        private void Update()
        {
            // Currents and motes outlive the sphere a little; keep stepping while either is still alive.
            bool lingering = (currents != null && currents.IsLingering) || (motes != null && motes.IsAlive(true));
            if (!_renderer.enabled && !lingering) return;
            Apply(Time.time - _startedAt, Time.deltaTime);
        }

        // Pushes the build state for `elapsed` seconds since Flash to the renderer and the wind currents (which advance by
        // `deltaTime`). Returns false (and hides the renderer) once the shield has fully disassembled; the currents
        // keep being stepped (draining, no new spawns) so the live ones fade out after the sphere is gone.
        public bool Apply(float elapsed, float deltaTime = 0f)
        {
            if (_shieldBuild == null)
            {
                _renderer.enabled = false;
                if (currents != null) currents.Hide();
                return false;
            }

            var phase = _shieldBuild.PhaseAt(elapsed);
            DriveMotes(phase);
            if (phase == ShieldBuildPhase.Done)
            {
                _renderer.enabled = false;
                if (currents != null) currents.Step(deltaTime, 0f, true);
                return false;
            }

            var color = _baseColor;
            float progress = _shieldBuild.Progress(elapsed);
            if (elapsed >= _deflectedAt && elapsed < _deflectedAt + deflectSeconds)
                color.a = Mathf.Clamp01(_baseColor.a * Mathf.Lerp(1f, deflectBoost, progress));
            _block.SetColor(BaseColor, color);
            _block.SetFloat(Build, progress);
            if (currents != null) currents.Step(deltaTime, progress, phase >= ShieldBuildPhase.Disassembling);
            _renderer.SetPropertyBlock(_block);
            return true;
        }
    }
}

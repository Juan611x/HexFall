using System;
using HexFall.Player.Locomotion;
using HexFall.Surfaces;
using UnityEngine;

namespace HexFall.Player
{
    // Plays a footstep when a foot of the wizard touches down. The contact comes from the ANIMATED foot
    // height (not from animation events, which a blend tree would fire from every blended clip), the
    // sound from the surface under that foot. Clips never repeat on a foot and each step varies in
    // pitch and volume within the effect's ranges. Each foot has its own source so its pitch is its own.
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerSurfaceSensor), typeof(PlayerIKDriver))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [Tooltip("Foot height above the character root that arms the next contact.")]
        [SerializeField] private float liftHeight = 0.12f;
        [Tooltip("Foot height above the character root at or below which the foot counts as planted.")]
        [SerializeField] private float contactHeight = 0.08f;
        [SerializeField] private float minStepInterval = 0.18f;
        [Tooltip("Planar speed (m/s) above which steps are heard.")]
        [SerializeField] private float minSpeed = 0.2f;
        [Range(0f, 1f)] [SerializeField] private float stepVolume = 0.7f;

        private PlayerMotor _motor;
        private PlayerSurfaceSensor _sensor;
        private PlayerIKDriver _ik;
        private FootContactDetector _leftDetector;
        private FootContactDetector _rightDetector;
        private AudioSource _leftSource;
        private AudioSource _rightSource;
        private readonly ClipVariation _leftVariation = new ClipVariation();
        private readonly ClipVariation _rightVariation = new ClipVariation();

        // Last animated heights above the root, for tuning and probes.
        public float LeftHeight { get; private set; }
        public float RightHeight { get; private set; }

        // Raised with (isLeft, clip) for every step played.
        public event Action<bool, AudioClip> Stepped;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _sensor = GetComponent<PlayerSurfaceSensor>();
            _ik = GetComponent<PlayerIKDriver>();
            var settings = new FootContactSettings(liftHeight, contactHeight, minStepInterval);
            _leftDetector = new FootContactDetector(settings);
            _rightDetector = new FootContactDetector(settings);
            _leftSource = CreateSource();
            _rightSource = CreateSource();
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void LateUpdate()
        {
            var machine = _motor.Machine;
            if (machine == null || !_ik.TryGetAnimatedFeet(out var left, out var right)) return;

            var rootY = transform.position.y;
            LeftHeight = left.y - rootY;
            RightHeight = right.y - rootY;

            var allowed = FootContactDetector.StepsAllowed(
                _motor.IsGrounded && machine.Current == LocomotionStateId.Grounded,
                machine.Context.PlanarSpeed, minSpeed);
            var dt = Time.deltaTime;
            if (_leftDetector.Tick(LeftHeight, allowed, dt)) Step(true, left);
            if (_rightDetector.Tick(RightHeight, allowed, dt)) Step(false, right);
        }

        private void Step(bool isLeft, Vector3 footPosition)
        {
            var surface = _sensor.SurfaceAt(footPosition);
            if (surface == null || !surface.TryGetEffect(SurfaceImpact.Footstep, out var effect)) return;

            var variation = isLeft ? _leftVariation : _rightVariation;
            var index = variation.Pick(effect, UnityEngine.Random.value);
            if (index < 0 || effect.clips[index] == null) return;

            var source = isLeft ? _leftSource : _rightSource;
            source.pitch = SurfaceEffectPicker.Lerp(effect.pitchRange, UnityEngine.Random.value);
            var volume = SurfaceEffectPicker.Lerp(effect.volumeRange, UnityEngine.Random.value) * stepVolume * EffectsVolume.Current();
            source.PlayOneShot(effect.clips[index], volume);
            Stepped?.Invoke(isLeft, effect.clips[index]);
        }
    }
}

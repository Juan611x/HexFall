using System;
using HexFall.Player.Locomotion;
using HexFall.Surfaces;
using UnityEngine;

namespace HexFall.Player
{
    // Plays a landing sound when the wizard touches the ground after a fall, from the surface under
    // the wizard. Volume follows the vertical speed of the impact; small drops stay silent.
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerSurfaceSensor))]
    public sealed class PlayerLandings : MonoBehaviour
    {
        [Tooltip("Vertical speed (m/s) below which a landing is silent (a stair, a small step).")]
        [SerializeField] private float minSpeed = 2f;
        [Tooltip("Vertical speed (m/s) at which a landing is at full loudness.")]
        [SerializeField] private float fullSpeed = 9f;
        [Range(0f, 1f)] [SerializeField] private float landingVolume = 0.9f;

        private PlayerMotor _motor;
        private PlayerSurfaceSensor _sensor;
        private AudioSource _source;
        private LocomotionMachine _subscribed;
        private readonly ClipVariation _variation = new ClipVariation();

        // Raised with (landingSpeed, loudness, clip) for every landing played.
        public event Action<float, float, AudioClip> Landed;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _sensor = GetComponent<PlayerSurfaceSensor>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        // The motor creates its machine in Awake; subscribe here, and again in Start in case it was
        // enabled after this component.
        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_motor == null || _motor.Machine == null || _subscribed == _motor.Machine) return;
            Unsubscribe();
            _subscribed = _motor.Machine;
            _subscribed.StateChanged += OnStateChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.StateChanged -= OnStateChanged;
            _subscribed = null;
        }

        private void OnStateChanged(LocomotionStateId from, LocomotionStateId to)
        {
            if (to != LocomotionStateId.Grounded || (from != LocomotionStateId.Fall && from != LocomotionStateId.Jump)) return;
            Land(_subscribed.Context.LastLandingSpeed);
        }

        private void Land(float landingSpeed)
        {
            var loudness = LandingLoudness.For(landingSpeed, minSpeed, fullSpeed);
            if (loudness <= 0f) return;

            var surface = _sensor.SurfaceAt(transform.position);
            if (surface == null || !surface.TryGetEffect(SurfaceImpact.Landing, out var effect)) return;

            var index = _variation.Pick(effect, UnityEngine.Random.value);
            if (index < 0 || effect.clips[index] == null) return;

            _source.pitch = SurfaceEffectPicker.Lerp(effect.pitchRange, UnityEngine.Random.value);
            var volume = SurfaceEffectPicker.Lerp(effect.volumeRange, UnityEngine.Random.value) * loudness * landingVolume * EffectsVolume.Current();
            _source.PlayOneShot(effect.clips[index], volume);
            Landed?.Invoke(landingSpeed, loudness, effect.clips[index]);
        }
    }
}

using System;
using HexFall.Player.Locomotion;
using HexFall.Surfaces;
using UnityEngine;

namespace HexFall.Player
{
    // Plays a scrape from the surface under the wizard while it slides: starts on entering the Slide
    // state (louder the faster the entry) and fades out shortly after leaving it.
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerSurfaceSensor))]
    public sealed class PlayerSlideScrape : MonoBehaviour
    {
        [Tooltip("Planar speed (m/s) on entering the slide at which the scrape is at full loudness.")]
        [SerializeField] private float fullSpeed = 6f;
        [Range(0f, 1f)] [SerializeField] private float scrapeVolume = 0.8f;
        [Tooltip("Seconds the scrape takes to fade out after the slide ends.")]
        [SerializeField] private float fadeOutSeconds = 0.12f;

        private PlayerMotor _motor;
        private PlayerSurfaceSensor _sensor;
        private AudioSource _source;
        private LocomotionMachine _subscribed;
        private readonly ClipVariation _variation = new ClipVariation();
        private bool _fading;
        private float _fadeElapsed;
        private float _fadeStartVolume;

        // Raised with (entrySpeed, loudness, clip) for every scrape started.
        public event Action<float, float, AudioClip> Scraped;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _sensor = GetComponent<PlayerSurfaceSensor>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.loop = false;
        }

        // The motor creates its machine in Awake; subscribe here, and again in Start in case it was
        // enabled after this component.
        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            StopScrape();
        }

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
            if (to == LocomotionStateId.Slide) Scrape(_subscribed.Context.PlanarSpeed);
            else if (from == LocomotionStateId.Slide) BeginFade();
        }

        private void Scrape(float entrySpeed)
        {
            var surface = _sensor.SurfaceAt(transform.position);
            if (surface == null || !surface.TryGetEffect(SurfaceImpact.Slide, out var effect)) return;

            var index = _variation.Pick(effect, UnityEngine.Random.value);
            if (index < 0 || effect.clips[index] == null) return;

            var loudness = SlideScrapeLevels.SpeedFactor(entrySpeed, fullSpeed);
            _fading = false;
            _source.Stop();
            _source.clip = effect.clips[index];
            _source.pitch = SurfaceEffectPicker.Lerp(effect.pitchRange, UnityEngine.Random.value);
            _source.volume = Mathf.Clamp01(SurfaceEffectPicker.Lerp(effect.volumeRange, loudness) * scrapeVolume * EffectsVolume.Current());
            _source.Play();
            Scraped?.Invoke(entrySpeed, loudness, effect.clips[index]);
        }

        private void BeginFade()
        {
            if (!_source.isPlaying) return;
            _fading = true;
            _fadeElapsed = 0f;
            _fadeStartVolume = _source.volume;
        }

        private void Update()
        {
            if (!_fading) return;
            _fadeElapsed += Time.deltaTime;
            var gain = SlideScrapeLevels.FadeGain(_fadeElapsed, fadeOutSeconds);
            if (gain <= 0f || !_source.isPlaying)
            {
                StopScrape();
                return;
            }
            _source.volume = _fadeStartVolume * gain;
        }

        private void StopScrape()
        {
            _fading = false;
            if (_source != null) _source.Stop();
        }
    }
}

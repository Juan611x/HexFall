using HexFall.Player.Actions;
using HexFall.Player.Locomotion;
using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // The local player's own action sounds, played from the action machine's events on a 2D source.
    [RequireComponent(typeof(PlayerCombat))]
    public sealed class WizardSfx : MonoBehaviour
    {
        [SerializeField] private WizardSfxLibrary library;

        private PlayerCombat _combat;
        private AudioSource _source;
        private PlayerMotor _motor;
        private ActionMachine _subscribed;
        private LocomotionMachine _subscribedLocomotion;

        public WizardSfxLibrary Library => library;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
            _motor = GetComponent<PlayerMotor>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        // The combat component creates its machine in Awake; subscribe here, and again in Start in case
        // it was enabled after this component.
        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            SubscribeLocomotion();
            if (_combat == null || _combat.Machine == null || _subscribed == _combat.Machine) return;
            UnsubscribeCombat();
            _subscribed = _combat.Machine;
            _subscribed.StateChanged += OnStateChanged;
            _subscribed.HitResolved += OnHitResolved;
            _subscribed.ShotFired += OnShotFired;
            _subscribed.AttackPressed += OnAttackPressed;
            _subscribed.ParryPressed += OnParryPressed;
        }

        // The motor is optional: without one the wizard just has no jump sound.
        private void SubscribeLocomotion()
        {
            if (_motor == null || _motor.Machine == null || _subscribedLocomotion == _motor.Machine) return;
            UnsubscribeLocomotion();
            _subscribedLocomotion = _motor.Machine;
            _subscribedLocomotion.StateChanged += OnLocomotionChanged;
        }

        private void UnsubscribeLocomotion()
        {
            if (_subscribedLocomotion == null) return;
            _subscribedLocomotion.StateChanged -= OnLocomotionChanged;
            _subscribedLocomotion = null;
        }

        private void Unsubscribe()
        {
            UnsubscribeLocomotion();
            UnsubscribeCombat();
        }

        private void UnsubscribeCombat()
        {
            if (_subscribed == null) return;
            _subscribed.StateChanged -= OnStateChanged;
            _subscribed.HitResolved -= OnHitResolved;
            _subscribed.ShotFired -= OnShotFired;
            _subscribed.AttackPressed -= OnAttackPressed;
            _subscribed.ParryPressed -= OnParryPressed;
            _subscribed = null;
        }

        private void OnStateChanged(ActionStateId from, ActionStateId to) => Play(WizardSfxCues.ForStateChange(from, to));

        private void OnLocomotionChanged(LocomotionStateId from, LocomotionStateId to) => Play(WizardSfxCues.ForLocomotionChange(from, to));

        private void OnHitResolved(HitResult hit) => Play(WizardSfxCues.ForHit(hit));

        private void OnShotFired(ProjectileShot shot) => Play(WizardSfxCues.ForBoltFired());

        private void OnAttackPressed() => Play(WizardSfxCues.ForAttackPress());

        private void OnParryPressed() => Play(WizardSfxCues.ForParryPress());

        private void Play(WizardSfxCue cue)
        {
            if (cue == WizardSfxCue.None || library == null) return;
            if (!library.TryGet(cue, out var clip, out var volume)) return;
            _source.PlayOneShot(clip, volume * EffectsVolume.Current());
        }
    }
}

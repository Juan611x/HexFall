using HexFall.Player.Actions;
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
        private ActionMachine _subscribed;

        public WizardSfxLibrary Library => library;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
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
            if (_combat == null || _combat.Machine == null || _subscribed == _combat.Machine) return;
            Unsubscribe();
            _subscribed = _combat.Machine;
            _subscribed.StateChanged += OnStateChanged;
            _subscribed.HitResolved += OnHitResolved;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.StateChanged -= OnStateChanged;
            _subscribed.HitResolved -= OnHitResolved;
            _subscribed = null;
        }

        private void OnStateChanged(ActionStateId from, ActionStateId to) => Play(WizardSfxCues.ForStateChange(from, to));

        private void OnHitResolved(HitResult hit) => Play(WizardSfxCues.ForHit(hit));

        private void Play(WizardSfxCue cue)
        {
            if (cue == WizardSfxCue.None || library == null) return;
            if (!library.TryGet(cue, out var clip, out var volume)) return;
            _source.PlayOneShot(clip, volume * EffectsVolume.Current());
        }
    }
}

using System;
using System.Collections.Generic;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using UnityEngine;

namespace HexFall.Player
{
    // Drives the UpperBody animator layer from the action machine: cross-fades to the state mapped to
    // the new action state. The controller has no trigger or exit-time transitions on that layer, so the
    // machine alone decides when a pose ends (Ready fades back to Empty).
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(Animator))]
    public sealed class ActionAnimatorBridge : MonoBehaviour
    {
        [Serializable]
        private sealed class StateMapping
        {
            public ActionStateId state;
            public string stateName;

            public StateMapping(ActionStateId state, string stateName)
            {
                this.state = state;
                this.stateName = stateName;
            }
        }

        // Resolved by name so a reordered controller still targets the right layer.
        public const string UpperBodyLayerName = "UpperBody";

        // Float parameter driving the speed of the Charge state: 1 plays the wind-up, 0 holds the pose.
        public const string ChargeSpeedParameter = "ChargeSpeed";

        [SerializeField] private float crossFadeSeconds = 0.1f;
        [SerializeField] private StateMapping[] stateMappings =
        {
            new StateMapping(ActionStateId.Ready, "Empty"),
            new StateMapping(ActionStateId.Attack, "Attack"),
            new StateMapping(ActionStateId.Parry, "Parry"),
            new StateMapping(ActionStateId.ParryEmpty, "Parry"),
            new StateMapping(ActionStateId.Charging, "Charge"),
            new StateMapping(ActionStateId.Channelling, "Attack"),
        };

        private Animator _animator;
        private PlayerCombat _combat;
        private ActionMachine _subscribed;
        private int _upperBodyLayer;
        private int _chargeSpeedId;
        private bool _hasChargeSpeed;
        private int _windUpHash;
        private float _windUpHold;
        private bool _holdingWindUp;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _combat = GetComponent<PlayerCombat>();
            _upperBodyLayer = _animator.GetLayerIndex(UpperBodyLayerName);
            _chargeSpeedId = Animator.StringToHash(ChargeSpeedParameter);
            _hasChargeSpeed = HasParameter(_chargeSpeedId);
            if (_upperBodyLayer < 0) Debug.LogWarning($"{name}: animator has no '{UpperBodyLayerName}' layer.", this);
        }

        // The combat component creates its machine in Awake; subscribe here, and again in Start in case
        // it was enabled after this component.
        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        // Animator states the UpperBody layer must contain for the mapping to work.
        public IEnumerable<string> RequiredAnimatorStates()
        {
            foreach (var mapping in stateMappings) yield return mapping.stateName;
        }

        // Animator state the given action state cross-fades to, or null when it is not mapped.
        public string StateNameFor(ActionStateId state)
        {
            foreach (var mapping in stateMappings)
            {
                if (mapping.state == state) return mapping.stateName;
            }
            return null;
        }

        private void Subscribe()
        {
            if (_combat == null || _combat.Machine == null || _subscribed == _combat.Machine) return;
            Unsubscribe();
            _subscribed = _combat.Machine;
            _subscribed.StateChanged += OnStateChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.StateChanged -= OnStateChanged;
            _subscribed = null;
        }

        // While a charge or a channel wind-up is held: once the wind-up has played to the spell's hold time,
        // freeze the pose (speed 0) on exactly that time. A held channel reports the pose to the combat component,
        // which is what really starts it. Leaving the state resumes the speed, so the throw plays normally.
        private void Update() => HoldWindUp();

        internal void HoldWindUp()
        {
            if (_upperBodyLayer < 0 || _windUpHash == 0) return;
            if (_combat == null || _combat.Machine == null) return;
            var current = _combat.Machine.Current;
            if (current != ActionStateId.Charging && current != ActionStateId.Channelling) return;
            if (_holdingWindUp)
            {
                // Re-pinned every frame so a state without the speed parameter holds too.
                _animator.Play(_windUpHash, _upperBodyLayer, Mathf.Clamp01(_windUpHold));
                return;
            }
            var info = _animator.IsInTransition(_upperBodyLayer)
                ? _animator.GetNextAnimatorStateInfo(_upperBodyLayer)
                : _animator.GetCurrentAnimatorStateInfo(_upperBodyLayer);
            if (info.shortNameHash != _windUpHash || !ChargeFeedback.WindUpReached(info.normalizedTime, _windUpHold)) return;
            _holdingWindUp = true;
            SetChargeSpeed(0f);
            _animator.Play(_windUpHash, _upperBodyLayer, Mathf.Clamp01(_windUpHold));
            if (current == ActionStateId.Channelling) _combat.ReportPoseReached();
        }

        // The wind-up state of a spell: its own windUpState when the layer has it, else the one mapped to the action state.
        private string WindUpStateFor(string own, string mapped)
        {
            if (string.IsNullOrEmpty(own) || own == mapped) return mapped;
            return _animator.HasState(_upperBodyLayer, Animator.StringToHash(own)) ? own : mapped;
        }

        private void SetChargeSpeed(float value)
        {
            if (_hasChargeSpeed) _animator.SetFloat(_chargeSpeedId, value);
        }

        private bool HasParameter(int hash)
        {
            foreach (var parameter in _animator.parameters)
                if (parameter.nameHash == hash) return true;
            return false;
        }

        private void OnStateChanged(ActionStateId from, ActionStateId to)
        {
            if (_upperBodyLayer < 0) return;
            _holdingWindUp = false;
            _windUpHash = 0;
            SetChargeSpeed(1f);
            var stateName = StateNameFor(to);
            var spell = _combat != null ? _combat.ActiveSpell : null;
            // A charge always winds up; a channel only when its spell has a wind-up pose (otherwise it plays the plain mapping).
            var winds = to == ActionStateId.Charging || (to == ActionStateId.Channelling && spell != null && spell.WindUp.Enabled);
            if (winds && stateName != null)
            {
                var pose = spell != null ? spell.WindUp : default;
                stateName = WindUpStateFor(pose.State, stateName);
                _windUpHash = Animator.StringToHash(stateName);
                _windUpHold = spell != null ? pose.HoldNormalized : 0.5f;
            }
            if (stateName == null)
            {
                Debug.LogWarning($"[ActionAnimatorBridge] No animator state mapped for '{to}'.", this);
                return;
            }

            _animator.CrossFadeInFixedTime(Animator.StringToHash(stateName), crossFadeSeconds, _upperBodyLayer);
        }
    }
}

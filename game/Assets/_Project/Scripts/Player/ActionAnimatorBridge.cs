using System;
using System.Collections.Generic;
using HexFall.Player.Actions;
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

        [SerializeField] private float crossFadeSeconds = 0.1f;
        [SerializeField] private StateMapping[] stateMappings =
        {
            new StateMapping(ActionStateId.Ready, "Empty"),
            new StateMapping(ActionStateId.Attack, "Attack"),
            new StateMapping(ActionStateId.Parry, "Parry"),
            new StateMapping(ActionStateId.ParryEmpty, "Parry"),
            new StateMapping(ActionStateId.Charging, "Attack"),
            new StateMapping(ActionStateId.Channelling, "Attack"),
        };

        private Animator _animator;
        private PlayerCombat _combat;
        private ActionMachine _subscribed;
        private int _upperBodyLayer;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _combat = GetComponent<PlayerCombat>();
            _upperBodyLayer = _animator.GetLayerIndex(UpperBodyLayerName);
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

        private void OnStateChanged(ActionStateId from, ActionStateId to)
        {
            if (_upperBodyLayer < 0) return;
            var stateName = StateNameFor(to);
            if (stateName == null)
            {
                Debug.LogWarning($"[ActionAnimatorBridge] No animator state mapped for '{to}'.", this);
                return;
            }

            _animator.CrossFadeInFixedTime(Animator.StringToHash(stateName), crossFadeSeconds, _upperBodyLayer);
        }
    }
}

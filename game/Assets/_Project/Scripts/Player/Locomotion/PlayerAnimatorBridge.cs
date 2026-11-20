using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Drives the Animator from the locomotion machine: cross-fades to the state chosen by
    // AnimatorStateSelector when the locomotion state changes and keeps the blend tree parameters
    // up to date every frame.
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimatorBridge : MonoBehaviour
    {
        [Serializable]
        private sealed class StateMapping
        {
            public string key;
            public string stateName;

            public StateMapping(string key, string stateName)
            {
                this.key = key;
                this.stateName = stateName;
            }
        }

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int FreeFallId = Animator.StringToHash("FreeFall");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        [SerializeField] private float crossFadeSeconds = 0.15f;
        [SerializeField] private float landingFadeSeconds = 0.1f;
        [SerializeField] private float hardLandingSpeed = 9f;
        [SerializeField] private StateMapping[] stateMappings =
        {
            new StateMapping(AnimatorStateSelector.Locomotion, "Idle Walk Run Blend"),
            new StateMapping(AnimatorStateSelector.JumpStart, "JumpStart"),
            new StateMapping(AnimatorStateSelector.InAir, "InAir"),
            new StateMapping(AnimatorStateSelector.JumpLand, "JumpLand"),
        };

        private Animator _animator;
        private PlayerMotor _motor;
        private LocomotionMachine _subscribed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _motor = GetComponent<PlayerMotor>();
        }

        // The motor creates its machine in Awake; subscribe here, and again in Start in case the
        // motor was enabled after this component.
        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (_motor == null || _motor.Machine == null) return;

            var machine = _motor.Machine;
            _animator.SetFloat(SpeedId, machine.Context.PlanarSpeed);
            _animator.SetFloat(MotionSpeedId, 1f);
            _animator.SetBool(GroundedId, _motor.IsGrounded);
            _animator.SetBool(FreeFallId, machine.Current == LocomotionStateId.Fall);
            _animator.SetBool(JumpId, machine.Current == LocomotionStateId.Jump);
        }

        // Animator state names the controller must contain for the mapping to work.
        public IEnumerable<string> RequiredAnimatorStates()
        {
            foreach (var mapping in stateMappings) yield return mapping.stateName;
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
            var context = _motor.Machine.Context;
            var key = AnimatorStateSelector.For(to, from, context.LastLandingSpeed, context.PlanarSpeed, hardLandingSpeed);
            var stateName = StateNameFor(key);
            if (stateName == null) return;

            var fade = to == LocomotionStateId.Grounded ? landingFadeSeconds : crossFadeSeconds;
            _animator.CrossFadeInFixedTime(Animator.StringToHash(stateName), fade, 0);
        }

        private string StateNameFor(string key)
        {
            foreach (var mapping in stateMappings)
            {
                if (mapping.key == key) return mapping.stateName;
            }
            return null;
        }
    }
}

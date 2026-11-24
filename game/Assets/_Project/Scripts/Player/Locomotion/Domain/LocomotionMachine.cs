using System;
using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Runs the current locomotion state each tick, keeps the shared timers (time since grounded,
    // jump buffer, jump cooldown) and performs at most one transition per tick.
    public sealed class LocomotionMachine
    {
        private readonly ILocomotionState[] _states;
        private readonly float _groundedStickSpeed;
        private ILocomotionState _current;

        public LocomotionMachine(LocomotionSettings settings, LocomotionStateId initial)
            : this(new ILocomotionState[] { new GroundedState(settings), new JumpState(settings), new FallState(settings), new CrouchState(settings), new SlideState(settings), new ClimbState(settings) }, initial, settings.groundedStickSpeed)
        {
        }

        public LocomotionMachine(ILocomotionState[] states, LocomotionStateId initial, float groundedStickSpeed = -2f)
        {
            _states = states;
            _groundedStickSpeed = groundedStickSpeed;
            Context = new LocomotionContext();
            _current = Find(initial);
            _current.Enter(Context);
        }

        public LocomotionContext Context { get; }

        public LocomotionStateId Current => _current.Id;

        public event Action<LocomotionStateId, LocomotionStateId> StateChanged;

        public void Tick(PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            Context.TimeSinceGrounded = ground.Grounded ? 0f : Context.TimeSinceGrounded + deltaTime;
            Context.TimeSinceJumpPressed = intent.JumpPressed ? 0f : Context.TimeSinceJumpPressed + deltaTime;
            if (Context.JumpCooldown > 0f) Context.JumpCooldown -= deltaTime;

            var next = _current.Tick(Context, intent, ground, deltaTime);
            if (next == _current.Id) return;

            var target = Find(next);
            var from = _current.Id;
            _current.Exit(Context);
            _current = target;
            _current.Enter(Context);
            StateChanged?.Invoke(from, next);
        }

        // Throws the body upward: enters Jump with the given vertical speed instead of the jump-height
        // speed, keeping the planar velocity. Works from any state but Climb; returns false when ignored.
        public bool Launch(float verticalSpeed)
        {
            if (_current.Id == LocomotionStateId.Climb) return false;

            Context.LaunchSpeed = verticalSpeed;
            var from = _current.Id;
            _current.Exit(Context);
            _current = Find(LocomotionStateId.Jump);
            _current.Enter(Context);
            StateChanged?.Invoke(from, LocomotionStateId.Jump);
            return true;
        }

        // Places the body: sets position and facing and enters Grounded (or Fall when the new spot is not
        // on the ground). Momentum is kept: the planar velocity (and so the speed) turns with the change
        // of facing, so the body keeps moving the same way relative to where it looks; the vertical speed
        // resets to the grounded stick speed (zero when airborne). Works from any state but Climb;
        // returns false when ignored.
        public bool Teleport(Vector3 position, float yaw, bool grounded)
        {
            if (_current.Id == LocomotionStateId.Climb) return false;

            var turn = Mathf.DeltaAngle(Context.FacingYaw, yaw);
            var planar = Quaternion.Euler(0f, turn, 0f) * new Vector3(Context.Velocity.x, 0f, Context.Velocity.z);
            Context.Position = position;
            Context.FacingYaw = yaw;
            Context.YawVelocity = 0f;
            Context.PlanarSpeed = planar.magnitude;
            Context.Velocity = new Vector3(planar.x, grounded ? _groundedStickSpeed : 0f, planar.z);
            Context.LaunchSpeed = float.NaN;
            Context.ClimbOverDrop = false;
            Context.TimeSinceGrounded = grounded ? 0f : Context.TimeSinceGrounded;

            var from = _current.Id;
            var to = grounded ? LocomotionStateId.Grounded : LocomotionStateId.Fall;
            _current.Exit(Context);
            _current = Find(to);
            _current.Enter(Context);
            if (from != to) StateChanged?.Invoke(from, to);
            return true;
        }

        private ILocomotionState Find(LocomotionStateId id)
        {
            foreach (var state in _states)
            {
                if (state.Id == id) return state;
            }
            throw new ArgumentException("No locomotion state registered for " + id);
        }
    }
}

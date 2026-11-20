using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // On the ground: accelerates toward the target speed, holds a small downward speed, and jumps on
    // a press, a buffered press or within the coyote window after leaving the ground.
    public sealed class GroundedState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;

        public GroundedState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Grounded;

        public void Enter(LocomotionContext ctx) { }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, 1f);
            ctx.Velocity = new Vector3(ctx.Velocity.x, _settings.groundedStickSpeed, ctx.Velocity.z);

            var canJump = ctx.JumpCooldown <= 0f
                && ctx.TimeSinceJumpPressed <= _settings.jumpBufferSeconds
                && ctx.TimeSinceGrounded <= _settings.coyoteSeconds;
            if (canJump) return LocomotionStateId.Jump;
            if (!ground.Grounded && ctx.TimeSinceGrounded > _settings.coyoteSeconds) return LocomotionStateId.Fall;
            return Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }

    // Rising part of a jump: launches on enter, gravity pulls until the apex, reduced air control.
    public sealed class JumpState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;

        public JumpState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Jump;

        public void Enter(LocomotionContext ctx)
        {
            var speed = Mathf.Sqrt(2f * _settings.jumpHeight * -_settings.gravity);
            ctx.Velocity = new Vector3(ctx.Velocity.x, speed, ctx.Velocity.z);
            ctx.JumpCooldown = _settings.jumpCooldownSeconds;
            ctx.TimeSinceJumpPressed = float.PositiveInfinity;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, _settings.airControl);
            LocomotionMath.ApplyGravity(ctx, _settings, deltaTime);
            return ctx.Velocity.y <= 0f ? LocomotionStateId.Fall : Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }

    // Falling: gravity accumulates up to the terminal velocity; touching the ground lands and records
    // the vertical speed of the impact.
    public sealed class FallState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;

        public FallState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Fall;

        public void Enter(LocomotionContext ctx) { }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, _settings.airControl);
            if (ground.Grounded)
            {
                ctx.LastLandingSpeed = Mathf.Abs(ctx.Velocity.y);
                return LocomotionStateId.Grounded;
            }
            LocomotionMath.ApplyGravity(ctx, _settings, deltaTime);
            return Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }
}

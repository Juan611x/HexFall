using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // On the ground: accelerates toward the target speed, holds a small downward speed, and jumps on
    // a press, a buffered press or within the coyote window after leaving the ground; a press toward a
    // climbable ledge climbs instead.
    public sealed class GroundedState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;

        public GroundedState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Grounded;

        public void Enter(LocomotionContext ctx)
        {
            ctx.BodyHeight = _settings.standHeight;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, 1f);
            ctx.Velocity = new Vector3(ctx.Velocity.x, _settings.groundedStickSpeed, ctx.Velocity.z);

            var canJump = ctx.JumpCooldown <= 0f
                && ctx.TimeSinceJumpPressed <= _settings.jumpBufferSeconds
                && ctx.TimeSinceGrounded <= _settings.coyoteSeconds;
            // A climb takes priority over the jump it would otherwise become.
            if (canJump && ClimbEntry.TryStart(ctx, _settings, intent, ground)) return LocomotionStateId.Climb;
            if (canJump) return LocomotionStateId.Jump;
            if (intent.CrouchPressed && ground.Grounded)
            {
                var slide = intent.Sprint && ctx.PlanarSpeed >= _settings.slideMinEntrySpeed;
                return slide ? LocomotionStateId.Slide : LocomotionStateId.Crouch;
            }
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
            var speed = float.IsNaN(ctx.LaunchSpeed) ? Mathf.Sqrt(2f * _settings.jumpHeight * -_settings.gravity) : ctx.LaunchSpeed;
            ctx.LaunchSpeed = float.NaN;
            ctx.Velocity = new Vector3(ctx.Velocity.x, speed, ctx.Velocity.z);
            ctx.JumpCooldown = _settings.jumpCooldownSeconds;
            ctx.TimeSinceJumpPressed = float.PositiveInfinity;
            ctx.BodyHeight = _settings.standHeight;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            if (ctx.TimeSinceGrounded <= _settings.climb.airGrabWindowSeconds && ClimbEntry.TryStart(ctx, _settings, intent, ground))
                return LocomotionStateId.Climb;
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

        // Falling always uses the stand height, including when stepping off a ledge while crouched.
        public void Enter(LocomotionContext ctx)
        {
            ctx.BodyHeight = _settings.standHeight;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            if (!ground.Grounded && ctx.TimeSinceGrounded <= _settings.climb.airGrabWindowSeconds && ClimbEntry.TryStart(ctx, _settings, intent, ground))
                return LocomotionStateId.Climb;
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, _settings.airControl);
            if (ground.Grounded)
            {
                ctx.LastLandingSpeed = Mathf.Abs(ctx.Velocity.y);
                ctx.LandedFromClimbOver = ctx.ClimbOverDrop;
                ctx.ClimbOverDrop = false;
                return LocomotionStateId.Grounded;
            }
            LocomotionMath.ApplyGravity(ctx, _settings, deltaTime);
            return Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }

    // Crouched on the ground: slower movement at the crouch height. Crouch again stands up, sprinting
    // with move input stands up into a run; both need headroom.
    public sealed class CrouchState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;
        // Sprint held since entering (a slide or a crouch pressed mid-sprint) must be released
        // once before sprinting can stand the player up again.
        private bool _sprintArmed;

        public CrouchState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Crouch;

        public void Enter(LocomotionContext ctx)
        {
            ctx.BodyHeight = _settings.crouchHeight;
            _sprintArmed = false;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            if (!intent.Sprint) _sprintArmed = true;
            LocomotionMath.ApplyPlanar(ctx, _settings, intent, deltaTime, 1f, LocomotionMath.CrouchTargetSpeed(_settings, intent));
            ctx.Velocity = new Vector3(ctx.Velocity.x, _settings.groundedStickSpeed, ctx.Velocity.z);

            if (ground.CanStand)
            {
                var canJump = ctx.JumpCooldown <= 0f
                    && ctx.TimeSinceJumpPressed <= _settings.jumpBufferSeconds
                    && ctx.TimeSinceGrounded <= _settings.coyoteSeconds;
                if (canJump) return LocomotionStateId.Jump;
                if (intent.CrouchPressed) return LocomotionStateId.Grounded;
                if (_sprintArmed && intent.Sprint && intent.Move.sqrMagnitude > 0.0001f) return LocomotionStateId.Grounded;
            }
            if (!ground.Grounded && ctx.TimeSinceGrounded > _settings.coyoteSeconds) return LocomotionStateId.Fall;
            return Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }

    // Slide: keeps the entry direction and momentum, loses speed to friction, steers weakly and
    // faces along the velocity. Ends in Crouch when slow or out of time.
    public sealed class SlideState : ILocomotionState
    {
        private readonly LocomotionSettings _settings;

        public SlideState(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public LocomotionStateId Id => LocomotionStateId.Slide;

        public void Enter(LocomotionContext ctx)
        {
            ctx.BodyHeight = _settings.crouchHeight;
            ctx.SlideElapsed = 0f;
            var planar = new Vector3(ctx.Velocity.x, 0f, ctx.Velocity.z);
            var speed = planar.magnitude;
            if (speed <= 0.0001f) return;
            var boosted = Mathf.Min(speed + _settings.slideBoost, Mathf.Max(speed, _settings.sprintSpeed + _settings.slideBoostCapMargin));
            planar = planar / speed * boosted;
            ctx.Velocity = new Vector3(planar.x, ctx.Velocity.y, planar.z);
            ctx.PlanarSpeed = boosted;
        }

        public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
        {
            ctx.SlideElapsed += deltaTime;
            var planar = new Vector3(ctx.Velocity.x, 0f, ctx.Velocity.z);
            var speed = planar.magnitude;

            if (intent.Move.sqrMagnitude > 0.0001f && speed > 0.0001f)
            {
                var local = new Vector3(intent.Move.x, 0f, intent.Move.y).normalized;
                var wanted = Quaternion.Euler(0f, intent.CameraYaw, 0f) * local * speed;
                planar = Vector3.MoveTowards(planar, wanted, _settings.turnAcceleration * _settings.slideSteering * deltaTime);
            }
            var progress = _settings.slideMaxSeconds > 0f ? Mathf.Clamp01(ctx.SlideElapsed / _settings.slideMaxSeconds) : 1f;
            var friction = Mathf.Lerp(_settings.slideFrictionStart, _settings.slideFrictionEnd, progress);
            var newSpeed = Mathf.Max(0f, planar.magnitude - friction * deltaTime);
            planar = planar.sqrMagnitude > 0.0001f ? planar.normalized * newSpeed : Vector3.zero;

            ctx.Velocity = new Vector3(planar.x, _settings.groundedStickSpeed, planar.z);
            ctx.PlanarSpeed = planar.magnitude;
            if (planar.sqrMagnitude > 0.0001f)
            {
                var yaw = Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg;
                ctx.FacingYaw = Mathf.SmoothDampAngle(ctx.FacingYaw, yaw, ref ctx.YawVelocity, _settings.rotationSmoothSeconds, Mathf.Infinity, deltaTime);
            }

            if (intent.CrouchPressed) return LocomotionStateId.Crouch;
            var canJump = ground.CanStand
                && ctx.JumpCooldown <= 0f
                && ctx.TimeSinceJumpPressed <= _settings.jumpBufferSeconds
                && ctx.TimeSinceGrounded <= _settings.coyoteSeconds;
            if (canJump) return LocomotionStateId.Jump;
            if (!ground.Grounded && ctx.TimeSinceGrounded > _settings.coyoteSeconds) return LocomotionStateId.Fall;
            if (ctx.PlanarSpeed <= _settings.slideEndSpeed || ctx.SlideElapsed >= _settings.slideMaxSeconds) return LocomotionStateId.Crouch;
            return Id;
        }

        public void Exit(LocomotionContext ctx) { }
    }
}

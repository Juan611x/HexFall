using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Movement maths shared by the states.
    internal static class LocomotionMath
    {
        private const float WalkThreshold = 0.6f;
        private const float TurnAngleThreshold = 15f;

        // Target planar speed: no input stops; a stick below 0.6 walks, scaled by magnitude / 0.6;
        // from 0.6 up it jogs; sprint with any input sprints.
        public static float TargetSpeed(LocomotionSettings settings, PlayerIntent intent)
        {
            var magnitude = Mathf.Clamp01(intent.Move.magnitude);
            if (magnitude <= 0.0001f) return 0f;
            if (intent.Sprint) return settings.sprintSpeed;
            if (magnitude < WalkThreshold) return settings.walkSpeed * magnitude / WalkThreshold;
            return settings.jogSpeed;
        }

        // Moves the planar velocity toward the camera-relative target and turns the facing toward it.
        // accelerationScale lowers the control while airborne.
        public static void ApplyPlanar(LocomotionContext ctx, LocomotionSettings settings, PlayerIntent intent, float deltaTime, float accelerationScale)
        {
            ApplyPlanar(ctx, settings, intent, deltaTime, accelerationScale, TargetSpeed(settings, intent));
        }

        // Crouch walking: same stick scaling as walking, against the crouch speed.
        public static float CrouchTargetSpeed(LocomotionSettings settings, PlayerIntent intent)
        {
            var magnitude = Mathf.Clamp01(intent.Move.magnitude);
            if (magnitude <= 0.0001f) return 0f;
            if (magnitude < WalkThreshold) return settings.crouchSpeed * magnitude / WalkThreshold;
            return settings.crouchSpeed;
        }

        public static void ApplyPlanar(LocomotionContext ctx, LocomotionSettings settings, PlayerIntent intent, float deltaTime, float accelerationScale, float targetSpeed)
        {
            var hasInput = intent.Move.sqrMagnitude > 0.0001f;
            var direction = Vector3.zero;
            if (hasInput)
            {
                var local = new Vector3(intent.Move.x, 0f, intent.Move.y).normalized;
                direction = Quaternion.Euler(0f, intent.CameraYaw, 0f) * local;
            }

            var target = direction * targetSpeed;
            var planar = new Vector3(ctx.Velocity.x, 0f, ctx.Velocity.z);
            var rate = target.sqrMagnitude > planar.sqrMagnitude ? settings.acceleration : settings.deceleration;
            // A direction change while moving uses the sharper turn rate; starts and stops do not.
            if (hasInput && planar.sqrMagnitude > 0.0001f && Vector3.Angle(planar, target) > TurnAngleThreshold)
                rate = Mathf.Max(rate, settings.turnAcceleration);
            planar = Vector3.MoveTowards(planar, target, rate * accelerationScale * deltaTime);
            ctx.Velocity = new Vector3(planar.x, ctx.Velocity.y, planar.z);
            ctx.PlanarSpeed = planar.magnitude;

            if (hasInput)
            {
                var targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                ctx.FacingYaw = Mathf.SmoothDampAngle(ctx.FacingYaw, targetYaw, ref ctx.YawVelocity, settings.rotationSmoothSeconds, Mathf.Infinity, deltaTime);
            }
        }

        public static void ApplyGravity(LocomotionContext ctx, LocomotionSettings settings, float deltaTime)
        {
            var vertical = Mathf.Max(ctx.Velocity.y + settings.gravity * deltaTime, settings.terminalVelocity);
            ctx.Velocity = new Vector3(ctx.Velocity.x, vertical, ctx.Velocity.z);
        }
    }
}

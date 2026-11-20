using UnityEngine;

namespace HexFall.Player.Locomotion
{
    public enum LocomotionStateId { Grounded, Jump, Fall }

    // What the player wants this frame. Move is x right / y forward (magnitude 0..1) and is read
    // relative to the camera yaw, given in degrees.
    public readonly struct PlayerIntent
    {
        public readonly Vector2 Move;
        public readonly bool Sprint;
        public readonly bool JumpPressed;
        public readonly float CameraYaw;

        public PlayerIntent(Vector2 move, bool sprint, bool jumpPressed, float cameraYaw)
        {
            Move = move;
            Sprint = sprint;
            JumpPressed = jumpPressed;
            CameraYaw = cameraYaw;
        }
    }

    // What the body senses this frame.
    public readonly struct GroundSense
    {
        public readonly bool Grounded;

        public GroundSense(bool grounded)
        {
            Grounded = grounded;
        }
    }

    // Tunable locomotion values; plain public fields so a MonoBehaviour can expose them.
    [System.Serializable]
    public sealed class LocomotionSettings
    {
        public float walkSpeed = 1.8f;
        public float jogSpeed = 3.5f;
        public float sprintSpeed = 6f;
        public float acceleration = 12f;
        public float deceleration = 16f;
        public float airControl = 0.35f;
        public float rotationSmoothSeconds = 0.08f;
        public float jumpHeight = 1.2f;
        public float gravity = -15f;
        public float terminalVelocity = -53f;
        public float groundedStickSpeed = -2f;
        public float coyoteSeconds = 0.12f;
        public float jumpBufferSeconds = 0.1f;
        public float jumpCooldownSeconds = 0.2f;
    }

    // Mutable body state shared by the states.
    public sealed class LocomotionContext
    {
        public Vector3 Velocity;
        public float FacingYaw;
        public float YawVelocity;
        public float PlanarSpeed;
        public float TimeSinceGrounded;
        public float TimeSinceJumpPressed = float.PositiveInfinity;
        public float JumpCooldown;
        public float LastLandingSpeed;
    }

    public interface ILocomotionState
    {
        LocomotionStateId Id { get; }
        void Enter(LocomotionContext ctx);
        LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime);
        void Exit(LocomotionContext ctx);
    }
}

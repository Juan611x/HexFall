using UnityEngine;

namespace HexFall.Player.Locomotion
{
    public enum LocomotionStateId { Grounded, Jump, Fall, Crouch, Slide }

    // What the player wants this frame. Move is x right / y forward (magnitude 0..1) and is read
    // relative to the camera yaw, given in degrees.
    public readonly struct PlayerIntent
    {
        public readonly Vector2 Move;
        public readonly bool Sprint;
        public readonly bool JumpPressed;
        public readonly float CameraYaw;
        // One-shot press of the crouch button; crouch is a toggle on this press.
        public readonly bool CrouchPressed;

        public PlayerIntent(Vector2 move, bool sprint, bool jumpPressed, float cameraYaw, bool crouchPressed = false)
        {
            CrouchPressed = crouchPressed;
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
        // False when a ceiling leaves no room to stand up from a crouch.
        public readonly bool CanStand;

        public GroundSense(bool grounded, bool canStand = true)
        {
            Grounded = grounded;
            CanStand = canStand;
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
        // Rate used when the requested direction diverges from the current velocity, so direction
        // changes feel planted while starts and stops keep acceleration and deceleration.
        public float turnAcceleration = 40f;
        public float airControl = 0.35f;
        public float rotationSmoothSeconds = 0.08f;
        public float jumpHeight = 1.2f;
        public float gravity = -15f;
        public float terminalVelocity = -53f;
        public float groundedStickSpeed = -2f;
        public float coyoteSeconds = 0.12f;
        public float jumpBufferSeconds = 0.1f;
        public float jumpCooldownSeconds = 0.2f;

        // Crouch and slide. standHeight matches the CharacterController height on the player prefab.
        public float crouchSpeed = 1.6f;
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;
        // A slide needs sprint held and at least this planar speed.
        public float slideMinEntrySpeed = 4.5f;
        // Speed added on entry, capped at sprintSpeed + slideBoostCapMargin.
        public float slideBoost = 2f;
        public float slideBoostCapMargin = 2.5f;
        // Friction eases from the start value to the end value over slideMaxSeconds, so the slide
        // glides at first and brakes toward the end.
        public float slideFrictionStart = 2f;
        [UnityEngine.Serialization.FormerlySerializedAs("slideFriction")]
        public float slideFrictionEnd = 7f;
        public float slideEndSpeed = 1.8f;
        public float slideMaxSeconds = 1.6f;
        // Fraction of the normal turn authority kept while sliding.
        public float slideSteering = 0.25f;
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
        // Target capsule height the motor applies; set by the states on enter.
        public float BodyHeight;
        public float SlideElapsed;
    }

    public interface ILocomotionState
    {
        LocomotionStateId Id { get; }
        void Enter(LocomotionContext ctx);
        LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime);
        void Exit(LocomotionContext ctx);
    }
}

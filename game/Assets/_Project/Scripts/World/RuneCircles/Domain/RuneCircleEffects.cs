using UnityEngine;

namespace HexFall.World.RuneCircles
{
    // What a rune circle can ask of the player; implemented by the player's motor.
    public interface IRuneCircleTarget
    {
        bool IsGrounded { get; }
        // World position of the body's feet (the bottom centre of its collider).
        Vector3 FeetPosition { get; }
        // Yaw of the body, in degrees.
        float FacingYaw { get; }
        // Magnitude of the gravity acceleration (positive), in m/s^2.
        float GravityMagnitude { get; }
        // Throws the body upward at the given vertical speed; ignored when the body cannot be launched.
        void Launch(float verticalSpeed);
        // Places the body with its feet at position, facing yaw degrees. The body keeps its planar speed
        // (turned by the change of facing) and loses its vertical speed. Returns false, and does nothing,
        // when the body cannot be teleported (while climbing).
        bool Teleport(Vector3 position, float yaw);
    }

    public readonly struct RuneCircleActivation
    {
        public readonly Transform Circle;
        public readonly IRuneCircleTarget Target;

        public RuneCircleActivation(Transform circle, IRuneCircleTarget target)
        {
            Circle = circle;
            Target = target;
        }
    }

    // A component on a rune circle that reacts when the circle fires.
    public interface IRuneCircleEffect
    {
        void Apply(RuneCircleActivation activation);
    }

    public static class LaunchMath
    {
        // Vertical speed that reaches the given height under the given gravity magnitude.
        public static float SpeedForHeight(float height, float gravityMagnitude)
        {
            if (height <= 0f || gravityMagnitude <= 0f) return 0f;
            return Mathf.Sqrt(2f * gravityMagnitude * height);
        }
    }
}

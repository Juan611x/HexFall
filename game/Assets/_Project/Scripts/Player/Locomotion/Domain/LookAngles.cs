using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Camera look maths: turns a look delta into yaw and pitch in degrees. Yaw wraps into [0, 360)
    // and pitch is clamped to the given limits.
    public static class LookAngles
    {
        private const float DeadSqrMagnitude = 0.0001f;

        // multiplier is 1 for a mouse (the delta is already per frame) and deltaTime for a stick.
        // A positive vertical delta looks up, which lowers the pitch.
        public static (float yaw, float pitch) Apply(float yaw, float pitch, Vector2 lookDelta, float multiplier, float bottomClamp, float topClamp)
        {
            if (lookDelta.sqrMagnitude >= DeadSqrMagnitude)
            {
                yaw += lookDelta.x * multiplier;
                pitch -= lookDelta.y * multiplier;
            }
            return (Mathf.Repeat(yaw, 360f), Mathf.Clamp(pitch, bottomClamp, topClamp));
        }
    }
}

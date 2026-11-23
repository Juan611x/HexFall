using UnityEngine;

namespace HexFall.Player
{
    // Idle motion shared by every pickup: a gentle vertical bob and a slow turn around Y.
    public static class PickupMotion
    {
        // Height offset at time t for a sine bob of the given amplitude and period.
        public static float Bob(float t, float amplitude, float periodSeconds) =>
            periodSeconds <= 0f ? 0f : amplitude * Mathf.Sin(2f * Mathf.PI * t / periodSeconds);

        // Yaw in degrees [0, 360) at time t when turning at the given speed.
        public static float Spin(float t, float degreesPerSecond) => Mathf.Repeat(t * degreesPerSecond, 360f);
    }
}

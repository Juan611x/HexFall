using UnityEngine;

namespace HexFall.Player
{
    // The yaw (degrees around Y) a character at `from` must face to look at `target` on the ground plane.
    // A target straight above or below gives no direction, so the current yaw is kept.
    public static class AimFacing
    {
        public static float YawToward(Vector3 from, Vector3 target, float currentYaw)
        {
            var x = target.x - from.x;
            var z = target.z - from.z;
            if (x * x + z * z < 1e-6f) return currentYaw;
            return Mathf.Atan2(x, z) * Mathf.Rad2Deg;
        }

        // Yaw after turning from current toward target by at most maxStep degrees, the short way round.
        public static float Turn(float currentYaw, float targetYaw, float maxStep) =>
            Mathf.MoveTowardsAngle(currentYaw, targetYaw, maxStep);
    }
}

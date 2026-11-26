using UnityEngine;

namespace HexFall.Player.Spells
{
    // A shot's spread is a yaw offset around the vertical axis, so the aim keeps its pitch.
    public static class ShotAim
    {
        public static Vector3 Offset(Vector3 direction, float spreadDegrees)
        {
            if (Mathf.Approximately(spreadDegrees, 0f)) return direction;
            return Quaternion.AngleAxis(spreadDegrees, Vector3.up) * direction;
        }
    }
}

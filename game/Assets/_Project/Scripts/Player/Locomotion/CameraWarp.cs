using Unity.Cinemachine;
using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Tells the Cinemachine cameras that follow a target that it jumped by delta (a teleport), so they
    // move with it at once instead of damping across the level.
    public static class CameraWarp
    {
        public static void Warp(Transform target, Vector3 delta)
        {
            if (target == null) return;
            foreach (var camera in Object.FindObjectsByType<CinemachineVirtualCameraBase>())
                camera.OnTargetObjectWarped(target, delta);
        }
    }
}

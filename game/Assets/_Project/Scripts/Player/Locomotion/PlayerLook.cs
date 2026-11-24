using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Turns the look input into yaw and pitch and applies them to the camera target in world space, so
    // turning the body does not drag the camera. The Cinemachine camera follows the camera target.
    // Runs after every script that turns the body (motor, combat aim facing at order 0), so the frame's
    // body turn never leaks into the child camera target, and before CinemachineBrain (order 100) reads it.
    [DefaultExecutionOrder(ExecutionOrder)]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerLook : MonoBehaviour
    {
        public const int ExecutionOrder = 50;

        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float topClamp = 70f;
        [SerializeField] private float bottomClamp = -30f;
        [SerializeField] private float gamepadSensitivity = 1f;

        private PlayerInputReader _input;
        private float _yaw;
        private float _pitch;

        public float Yaw => _yaw;
        public Transform CameraTarget => cameraTarget;

        // Turns the view by delta degrees, e.g. when the body is turned by a teleport.
        public void AddYaw(float delta) => _yaw += delta;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            if (cameraTarget != null) _yaw = cameraTarget.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (cameraTarget == null) return;

            var multiplier = _input.IsMouseLook ? 1f : Time.deltaTime * gamepadSensitivity;
            (_yaw, _pitch) = LookAngles.Apply(_yaw, _pitch, _input.Look, multiplier, bottomClamp, topClamp);
            cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}

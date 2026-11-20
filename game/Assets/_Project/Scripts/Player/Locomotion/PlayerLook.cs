using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Turns the look input into yaw and pitch and applies them to the camera target in world space, so
    // turning the body does not drag the camera. The Cinemachine camera follows the camera target.
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float topClamp = 70f;
        [SerializeField] private float bottomClamp = -30f;
        [SerializeField] private float gamepadSensitivity = 1f;

        private PlayerInputReader _input;
        private float _yaw;
        private float _pitch;

        public float Yaw => _yaw;

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

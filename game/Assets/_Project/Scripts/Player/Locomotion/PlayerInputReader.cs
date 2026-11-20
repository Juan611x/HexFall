using UnityEngine;
using UnityEngine.InputSystem;

namespace HexFall.Player.Locomotion
{
    // Receives Move, Look, Jump and Sprint from PlayerInput (Send Messages) and exposes them as plain
    // values. IsMouseLook is true while the active control scheme is KeyboardMouse, which is how the
    // look delta is told apart (a mouse delta is per frame, a stick value is a rate). Also locks the
    // cursor while the game has focus.
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string KeyboardMouseScheme = "KeyboardMouse";

        [SerializeField] private bool lockCursor = true;

        private PlayerInput _playerInput;
        private bool _jumpPressed;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }

        public bool IsMouseLook => _playerInput != null && _playerInput.currentControlScheme == KeyboardMouseScheme;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            ApplyCursorLock(lockCursor);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplyCursorLock(hasFocus && lockCursor);
        }

        public void OnMove(InputValue value) => Move = value.Get<Vector2>();

        public void OnLook(InputValue value) => Look = value.Get<Vector2>();

        public void OnJump(InputValue value)
        {
            if (value.isPressed) _jumpPressed = true;
        }

        public void OnSprint(InputValue value) => Sprint = value.isPressed;

        // True once per press.
        public bool ConsumeJumpPressed()
        {
            var pressed = _jumpPressed;
            _jumpPressed = false;
            return pressed;
        }

        private static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

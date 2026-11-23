using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // Feeds the locomotion machine with the intent and ground sensing each frame and applies the
    // resulting velocity and facing to the CharacterController.
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader), typeof(PlayerLook))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private LocomotionSettings settings = new LocomotionSettings();
        [Header("Ground check")]
        [SerializeField] private float groundedOffset = -0.14f;
        [SerializeField] private float groundedRadius = 0.28f;
        [SerializeField] private LayerMask groundLayers = 1;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private PlayerLook _look;

        public LocomotionMachine Machine { get; private set; }
        public bool IsGrounded { get; private set; }
        public LayerMask GroundLayers => groundLayers;
        public float InputMagnitude { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _look = GetComponent<PlayerLook>();
            Machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            IsGrounded = CheckGround();
            InputMagnitude = Mathf.Clamp01(_input.Move.magnitude);

            // Other systems (e.g. attack facing) may have turned the body since the last frame.
            Machine.Context.FacingYaw = transform.eulerAngles.y;

            var intent = new PlayerIntent(_input.Move, _input.Sprint, _input.ConsumeJumpPressed(), _look.Yaw);
            Machine.Tick(intent, new GroundSense(IsGrounded), deltaTime);

            _controller.Move(Machine.Context.Velocity * deltaTime);
            transform.rotation = Quaternion.Euler(0f, Machine.Context.FacingYaw, 0f);
        }

        private bool CheckGround()
        {
            var origin = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            return Physics.CheckSphere(origin, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        }
    }
}

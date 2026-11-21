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
        [Header("Capsule")]
        [Tooltip("Height change per second when crouching or standing up.")]
        [SerializeField] private float capsuleResizeSpeed = 8f;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private PlayerLook _look;
        private StandClearance _clearance;
        private float _feetOffset;
        private float _height;

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

            // Keep the capsule bottom where the prefab put it, whatever the height.
            _height = _controller.height;
            _feetOffset = _controller.center.y - _height * 0.5f;
            _clearance = new StandClearance(groundLayers, _controller.radius, settings.standHeight, _feetOffset, transform);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            IsGrounded = CheckGround();
            InputMagnitude = Mathf.Clamp01(_input.Move.magnitude);

            // Other systems (e.g. attack facing) may have turned the body since the last frame.
            Machine.Context.FacingYaw = transform.eulerAngles.y;

            var canStand = _clearance.CanStand(transform.position, _height);
            var intent = new PlayerIntent(_input.Move, _input.Sprint, _input.ConsumeJumpPressed(), _look.Yaw,
                _input.ConsumeCrouchPressed());
            Machine.Tick(intent, new GroundSense(IsGrounded, canStand), deltaTime);
            ApplyCapsule(deltaTime);

            _controller.Move(Machine.Context.Velocity * deltaTime);
            transform.rotation = Quaternion.Euler(0f, Machine.Context.FacingYaw, 0f);
        }

        private void ApplyCapsule(float deltaTime)
        {
            var target = Machine.Context.BodyHeight;
            if (Mathf.Approximately(_height, target)) return;

            _height = CapsuleShape.Step(_height, target, capsuleResizeSpeed, deltaTime);
            var (height, centerY) = CapsuleShape.For(_height, _feetOffset);
            _controller.height = height;
            _controller.center = new Vector3(_controller.center.x, centerY, _controller.center.z);
        }

        private bool CheckGround()
        {
            var origin = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            return Physics.CheckSphere(origin, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        }
    }
}

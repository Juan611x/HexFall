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
        private LedgeProbe _ledgeProbe;
        private float _feetOffset;
        private float _height;

        public LocomotionMachine Machine { get; private set; }
        public bool IsGrounded { get; private set; }
        public LayerMask GroundLayers => groundLayers;
        public float InputMagnitude { get; private set; }
        // The last ledge measured; only refreshed when a jump could start a climb. Used by the gizmos.
        public LedgeMeasurement LastLedge { get; private set; }

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
            // The climb keeps the body at the crouch height, so that is the capsule the landing must fit.
            _ledgeProbe = new LedgeProbe(groundLayers, _controller.radius, _controller.slopeLimit, settings.climb,
                settings.crouchHeight, transform);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            IsGrounded = CheckGround();
            InputMagnitude = Mathf.Clamp01(_input.Move.magnitude);

            // Other systems (e.g. attack facing) may have turned the body since the last frame.
            Machine.Context.FacingYaw = transform.eulerAngles.y;

            // The climb path starts from, and is stepped relative to, where the body really is.
            Machine.Context.Position = transform.position;

            var canStand = _clearance.CanStand(transform.position, _height);
            var intent = new PlayerIntent(_input.Move, _input.Sprint, _input.ConsumeJumpPressed(), _look.Yaw,
                _input.ConsumeCrouchPressed());
            var ledge = SenseLedge(intent, deltaTime);
            Machine.Tick(intent, new GroundSense(IsGrounded, canStand, ledge), deltaTime);
            ApplyCapsule(deltaTime);

            if (Machine.Current == LocomotionStateId.Climb) FollowClimbPath();
            else _controller.Move(Machine.Context.Velocity * deltaTime);
            transform.rotation = Quaternion.Euler(0f, Machine.Context.FacingYaw, 0f);
        }

        // The ledge only matters when a jump press could start a climb: pressed now or still buffered,
        // moving, and on the ground or inside the air grab window. Skipping the casts otherwise keeps
        // the probe off the hot path.
        private LedgeMeasurement SenseLedge(PlayerIntent intent, float deltaTime)
        {
            var ctx = Machine.Context;
            if (Machine.Current == LocomotionStateId.Climb) return default;
            if (intent.Move.sqrMagnitude <= 0.0001f) return default;
            var jumpRelevant = intent.JumpPressed || ctx.TimeSinceJumpPressed + deltaTime <= settings.jumpBufferSeconds;
            if (!jumpRelevant) return default;
            var canGrab = IsGrounded || ctx.TimeSinceGrounded + deltaTime <= settings.climb.airGrabWindowSeconds;
            if (!canGrab) return default;

            var wanted = Quaternion.Euler(0f, _look.Yaw, 0f) * new Vector3(intent.Move.x, 0f, intent.Move.y);
            LastLedge = _ledgeProbe.Measure(transform.position, wanted);
            return LastLedge;
        }

        // The climb is a scripted path through the edge, so the body is placed on it instead of moved
        // with the CharacterController, whose collision would snag or push the capsule on the corner it
        // is deliberately passing. The domain guarantees the space (headroom or landing) before it starts.
        // Toggling the controller makes it accept the new transform position.
        private void FollowClimbPath()
        {
            _controller.enabled = false;
            transform.position = Machine.Context.ClimbTarget;
            _controller.enabled = true;
        }

        private void OnDrawGizmosSelected()
        {
            var ledge = LastLedge;
            if (!ledge.HasLedge) return;
            var kind = settings != null ? LedgeClassifier.Classify(ledge, settings.climb) : ClimbKind.None;
            Gizmos.color = kind == ClimbKind.ClimbUp ? Color.green : kind == ClimbKind.ClimbOver ? Color.cyan : Color.red;
            Gizmos.DrawSphere(ledge.TopPoint, 0.05f);
            var depth = float.IsInfinity(ledge.TopDepth) ? settings.climb.standDepth : ledge.TopDepth;
            Gizmos.DrawLine(ledge.TopPoint, ledge.TopPoint + ledge.ApproachDirection * depth);
            if (ledge.LandingClear)
            {
                var far = ledge.TopPoint + ledge.ApproachDirection * (ledge.TopDepth + settings.climb.landingClearDepth);
                Gizmos.DrawWireSphere(new Vector3(far.x, transform.position.y + ledge.LandingHeight, far.z), 0.1f);
            }
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

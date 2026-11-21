using System.Collections.Generic;
using HexFall.Player.Actions;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace HexFall.Player.Locomotion
{
    // Applies the IK profile of the current locomotion state to the Animation Rigging constraints:
    // feet are planted on the ground with ground rays and the body sinks to reach the lower foot;
    // the head and chest aim at the crosshair. Any missing reference only disables its own part.
    // The feet are placed from the ANIMATED foot positions, captured in OnAnimatorIK (the humanoid IK
    // pass, before Animation Rigging runs). Reading the foot bones in LateUpdate would return the pose
    // the rig already produced, feed it back as the next target and pin the feet in place. The base
    // layer must have its IK pass enabled; until it reports, the feet stay unconstrained. Foot rotation
    // follows the animation (the constraints use position only): humanoid IK rotations live in the
    // avatar's normalised space, not the bones', so aligning feet to slopes is left for later.
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerIKDriver : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint leftFoot;
        [SerializeField] private TwoBoneIKConstraint rightFoot;
        [SerializeField] private MultiAimConstraint headAim;
        [SerializeField] private MultiAimConstraint chestAim;
        [Tooltip("Leans the abdomen back while sliding; evaluated before the chest aim.")]
        [SerializeField] private OverrideTransform slideLean;
        [Tooltip("Leans the chest back on top of the abdomen while sliding, for a curved spine.")]
        [SerializeField] private OverrideTransform slideLeanChest;
        [Header("Targets and bones")]
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private Transform aimTarget;
        [Tooltip("The animated model (CharacterArmature); lowered so the lower foot reaches the ground.")]
        [SerializeField] private Transform modelRoot;
        [Header("Tuning")]
        [SerializeField] private float blendRate = 6f;
        [SerializeField] private float rayStartHeight = 0.6f;
        [SerializeField] private float rayLength = 1.2f;
        [SerializeField] private float maxHipDrop = 0.35f;
        [SerializeField] private float hipSmoothing = 10f;
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField] private float aimDistance = 100f;

        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private Animator _animator;
        private Vector3 _animatedLeftFoot;
        private Vector3 _animatedRightFoot;
        private bool _hasAnimatedFeet;
        private IkProfile _profile;
        private float _hipY;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
            _animator = GetComponent<Animator>();
        }

        // The animation's own foot positions of this frame in world space (before any IK correction);
        // false until the humanoid IK pass has reported once.
        public bool TryGetAnimatedFeet(out Vector3 left, out Vector3 right)
        {
            left = _animatedLeftFoot;
            right = _animatedRightFoot;
            return _hasAnimatedFeet;
        }

        // Humanoid IK pass of the base layer: the foot goals here are still the animation's own.
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;
            _animatedLeftFoot = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            _animatedRightFoot = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            _hasAnimatedFeet = true;
        }

        private void OnEnable()
        {
            var missing = new List<string>();
            if (rig == null) missing.Add(nameof(rig));
            if (leftFoot == null) missing.Add(nameof(leftFoot));
            if (rightFoot == null) missing.Add(nameof(rightFoot));
            if (headAim == null) missing.Add(nameof(headAim));
            if (chestAim == null) missing.Add(nameof(chestAim));
            if (slideLean == null) missing.Add(nameof(slideLean));
            if (slideLeanChest == null) missing.Add(nameof(slideLeanChest));
            if (leftFootTarget == null) missing.Add(nameof(leftFootTarget));
            if (rightFootTarget == null) missing.Add(nameof(rightFootTarget));
            if (aimTarget == null) missing.Add(nameof(aimTarget));
            if (modelRoot == null) missing.Add(nameof(modelRoot));
            if (_combat == null) missing.Add("PlayerCombat");
            if (missing.Count > 0)
                Debug.LogWarning("PlayerIKDriver: missing references, those parts are skipped: " + string.Join(", ", missing), this);
        }

        private void LateUpdate()
        {
            var machine = _motor.Machine;
            if (machine == null) return;

            var action = _combat != null && _combat.Machine != null ? _combat.Machine.Current : ActionStateId.Ready;
            var target = IkProfiles.For(machine.Current, action);
            _profile = IkBlend.Toward(_profile, target, blendRate, Time.deltaTime);

            var feetWeight = _hasAnimatedFeet ? _profile.FeetWeight : 0f;
            if (leftFoot != null) leftFoot.weight = feetWeight;
            if (rightFoot != null) rightFoot.weight = feetWeight;
            if (headAim != null) headAim.weight = _profile.HeadAimWeight;
            if (chestAim != null) chestAim.weight = _profile.ChestAimWeight;
            if (slideLean != null) slideLean.weight = _profile.SlideLeanWeight;
            if (slideLeanChest != null) slideLeanChest.weight = _profile.SlideLeanWeight;

            var leftDrop = _hasAnimatedFeet ? PlaceFoot(_animatedLeftFoot, leftFootTarget) : 0f;
            var rightDrop = _hasAnimatedFeet ? PlaceFoot(_animatedRightFoot, rightFootTarget) : 0f;
            ApplyHipOffset(leftDrop, rightDrop, feetWeight);
            PlaceAimTarget();
        }

        // Moves the target to the animated foot shifted by how much the ground under it differs from
        // the ground under the body, and returns how much lower that ground is (for the hip drop).
        private float PlaceFoot(Vector3 animatedPosition, Transform target)
        {
            if (target == null) return 0f;

            var rootGround = transform.position.y;
            var origin = new Vector3(animatedPosition.x, rootGround + rayStartHeight, animatedPosition.z);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, rayLength, groundLayers, QueryTriggerInteraction.Ignore))
            {
                target.position = animatedPosition;
                return 0f;
            }

            target.position = FootPlacement.Follow(animatedPosition, rootGround, hit.point.y);
            return FootPlacement.GroundDrop(rootGround, hit.point.y);
        }

        private void ApplyHipOffset(float leftDrop, float rightDrop, float feetWeight)
        {
            if (modelRoot == null) return;
            var wanted = FootPlacement.HipOffset(leftDrop, rightDrop, maxHipDrop) * feetWeight;
            _hipY = Mathf.Lerp(_hipY, wanted, 1f - Mathf.Exp(-hipSmoothing * Time.deltaTime));
            var local = modelRoot.localPosition;
            local.y = _hipY;
            modelRoot.localPosition = local;
        }

        // The point under the crosshair: the first thing the camera's centre ray hits that is not the
        // wizard, or a far point along that ray.
        private void PlaceAimTarget()
        {
            if (aimTarget == null) return;
            var cam = Camera.main;
            if (cam == null)
            {
                aimTarget.position = transform.position + transform.forward * aimDistance;
                return;
            }

            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var best = ray.GetPoint(aimDistance);
            var bestDistance = aimDistance;
            foreach (var hit in Physics.RaycastAll(ray, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root == transform.root || hit.distance >= bestDistance) continue;
                best = hit.point;
                bestDistance = hit.distance;
            }
            aimTarget.position = best;
        }
    }
}

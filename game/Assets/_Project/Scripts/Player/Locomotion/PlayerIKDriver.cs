using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace HexFall.Player.Locomotion
{
    // Applies the IK profile of the current locomotion state to the Animation Rigging constraints:
    // feet are planted on the ground with ground rays and the body sinks to reach the lower foot;
    // the head and chest aim at the crosshair. Any missing reference only disables its own part.
    // Animation Rigging evaluates during the animation update, so the foot bones read in LateUpdate
    // hold last frame's pose; that one frame of lag is acceptable for placing the targets.
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerIKDriver : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Rig rig;
        [SerializeField] private TwoBoneIKConstraint leftFoot;
        [SerializeField] private TwoBoneIKConstraint rightFoot;
        [SerializeField] private MultiAimConstraint headAim;
        [SerializeField] private MultiAimConstraint chestAim;
        [Header("Targets and bones")]
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform rightFootBone;
        [Tooltip("The animated model (CharacterArmature); lowered so the lower foot reaches the ground.")]
        [SerializeField] private Transform modelRoot;
        [Header("Tuning")]
        [SerializeField] private float blendRate = 6f;
        [SerializeField] private float footHeight = 0.08f;
        [SerializeField] private float rayStartHeight = 0.6f;
        [SerializeField] private float rayLength = 1.2f;
        [SerializeField] private float maxHipDrop = 0.35f;
        [SerializeField] private float hipSmoothing = 10f;
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField] private float aimDistance = 100f;

        private PlayerMotor _motor;
        private PlayerCombat _combat;
        private IkProfile _profile;
        private float _hipY;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _combat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            var missing = new List<string>();
            if (rig == null) missing.Add(nameof(rig));
            if (leftFoot == null) missing.Add(nameof(leftFoot));
            if (rightFoot == null) missing.Add(nameof(rightFoot));
            if (headAim == null) missing.Add(nameof(headAim));
            if (chestAim == null) missing.Add(nameof(chestAim));
            if (leftFootTarget == null) missing.Add(nameof(leftFootTarget));
            if (rightFootTarget == null) missing.Add(nameof(rightFootTarget));
            if (aimTarget == null) missing.Add(nameof(aimTarget));
            if (leftFootBone == null) missing.Add(nameof(leftFootBone));
            if (rightFootBone == null) missing.Add(nameof(rightFootBone));
            if (modelRoot == null) missing.Add(nameof(modelRoot));
            if (_combat == null) missing.Add("PlayerCombat");
            if (missing.Count > 0)
                Debug.LogWarning("PlayerIKDriver: missing references, those parts are skipped: " + string.Join(", ", missing), this);
        }

        private void LateUpdate()
        {
            var machine = _motor.Machine;
            if (machine == null) return;

            var acting = _combat != null && _combat.IsActing;
            var target = IkProfiles.For(machine.Current, acting);
            _profile = IkBlend.Toward(_profile, target, blendRate, Time.deltaTime);

            if (leftFoot != null) leftFoot.weight = _profile.FeetWeight;
            if (rightFoot != null) rightFoot.weight = _profile.FeetWeight;
            if (headAim != null) headAim.weight = _profile.HeadAimWeight;
            if (chestAim != null) chestAim.weight = _profile.ChestAimWeight;

            var leftDrop = PlaceFoot(leftFootBone, leftFootTarget);
            var rightDrop = PlaceFoot(rightFootBone, rightFootTarget);
            ApplyHipOffset(leftDrop, rightDrop);
            PlaceAimTarget();
        }

        // Puts the target on the ground below the animated foot and returns how far below the
        // animated foot the ground is (positive when the ground is lower).
        private float PlaceFoot(Transform bone, Transform target)
        {
            if (bone == null || target == null) return 0f;

            var animatedPosition = bone.position;
            var animatedRotation = bone.rotation;
            var origin = animatedPosition + Vector3.up * rayStartHeight;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, rayLength, groundLayers, QueryTriggerInteraction.Ignore))
            {
                target.SetPositionAndRotation(animatedPosition, animatedRotation);
                return 0f;
            }

            var placed = FootPlacement.Target(animatedPosition, footHeight, hit.point, hit.normal);
            target.SetPositionAndRotation(placed, FootPlacement.Rotation(animatedRotation, hit.normal));
            return animatedPosition.y - placed.y;
        }

        private void ApplyHipOffset(float leftDrop, float rightDrop)
        {
            if (modelRoot == null) return;
            var wanted = FootPlacement.HipOffset(leftDrop, rightDrop, maxHipDrop) * _profile.FeetWeight;
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

using HexFall.Player.Actions;
using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // How strongly each IK constraint should act, as weights from 0 (animation only) to 1 (full IK).
    public readonly struct IkProfile
    {
        public readonly float FeetWeight;
        public readonly float HeadAimWeight;
        public readonly float ChestAimWeight;
        public readonly float SlideLeanWeight;

        public IkProfile(float feetWeight, float headAimWeight, float chestAimWeight, float slideLeanWeight = 0f)
        {
            FeetWeight = feetWeight;
            HeadAimWeight = headAimWeight;
            ChestAimWeight = chestAimWeight;
            SlideLeanWeight = slideLeanWeight;
        }
    }

    // The IK profile each locomotion state asks for: feet are planted only on the ground, and the
    // head and chest follow the crosshair, more strongly while the wizard attacks or parries. The
    // torso leans back only while sliding, and then the chest aim is dropped so it cannot cancel the
    // lean while the head keeps looking along the slide.
    public static class IkProfiles
    {
        public const float IdleHeadAim = 0.35f;
        public const float IdleChestAim = 0.15f;
        public const float ActingHeadAim = 0.8f;
        public const float ActingChestAim = 0.6f;
        public const float SlideHeadAim = 1f;
        public const float SlideChestAim = 0f;

        public static IkProfile For(LocomotionStateId state, ActionStateId action)
        {
            var acting = action != ActionStateId.Ready;
            var planted = state == LocomotionStateId.Grounded || state == LocomotionStateId.Crouch;
            var feet = planted ? 1f : 0f;
            if (state == LocomotionStateId.Slide)
                return new IkProfile(feet, SlideHeadAim, SlideChestAim, 1f);
            return acting
                ? new IkProfile(feet, ActingHeadAim, ActingChestAim)
                : new IkProfile(feet, IdleHeadAim, IdleChestAim);
        }
    }

    // The low feet-first slide pose, blended by the slide weight: the body sinks until the seat is
    // near the floor and both feet are placed ahead of the hips by leg IK. The left leg leads and is
    // nearly straight, the right one trails slightly bent. Offsets are in the body's local space.
    public static class SlidePose
    {
        // How far the whole model is lowered at full weight: the crouch pelvis sits about 0.28 m up, so this settles it near 0.16 m.
        public const float HipDrop = 0.12f;
        private const float LeadReach = 0.42f;
        private const float TrailReach = 0.3f;
        private const float HalfStance = 0.14f;
        private const float FootHeight = 0.06f;

        public static float HipOffset(float slideWeight)
        {
            return -HipDrop * Mathf.Clamp01(slideWeight);
        }

        // The foot goal relative to the body: left leads, right trails; y is above the ground.
        public static Vector3 FootLocal(bool left)
        {
            return new Vector3(left ? -HalfStance : HalfStance, FootHeight, left ? LeadReach : TrailReach);
        }

        public static Vector3 FootWorld(Vector3 rootPosition, Quaternion rootRotation, float groundHeight, bool left)
        {
            var local = FootLocal(left);
            var flat = rootRotation * new Vector3(local.x, 0f, local.z);
            return new Vector3(rootPosition.x + flat.x, groundHeight + local.y, rootPosition.z + flat.z);
        }

        // The leg IK stays on while either the grounded planting or the slide pose needs it.
        public static float FeetWeight(float groundedFeetWeight, float slideWeight)
        {
            return Mathf.Max(groundedFeetWeight, slideWeight);
        }
    }

    // Moves a profile toward a target at a limited speed so IK fades in and out instead of popping.
    public static class IkBlend
    {
        public static IkProfile Toward(IkProfile current, IkProfile target, float ratePerSecond, float deltaTime)
        {
            var step = ratePerSecond * deltaTime;
            return new IkProfile(
                Step(current.FeetWeight, target.FeetWeight, step),
                Step(current.HeadAimWeight, target.HeadAimWeight, step),
                Step(current.ChestAimWeight, target.ChestAimWeight, step),
                Step(current.SlideLeanWeight, target.SlideLeanWeight, step));
        }

        private static float Step(float current, float target, float step)
        {
            return Mathf.Clamp01(Mathf.MoveTowards(current, target, step));
        }
    }

    // Foot placement maths, fed with ground heights instead of querying physics so it stays testable.
    // The animation is authored on flat ground under the body, so a foot only needs moving by how
    // much its own ground differs from that: the step's lift is kept, slopes and steps are followed.
    public static class FootPlacement
    {
        // The animated foot shifted vertically by the ground difference between the foot and the body.
        public static Vector3 Follow(Vector3 animatedFoot, float rootGroundHeight, float groundHeightUnderFoot)
        {
            return animatedFoot + Vector3.up * (groundHeightUnderFoot - rootGroundHeight);
        }

        // How much lower the ground under a foot is than under the body (0 when it is higher).
        public static float GroundDrop(float rootGroundHeight, float groundHeightUnderFoot)
        {
            return Mathf.Max(0f, rootGroundHeight - groundHeightUnderFoot);
        }

        // Drops are positive distances the feet must go down. The body sinks by the larger one so
        // the lower foot reaches the ground; it never rises.
        public static float HipOffset(float leftDrop, float rightDrop, float maxDrop)
        {
            return -Mathf.Clamp(Mathf.Max(leftDrop, rightDrop, 0f), 0f, maxDrop);
        }
    }
}

using UnityEngine;

namespace HexFall.Player.Locomotion
{
    // How strongly each IK constraint should act, as weights from 0 (animation only) to 1 (full IK).
    public readonly struct IkProfile
    {
        public readonly float FeetWeight;
        public readonly float HeadAimWeight;
        public readonly float ChestAimWeight;

        public IkProfile(float feetWeight, float headAimWeight, float chestAimWeight)
        {
            FeetWeight = feetWeight;
            HeadAimWeight = headAimWeight;
            ChestAimWeight = chestAimWeight;
        }
    }

    // The IK profile each locomotion state asks for: feet are planted only on the ground, and the
    // head and chest follow the crosshair, more strongly while the wizard attacks or parries.
    public static class IkProfiles
    {
        public const float IdleHeadAim = 0.35f;
        public const float IdleChestAim = 0.15f;
        public const float ActingHeadAim = 0.8f;
        public const float ActingChestAim = 0.6f;

        public static IkProfile For(LocomotionStateId state, bool acting)
        {
            var feet = state == LocomotionStateId.Grounded ? 1f : 0f;
            return acting
                ? new IkProfile(feet, ActingHeadAim, ActingChestAim)
                : new IkProfile(feet, IdleHeadAim, IdleChestAim);
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
                Step(current.ChestAimWeight, target.ChestAimWeight, step));
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

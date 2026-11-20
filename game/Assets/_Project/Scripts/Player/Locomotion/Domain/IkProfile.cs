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

    // Foot placement maths, fed with a ground hit instead of querying physics so it stays testable.
    public static class FootPlacement
    {
        // Where the foot should go: on the ground plane below the animated foot's horizontal
        // position, lifted along the normal by the distance between the foot bone and the sole.
        public static Vector3 Target(Vector3 animatedFoot, float footHeightAboveSole, Vector3 groundPoint, Vector3 groundNormal)
        {
            var onPlane = new Vector3(animatedFoot.x, groundPoint.y, animatedFoot.z);
            if (groundNormal.y > 0.001f)
            {
                var planeY = groundPoint.y
                    - (groundNormal.x * (animatedFoot.x - groundPoint.x) + groundNormal.z * (animatedFoot.z - groundPoint.z)) / groundNormal.y;
                onPlane.y = planeY;
            }
            return onPlane + groundNormal * footHeightAboveSole;
        }

        // The animated rotation tilted so the foot's up follows the ground normal.
        public static Quaternion Rotation(Quaternion animatedFootRotation, Vector3 groundNormal)
        {
            return Quaternion.FromToRotation(Vector3.up, groundNormal) * animatedFootRotation;
        }

        // Drops are positive distances the feet must go down. The body sinks by the larger one so
        // the lower foot reaches the ground; it never rises.
        public static float HipOffset(float leftDrop, float rightDrop, float maxDrop)
        {
            return -Mathf.Clamp(Mathf.Max(leftDrop, rightDrop, 0f), 0f, maxDrop);
        }
    }
}

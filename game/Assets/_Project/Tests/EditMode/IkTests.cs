using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class IkProfileTests
    {
        [Test]
        public void GroundedPlantsTheFeet()
        {
            Assert.AreEqual(1f, IkProfiles.For(LocomotionStateId.Grounded, false).FeetWeight, 0.0001f);
        }

        [Test]
        public void JumpReleasesTheFeet()
        {
            Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Jump, false).FeetWeight, 0.0001f);
        }

        [Test]
        public void FallReleasesTheFeet()
        {
            Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Fall, false).FeetWeight, 0.0001f);
        }

        [Test]
        public void AimIsSoftWhenNotActing()
        {
            var profile = IkProfiles.For(LocomotionStateId.Grounded, false);
            Assert.AreEqual(IkProfiles.IdleHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.IdleChestAim, profile.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void AimIsStrongWhenActing()
        {
            var profile = IkProfiles.For(LocomotionStateId.Grounded, true);
            Assert.AreEqual(IkProfiles.ActingHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, profile.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void ActingAimIsStrongerThanIdleAim()
        {
            Assert.Greater(IkProfiles.ActingHeadAim, IkProfiles.IdleHeadAim);
            Assert.Greater(IkProfiles.ActingChestAim, IkProfiles.IdleChestAim);
        }

        [Test]
        public void AimStaysOnWhileAirborne()
        {
            var profile = IkProfiles.For(LocomotionStateId.Fall, true);
            Assert.AreEqual(IkProfiles.ActingHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, profile.ChestAimWeight, 0.0001f);
        }
    }

    public class IkBlendTests
    {
        private static readonly IkProfile Off = new IkProfile(0f, 0f, 0f);
        private static readonly IkProfile On = new IkProfile(1f, 1f, 1f);

        [Test]
        public void MovesTowardTheTargetByAtMostRateTimesDelta()
        {
            var result = IkBlend.Toward(Off, On, 6f, 0.05f);
            Assert.AreEqual(0.3f, result.FeetWeight, 0.0001f);
            Assert.AreEqual(0.3f, result.HeadAimWeight, 0.0001f);
            Assert.AreEqual(0.3f, result.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void StopsOnTheTargetWithoutOvershooting()
        {
            var result = IkBlend.Toward(new IkProfile(0.9f, 0.9f, 0.9f), On, 6f, 1f);
            Assert.AreEqual(1f, result.FeetWeight, 0.0001f);
        }

        [Test]
        public void MovesDownwardToo()
        {
            var result = IkBlend.Toward(On, Off, 6f, 0.05f);
            Assert.AreEqual(0.7f, result.FeetWeight, 0.0001f);
        }

        [Test]
        public void ZeroDeltaKeepsTheCurrentProfile()
        {
            var current = new IkProfile(0.4f, 0.2f, 0.1f);
            var result = IkBlend.Toward(current, On, 6f, 0f);
            Assert.AreEqual(0.4f, result.FeetWeight, 0.0001f);
            Assert.AreEqual(0.2f, result.HeadAimWeight, 0.0001f);
            Assert.AreEqual(0.1f, result.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void ClampsOutOfRangeTargets()
        {
            var result = IkBlend.Toward(On, new IkProfile(3f, -2f, 5f), 100f, 1f);
            Assert.AreEqual(1f, result.FeetWeight, 0.0001f);
            Assert.AreEqual(0f, result.HeadAimWeight, 0.0001f);
            Assert.AreEqual(1f, result.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void WeightsMoveIndependently()
        {
            var result = IkBlend.Toward(Off, new IkProfile(1f, 0f, 0.05f), 6f, 0.1f);
            Assert.AreEqual(0.6f, result.FeetWeight, 0.0001f);
            Assert.AreEqual(0f, result.HeadAimWeight, 0.0001f);
            Assert.AreEqual(0.05f, result.ChestAimWeight, 0.0001f);
        }
    }

    public class FootPlacementTests
    {
        private const float Height = 0.08f;

        [Test]
        public void FlatGroundPutsTheFootAboveTheSurface()
        {
            var target = FootPlacement.Target(new Vector3(1f, 0.3f, 2f), Height, new Vector3(1f, 0f, 2f), Vector3.up);
            Assert.AreEqual(1f, target.x, 0.0001f);
            Assert.AreEqual(Height, target.y, 0.0001f);
            Assert.AreEqual(2f, target.z, 0.0001f);
        }

        [Test]
        public void FlatGroundKeepsTheAnimatedHorizontalPosition()
        {
            var target = FootPlacement.Target(new Vector3(0.4f, 0.3f, -0.7f), Height, new Vector3(0.1f, 0f, -0.2f), Vector3.up);
            Assert.AreEqual(0.4f, target.x, 0.0001f);
            Assert.AreEqual(-0.7f, target.z, 0.0001f);
        }

        [Test]
        public void SlopeKeepsHorizontalPositionAndLiesOnThePlane()
        {
            var normal = Quaternion.Euler(14f, 0f, 0f) * Vector3.up;
            var groundPoint = new Vector3(0f, 1f, 0f);
            var animated = new Vector3(0.3f, 1.4f, 0.5f);

            var target = FootPlacement.Target(animated, Height, groundPoint, normal);
            var sole = target - normal * Height;

            Assert.AreEqual(animated.x, sole.x, 0.0001f);
            Assert.AreEqual(animated.z, sole.z, 0.0001f);
            Assert.AreEqual(0f, Vector3.Dot(sole - groundPoint, normal), 0.0001f);
        }

        [Test]
        public void SlopeLiftsTheFootByTheHeightAlongTheNormal()
        {
            var normal = Quaternion.Euler(0f, 0f, 14f) * Vector3.up;
            var groundPoint = new Vector3(2f, 0.5f, 2f);
            var animated = new Vector3(2.2f, 0.9f, 2.1f);

            var target = FootPlacement.Target(animated, Height, groundPoint, normal);

            Assert.AreEqual(Height, Vector3.Dot(target - groundPoint, normal), 0.0001f);
        }

        [Test]
        public void RotationOnFlatGroundIsUnchanged()
        {
            var animated = Quaternion.Euler(10f, 40f, 0f);
            var result = FootPlacement.Rotation(animated, Vector3.up);
            Assert.AreEqual(0f, Quaternion.Angle(animated, result), 0.001f);
        }

        [Test]
        public void RotationAlignsTheFootUpWithTheNormal()
        {
            var normal = Quaternion.Euler(14f, 0f, 0f) * Vector3.up;
            var result = FootPlacement.Rotation(Quaternion.identity, normal);
            Assert.AreEqual(0f, Vector3.Angle(result * Vector3.up, normal), 0.001f);
        }

        [Test]
        public void RotationKeepsTheAnimatedHeading()
        {
            var normal = Quaternion.Euler(14f, 0f, 0f) * Vector3.up;
            var result = FootPlacement.Rotation(Quaternion.Euler(0f, 90f, 0f), normal);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.ProjectOnPlane(result * Vector3.forward, Vector3.up), Vector3.right), 15f);
        }
    }

    public class HipOffsetTests
    {
        [Test]
        public void PicksTheLargerDrop()
        {
            Assert.AreEqual(-0.2f, FootPlacement.HipOffset(0.05f, 0.2f, 0.35f), 0.0001f);
            Assert.AreEqual(-0.2f, FootPlacement.HipOffset(0.2f, 0.05f, 0.35f), 0.0001f);
        }

        [Test]
        public void NeverRaisesTheHips()
        {
            Assert.AreEqual(0f, FootPlacement.HipOffset(-0.3f, -0.1f, 0.35f), 0.0001f);
        }

        [Test]
        public void NoDropMeansNoOffset()
        {
            Assert.AreEqual(0f, FootPlacement.HipOffset(0f, 0f, 0.35f), 0.0001f);
        }

        [Test]
        public void OffsetIsClampedToTheMaximumDrop()
        {
            Assert.AreEqual(-0.35f, FootPlacement.HipOffset(1f, 0.2f, 0.35f), 0.0001f);
        }

        [Test]
        public void OneRaisedFootDoesNotCancelTheOtherDrop()
        {
            Assert.AreEqual(-0.1f, FootPlacement.HipOffset(-0.4f, 0.1f, 0.35f), 0.0001f);
        }
    }
}

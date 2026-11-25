using HexFall.Player.Actions;
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
            Assert.AreEqual(1f, IkProfiles.For(LocomotionStateId.Grounded, ActionStateId.Ready).FeetWeight, 0.0001f);
        }

        [Test]
        public void JumpReleasesTheFeet()
        {
            Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Jump, ActionStateId.Ready).FeetWeight, 0.0001f);
        }

        [Test]
        public void FallReleasesTheFeet()
        {
            Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Fall, ActionStateId.Ready).FeetWeight, 0.0001f);
        }

        [Test]
        public void AimIsSoftWhenNotActing()
        {
            var profile = IkProfiles.For(LocomotionStateId.Grounded, ActionStateId.Ready);
            Assert.AreEqual(IkProfiles.IdleHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.IdleChestAim, profile.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void AimIsStrongWhenActing()
        {
            var profile = IkProfiles.For(LocomotionStateId.Grounded, ActionStateId.Attack);
            Assert.AreEqual(IkProfiles.ActingHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, profile.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void ParryAimsAsStronglyAsAttack()
        {
            var profile = IkProfiles.For(LocomotionStateId.Grounded, ActionStateId.Parry);
            Assert.AreEqual(IkProfiles.ActingHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, profile.ChestAimWeight, 0.0001f);
        }

        [Test]
        public void ParryEmptyAimsLikeAParry()
        {
            var empty = IkProfiles.For(LocomotionStateId.Grounded, ActionStateId.ParryEmpty);
            Assert.AreEqual(IkProfiles.ActingHeadAim, empty.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, empty.ChestAimWeight, 0.0001f);
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
            var profile = IkProfiles.For(LocomotionStateId.Fall, ActionStateId.Attack);
            Assert.AreEqual(IkProfiles.ActingHeadAim, profile.HeadAimWeight, 0.0001f);
            Assert.AreEqual(IkProfiles.ActingChestAim, profile.ChestAimWeight, 0.0001f);
        }
    }

    public class SlideAimProfileTests
    {
        // The UAL2 slide clip already holds the head and the torso, so the chest must not twist toward
        // the crosshair and the head only follows it lightly.
        [Test]
        public void SlideDropsTheChestAimAndOnlyLooselyAimsTheHead()
        {
            foreach (ActionStateId action in System.Enum.GetValues(typeof(ActionStateId)))
            {
                var profile = IkProfiles.For(LocomotionStateId.Slide, action);
                Assert.AreEqual(0f, profile.ChestAimWeight, 0.0001f, $"chest {action}");
                Assert.AreEqual(IkProfiles.SlideHeadAim, profile.HeadAimWeight, 0.0001f, $"head {action}");
                Assert.That(profile.HeadAimWeight, Is.LessThanOrEqualTo(IkProfiles.IdleHeadAim), $"head {action}");
            }
        }

        [Test]
        public void SlideLeavesTheFeetToTheClip()
        {
            foreach (ActionStateId action in System.Enum.GetValues(typeof(ActionStateId)))
                Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Slide, action).FeetWeight, 0.0001f, $"{action}");
        }
    }

    public class ClimbAimProfileTests
    {
        // The climb clip owns the whole body: no foot planting, no chest twist, and the head is left to
        // the clip (it looks at the ledge) unless the wizard is acting.
        [Test]
        public void ClimbLeavesTheBodyToTheClip()
        {
            foreach (ActionStateId action in System.Enum.GetValues(typeof(ActionStateId)))
            {
                var profile = IkProfiles.For(LocomotionStateId.Climb, action);
                Assert.AreEqual(0f, profile.FeetWeight, 0.0001f, $"feet {action}");
                Assert.AreEqual(0f, profile.ChestAimWeight, 0.0001f, $"chest {action}");
                Assert.AreEqual(IkProfiles.ClimbHeadAim, profile.HeadAimWeight, 0.0001f, $"head {action}");
            }
            Assert.AreEqual(0f, IkProfiles.ClimbHeadAim, 0.0001f);
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
        [Test]
        public void FlatGroundKeepsTheAnimatedFootUntouched()
        {
            var lifted = new Vector3(0.2f, 0.3f, 0.4f);
            Assert.AreEqual(lifted, FootPlacement.Follow(lifted, rootGroundHeight: 0f, groundHeightUnderFoot: 0f));
        }

        [Test]
        public void HigherGroundRaisesTheFootByTheDifference()
        {
            var planted = new Vector3(0.1f, 0.08f, 0.3f);
            var result = FootPlacement.Follow(planted, rootGroundHeight: 1f, groundHeightUnderFoot: 1.25f);
            Assert.AreEqual(new Vector3(0.1f, 0.33f, 0.3f).y, result.y, 0.0001f);
            Assert.AreEqual(planted.x, result.x, 0.0001f);
            Assert.AreEqual(planted.z, result.z, 0.0001f);
        }

        [Test]
        public void LowerGroundLowersTheFootByTheDifference()
        {
            var result = FootPlacement.Follow(new Vector3(0f, 1.08f, 0f), rootGroundHeight: 1f, groundHeightUnderFoot: 0.9f);
            Assert.AreEqual(0.98f, result.y, 0.0001f);
        }

        [Test]
        public void ALiftedFootStaysLiftedOnASlope()
        {
            // Mid-stride the foot is 30 cm up; on a slope it keeps those 30 cm above its own ground.
            var result = FootPlacement.Follow(new Vector3(0f, 1.3f, 0f), rootGroundHeight: 1f, groundHeightUnderFoot: 1.1f);
            Assert.AreEqual(1.4f, result.y, 0.0001f);
        }

        [Test]
        public void DropIsHowMuchLowerTheGroundIsThanUnderTheBody()
        {
            Assert.AreEqual(0.15f, FootPlacement.GroundDrop(rootGroundHeight: 1f, groundHeightUnderFoot: 0.85f), 0.0001f);
        }

        [Test]
        public void HigherGroundIsNoDrop()
        {
            Assert.AreEqual(0f, FootPlacement.GroundDrop(rootGroundHeight: 1f, groundHeightUnderFoot: 1.2f), 0.0001f);
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

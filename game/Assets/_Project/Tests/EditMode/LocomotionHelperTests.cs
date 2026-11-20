using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class LocomotionHelperTests
    {
        private const float Hard = 9f;

        // LookAngles

        [Test]
        public void LookAddsYawFromHorizontalDeltaAndLooksUpOnPositiveVertical()
        {
            var (yaw, pitch) = LookAngles.Apply(10f, 0f, new Vector2(5f, 3f), 1f, -30f, 70f);

            Assert.AreEqual(15f, yaw, 0.0001f);
            Assert.AreEqual(-3f, pitch, 0.0001f);
        }

        [Test]
        public void LookScalesDeltaByMultiplier()
        {
            var (yaw, pitch) = LookAngles.Apply(0f, 0f, new Vector2(10f, -10f), 0.5f, -30f, 70f);

            Assert.AreEqual(5f, yaw, 0.0001f);
            Assert.AreEqual(5f, pitch, 0.0001f);
        }

        [Test]
        public void LookWrapsYawIntoZeroTo360()
        {
            var (high, _) = LookAngles.Apply(355f, 0f, new Vector2(10f, 0f), 1f, -30f, 70f);
            var (low, _) = LookAngles.Apply(5f, 0f, new Vector2(-10f, 0f), 1f, -30f, 70f);

            Assert.AreEqual(5f, high, 0.0001f);
            Assert.AreEqual(355f, low, 0.0001f);
        }

        [Test]
        public void LookClampsPitchToLimits()
        {
            var (_, down) = LookAngles.Apply(0f, 60f, new Vector2(0f, -100f), 1f, -30f, 70f);
            var (_, up) = LookAngles.Apply(0f, -20f, new Vector2(0f, 100f), 1f, -30f, 70f);

            Assert.AreEqual(70f, down, 0.0001f);
            Assert.AreEqual(-30f, up, 0.0001f);
        }

        [Test]
        public void LookIgnoresTinyDeltas()
        {
            var (yaw, pitch) = LookAngles.Apply(12f, 4f, new Vector2(0.005f, 0.005f), 1f, -30f, 70f);

            Assert.AreEqual(12f, yaw, 0.0001f);
            Assert.AreEqual(4f, pitch, 0.0001f);
        }

        // AnimatorStateSelector

        [Test]
        public void JumpSelectsJumpStart()
        {
            Assert.AreEqual(AnimatorStateSelector.JumpStart,
                AnimatorStateSelector.For(LocomotionStateId.Jump, LocomotionStateId.Grounded, 0f, 3f, Hard));
        }

        [Test]
        public void FallSelectsInAir()
        {
            Assert.AreEqual(AnimatorStateSelector.InAir,
                AnimatorStateSelector.For(LocomotionStateId.Fall, LocomotionStateId.Jump, 0f, 3f, Hard));
        }

        [Test]
        public void StandingHardLandingSelectsJumpLand()
        {
            Assert.AreEqual(AnimatorStateSelector.JumpLand,
                AnimatorStateSelector.For(LocomotionStateId.Grounded, LocomotionStateId.Fall, 12f, 0.1f, Hard));
        }

        [Test]
        public void MovingHardLandingBlendsIntoLocomotion()
        {
            Assert.AreEqual(AnimatorStateSelector.Locomotion,
                AnimatorStateSelector.For(LocomotionStateId.Grounded, LocomotionStateId.Fall, 12f, 3f, Hard));
        }

        [Test]
        public void SoftLandingBlendsIntoLocomotion()
        {
            Assert.AreEqual(AnimatorStateSelector.Locomotion,
                AnimatorStateSelector.For(LocomotionStateId.Grounded, LocomotionStateId.Fall, 4f, 0f, Hard));
        }

        [Test]
        public void GroundedEntryNotAfterFallSelectsLocomotion()
        {
            Assert.AreEqual(AnimatorStateSelector.Locomotion,
                AnimatorStateSelector.For(LocomotionStateId.Grounded, LocomotionStateId.Jump, 12f, 0f, Hard));
        }

        [Test]
        public void KeysAreStable()
        {
            Assert.AreEqual("Locomotion", AnimatorStateSelector.Locomotion);
            Assert.AreEqual("JumpStart", AnimatorStateSelector.JumpStart);
            Assert.AreEqual("InAir", AnimatorStateSelector.InAir);
            Assert.AreEqual("JumpLand", AnimatorStateSelector.JumpLand);
        }
    }
}

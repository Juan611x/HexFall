using HexFall.Player.Locomotion;
using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // Launching the player from a rune circle: the speed for a height and the machine's launch transitions.
    public class LaunchTests
    {
        private const float Dt = 0.02f;

        private static PlayerIntent Idle() => new PlayerIntent(Vector2.zero, false, false, 0f);

        [Test]
        public void SpeedForHeightMatchesTheBallisticFormula()
        {
            Assert.AreEqual(Mathf.Sqrt(2f * 15f * 6f), LaunchMath.SpeedForHeight(6f, 15f), 0.0001f);
        }

        [Test]
        public void SpeedForHeightIsZeroWithoutHeightOrGravity()
        {
            Assert.AreEqual(0f, LaunchMath.SpeedForHeight(0f, 15f));
            Assert.AreEqual(0f, LaunchMath.SpeedForHeight(-2f, 15f));
            Assert.AreEqual(0f, LaunchMath.SpeedForHeight(6f, 0f));
        }

        [TestCase(LocomotionStateId.Grounded)]
        [TestCase(LocomotionStateId.Crouch)]
        [TestCase(LocomotionStateId.Slide)]
        [TestCase(LocomotionStateId.Fall)]
        public void LaunchEntersJumpWithTheGivenVerticalSpeed(LocomotionStateId from)
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, from);

            Assert.IsTrue(machine.Launch(10f));

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
            Assert.AreEqual(10f, machine.Context.Velocity.y, 0.0001f);
            Assert.AreEqual(settings.standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void LaunchKeepsThePlanarVelocity()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);
            machine.Context.Velocity = new Vector3(2f, -2f, 3f);

            machine.Launch(8f);

            Assert.AreEqual(2f, machine.Context.Velocity.x, 0.0001f);
            Assert.AreEqual(3f, machine.Context.Velocity.z, 0.0001f);
        }

        [Test]
        public void LaunchResetsTheJumpBufferAndStartsTheCooldown()
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Context.TimeSinceJumpPressed = 0f;

            machine.Launch(8f);

            Assert.IsTrue(float.IsPositiveInfinity(machine.Context.TimeSinceJumpPressed));
            Assert.AreEqual(settings.jumpCooldownSeconds, machine.Context.JumpCooldown, 0.0001f);
        }

        [Test]
        public void LaunchIsIgnoredWhileClimbing()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Climb);

            Assert.IsFalse(machine.Launch(10f));

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
        }

        [Test]
        public void LaunchRaisesTheStateChange()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);
            LocomotionStateId from = default, to = default;
            machine.StateChanged += (a, b) => { from = a; to = b; };

            machine.Launch(10f);

            Assert.AreEqual(LocomotionStateId.Grounded, from);
            Assert.AreEqual(LocomotionStateId.Jump, to);
        }

        [Test]
        public void ALaunchedBodyRisesThenFallsAndLands()
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Launch(LaunchMath.SpeedForHeight(6f, -settings.gravity));

            var y = 0f;
            var peak = 0f;
            var sawFall = false;
            for (var i = 0; i < 1000; i++)
            {
                machine.Tick(Idle(), new GroundSense(y <= 0f && machine.Context.Velocity.y < 0f), Dt);
                y += machine.Context.Velocity.y * Dt;
                if (y < 0f) y = 0f;
                peak = Mathf.Max(peak, y);
                if (machine.Current == LocomotionStateId.Fall) sawFall = true;
                if (machine.Current == LocomotionStateId.Grounded) break;
            }

            Assert.IsTrue(sawFall);
            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(6f, peak, 0.35f);
        }
    }
}

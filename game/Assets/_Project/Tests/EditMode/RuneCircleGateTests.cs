using HexFall.World.RuneCircles;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class RuneCircleGateTests
    {
        private const float Dt = 0.1f;

        private static RuneCircleGateSettings OnEnter(float cooldown = 0.5f, bool requireGrounded = true) => new RuneCircleGateSettings
        {
            mode = RuneCircleActivationMode.OnEnter,
            cooldownSeconds = cooldown,
            requireGrounded = requireGrounded,
        };

        private static RuneCircleGateSettings AfterStanding(float stand, float cooldown = 0.5f, bool requireGrounded = true) => new RuneCircleGateSettings
        {
            mode = RuneCircleActivationMode.AfterStanding,
            standSeconds = stand,
            cooldownSeconds = cooldown,
            requireGrounded = requireGrounded,
        };

        // Ticks for the given seconds and returns how many times the gate fired.
        private static int Run(RuneCircleGate gate, float seconds, bool inside, bool grounded = true)
        {
            var fires = 0;
            var ticks = (int)System.Math.Round(seconds / Dt);
            for (var i = 0; i < ticks; i++)
                if (gate.Tick(inside, grounded, Dt)) fires++;
            return fires;
        }

        [Test]
        public void OutsideNeverFires()
        {
            Assert.AreEqual(0, Run(new RuneCircleGate(OnEnter()), 5f, inside: false));
        }

        [Test]
        public void OnEnterFiresOnTheFirstTickInside()
        {
            var gate = new RuneCircleGate(OnEnter());
            Assert.IsTrue(gate.Tick(true, true, Dt));
        }

        [Test]
        public void StayingInsideDoesNotRefire()
        {
            var gate = new RuneCircleGate(OnEnter());
            Assert.AreEqual(1, Run(gate, 10f, inside: true));
        }

        [Test]
        public void LeavingAndReenteringAfterTheCooldownFiresAgain()
        {
            var gate = new RuneCircleGate(OnEnter(cooldown: 0.5f));
            Run(gate, 0.1f, true);
            Run(gate, 0.8f, false);
            Assert.AreEqual(1, Run(gate, 0.1f, true));
        }

        [Test]
        public void ReenteringWithinTheCooldownFiresOnceWhenItElapses()
        {
            var gate = new RuneCircleGate(OnEnter(cooldown: 0.5f));
            gate.Tick(true, true, Dt);
            gate.Tick(false, true, Dt);
            Assert.IsFalse(gate.Tick(true, true, Dt));
            Assert.IsFalse(gate.Tick(true, true, Dt));
            Assert.IsFalse(gate.Tick(true, true, Dt));
            Assert.IsTrue(gate.Tick(true, true, Dt));
            Assert.AreEqual(0, Run(gate, 5f, true));
        }

        [Test]
        public void RequireGroundedWaitsForTheLanding()
        {
            var gate = new RuneCircleGate(OnEnter(requireGrounded: true));
            Assert.AreEqual(0, Run(gate, 1f, inside: true, grounded: false));
            Assert.IsTrue(gate.Tick(true, true, Dt));
        }

        [Test]
        public void WithoutRequireGroundedItFiresInTheAir()
        {
            var gate = new RuneCircleGate(OnEnter(requireGrounded: false));
            Assert.IsTrue(gate.Tick(true, false, Dt));
        }

        [Test]
        public void AfterStandingFiresOnceTheTimeHasElapsed()
        {
            var gate = new RuneCircleGate(AfterStanding(1f));
            Assert.AreEqual(0, Run(gate, 0.9f, true));
            Assert.AreEqual(1, Run(gate, 0.3f, true));
        }

        [Test]
        public void AfterStandingFiresOncePerStay()
        {
            var gate = new RuneCircleGate(AfterStanding(1f));
            Assert.AreEqual(1, Run(gate, 10f, true));
        }

        [Test]
        public void AfterStandingRestartsWhenThePlayerLeaves()
        {
            var gate = new RuneCircleGate(AfterStanding(1f));
            Run(gate, 0.7f, true);
            Run(gate, 0.1f, false);
            Assert.AreEqual(0, Run(gate, 0.7f, true));
            Assert.AreEqual(1, Run(gate, 0.5f, true));
        }

        [Test]
        public void AfterStandingRestartsWhenLeavingTheGround()
        {
            var gate = new RuneCircleGate(AfterStanding(1f));
            Run(gate, 0.7f, true);
            Run(gate, 0.1f, true, grounded: false);
            Assert.AreEqual(0, Run(gate, 0.7f, true));
            Assert.AreEqual(1, Run(gate, 0.5f, true));
        }

        [Test]
        public void LeavingInTheAirAndLandingBackOnTheCircleDoesNotRefire()
        {
            // A launch carries the player out of the trigger volume; landing back must not relaunch.
            var gate = new RuneCircleGate(OnEnter());
            Assert.AreEqual(1, Run(gate, 0.1f, true));
            Run(gate, 1.5f, false, grounded: false);
            Assert.AreEqual(0, Run(gate, 2f, true));
        }

        [Test]
        public void TouchingTheGroundOutsideRearmsTheCircle()
        {
            var gate = new RuneCircleGate(OnEnter());
            Run(gate, 0.1f, true);
            Run(gate, 1f, false, grounded: false);
            Run(gate, 0.1f, false, grounded: true);
            Assert.AreEqual(1, Run(gate, 0.1f, true));
        }

        [Test]
        public void WithoutRequireGroundedAnyExitRearms()
        {
            var gate = new RuneCircleGate(OnEnter(requireGrounded: false));
            Run(gate, 0.1f, true, grounded: false);
            Run(gate, 1f, false, grounded: false);
            Assert.AreEqual(1, Run(gate, 0.1f, true, grounded: false));
        }

        [Test]
        public void LockSuppressesFiringUntilThePlayerLeaves()
        {
            var gate = new RuneCircleGate(OnEnter());
            gate.Lock();
            Assert.AreEqual(0, Run(gate, 3f, inside: true));
            Run(gate, 0.1f, false);
            Assert.AreEqual(1, Run(gate, 0.1f, true));
        }

        [Test]
        public void LockSurvivesTheTicksBeforeThePlayerIsDetectedInside()
        {
            var gate = new RuneCircleGate(OnEnter());
            gate.Lock();
            Run(gate, 0.3f, inside: false);
            Assert.AreEqual(0, Run(gate, 1f, inside: true));
            Run(gate, 0.1f, false);
            Assert.AreEqual(1, Run(gate, 0.1f, true));
        }

        [Test]
        public void LockedAfterStandingStillWaitsTheFullTimeAfterTheNextEntry()
        {
            var gate = new RuneCircleGate(AfterStanding(1f));
            gate.Lock();
            Run(gate, 2f, true);
            Run(gate, 0.1f, false);
            Assert.AreEqual(0, Run(gate, 0.9f, true));
            Assert.AreEqual(1, Run(gate, 0.3f, true));
        }
    }
}

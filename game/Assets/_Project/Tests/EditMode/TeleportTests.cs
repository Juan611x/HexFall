using System.Collections.Generic;
using System.Text.RegularExpressions;
using HexFall.Player.Locomotion;
using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexFall.UI.Tests
{
    // Teleporting the player between rune circles: the machine's reset, the effect and a full A <-> B round trip.
    public class TeleportTests
    {
        private const float Dt = 0.1f;

        private static PlayerIntent Idle() => new PlayerIntent(Vector2.zero, false, false, 0f);

        // --- LocomotionMachine.Teleport ---

        [TestCase(LocomotionStateId.Grounded)]
        [TestCase(LocomotionStateId.Jump)]
        [TestCase(LocomotionStateId.Fall)]
        [TestCase(LocomotionStateId.Crouch)]
        [TestCase(LocomotionStateId.Slide)]
        public void TeleportGroundedEntersGroundedWithStandHeight(LocomotionStateId from)
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, from);

            Assert.IsTrue(machine.Teleport(new Vector3(1f, 2f, 3f), 90f, grounded: true));

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(settings.standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void TeleportAirborneEntersFall()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);

            Assert.IsTrue(machine.Teleport(Vector3.zero, 0f, grounded: false));

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        [Test]
        public void TeleportResetsVelocityAndSpeedAndSetsPositionAndFacing()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Fall);
            machine.Context.Velocity = new Vector3(3f, -20f, 4f);
            machine.Context.PlanarSpeed = 5f;
            machine.Context.YawVelocity = 12f;
            machine.Context.LaunchSpeed = 9f;

            machine.Teleport(new Vector3(20f, 0.05f, 6f), 135f, grounded: true);

            Assert.AreEqual(Vector3.zero, machine.Context.Velocity);
            Assert.AreEqual(0f, machine.Context.PlanarSpeed);
            Assert.AreEqual(0f, machine.Context.YawVelocity);
            Assert.IsNaN(machine.Context.LaunchSpeed);
            Assert.AreEqual(new Vector3(20f, 0.05f, 6f), machine.Context.Position);
            Assert.AreEqual(135f, machine.Context.FacingYaw, 0.0001f);
        }

        [Test]
        public void TeleportGroundedResetsTheCoyoteTimer()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Fall);
            machine.Context.TimeSinceGrounded = 3f;

            machine.Teleport(Vector3.zero, 0f, grounded: true);

            Assert.AreEqual(0f, machine.Context.TimeSinceGrounded);
        }

        [Test]
        public void TeleportRaisesStateChangedOnlyWhenTheStateChanges()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Slide);
            var changes = new List<(LocomotionStateId, LocomotionStateId)>();
            machine.StateChanged += (a, b) => changes.Add((a, b));

            machine.Teleport(Vector3.zero, 0f, grounded: true);
            machine.Teleport(Vector3.one, 0f, grounded: true);

            CollectionAssert.AreEqual(new[] { (LocomotionStateId.Slide, LocomotionStateId.Grounded) }, changes);
        }

        [Test]
        public void TeleportIsIgnoredWhileClimbing()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Climb);
            machine.Context.Velocity = new Vector3(1f, 2f, 3f);
            var before = machine.Context.Position;

            Assert.IsFalse(machine.Teleport(new Vector3(9f, 9f, 9f), 45f, grounded: true));

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), machine.Context.Velocity);
            Assert.AreEqual(before, machine.Context.Position);
        }

        [Test]
        public void TheMachineKeepsTickingAfterATeleport()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Fall);
            machine.Teleport(Vector3.zero, 0f, grounded: false);

            machine.Tick(Idle(), new GroundSense(true), 0.02f);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        // --- TeleportEffect ---

        private sealed class FakeTarget : IRuneCircleTarget
        {
            public Vector3 FeetPosition { get; set; }
            public float FacingYaw { get; set; } = 40f;
            public bool IsGrounded { get; set; } = true;
            public float GravityMagnitude => 15f;
            public readonly List<(Vector3 position, float yaw)> Teleports = new List<(Vector3, float)>();
            public void Launch(float verticalSpeed) { }

            public void Teleport(Vector3 position, float yaw)
            {
                Teleports.Add((position, yaw));
                FeetPosition = position;
                FacingYaw = yaw;
            }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private RuneCircle NewCircle(string name, Vector3 position, float yaw = 0f)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go.AddComponent<RuneCircle>();
        }

        private static TeleportEffect AddEffect(RuneCircle source, RuneCircle destination, bool keepFacing = true)
        {
            var effect = source.gameObject.AddComponent<TeleportEffect>();
            effect.Configure(destination, keepFacing);
            return effect;
        }

        [Test]
        public void TeleportEffectMovesTheTargetToTheDestinationCentreKeepingItsFacing()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f), yaw: 200f);
            var effect = AddEffect(a, b);
            var target = new FakeTarget { FacingYaw = 40f };

            effect.Apply(new RuneCircleActivation(a.transform, target));

            Assert.AreEqual(1, target.Teleports.Count);
            var (position, yaw) = target.Teleports[0];
            Assert.AreEqual(20f, position.x, 0.0001f);
            Assert.AreEqual(6f, position.z, 0.0001f);
            Assert.GreaterOrEqual(position.y, 0.01f);
            Assert.LessOrEqual(position.y, 0.2f, "Arrives on the floor, not high above it.");
            Assert.AreEqual(40f, yaw, 0.0001f);
        }

        [Test]
        public void TeleportEffectCanFaceTheDestinationForwardInstead()
        {
            var a = NewCircle("A", Vector3.zero);
            var b = NewCircle("B", new Vector3(20f, 0f, 0f), yaw: 200f);
            var effect = AddEffect(a, b, keepFacing: false);
            var target = new FakeTarget { FacingYaw = 40f };

            effect.Apply(new RuneCircleActivation(a.transform, target));

            Assert.AreEqual(200f, target.Teleports[0].yaw, 0.001f);
        }

        [Test]
        public void TeleportEffectWithoutADestinationWarnsOnceAndDoesNothing()
        {
            var a = NewCircle("A", Vector3.zero);
            var effect = AddEffect(a, null);
            var target = new FakeTarget();
            var activation = new RuneCircleActivation(a.transform, target);

            LogAssert.Expect(LogType.Warning, new Regex("destination"));
            effect.Apply(activation);
            effect.Apply(activation);

            Assert.AreEqual(0, target.Teleports.Count);
            LogAssert.NoUnexpectedReceived();
        }

        // --- Round trip A <-> B ---

        private static void Run(float seconds, params RuneCircle[] circles)
        {
            var ticks = Mathf.RoundToInt(seconds / Dt);
            for (var i = 0; i < ticks; i++)
                foreach (var circle in circles) circle.Tick(Dt);
        }

        private static void SetGate(RuneCircle circle)
        {
            circle.Gate.mode = RuneCircleActivationMode.AfterStanding;
            circle.Gate.standSeconds = 0.6f;
            circle.Gate.cooldownSeconds = 1f;
            circle.Gate.requireGrounded = true;
        }

        [Test]
        public void StandingOnAGoesToBWithoutBouncingBackAndReturnsAfterSteppingOffAndOn()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f));
            SetGate(a);
            SetGate(b);
            AddEffect(a, b);
            AddEffect(b, a);
            var target = new FakeTarget { FeetPosition = a.transform.position };
            a.Track(target);

            Run(0.5f, a, b);
            Assert.AreEqual(0, target.Teleports.Count, "Standing less than standSeconds does not teleport.");

            Run(0.3f, a, b);
            Assert.AreEqual(1, target.Teleports.Count);
            Assert.AreEqual(20f, target.Teleports[0].position.x, 0.0001f);

            Run(5f, a, b);
            Assert.AreEqual(1, target.Teleports.Count, "Standing on the arrival circle must not bounce back.");

            // Step off B and back on.
            target.FeetPosition = new Vector3(23f, 0.01f, 6f);
            Run(0.5f, a, b);
            target.FeetPosition = b.transform.position;
            Run(1.0f, a, b);

            Assert.AreEqual(2, target.Teleports.Count);
            Assert.AreEqual(-16f, target.Teleports[1].position.x, 0.0001f);
        }

        [Test]
        public void ThePlayerInsideTheSourceCircleDoesNotRetriggerAfterTheTeleport()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f));
            SetGate(a);
            SetGate(b);
            AddEffect(a, b);
            var target = new FakeTarget { FeetPosition = a.transform.position };
            a.Track(target);

            Run(10f, a, b);

            Assert.AreEqual(1, target.Teleports.Count);
        }
    }
}

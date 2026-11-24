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
        public void TeleportKeepsThePlanarVelocityWhenTheFacingIsKept()
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Context.FacingYaw = 30f;
            machine.Context.Velocity = new Vector3(3f, -20f, 4f);
            machine.Context.PlanarSpeed = 5f;
            machine.Context.YawVelocity = 12f;
            machine.Context.LaunchSpeed = 9f;

            machine.Teleport(new Vector3(20f, 0.05f, 6f), 30f, grounded: true);

            Assert.AreEqual(3f, machine.Context.Velocity.x, 0.0001f);
            Assert.AreEqual(4f, machine.Context.Velocity.z, 0.0001f);
            Assert.AreEqual(5f, machine.Context.PlanarSpeed, 0.0001f);
            Assert.AreEqual(settings.groundedStickSpeed, machine.Context.Velocity.y, 0.0001f);
            Assert.AreEqual(0f, machine.Context.YawVelocity);
            Assert.IsNaN(machine.Context.LaunchSpeed);
            Assert.AreEqual(new Vector3(20f, 0.05f, 6f), machine.Context.Position);
            Assert.AreEqual(30f, machine.Context.FacingYaw, 0.0001f);
        }

        [Test]
        public void TeleportRotatesThePlanarVelocityWithTheFacing()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);
            machine.Context.FacingYaw = 0f;
            machine.Context.Velocity = new Vector3(0f, -2f, 6f);
            machine.Context.PlanarSpeed = 6f;

            machine.Teleport(Vector3.zero, 90f, grounded: true);

            Assert.AreEqual(6f, machine.Context.Velocity.x, 0.001f);
            Assert.AreEqual(0f, machine.Context.Velocity.z, 0.001f);
            Assert.AreEqual(6f, machine.Context.PlanarSpeed, 0.001f);
            Assert.AreEqual(90f, machine.Context.FacingYaw, 0.0001f);
        }

        [Test]
        public void TeleportRotatesByTheShortestAngle()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);
            machine.Context.FacingYaw = 350f;
            machine.Context.Velocity = new Vector3(0f, -2f, 4f);
            machine.Context.PlanarSpeed = 4f;

            machine.Teleport(Vector3.zero, 10f, grounded: true);

            // +20 degrees about Y turns (0,0,4) toward +x.
            Assert.AreEqual(4f * Mathf.Sin(20f * Mathf.Deg2Rad), machine.Context.Velocity.x, 0.001f);
            Assert.AreEqual(4f * Mathf.Cos(20f * Mathf.Deg2Rad), machine.Context.Velocity.z, 0.001f);
        }

        [Test]
        public void TeleportAirborneDropsTheVerticalSpeedButKeepsThePlanarOne()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Fall);
            machine.Context.Velocity = new Vector3(3f, -20f, 4f);
            machine.Context.PlanarSpeed = 5f;

            machine.Teleport(Vector3.zero, 0f, grounded: false);

            Assert.AreEqual(0f, machine.Context.Velocity.y);
            Assert.AreEqual(5f, machine.Context.PlanarSpeed, 0.0001f);
        }

        [Test]
        public void TeleportFromStandStillStaysStill()
        {
            var machine = new LocomotionMachine(new LocomotionSettings(), LocomotionStateId.Grounded);

            machine.Teleport(Vector3.one, 45f, grounded: true);

            Assert.AreEqual(0f, machine.Context.PlanarSpeed);
            Assert.AreEqual(0f, machine.Context.Velocity.x);
            Assert.AreEqual(0f, machine.Context.Velocity.z);
        }

        [Test]
        public void TheRunningPlayerKeepsTheRunAfterTheTeleport()
        {
            var settings = new LocomotionSettings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            var run = new PlayerIntent(new Vector2(0f, 1f), true, false, 0f);
            for (var i = 0; i < 40; i++) machine.Tick(run, new GroundSense(true), 0.05f);
            var speed = machine.Context.PlanarSpeed;
            Assert.Greater(speed, 1f);

            machine.Teleport(new Vector3(20f, 0.05f, 6f), machine.Context.FacingYaw, grounded: true);
            machine.Tick(run, new GroundSense(true), 0.02f);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(speed, machine.Context.PlanarSpeed, 0.1f);
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
            public bool Accepts = true;
            public readonly List<(Vector3 position, float yaw)> Teleports = new List<(Vector3, float)>();
            public void Launch(float verticalSpeed) { }

            public bool Teleport(Vector3 position, float yaw)
            {
                Teleports.Add((position, yaw));
                if (!Accepts) return false;
                FeetPosition = position;
                FacingYaw = yaw;
                return true;
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

        [Test]
        public void ARefusedTeleportDoesNotLockTheDestination()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f));
            SetGate(b);
            var effectA = AddEffect(a, b);
            AddEffect(b, a);
            var target = new FakeTarget { FeetPosition = b.transform.position, Accepts = false };
            b.Track(target);

            effectA.Apply(new RuneCircleActivation(a.transform, target));
            Run(0.5f, b);

            Assert.AreEqual(2, target.Teleports.Count, "A refused teleport leaves B free to fire for a player standing on it.");
        }

        [Test]
        public void AnAcceptedTeleportLocksTheDestination()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f));
            SetGate(b);
            var effectA = AddEffect(a, b);
            AddEffect(b, a);
            var target = new FakeTarget { FeetPosition = b.transform.position };

            effectA.Apply(new RuneCircleActivation(a.transform, target));
            Run(0.5f, b);

            Assert.AreEqual(1, target.Teleports.Count);
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
            circle.Gate.mode = RuneCircleActivationMode.OnEnter;
            circle.Gate.cooldownSeconds = 1f;
            circle.Gate.requireGrounded = true;
        }

        [Test]
        public void RunningThroughAGoesToBAtOnceWithoutBouncingBack()
        {
            var a = NewCircle("A", new Vector3(-16f, 0.01f, -8f));
            var b = NewCircle("B", new Vector3(20f, 0.01f, 6f));
            SetGate(a);
            SetGate(b);
            AddEffect(a, b);
            AddEffect(b, a);
            var target = new FakeTarget { FeetPosition = new Vector3(-19f, 0.01f, -8f) };
            a.Track(target);

            // Sprint along +x at 8 m/s, one 0.1 s tick at a time; the teleport moves the target to B,
            // where it keeps running along +x and leaves.
            var enteredAt = -1;
            for (var i = 0; i < 100; i++)
            {
                var f = target.FeetPosition;
                target.FeetPosition = new Vector3(f.x + 8f * Dt, f.y, f.z);
                a.Tick(Dt);
                b.Tick(Dt);
                if (enteredAt < 0 && target.Teleports.Count > 0) enteredAt = i;
            }

            Assert.AreEqual(1, target.Teleports.Count, "Fires once on entry and never bounces back from B.");
            Assert.AreEqual(20f, target.Teleports[0].position.x, 0.0001f);
            Assert.LessOrEqual(enteredAt, 5, "Fires as soon as the feet reach the ring, within a few ticks.");
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

            Run(0.2f, a, b);
            Assert.AreEqual(1, target.Teleports.Count, "Entering fires at once, with no standing wait.");
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

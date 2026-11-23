using System.Collections.Generic;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class LocomotionTests
    {
        private const float Dt = 0.02f;

        private static LocomotionSettings Settings() => new LocomotionSettings();

        private static PlayerIntent Idle() => new PlayerIntent(Vector2.zero, false, false, 0f);

        private static PlayerIntent Move(Vector2 move, bool sprint = false, float cameraYaw = 0f) =>
            new PlayerIntent(move, sprint, false, cameraYaw);

        private static PlayerIntent Jump() => new PlayerIntent(Vector2.zero, false, true, 0f);

        private static readonly GroundSense OnGround = new GroundSense(true);
        private static readonly GroundSense InAir = new GroundSense(false);

        private static void TickMany(LocomotionMachine machine, int count, PlayerIntent intent, GroundSense ground)
        {
            for (var i = 0; i < count; i++) machine.Tick(intent, ground, Dt);
        }

        private static float Planar(LocomotionMachine machine) =>
            new Vector2(machine.Context.Velocity.x, machine.Context.Velocity.z).magnitude;

        // Grounded

        [Test]
        public void GroundedAcceleratesGraduallyInsteadOfInstantly()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            machine.Tick(Move(Vector2.up), OnGround, Dt);

            Assert.Greater(Planar(machine), 0f);
            Assert.Less(Planar(machine), 3.5f);
        }

        [Test]
        public void GroundedReachesJogSpeedAfterEnoughTicks()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            TickMany(machine, 60, Move(Vector2.up), OnGround);

            Assert.AreEqual(3.5f, Planar(machine), 0.001f);
            Assert.AreEqual(3.5f, machine.Context.PlanarSpeed, 0.001f);
        }

        [Test]
        public void MovementIsRelativeToTheCameraYaw()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            TickMany(machine, 60, Move(Vector2.up, false, 90f), OnGround);

            Assert.AreEqual(3.5f, machine.Context.Velocity.x, 0.001f);
            Assert.AreEqual(0f, machine.Context.Velocity.z, 0.001f);
        }

        [Test]
        public void SprintReachesSprintSpeed()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            TickMany(machine, 100, Move(Vector2.up, true), OnGround);

            Assert.AreEqual(6f, Planar(machine), 0.001f);
        }

        [Test]
        public void PartialStickGivesWalkSpeedScaledByMagnitude()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            TickMany(machine, 60, Move(new Vector2(0f, 0.3f)), OnGround);

            Assert.AreEqual(1.8f * 0.3f / 0.6f, Planar(machine), 0.001f);
        }

        [Test]
        public void StickAtTheWalkThresholdGivesJogSpeed()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            TickMany(machine, 60, Move(new Vector2(0f, 0.6f)), OnGround);

            Assert.AreEqual(3.5f, Planar(machine), 0.001f);
        }

        [Test]
        public void GroundedDeceleratesToZeroWithNoInput()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Context.Velocity = new Vector3(3.5f, 0f, 0f);

            machine.Tick(Idle(), OnGround, Dt);
            Assert.Greater(Planar(machine), 0f);

            TickMany(machine, 30, Idle(), OnGround);
            Assert.AreEqual(0f, Planar(machine), 0.0001f);
            Assert.AreEqual(0f, machine.Context.PlanarSpeed, 0.0001f);
        }

        [Test]
        public void SideInputWhileJoggingTurnsTheVelocityAtTheTurnRate()
        {
            var settings = Settings();
            settings.turnAcceleration = 40f;
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Context.Velocity = new Vector3(0f, 0f, 3.5f);

            TickMany(machine, 5, Move(Vector2.right), OnGround);

            // Five ticks at 40 * 0.02 cover 4.0 of the 4.95 distance; deceleration alone would cover 1.6.
            var remaining = (new Vector3(machine.Context.Velocity.x, 0f, machine.Context.Velocity.z) - new Vector3(3.5f, 0f, 0f)).magnitude;
            Assert.AreEqual(4.95f - 4f, remaining, 0.01f);
        }

        [Test]
        public void FullReversalFlipsTheVelocityWithinAFewTicks()
        {
            var settings = Settings();
            settings.turnAcceleration = 40f;
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Context.Velocity = new Vector3(0f, 0f, 3.5f);

            TickMany(machine, 5, Move(Vector2.down), OnGround);

            // Four ticks at 0.8 stop the body; deceleration alone would still be moving forward.
            Assert.Less(machine.Context.Velocity.z, 0f);
        }

        [Test]
        public void StartingFromRestStillUsesTheAcceleration()
        {
            var settings = Settings();
            settings.turnAcceleration = 40f;
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);

            machine.Tick(Move(Vector2.right), OnGround, Dt);

            Assert.AreEqual(settings.acceleration * Dt, Planar(machine), 0.0001f);
        }

        [Test]
        public void ReleasingTheInputStillUsesTheDeceleration()
        {
            var settings = Settings();
            settings.turnAcceleration = 40f;
            var machine = new LocomotionMachine(settings, LocomotionStateId.Grounded);
            machine.Context.Velocity = new Vector3(0f, 0f, 3.5f);

            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(3.5f - settings.deceleration * Dt, Planar(machine), 0.0001f);
        }

        [Test]
        public void FacingTurnsSmoothlyTowardTheMovementDirection()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            machine.Tick(Move(Vector2.right), OnGround, Dt);
            Assert.Greater(machine.Context.FacingYaw, 0f);
            Assert.Less(machine.Context.FacingYaw, 90f);

            TickMany(machine, 100, Move(Vector2.right), OnGround);
            Assert.AreEqual(90f, machine.Context.FacingYaw, 1f);
        }

        [Test]
        public void FacingStaysPutWithNoInput()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Context.FacingYaw = 30f;

            TickMany(machine, 20, Idle(), OnGround);

            Assert.AreEqual(30f, machine.Context.FacingYaw, 0.0001f);
        }

        [Test]
        public void GroundedHoldsTheStickSpeedVertically()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(-2f, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void JumpPressedOnTheGroundStartsAJump()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            machine.Tick(Jump(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void JumpPressedShortlyBeforeLandingJumpsOnLanding()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);

            machine.Tick(Jump(), InAir, Dt);
            machine.Tick(Idle(), InAir, Dt);
            machine.Tick(Idle(), OnGround, Dt);
            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);

            machine.Tick(Idle(), OnGround, Dt);
            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void JumpPressedTooEarlyBeforeLandingIsForgotten()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);

            machine.Tick(Jump(), InAir, Dt);
            TickMany(machine, 10, Idle(), InAir);
            machine.Tick(Idle(), OnGround, Dt);
            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void LeavingTheGroundStaysGroundedWithinCoyoteTime()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Tick(Idle(), OnGround, Dt);

            TickMany(machine, 5, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void JumpIsStillPossibleWithinCoyoteTime()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Tick(Idle(), OnGround, Dt);
            TickMany(machine, 5, Idle(), InAir);

            machine.Tick(Jump(), InAir, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void StayingOffTheGroundPastCoyoteTimeFalls()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Tick(Idle(), OnGround, Dt);

            TickMany(machine, 8, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        [Test]
        public void JumpIsBlockedWhileTheCooldownRuns()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            machine.Context.JumpCooldown = 0.2f;

            machine.Tick(Jump(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        // Jump

        [Test]
        public void EnteringJumpSetsTheVerticalSpeedForTheConfiguredHeight()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Jump);

            var expected = Mathf.Sqrt(2f * settings.jumpHeight * -settings.gravity);
            Assert.AreEqual(expected, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void JumpAppliesGravityEachTick()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Jump);
            var start = machine.Context.Velocity.y;

            machine.Tick(Idle(), InAir, Dt);

            Assert.AreEqual(start + settings.gravity * Dt, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void JumpBecomesFallAtTheApex()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Jump);

            TickMany(machine, 100, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
            Assert.LessOrEqual(machine.Context.Velocity.y, 0f);
        }

        [Test]
        public void JumpReachesRoughlyTheConfiguredHeight()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Jump);
            var height = 0f;

            for (var i = 0; i < 100 && machine.Current == LocomotionStateId.Jump; i++)
            {
                machine.Tick(Idle(), InAir, Dt);
                height += machine.Context.Velocity.y * Dt;
            }

            Assert.AreEqual(settings.jumpHeight, height, 0.1f);
        }

        [Test]
        public void AirborneControlIsReducedByAirControl()
        {
            var ground = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            var air = new LocomotionMachine(Settings(), LocomotionStateId.Jump);

            ground.Tick(Move(Vector2.up), OnGround, Dt);
            air.Tick(Move(Vector2.up), InAir, Dt);

            Assert.AreEqual(Planar(ground) * 0.35f, Planar(air), 0.0001f);
        }

        [Test]
        public void JumpPressedMidJumpDoesNotJumpAgain()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Jump);
            var before = machine.Context.Velocity.y;

            machine.Tick(Jump(), InAir, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
            Assert.Less(machine.Context.Velocity.y, before);
        }

        // Fall

        [Test]
        public void FallAccumulatesGravity()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Fall);

            machine.Tick(Idle(), InAir, Dt);
            machine.Tick(Idle(), InAir, Dt);

            Assert.AreEqual(settings.gravity * Dt * 2f, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void FallSpeedIsClampedAtTerminalVelocity()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Fall);

            TickMany(machine, 400, Idle(), InAir);

            Assert.AreEqual(settings.terminalVelocity, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void LandingReturnsToGroundedAndRecordsTheLandingSpeed()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);
            machine.Context.Velocity = new Vector3(0f, -10f, 0f);

            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(10f, machine.Context.LastLandingSpeed, 0.0001f);
        }

        [Test]
        public void FallKeepsFallingWhileNotGrounded()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);

            TickMany(machine, 10, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        // Machine

        [Test]
        public void StateChangedReportsFromAndTo()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            var changes = new List<(LocomotionStateId from, LocomotionStateId to)>();
            machine.StateChanged += (from, to) => changes.Add((from, to));

            machine.Tick(Jump(), OnGround, Dt);

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(LocomotionStateId.Grounded, changes[0].from);
            Assert.AreEqual(LocomotionStateId.Jump, changes[0].to);
        }

        [Test]
        public void StateChangedIsNotRaisedWhenStaying()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            var raised = 0;
            machine.StateChanged += (from, to) => raised++;

            TickMany(machine, 5, Idle(), OnGround);

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ExitOfTheOldAndEnterOfTheNewRunOncePerTransition()
        {
            var log = new List<string>();
            var grounded = new SpyState(LocomotionStateId.Grounded, LocomotionStateId.Jump, log);
            var jump = new SpyState(LocomotionStateId.Jump, LocomotionStateId.Jump, log);
            var fall = new SpyState(LocomotionStateId.Fall, LocomotionStateId.Fall, log);
            var machine = new LocomotionMachine(new ILocomotionState[] { grounded, jump, fall }, LocomotionStateId.Grounded);
            log.Clear();

            machine.Tick(Idle(), OnGround, Dt);
            machine.Tick(Idle(), OnGround, Dt);

            CollectionAssert.AreEqual(
                new[] { "Grounded.Tick", "Grounded.Exit", "Jump.Enter", "Jump.Tick" },
                log);
        }

        [Test]
        public void AtMostOneTransitionHappensPerTick()
        {
            var log = new List<string>();
            var grounded = new SpyState(LocomotionStateId.Grounded, LocomotionStateId.Jump, log);
            var jump = new SpyState(LocomotionStateId.Jump, LocomotionStateId.Fall, log);
            var fall = new SpyState(LocomotionStateId.Fall, LocomotionStateId.Fall, log);
            var machine = new LocomotionMachine(new ILocomotionState[] { grounded, jump, fall }, LocomotionStateId.Grounded);

            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void ConstructingWithUnregisteredInitialStateThrows()
        {
            var log = new List<string>();
            var states = new ILocomotionState[] { new SpyState(LocomotionStateId.Grounded, LocomotionStateId.Grounded, log) };

            Assert.Throws<System.ArgumentException>(() => new LocomotionMachine(states, LocomotionStateId.Fall));
        }

        [Test]
        public void TransitionToUnregisteredStateThrowsWithoutExitingOrChangingCurrent()
        {
            var log = new List<string>();
            var states = new ILocomotionState[] { new SpyState(LocomotionStateId.Grounded, LocomotionStateId.Fall, log) };
            var machine = new LocomotionMachine(states, LocomotionStateId.Grounded);
            log.Clear();

            Assert.Throws<System.ArgumentException>(() => machine.Tick(Idle(), OnGround, Dt));

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            CollectionAssert.DoesNotContain(log, "Grounded.Exit");
        }

        private sealed class SpyState : ILocomotionState
        {
            private readonly LocomotionStateId _next;
            private readonly List<string> _log;

            public SpyState(LocomotionStateId id, LocomotionStateId next, List<string> log)
            {
                Id = id;
                _next = next;
                _log = log;
            }

            public LocomotionStateId Id { get; }

            public void Enter(LocomotionContext ctx) => _log.Add(Id + ".Enter");

            public LocomotionStateId Tick(LocomotionContext ctx, PlayerIntent intent, GroundSense ground, float deltaTime)
            {
                _log.Add(Id + ".Tick");
                return _next;
            }

            public void Exit(LocomotionContext ctx) => _log.Add(Id + ".Exit");
        }
    }
}

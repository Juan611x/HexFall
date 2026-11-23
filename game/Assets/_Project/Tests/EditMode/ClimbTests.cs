using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class ClimbTests
    {
        private const float Dt = 0.02f;

        private static LocomotionSettings Settings() => new LocomotionSettings();

        private static PlayerIntent Idle() => new PlayerIntent(Vector2.zero, false, false, 0f);

        // Forward stick, camera yaw 0: the wanted direction is +Z, the approach direction of the test ledges.
        private static PlayerIntent Forward(bool jump = false, bool crouch = false, float cameraYaw = 0f) =>
            new PlayerIntent(Vector2.up, false, jump, cameraYaw, crouch);

        private static PlayerIntent Backward(bool jump) => new PlayerIntent(Vector2.down, false, jump, 0f);

        private static PlayerIntent JumpOnly() => new PlayerIntent(Vector2.zero, false, true, 0f);

        private static LedgeMeasurement Ledge(float height = 1f, float depth = 2f, float headroom = 3f, bool landingClear = true) =>
            new LedgeMeasurement(true, height, new Vector3(0f, height, 1f), Vector3.forward, depth, headroom, landingClear, 0f);

        private static LedgeMeasurement ClimbUpLedge() => Ledge();
        private static LedgeMeasurement ClimbOverLedge() => Ledge(depth: 0.3f, headroom: 3f, landingClear: true);
        private static LedgeMeasurement TooHighLedge() => Ledge(height: 2f);

        private static GroundSense Ground(LedgeMeasurement ledge) => new GroundSense(true, true, ledge);
        private static GroundSense Air(LedgeMeasurement ledge) => new GroundSense(false, true, ledge);
        private static readonly GroundSense NoLedgeGround = new GroundSense(true);
        private static readonly GroundSense NoLedgeAir = new GroundSense(false);

        private static void TickMany(LocomotionMachine machine, int count, PlayerIntent intent, GroundSense ground)
        {
            for (var i = 0; i < count; i++) machine.Tick(intent, ground, Dt);
        }

        // A machine standing on the ground at the origin with the motor-fed position set.
        private static LocomotionMachine StandingAtOrigin(LocomotionSettings settings = null)
        {
            var machine = new LocomotionMachine(settings ?? Settings(), LocomotionStateId.Grounded);
            machine.Context.Position = Vector3.zero;
            return machine;
        }

        // Plays the climb like the motor would: the body ends each tick at the target.
        private static int RunClimb(LocomotionMachine machine, GroundSense ground, int maxTicks = 1000)
        {
            var ticks = 0;
            while (machine.Current == LocomotionStateId.Climb && ticks < maxTicks)
            {
                machine.Tick(Idle(), ground, Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
                ticks++;
            }
            return ticks;
        }

        // Entering from the ground

        [Test]
        public void JumpPressedTowardAClimbableLedgeStartsTheClimbInsteadOfAJump()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.AreEqual(ClimbKind.ClimbUp, machine.Context.ClimbKind);
            Assert.AreEqual(0f, machine.Context.JumpCooldown, 0.0001f, "No jump was launched");
        }

        [Test]
        public void ClimbOverIsRecordedForAThinObstacle()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), Ground(ClimbOverLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.AreEqual(ClimbKind.ClimbOver, machine.Context.ClimbKind);
        }

        [Test]
        public void ClimbUsesTheCrouchHeight()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(Settings().crouchHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void WithoutAJumpPressTheLedgeIsIgnored()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void WithoutMoveInputTheJumpIsNormal()
        {
            var machine = StandingAtOrigin();

            machine.Tick(JumpOnly(), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void MovingAwayFromTheLedgeJumpsNormally()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Backward(true), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void MovingSidewaysAlongTheWallJumpsNormally()
        {
            var machine = StandingAtOrigin();

            machine.Tick(new PlayerIntent(Vector2.right, false, true, 0f), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void TheApproachAngleIsReadRelativeToTheCamera()
        {
            var machine = StandingAtOrigin();

            // Camera turned 180 degrees: forward stick now means -Z, away from the +Z ledge.
            machine.Tick(Forward(jump: true, cameraYaw: 180f), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void TheApproachThresholdComesFromTheSettings()
        {
            var settings = Settings();
            settings.climb.approachMinDot = -1f;
            var machine = StandingAtOrigin(settings);

            machine.Tick(Backward(true), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
        }

        [Test]
        public void ALedgeThatCannotBeClimbedJumpsNormally()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), Ground(TooHighLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void NoLedgeJumpsNormally()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), NoLedgeGround, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
        }

        [Test]
        public void ABufferedJumpPressStillStartsTheClimbWhenTheLedgeAppears()
        {
            var machine = StandingAtOrigin();
            machine.Context.TimeSinceJumpPressed = 0.05f;

            machine.Tick(Forward(), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
        }

        [Test]
        public void AStaleJumpPressDoesNotStartTheClimb()
        {
            var machine = StandingAtOrigin();
            machine.Context.TimeSinceJumpPressed = Settings().jumpBufferSeconds + 0.5f;

            machine.Tick(Forward(), Ground(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void ClimbConsumesTheJumpPress()
        {
            var machine = StandingAtOrigin();

            machine.Tick(Forward(jump: true), Ground(ClimbUpLedge()), Dt);

            Assert.IsTrue(float.IsPositiveInfinity(machine.Context.TimeSinceJumpPressed));
        }

        // Airborne grab

        [Test]
        public void JumpPressedInTheAirWithinTheWindowGrabsTheLedge()
        {
            var machine = StandingAtOrigin();
            machine.Tick(JumpOnly(), NoLedgeGround, Dt);
            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
            TickMany(machine, 3, Idle(), NoLedgeAir);

            machine.Tick(Forward(jump: true), Air(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.AreEqual(ClimbKind.ClimbUp, machine.Context.ClimbKind);
        }

        [Test]
        public void JumpPressedWhileFallingWithinTheWindowGrabsTheLedge()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);
            machine.Context.Position = Vector3.zero;
            TickMany(machine, 5, Idle(), NoLedgeAir);

            machine.Tick(Forward(jump: true), Air(ClimbOverLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.AreEqual(ClimbKind.ClimbOver, machine.Context.ClimbKind);
        }

        [Test]
        public void TheAirGrabWindowCanBeConfigured()
        {
            var settings = Settings();
            settings.climb.airGrabWindowSeconds = 0.05f;
            var machine = new LocomotionMachine(settings, LocomotionStateId.Fall);
            machine.Context.Position = Vector3.zero;
            TickMany(machine, 10, Idle(), NoLedgeAir);

            machine.Tick(Forward(jump: true), Air(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        [Test]
        public void FallingLongerThanTheWindowCannotGrab()
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, LocomotionStateId.Fall);
            machine.Context.Position = Vector3.zero;
            var ticks = Mathf.CeilToInt(settings.climb.airGrabWindowSeconds / Dt) + 5;
            TickMany(machine, ticks, Idle(), NoLedgeAir);

            machine.Tick(Forward(jump: true), Air(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        [Test]
        public void AFallingPlayerWithoutAPressDoesNotGrab()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);
            machine.Context.Position = Vector3.zero;

            machine.Tick(Forward(), Air(ClimbUpLedge()), Dt);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        // Not from crouch or slide

        [Test]
        public void CrouchCannotStartAClimb()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Crouch);
            machine.Context.Position = Vector3.zero;

            machine.Tick(Forward(jump: true), Ground(ClimbUpLedge()), Dt);

            Assert.AreNotEqual(LocomotionStateId.Climb, machine.Current);
        }

        [Test]
        public void SlideCannotStartAClimb()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Slide);
            machine.Context.Position = Vector3.zero;

            machine.Tick(Forward(jump: true), Ground(ClimbUpLedge()), Dt);

            Assert.AreNotEqual(LocomotionStateId.Climb, machine.Current);
        }

        // The scripted path

        [Test]
        public void ClimbUpEndsStandingOnTopAtTheForwardPoint()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            var expected = new Vector3(0f, ledge.TopPoint.y, ledge.TopPoint.z + settings.climb.forwardOntoTop);
            Assert.AreEqual(expected.x, machine.Context.Position.x, 0.001f);
            Assert.AreEqual(expected.y, machine.Context.Position.y, 0.001f);
            Assert.AreEqual(expected.z, machine.Context.Position.z, 0.001f);
            Assert.AreEqual(settings.standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void TheForwardDistanceNeverPassesHalfTheTopDepth()
        {
            var settings = Settings();
            settings.climb.forwardOntoTop = 5f;
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            Assert.AreEqual(ledge.TopPoint.z + ledge.TopDepth * 0.5f, machine.Context.Position.z, 0.001f);
        }

        [Test]
        public void ClimbOverEndsInAFallWithAForwardPush()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbOverLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
            Assert.AreEqual(settings.climb.overPushSpeed, machine.Context.Velocity.z, 0.001f);
            Assert.AreEqual(0f, machine.Context.Velocity.x, 0.001f);
            Assert.AreEqual(0f, machine.Context.Velocity.y, 0.001f);
            Assert.AreEqual(settings.standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void ClimbUpLeavesNoPlanarVelocity()
        {
            var machine = StandingAtOrigin();
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            Assert.AreEqual(0f, new Vector2(machine.Context.Velocity.x, machine.Context.Velocity.z).magnitude, 0.001f);
        }

        [Test]
        public void TheVelocityCarriesTheBodyToTheTargetInOneTick()
        {
            var machine = StandingAtOrigin();
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            machine.Context.Position = Vector3.zero;

            machine.Tick(Idle(), Ground(ledge), Dt);

            var step = machine.Context.Velocity * Dt;
            Assert.AreEqual(machine.Context.ClimbTarget.x, machine.Context.Position.x + step.x, 0.001f);
            Assert.AreEqual(machine.Context.ClimbTarget.y, machine.Context.Position.y + step.y, 0.001f);
            Assert.AreEqual(machine.Context.ClimbTarget.z, machine.Context.Position.z + step.z, 0.001f);
        }

        [Test]
        public void TheBodyStopsAtTheWallFaceBetweenThePhases()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            var reachTicks = Mathf.RoundToInt(settings.climb.reachSeconds / Dt);
            for (var i = 0; i < reachTicks; i++)
            {
                machine.Tick(Idle(), Ground(ledge), Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
            }

            Assert.AreEqual(ledge.TopPoint.z - settings.climb.wallStandoff, machine.Context.Position.z, 0.01f);
            Assert.Less(machine.Context.Position.y, ledge.TopPoint.y, "Still below the top while hanging at the wall");
            Assert.Greater(machine.Context.Position.y, 0f);
        }

        [Test]
        public void ProgressRunsFromZeroToOneAndElapsedTracksTheTime()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            Assert.AreEqual(0f, machine.Context.ClimbProgress, 0.0001f);
            Assert.AreEqual(0f, machine.Context.ClimbElapsed, 0.0001f);

            var previous = 0f;
            while (machine.Current == LocomotionStateId.Climb)
            {
                machine.Tick(Idle(), Ground(ledge), Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
                Assert.GreaterOrEqual(machine.Context.ClimbProgress, previous);
                previous = machine.Context.ClimbProgress;
            }

            Assert.AreEqual(1f, machine.Context.ClimbProgress, 0.0001f);
            Assert.AreEqual(settings.climb.reachSeconds + settings.climb.pullUpSeconds, machine.Context.ClimbElapsed, 0.0001f);
            Assert.AreEqual(ClimbKind.ClimbUp, machine.Context.ClimbKind, "Kept after the climb for the animator");
        }

        [Test]
        public void TheDurationComesFromTheSettings()
        {
            var settings = Settings();
            settings.climb.reachSeconds = 0.1f;
            settings.climb.pullUpSeconds = 0.2f;
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            var ticks = RunClimb(machine, Ground(ledge));

            // 0.3 s of path plus the tick that hands over to the next state.
            Assert.AreEqual(Mathf.RoundToInt(0.3f / Dt) + 1, ticks);
        }

        [Test]
        public void InputIsIgnoredDuringTheClimb()
        {
            var machine = StandingAtOrigin();
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            machine.Tick(new PlayerIntent(Vector2.left, true, true, 90f, true), Air(ledge), Dt);
            machine.Context.Position = machine.Context.ClimbTarget;
            machine.Tick(new PlayerIntent(Vector2.left, true, true, 90f, true), Ground(ledge), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
        }

        [Test]
        public void TheBodyTurnsTowardTheApproachDirection()
        {
            var machine = StandingAtOrigin();
            machine.Context.FacingYaw = 90f;
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            for (var i = 0; i < 30; i++)
            {
                machine.Tick(Idle(), Ground(ledge), Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
            }

            Assert.AreEqual(0f, Mathf.DeltaAngle(machine.Context.FacingYaw, 0f), 1f);
        }

        [Test]
        public void ClimbingAfterLandingDoesNotRetriggerFromAStalePress()
        {
            var machine = StandingAtOrigin();
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            RunClimb(machine, Ground(ledge));
            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);

            machine.Tick(Forward(), NoLedgeGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void StateChangesAreReported()
        {
            var machine = StandingAtOrigin();
            var changes = new System.Collections.Generic.List<LocomotionStateId>();
            machine.StateChanged += (from, to) => changes.Add(to);
            var ledge = ClimbOverLedge();

            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            RunClimb(machine, Ground(ledge));

            CollectionAssert.AreEqual(new[] { LocomotionStateId.Climb, LocomotionStateId.Fall }, changes);
        }

        // Climbing over: the path carries the body past the far edge before the fall

        private const float ControllerRadius = 0.28f;

        [Test]
        public void ClimbOverEndsPastTheFarEdgeByAtLeastTheBodyRadiusAtTopHeight()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbOverLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            var farEdge = ledge.TopPoint.z + ledge.TopDepth;
            Assert.GreaterOrEqual(machine.Context.Position.z, farEdge + ControllerRadius);
            Assert.AreEqual(farEdge + settings.climb.overClearDistance, machine.Context.Position.z, 0.001f);
            Assert.AreEqual(ledge.TopPoint.y, machine.Context.Position.y, 0.001f);
        }

        [Test]
        public void TheOverPhaseNeverDipsBelowTheTop()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbOverLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            var pullUpEnd = settings.climb.reachSeconds + settings.climb.pullUpSeconds;

            var lowest = float.MaxValue;
            while (machine.Current == LocomotionStateId.Climb)
            {
                machine.Tick(Idle(), Ground(ledge), Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
                if (machine.Context.ClimbElapsed >= pullUpEnd - 0.0001f) lowest = Mathf.Min(lowest, machine.Context.Position.y);
            }

            Assert.GreaterOrEqual(lowest, ledge.TopPoint.y - 0.001f);
        }

        [Test]
        public void ClimbOverTakesTheOverTimeLongerAndClimbUpDoesNot()
        {
            var settings = Settings();
            settings.climb.reachSeconds = 0.1f;
            settings.climb.pullUpSeconds = 0.2f;
            settings.climb.overSeconds = 0.4f;

            var over = StandingAtOrigin(settings);
            var overLedge = ClimbOverLedge();
            over.Tick(Forward(jump: true), Ground(overLedge), Dt);
            var overTicks = RunClimb(over, Ground(overLedge));

            var up = StandingAtOrigin(settings);
            var upLedge = ClimbUpLedge();
            up.Tick(Forward(jump: true), Ground(upLedge), Dt);
            var upTicks = RunClimb(up, Ground(upLedge));

            Assert.AreEqual(Mathf.RoundToInt(0.7f / Dt) + 1, overTicks);
            Assert.AreEqual(Mathf.RoundToInt(0.3f / Dt) + 1, upTicks);
        }

        [Test]
        public void ClimbUpIsUnchangedByTheOverSettings()
        {
            var settings = Settings();
            settings.climb.overClearDistance = 3f;
            settings.climb.overSeconds = 2f;
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            RunClimb(machine, Ground(ledge));

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(ledge.TopPoint.z + settings.climb.forwardOntoTop, machine.Context.Position.z, 0.001f);
        }

        // The motor only probes when ClimbEntry could accept the press: the probe gate must never skip a
        // situation in which the machine actually starts a climb, and must skip the clear negatives.

        private static bool ClimbsWithProbeGate(LocomotionStateId initial, bool grounded, float sinceJump, float sinceGrounded,
            bool pressed, PlayerIntent baseIntent, out bool probed)
        {
            var settings = Settings();
            var machine = new LocomotionMachine(settings, initial);
            machine.Context.Position = Vector3.zero;
            machine.Context.TimeSinceJumpPressed = sinceJump;
            machine.Context.TimeSinceGrounded = sinceGrounded;
            var intent = new PlayerIntent(baseIntent.Move, false, pressed, 0f);
            probed = ClimbEntry.ShouldProbe(machine.Context, intent, settings, Dt, grounded);
            machine.Tick(intent, new GroundSense(grounded, true, ClimbUpLedge()), Dt);
            return machine.Current == LocomotionStateId.Climb;
        }

        [Test]
        public void ProbeGateAcceptsEverySituationTheClimbEntryAccepts()
        {
            var states = new[] { LocomotionStateId.Grounded, LocomotionStateId.Jump, LocomotionStateId.Fall };
            var sinceJumps = new[] { 0f, 0.05f, 0.1f, 0.13f, 0.14f, 0.15f, 0.2f, 1f, float.PositiveInfinity };
            var sinceGroundeds = new[] { 0f, 0.1f, 0.4f, 0.48f, 0.49f, 0.5f, 0.52f, 1f };
            var climbs = 0;
            foreach (var state in states)
            foreach (var grounded in new[] { true, false })
            foreach (var pressed in new[] { true, false })
            foreach (var sinceJump in sinceJumps)
            foreach (var sinceGrounded in sinceGroundeds)
            {
                var climbed = ClimbsWithProbeGate(state, grounded, sinceJump, sinceGrounded, pressed, Forward(), out var probed);
                if (!climbed) continue;
                climbs++;
                Assert.IsTrue(probed, $"{state} grounded={grounded} pressed={pressed} sinceJump={sinceJump} sinceGrounded={sinceGrounded}");
            }
            Assert.Greater(climbs, 0, "the grid must contain accepted situations");
        }

        [Test]
        public void ProbeGateSkipsTheClearNegatives()
        {
            var settings = Settings();
            var ctx = new LocomotionContext { TimeSinceJumpPressed = 0f, TimeSinceGrounded = 0f };
            var move = new PlayerIntent(Vector2.up, false, true, 0f);
            Assert.IsTrue(ClimbEntry.ShouldProbe(ctx, move, settings, Dt, true), "control");
            Assert.IsFalse(ClimbEntry.ShouldProbe(ctx, new PlayerIntent(Vector2.zero, false, true, 0f), settings, Dt, true), "no move input");

            var stale = new LocomotionContext { TimeSinceJumpPressed = 5f, TimeSinceGrounded = 0f };
            Assert.IsFalse(ClimbEntry.ShouldProbe(stale, new PlayerIntent(Vector2.up, false, false, 0f), settings, Dt, true), "no press, buffer expired");

            var late = new LocomotionContext { TimeSinceJumpPressed = 0f, TimeSinceGrounded = 5f };
            Assert.IsFalse(ClimbEntry.ShouldProbe(late, move, settings, Dt, false), "airborne past the grab window");
        }

        // Climb over: the animator hops into the in-air pose when the over phase starts

        // Ticks until the over phase flag turns on (or the climb ends); returns the elapsed time then.
        private static float TickUntilOverPhase(LocomotionMachine machine, GroundSense ground)
        {
            for (var i = 0; i < 1000 && machine.Current == LocomotionStateId.Climb; i++)
            {
                machine.Tick(Idle(), ground, Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
                if (machine.Context.ClimbOverPhase) return machine.Context.ClimbElapsed;
            }
            return -1f;
        }

        [Test]
        public void ClimbOverPhaseStartsOnlyAfterThePullUpEnds()
        {
            var settings = Settings();
            var machine = StandingAtOrigin(settings);
            var ledge = ClimbOverLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);
            Assert.IsFalse(machine.Context.ClimbOverPhase, "not on enter");

            var elapsed = TickUntilOverPhase(machine, Ground(ledge));

            var pullEnd = settings.climb.reachSeconds + settings.climb.pullUpSeconds;
            Assert.Greater(elapsed, pullEnd);
            Assert.LessOrEqual(elapsed, pullEnd + Dt + 0.0001f);
        }

        [Test]
        public void ClimbUpNeverRaisesTheOverPhase()
        {
            var machine = StandingAtOrigin();
            var ledge = ClimbUpLedge();
            machine.Tick(Forward(jump: true), Ground(ledge), Dt);

            var sawOver = false;
            while (machine.Current == LocomotionStateId.Climb)
            {
                machine.Tick(Idle(), Ground(ledge), Dt);
                machine.Context.Position = machine.Context.ClimbTarget;
                sawOver |= machine.Context.ClimbOverPhase;
            }

            Assert.IsFalse(sawOver);
        }

        [Test]
        public void TheOverPhaseFlagResetsWhenAnotherClimbStarts()
        {
            var machine = StandingAtOrigin();
            var over = ClimbOverLedge();
            machine.Tick(Forward(jump: true), Ground(over), Dt);
            RunClimb(machine, Ground(over));
            Assert.IsTrue(machine.Context.ClimbOverPhase, "kept after the over climb ends");

            var up = ClimbUpLedge();
            machine.Context.Position = Vector3.zero;
            machine.Tick(Forward(jump: true), Air(up), Dt);

            Assert.AreEqual(LocomotionStateId.Climb, machine.Current);
            Assert.IsFalse(machine.Context.ClimbOverPhase);
        }

        [Test]
        public void ClimbShowsTheClimbClipUntilTheOverPhaseThenTheInAirPose()
        {
            Assert.AreEqual(AnimatorStateSelector.Climb, AnimatorStateSelector.ForClimb(false));
            Assert.AreEqual(AnimatorStateSelector.InAir, AnimatorStateSelector.ForClimb(true));
        }

        [Test]
        public void TheFallAfterAnOverClimbDoesNotRestartTheInAirFade()
        {
            Assert.IsTrue(AnimatorStateSelector.FadeAlreadyShown(LocomotionStateId.Climb, LocomotionStateId.Fall, true));
            Assert.IsFalse(AnimatorStateSelector.FadeAlreadyShown(LocomotionStateId.Climb, LocomotionStateId.Fall, false));
            Assert.IsFalse(AnimatorStateSelector.FadeAlreadyShown(LocomotionStateId.Climb, LocomotionStateId.Grounded, true));
            Assert.IsFalse(AnimatorStateSelector.FadeAlreadyShown(LocomotionStateId.Jump, LocomotionStateId.Fall, true));
        }
    }
}

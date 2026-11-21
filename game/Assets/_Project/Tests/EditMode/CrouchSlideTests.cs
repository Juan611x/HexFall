using HexFall.Player.Actions;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class CrouchSlideTests
    {
        private const float Dt = 0.02f;

        private static LocomotionSettings Settings() => new LocomotionSettings();

        private static PlayerIntent Idle() => new PlayerIntent(Vector2.zero, false, false, 0f);

        private static PlayerIntent Move(Vector2 move, bool sprint = false, float cameraYaw = 0f) =>
            new PlayerIntent(move, sprint, false, cameraYaw);

        private static PlayerIntent PressCrouch(Vector2 move = default, bool sprint = false, float cameraYaw = 0f) =>
            new PlayerIntent(move, sprint, false, cameraYaw, true);

        private static PlayerIntent Jump() => new PlayerIntent(Vector2.zero, false, true, 0f);

        private static readonly GroundSense OnGround = new GroundSense(true);
        private static readonly GroundSense InAir = new GroundSense(false);
        private static readonly GroundSense Blocked = new GroundSense(true, false);

        private static void TickMany(LocomotionMachine machine, int count, PlayerIntent intent, GroundSense ground)
        {
            for (var i = 0; i < count; i++) machine.Tick(intent, ground, Dt);
        }

        private static float Planar(LocomotionMachine machine) =>
            new Vector2(machine.Context.Velocity.x, machine.Context.Velocity.z).magnitude;

        // A machine already sprinting forward on the ground.
        private static LocomotionMachine Sprinting(LocomotionSettings settings = null)
        {
            var machine = new LocomotionMachine(settings ?? Settings(), LocomotionStateId.Grounded);
            TickMany(machine, 100, Move(Vector2.up, true), OnGround);
            return machine;
        }

        private static LocomotionMachine Crouching(LocomotionSettings settings = null) =>
            new LocomotionMachine(settings ?? Settings(), LocomotionStateId.Crouch);

        // Entering crouch

        [Test]
        public void CrouchPressedOnTheGroundStartsCrouching()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            machine.Tick(PressCrouch(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
            Assert.AreEqual(Settings().crouchHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void CrouchPressedWhileSprintingTooSlowCrouchesInstead()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            TickMany(machine, 3, Move(Vector2.up, true), OnGround);

            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void CrouchPressedWhileSprintingFastStartsASlide()
        {
            var machine = Sprinting();

            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);
            Assert.AreEqual(Settings().crouchHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void CrouchPressedWithoutSprintHeldDoesNotSlideEvenWhenFast()
        {
            var machine = Sprinting();

            machine.Tick(PressCrouch(Vector2.up, false), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void CrouchPressedInTheAirDoesNothing()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Fall);

            machine.Tick(PressCrouch(), InAir, Dt);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
        }

        [Test]
        public void GroundedBodyIsStandHeight()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);

            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        // Crouch

        [Test]
        public void CrouchWalkReachesCrouchSpeed()
        {
            var machine = Crouching();

            TickMany(machine, 60, Move(Vector2.up), OnGround);

            Assert.AreEqual(1.6f, Planar(machine), 0.001f);
            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void CrouchPartialStickScalesTheSpeed()
        {
            var machine = Crouching();

            TickMany(machine, 60, Move(new Vector2(0f, 0.3f)), OnGround);

            Assert.AreEqual(1.6f * 0.3f / 0.6f, Planar(machine), 0.001f);
        }

        [Test]
        public void CrouchHoldsTheGroundStickSpeed()
        {
            var machine = Crouching();

            machine.Tick(Idle(), OnGround, Dt);

            Assert.AreEqual(Settings().groundedStickSpeed, machine.Context.Velocity.y, 0.0001f);
        }

        [Test]
        public void CrouchPressedAgainStandsUp()
        {
            var machine = Crouching();

            machine.Tick(PressCrouch(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void CrouchPressedUnderACeilingStaysCrouched()
        {
            var machine = Crouching();

            machine.Tick(PressCrouch(), Blocked, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void SprintWithMoveInputLeavesCrouchToGrounded()
        {
            var machine = Crouching();
            machine.Tick(Idle(), OnGround, Dt);

            machine.Tick(Move(Vector2.up, true), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void SprintWithoutMoveInputKeepsCrouching()
        {
            var machine = Crouching();
            machine.Tick(Idle(), OnGround, Dt);

            machine.Tick(new PlayerIntent(Vector2.zero, true, false, 0f), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void SprintUnderACeilingKeepsCrouching()
        {
            var machine = Crouching();
            machine.Tick(Idle(), Blocked, Dt);

            machine.Tick(Move(Vector2.up, true), Blocked, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void SprintHeldSinceEnteringCrouchDoesNotStandUntilReleased()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            TickMany(machine, 3, Move(Vector2.up, true), OnGround);
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            TickMany(machine, 5, Move(Vector2.up, true), OnGround);
            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);

            machine.Tick(Move(Vector2.up), OnGround, Dt);
            machine.Tick(Move(Vector2.up, true), OnGround, Dt);
            Assert.AreEqual(LocomotionStateId.Grounded, machine.Current);
        }

        [Test]
        public void JumpFromCrouchJumps()
        {
            var machine = Crouching();
            machine.Tick(Idle(), OnGround, Dt);

            machine.Tick(Jump(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void JumpUnderACeilingIsBlocked()
        {
            var machine = Crouching();
            machine.Tick(Idle(), Blocked, Dt);

            machine.Tick(Jump(), Blocked, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void LeavingTheGroundWhileCrouchedFallsAtStandHeight()
        {
            var machine = Crouching();

            TickMany(machine, 10, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void CrouchToleratesTheCoyoteWindowBeforeFalling()
        {
            var machine = Crouching();

            machine.Tick(Idle(), InAir, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        // Slide

        [Test]
        public void SlideEntryAddsTheBoostCappedAtSprintPlusMargin()
        {
            var settings = Settings();
            var machine = Sprinting(settings);
            var before = Planar(machine);

            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);
            var entry = Planar(machine);

            Assert.Greater(entry, before);
            Assert.LessOrEqual(entry, settings.sprintSpeed + settings.slideBoostCapMargin + 0.001f);
        }

        [Test]
        public void SlideFromSprintEntersAtEightMetresPerSecond()
        {
            var machine = Sprinting();

            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            Assert.AreEqual(6f, Settings().sprintSpeed, 0.0001f);
            Assert.AreEqual(8f, Planar(machine), 0.05f);
        }

        [Test]
        public void SlideBoostIsCapped()
        {
            var settings = Settings();
            settings.slideBoost = 20f;
            var machine = Sprinting(settings);

            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            Assert.AreEqual(settings.sprintSpeed + settings.slideBoostCapMargin, Planar(machine), 0.3f);
        }

        [Test]
        public void SlideKeepsTheVelocityDirection()
        {
            var machine = new LocomotionMachine(Settings(), LocomotionStateId.Grounded);
            TickMany(machine, 100, Move(Vector2.up, true, 90f), OnGround);

            machine.Tick(PressCrouch(Vector2.up, true, 90f), OnGround, Dt);
            machine.Tick(Idle(), OnGround, Dt);

            Assert.Greater(machine.Context.Velocity.x, 1f);
            Assert.AreEqual(0f, machine.Context.Velocity.z, 0.01f);
        }

        [Test]
        public void SlideDeceleratesWithFriction()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);
            var start = Planar(machine);

            TickMany(machine, 10, Idle(), OnGround);

            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);
            Assert.Less(Planar(machine), start);
        }

        [Test]
        public void SlideLosesLessSpeedEarlyThanLate()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            var start = Planar(machine);
            TickMany(machine, 10, Idle(), OnGround);
            var afterEarly = Planar(machine);
            TickMany(machine, 40, Idle(), OnGround);
            var beforeLate = Planar(machine);
            TickMany(machine, 10, Idle(), OnGround);
            var afterLate = Planar(machine);

            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);
            Assert.Less(start - afterEarly, beforeLate - afterLate);
        }

        [Test]
        public void SlideFrictionEasesFromTheStartToTheEndValue()
        {
            var settings = Settings();

            Assert.AreEqual(2f, settings.slideFrictionStart, 0.0001f);
            Assert.AreEqual(7f, settings.slideFrictionEnd, 0.0001f);
            Assert.Less(settings.slideFrictionStart, settings.slideFrictionEnd);
        }

        [Test]
        public void SlideFromSprintLastsBetweenOnePointThreeAndOnePointSixSeconds()
        {
            const float frame = 1f / 60f;
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, frame);
            var elapsed = 0f;

            while (machine.Current == LocomotionStateId.Slide && elapsed < 5f)
            {
                machine.Tick(Idle(), OnGround, frame);
                elapsed += frame;
            }

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
            Assert.GreaterOrEqual(elapsed, 1.3f);
            Assert.LessOrEqual(elapsed, 1.6f + frame);
        }

        [Test]
        public void ThePlayerPrefabCarriesTheSlideTuning()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            Assert.IsNotNull(prefab);
            var settings = new UnityEditor.SerializedObject(prefab.GetComponent<PlayerMotor>()).FindProperty("settings");

            Assert.AreEqual(2f, settings.FindPropertyRelative("slideBoost").floatValue, 0.0001f);
            Assert.AreEqual(2.5f, settings.FindPropertyRelative("slideBoostCapMargin").floatValue, 0.0001f);
            Assert.AreEqual(2f, settings.FindPropertyRelative("slideFrictionStart").floatValue, 0.0001f);
            Assert.AreEqual(7f, settings.FindPropertyRelative("slideFrictionEnd").floatValue, 0.0001f);
            Assert.AreEqual(1.6f, settings.FindPropertyRelative("slideMaxSeconds").floatValue, 0.0001f);
            Assert.AreEqual(1.8f, settings.FindPropertyRelative("slideEndSpeed").floatValue, 0.0001f);
        }

        [Test]
        public void SlideEndsInCrouchWhenSlowEnough()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            TickMany(machine, 90, Idle(), OnGround);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
            Assert.AreEqual(Settings().crouchHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void SlideEndsInCrouchAfterTheMaxDuration()
        {
            var settings = Settings();
            settings.slideFrictionStart = 0f;
            settings.slideFrictionEnd = 0f;
            var machine = Sprinting(settings);
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            TickMany(machine, 70, Idle(), OnGround);
            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);

            TickMany(machine, 20, Idle(), OnGround);
            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void SlideSteeringIsLimited()
        {
            var settings = Settings();
            settings.slideFrictionStart = 0f;
            settings.slideFrictionEnd = 0f;
            var machine = Sprinting(settings);
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);
            TickMany(machine, 10, Move(Vector2.right), OnGround);

            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);
            // Normal turning would have swung most of the way to +x by now; the slide barely turns.
            Assert.Less(machine.Context.Velocity.x, machine.Context.Velocity.z);
            Assert.Greater(machine.Context.Velocity.x, 0f);
        }

        [Test]
        public void SlideFacingFollowsTheVelocityNotTheInput()
        {
            var settings = Settings();
            settings.slideFrictionStart = 0f;
            settings.slideFrictionEnd = 0f;
            var machine = Sprinting(settings);
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            TickMany(machine, 20, Move(Vector2.right), OnGround);

            var velocityYaw = Mathf.Atan2(machine.Context.Velocity.x, machine.Context.Velocity.z) * Mathf.Rad2Deg;
            Assert.AreEqual(velocityYaw, machine.Context.FacingYaw, 10f);
        }

        [Test]
        public void CrouchPressedDuringASlideCancelsToCrouch()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            machine.Tick(PressCrouch(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Crouch, machine.Current);
        }

        [Test]
        public void JumpDuringASlideKeepsTheMomentum()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);
            machine.Tick(Idle(), OnGround, Dt);
            var before = Planar(machine);

            machine.Tick(Jump(), OnGround, Dt);

            Assert.AreEqual(LocomotionStateId.Jump, machine.Current);
            Assert.Greater(Planar(machine), before - 0.5f);
            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        [Test]
        public void JumpDuringASlideUnderACeilingIsBlocked()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            machine.Tick(Jump(), Blocked, Dt);

            Assert.AreEqual(LocomotionStateId.Slide, machine.Current);
        }

        [Test]
        public void SlideOffALedgeFalls()
        {
            var machine = Sprinting();
            machine.Tick(PressCrouch(Vector2.up, true), OnGround, Dt);

            TickMany(machine, 10, Idle(), InAir);

            Assert.AreEqual(LocomotionStateId.Fall, machine.Current);
            Assert.AreEqual(Settings().standHeight, machine.Context.BodyHeight, 0.0001f);
        }

        // Animator keys and IK

        [Test]
        public void CrouchAndSlideMapToTheirAnimatorKeys()
        {
            Assert.AreEqual(AnimatorStateSelector.Crouch,
                AnimatorStateSelector.For(LocomotionStateId.Crouch, LocomotionStateId.Grounded, 0f, 0f, 5f));
            Assert.AreEqual(AnimatorStateSelector.Slide,
                AnimatorStateSelector.For(LocomotionStateId.Slide, LocomotionStateId.Grounded, 0f, 6f, 5f));
            Assert.AreEqual("Crouch", AnimatorStateSelector.Crouch);
            Assert.AreEqual("Slide", AnimatorStateSelector.Slide);
        }

        [Test]
        public void CrouchPlantsTheFeetAndSlideReleasesThem()
        {
            Assert.AreEqual(1f, IkProfiles.For(LocomotionStateId.Crouch, ActionStateId.Ready).FeetWeight, 0.0001f);
            Assert.AreEqual(0f, IkProfiles.For(LocomotionStateId.Slide, ActionStateId.Ready).FeetWeight, 0.0001f);
        }

        [Test]
        public void CrouchAndSlideAimLikeGrounded()
        {
            foreach (var state in new[] { LocomotionStateId.Crouch, LocomotionStateId.Slide })
            {
                var idle = IkProfiles.For(state, ActionStateId.Ready);
                var acting = IkProfiles.For(state, ActionStateId.Attack);
                Assert.AreEqual(IkProfiles.IdleHeadAim, idle.HeadAimWeight, 0.0001f);
                Assert.AreEqual(IkProfiles.ActingChestAim, acting.ChestAimWeight, 0.0001f);
            }
        }
    }
}

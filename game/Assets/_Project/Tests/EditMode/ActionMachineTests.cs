using System.Collections.Generic;
using HexFall.Player;
using HexFall.Player.Actions;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ActionMachineTests
    {
        private static readonly ActionIntent None = default;
        private static readonly ActionIntent Attack = new ActionIntent(true, false);
        private static readonly ActionIntent Parry = new ActionIntent(false, true);

        private static ActionMachine Create() => new ActionMachine(new ActionSettings());

        [Test]
        public void StartsReadyAndNotActing()
        {
            var machine = Create();
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            Assert.IsFalse(machine.IsActing);
            Assert.IsFalse(machine.FacesAim);
        }

        [Test]
        public void IdleTicksStayReady()
        {
            var machine = Create();
            machine.Tick(None, 1f);
            machine.Tick(None, 2f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void AttackPressStartsAttackAndFacesAim()
        {
            var machine = Create();
            machine.Tick(Attack, 1f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            Assert.IsTrue(machine.IsActing);
            Assert.IsTrue(machine.FacesAim);
        }

        [Test]
        public void StateChangedReportsFromAndTo()
        {
            var machine = Create();
            var changes = new List<(ActionStateId, ActionStateId)>();
            machine.StateChanged += (from, to) => changes.Add((from, to));
            machine.Tick(Attack, 1f);
            machine.Tick(None, 1.51f);
            CollectionAssert.AreEqual(
                new[] { (ActionStateId.Ready, ActionStateId.Attack), (ActionStateId.Attack, ActionStateId.Ready) },
                changes);
        }

        [Test]
        public void BoltFiresOnceAtTheBoltDelay()
        {
            var machine = Create();
            var fired = 0;
            machine.BoltFired += () => fired++;
            machine.Tick(Attack, 1f);
            machine.Tick(None, 1.19f);
            Assert.AreEqual(0, fired);
            machine.Tick(None, 1.2f);
            Assert.AreEqual(1, fired);
            machine.Tick(None, 1.3f);
            machine.Tick(None, 1.6f);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void BoltStillFiresWhenASlowTickSkipsPastTheDelay()
        {
            var machine = Create();
            var fired = 0;
            machine.BoltFired += () => fired++;
            machine.Tick(Attack, 1f);
            machine.Tick(None, 2f);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void AttackLastsBoltDelayPlusFaceAimHold()
        {
            var machine = Create();
            machine.Tick(Attack, 1f);
            machine.Tick(None, 1.49f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            machine.Tick(None, 1.51f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            Assert.IsFalse(machine.FacesAim);
        }

        [Test]
        public void AttackPressIsRefusedWhileTheCooldownRuns()
        {
            var settings = new ActionSettings { attackCooldownSeconds = 1f };
            var machine = new ActionMachine(settings);
            machine.Tick(Attack, 1f);
            machine.Tick(None, 1.51f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            machine.Tick(Attack, 1.9f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            machine.Tick(Attack, 2f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
        }

        [Test]
        public void AttackPressDuringAttackDoesNotRestartIt()
        {
            var machine = Create();
            var fired = 0;
            machine.BoltFired += () => fired++;
            machine.Tick(Attack, 1f);
            machine.Tick(Attack, 1.3f);
            machine.Tick(None, 1.51f);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ParryPressRaisesTheShieldAndEntersParry()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            Assert.IsTrue(machine.IsActing);
            Assert.IsFalse(machine.FacesAim);
            Assert.AreEqual(2, machine.ShieldCharges(1f));
        }

        [Test]
        public void ParryLastsWhileTheShieldGuards()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            machine.Tick(None, 1.39f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            machine.Tick(None, 1.41f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ParryPressWithoutChargesOpensNoShield()
        {
            var settings = new ActionSettings { shieldCharges = 1, guardSeconds = 0.4f, chargeRechargeSeconds = 100f };
            var machine = new ActionMachine(settings);
            machine.Tick(Parry, 1f);
            machine.Tick(None, 2f);
            machine.Tick(Parry, 3f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
            Assert.AreEqual(ParryGrade.None, machine.ResolveHit(10, 3.1f).Grade);
        }

        [Test]
        public void AttackPressDuringParryIsRefusedAndKeepsTheCooldownFree()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            machine.Tick(Attack, 1.2f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            machine.Tick(Attack, 1.41f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
        }

        // Behaviour choice: a parry is defensive, so it interrupts an attack. The bolt that had not
        // fired yet is cancelled.
        [Test]
        public void ParryPressDuringAttackInterruptsAndCancelsTheUnfiredBolt()
        {
            var machine = Create();
            var fired = 0;
            machine.BoltFired += () => fired++;
            machine.Tick(Attack, 1f);
            machine.Tick(Parry, 1.1f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            machine.Tick(None, 1.2f);
            machine.Tick(None, 1.3f);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void ParryPressDuringAttackAfterTheBoltKeepsTheFiredBolt()
        {
            var machine = Create();
            var fired = 0;
            machine.BoltFired += () => fired++;
            machine.Tick(Attack, 1f);
            machine.Tick(None, 1.2f);
            machine.Tick(Parry, 1.3f);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
        }

        [Test]
        public void ParryAndAttackOnTheSameTickParryWins()
        {
            var machine = Create();
            machine.Tick(new ActionIntent(true, true), 1f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
        }

        [Test]
        public void ParryPressOnTheTickTheGuardEndsStartsAnotherParryIfChargesRemain()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            machine.Tick(Parry, 1.41f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            Assert.AreEqual(1, machine.ShieldCharges(1.41f));
        }

        [Test]
        public void PressEventsFireOnEveryPressSeen()
        {
            var machine = Create();
            int attacks = 0, parries = 0;
            machine.AttackPressed += () => attacks++;
            machine.ParryPressed += () => parries++;
            machine.Tick(None, 1f);
            Assert.AreEqual(0, attacks + parries);
            machine.Tick(Attack, 2f);
            Assert.AreEqual(1, attacks);
            Assert.AreEqual(0, parries);
            machine.Tick(Parry, 3f);
            Assert.AreEqual(1, attacks);
            Assert.AreEqual(1, parries);
        }

        [Test]
        public void PressEventsFireTogetherWhenBothArePressed()
        {
            var machine = Create();
            int attacks = 0, parries = 0;
            machine.AttackPressed += () => attacks++;
            machine.ParryPressed += () => parries++;
            machine.Tick(new ActionIntent(true, true), 1f);
            Assert.AreEqual(1, attacks);
            Assert.AreEqual(1, parries);
        }

        [Test]
        public void AttackPressEventFiresWhenRefusedByTheCooldownOrTheGuard()
        {
            var machine = Create();
            int attacks = 0;
            machine.AttackPressed += () => attacks++;
            machine.Tick(Attack, 1f);
            machine.Tick(Attack, 1.1f);
            Assert.AreEqual(2, attacks);
            machine.Tick(None, 3f);
            machine.Tick(Parry, 4f);
            machine.Tick(Attack, 4.1f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            Assert.AreEqual(3, attacks);
        }

        [Test]
        public void ParryPressEventFiresWhenRefusedByNoCharges()
        {
            var machine = Create();
            int parries = 0;
            machine.ParryPressed += () => parries++;
            machine.Tick(Parry, 1f);
            machine.Tick(Parry, 1.5f);
            machine.Tick(Parry, 2f);
            machine.Tick(Parry, 2.5f);
            Assert.AreEqual(0, machine.ShieldCharges(2.5f));
            machine.Tick(Parry, 3f);
            machine.Tick(Parry, 3.5f);
            Assert.AreEqual(6, parries);
        }

        [Test]
        public void ResolveHitGradesAgainstTheGuardAndRaisesTheEvent()
        {
            var machine = Create();
            var results = new List<HitResult>();
            machine.HitResolved += results.Add;
            machine.Tick(Parry, 1f);
            var perfect = machine.ResolveHit(20, 1.1f);
            var partial = machine.ResolveHit(20, 1.3f);
            var none = machine.ResolveHit(20, 2f);
            Assert.AreEqual(ParryGrade.Perfect, perfect.Grade);
            Assert.AreEqual(0, perfect.Taken);
            Assert.AreEqual(ParryGrade.Partial, partial.Grade);
            Assert.AreEqual(10, partial.Taken);
            Assert.AreEqual(ParryGrade.None, none.Grade);
            Assert.AreEqual(20, none.Taken);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(ParryGrade.Perfect, results[0].Grade);
        }

        [Test]
        public void RefillRestoresTheCharges()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            Assert.IsTrue(machine.RefillShield(1.1f));
            Assert.AreEqual(machine.MaxShieldCharges, machine.ShieldCharges(1.1f));
            Assert.IsFalse(machine.RefillShield(1.2f));
        }

        private static ActionMachine CreateOutOfCharges()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            machine.Tick(Parry, 2f);
            machine.Tick(Parry, 3f);
            machine.Tick(None, 3.5f);
            return machine;
        }

        [Test]
        public void ParryWithoutChargesEntersParryEmptyAndSpendsNothing()
        {
            var machine = CreateOutOfCharges();
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            machine.Tick(Parry, 4f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
            Assert.IsTrue(machine.IsActing);
            Assert.AreEqual(0, machine.ShieldCharges(4f));
        }

        [Test]
        public void ParryEmptyDuringAnAttackCancelsTheUnfiredBolt()
        {
            var settings = new ActionSettings();
            var machine = CreateOutOfCharges();
            var fired = 0;
            machine.BoltFired += () => fired++;

            machine.Tick(Attack, 4f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            machine.Tick(Parry, 4f + settings.boltDelaySeconds * 0.5f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);

            machine.Tick(None, 4f + settings.boltDelaySeconds + 0.05f); // past the original bolt time
            machine.Tick(None, 4f + settings.boltDelaySeconds + settings.faceAimHoldSeconds + 0.05f);
            machine.Tick(None, 4f + settings.boltDelaySeconds + settings.faceAimHoldSeconds + settings.guardSeconds + 0.1f);

            Assert.AreEqual(0, fired, "the interrupted attack's bolt never leaves");
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ParryEmptyLastsTheGuardTimeThenReturnsToReady()
        {
            var machine = CreateOutOfCharges();
            machine.Tick(Parry, 4f);
            machine.Tick(None, 4.39f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
            machine.Tick(None, 4.41f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ParryEmptyOpensNoGuard()
        {
            var machine = CreateOutOfCharges();
            machine.Tick(Parry, 4f);
            var hit = machine.ResolveHit(20, 4.1f);
            Assert.AreEqual(ParryGrade.None, hit.Grade);
            Assert.AreEqual(20, hit.Taken);
        }

        [Test]
        public void ParryEmptyDoesNotRetriggerWhileActive()
        {
            var machine = CreateOutOfCharges();
            var changes = 0;
            machine.Tick(Parry, 4f);
            machine.StateChanged += (f, t) => changes++;
            machine.Tick(Parry, 4.2f);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
        }

        [Test]
        public void AttackIsRefusedDuringParryEmpty()
        {
            var machine = CreateOutOfCharges();
            machine.Tick(Parry, 4f);
            machine.Tick(Attack, 4.2f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
        }

        [Test]
        public void ParryWithoutChargesInterruptsAnAttack()
        {
            var machine = CreateOutOfCharges();
            machine.Tick(Attack, 4f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            machine.Tick(Parry, 4.1f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
        }

        [Test]
        public void ARechargeDuringParryEmptyDoesNotUpgradeIt()
        {
            var machine = CreateOutOfCharges();
            machine.Tick(Parry, 6.8f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
            machine.Tick(Parry, 7.1f);
            Assert.AreEqual(ActionStateId.ParryEmpty, machine.Current);
            Assert.AreEqual(ParryGrade.None, machine.ResolveHit(10, 7.1f).Grade);
        }

        [Test]
        public void ParryPressWhileTheGuardRunsChangesNothing()
        {
            var machine = Create();
            machine.Tick(Parry, 1f);
            machine.Tick(Parry, 1.2f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
        }
    }
}

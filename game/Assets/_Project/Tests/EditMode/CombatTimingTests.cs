using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ParryWindowTests
    {
        private const float Active = 0.25f;
        private const float Recovery = 0.6f;

        private static ParryWindow Raised(float at)
        {
            var parry = new ParryWindow(Active, Recovery);
            Assert.IsTrue(parry.TryRaise(at));
            return parry;
        }

        [Test]
        public void HitInsideTheWindowIsParried()
        {
            Assert.AreEqual(HitOutcome.Parried, Raised(1f).Resolve(1.1f));
        }

        [Test]
        public void HitAtTheMomentOfRaisingIsParried()
        {
            Assert.AreEqual(HitOutcome.Parried, Raised(1f).Resolve(1f));
        }

        [Test]
        public void HitWhenTheWindowHasClosedDealsDamage()
        {
            Assert.AreEqual(HitOutcome.Damaged, Raised(1f).Resolve(1f + Active));
        }

        [Test]
        public void HitBeforeRaisingDealsDamage()
        {
            Assert.AreEqual(HitOutcome.Damaged, Raised(1f).Resolve(0.9f));
        }

        [Test]
        public void HitWithoutAnyParryDealsDamage()
        {
            Assert.AreEqual(HitOutcome.Damaged, new ParryWindow(Active, Recovery).Resolve(5f));
        }

        [Test]
        public void ActiveOnlyDuringTheWindow()
        {
            var parry = Raised(1f);

            Assert.IsTrue(parry.IsActive(1.2f));
            Assert.IsFalse(parry.IsActive(1.3f));
        }

        [Test]
        public void CannotRaiseAgainDuringTheWindowOrTheRecovery()
        {
            var parry = Raised(1f);

            Assert.IsFalse(parry.TryRaise(1.1f), "during the window");
            Assert.IsFalse(parry.TryRaise(1.8f), "during the recovery");
            Assert.IsTrue(parry.TryRaise(1f + Active + Recovery), "once recovered");
        }

        [Test]
        public void ARefusedRaiseDoesNotMoveTheWindow()
        {
            var parry = Raised(1f);
            parry.TryRaise(1.2f);

            Assert.AreEqual(HitOutcome.Damaged, parry.Resolve(1.4f));
        }
    }

    public class AttackCooldownTests
    {
        [Test]
        public void FirstAttackIsAllowed()
        {
            Assert.IsTrue(new AttackCooldown(0.5f).TryAttack(3f));
        }

        [Test]
        public void AttackDuringTheCooldownIsRefused()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);

            Assert.IsFalse(attack.TryAttack(3.4f));
        }

        [Test]
        public void AttackAfterTheCooldownIsAllowed()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);

            Assert.IsTrue(attack.TryAttack(3.5f));
        }

        [Test]
        public void ARefusedAttackDoesNotRestartTheCooldown()
        {
            var attack = new AttackCooldown(0.5f);
            attack.TryAttack(3f);
            attack.TryAttack(3.4f);

            Assert.IsTrue(attack.TryAttack(3.5f));
        }
    }
}

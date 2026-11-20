using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class HealthTests
    {
        [Test]
        public void StartsFull()
        {
            var health = new Health(100);
            Assert.AreEqual(100, health.Current);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void DamageLowersHealthAndReportsWhatWasApplied()
        {
            var health = new Health(100);
            Assert.AreEqual(30, health.TakeDamage(30));
            Assert.AreEqual(70, health.Current);
        }

        [Test]
        public void HealthNeverDropsBelowZeroAndThenIsDead()
        {
            var health = new Health(20);
            Assert.AreEqual(20, health.TakeDamage(50));
            Assert.AreEqual(0, health.Current);
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void NegativeDamageIsIgnored()
        {
            var health = new Health(100);
            Assert.AreEqual(0, health.TakeDamage(-10));
            Assert.AreEqual(100, health.Current);
        }

        [Test]
        public void ResetRestoresFullHealth()
        {
            var health = new Health(100);
            health.TakeDamage(100);
            health.Reset();
            Assert.AreEqual(100, health.Current);
            Assert.IsFalse(health.IsDead);
        }
    }

    public class ShieldStateTests
    {
        // 3 charges, 6 s recharge, 0.15 s perfect window, guard until 0.4 s, partial parry blocks 50 %.
        private static ShieldState NewShield() => new ShieldState(3, 6f, 0.15f, 0.4f, 0.5f);

        [Test]
        public void StartsWithAllCharges()
        {
            Assert.AreEqual(3, NewShield().Charges(0f));
        }

        [Test]
        public void RaisingSpendsACharge()
        {
            var shield = NewShield();
            Assert.IsTrue(shield.TryRaise(1f));
            Assert.AreEqual(2, shield.Charges(1f));
        }

        [Test]
        public void CannotRaiseWhileTheGuardIsUp()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            Assert.IsFalse(shield.TryRaise(1.2f));
            Assert.AreEqual(2, shield.Charges(1.2f));
        }

        [Test]
        public void CannotRaiseWithoutCharges()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            shield.TryRaise(2f);
            shield.TryRaise(3f);
            Assert.IsFalse(shield.TryRaise(4f));
        }

        [Test]
        public void ChargesRechargeOneAtATime()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            shield.TryRaise(2f);
            Assert.AreEqual(1, shield.Charges(6.9f));
            Assert.AreEqual(2, shield.Charges(7f));
            Assert.AreEqual(3, shield.Charges(13f));
            Assert.AreEqual(3, shield.Charges(100f));
        }

        [Test]
        public void HitEarlyInTheGuardIsAPerfectParryAndBlocksEverything()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            var hit = shield.Resolve(10, 1.1f);
            Assert.AreEqual(ParryGrade.Perfect, hit.Grade);
            Assert.AreEqual(0, hit.Taken);
            Assert.AreEqual(10, hit.Absorbed);
        }

        [Test]
        public void HitLateInTheGuardIsAPartialParryAndBlocksThePercentage()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            var hit = shield.Resolve(10, 1.3f);
            Assert.AreEqual(ParryGrade.Partial, hit.Grade);
            Assert.AreEqual(5, hit.Taken);
            Assert.AreEqual(5, hit.Absorbed);
        }

        [Test]
        public void HitAfterTheGuardDealsFullDamage()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            var hit = shield.Resolve(10, 1.45f);
            Assert.AreEqual(ParryGrade.None, hit.Grade);
            Assert.AreEqual(10, hit.Taken);
        }

        [Test]
        public void HitWithoutRaisingDealsFullDamage()
        {
            var hit = NewShield().Resolve(10, 2f);
            Assert.AreEqual(ParryGrade.None, hit.Grade);
            Assert.AreEqual(10, hit.Taken);
            Assert.AreEqual(0, hit.Absorbed);
        }

        [Test]
        public void PartialBlockRoundsInThePlayersFavour()
        {
            var shield = new ShieldState(3, 6f, 0.15f, 0.4f, 0.5f);
            shield.TryRaise(0f);
            Assert.AreEqual(5, shield.Resolve(11, 0.3f).Taken);
        }
    }
}

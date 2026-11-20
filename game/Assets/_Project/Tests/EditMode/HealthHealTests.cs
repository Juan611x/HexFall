using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class HealthHealTests
    {
        [Test]
        public void HealRestoresHealthAndReportsWhatWasHealed()
        {
            var health = new Health(100);
            health.TakeDamage(40);

            Assert.AreEqual(25, health.Heal(25));
            Assert.AreEqual(85, health.Current);
        }

        [Test]
        public void HealNeverGoesAboveMax()
        {
            var health = new Health(100);
            health.TakeDamage(30);

            Assert.AreEqual(30, health.Heal(100));
            Assert.AreEqual(100, health.Current);
        }

        [Test]
        public void HealAtFullHealthHealsNothing()
        {
            Assert.AreEqual(0, new Health(100).Heal(50));
        }

        [Test]
        public void NonPositiveHealIsIgnored()
        {
            var health = new Health(100);
            health.TakeDamage(50);

            Assert.AreEqual(0, health.Heal(-10));
            Assert.AreEqual(50, health.Current);
        }

        [Test]
        public void HealDoesNotReviveTheDead()
        {
            var health = new Health(100);
            health.TakeDamage(100);

            Assert.AreEqual(0, health.Heal(50));
            Assert.IsTrue(health.IsDead);
        }
    }
}

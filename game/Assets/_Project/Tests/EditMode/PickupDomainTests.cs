using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ShieldRefillTests
    {
        private static ShieldState NewShield() => new ShieldState(3, 6f, 0.15f, 0.4f, 0.5f);

        [Test]
        public void RefillRestoresEverySpentCharge()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            shield.TryRaise(2f);

            Assert.IsTrue(shield.Refill(3f));
            Assert.AreEqual(3, shield.Charges(3f));
        }

        [Test]
        public void RefillOnAFullShieldDoesNothing()
        {
            Assert.IsFalse(NewShield().Refill(1f));
        }

        [Test]
        public void ChargesSpentAfterARefillRechargeFromThatNewRaise()
        {
            var shield = NewShield();
            shield.TryRaise(1f);
            shield.Refill(2f);
            shield.TryRaise(10f);

            Assert.AreEqual(2, shield.Charges(15.9f));
            Assert.AreEqual(3, shield.Charges(16f));
        }
    }

    public class PickupMotionTests
    {
        [Test]
        public void BobStartsAtRest()
        {
            Assert.AreEqual(0f, PickupMotion.Bob(0f, 0.2f, 2f), 0.0001f);
        }

        [Test]
        public void BobPeaksAtAQuarterPeriod()
        {
            Assert.AreEqual(0.2f, PickupMotion.Bob(0.5f, 0.2f, 2f), 0.0001f);
        }

        [Test]
        public void BobWithoutAPeriodStaysAtRest()
        {
            Assert.AreEqual(0f, PickupMotion.Bob(0.5f, 0.2f, 0f), 0.0001f);
        }

        [Test]
        public void SpinTurnsAtTheGivenSpeed()
        {
            Assert.AreEqual(90f, PickupMotion.Spin(2f, 45f), 0.0001f);
        }

        [Test]
        public void SpinWrapsAroundAFullTurn()
        {
            Assert.AreEqual(90f, PickupMotion.Spin(10f, 45f), 0.0001f);
        }
    }

    public class RespawnTimerTests
    {
        [Test]
        public void ANewPickupIsAvailable()
        {
            Assert.IsTrue(new RespawnTimer(5f).IsAvailable(0f));
        }

        [Test]
        public void ConsumingHidesItUntilTheRespawnTime()
        {
            var timer = new RespawnTimer(5f);
            Assert.IsTrue(timer.Consume(2f));

            Assert.IsFalse(timer.IsAvailable(6.9f));
            Assert.IsTrue(timer.IsAvailable(7f));
        }

        [Test]
        public void AnUnavailablePickupCannotBeConsumed()
        {
            var timer = new RespawnTimer(5f);
            timer.Consume(2f);

            Assert.IsFalse(timer.Consume(3f));
        }
    }
}

using HexFall.UI;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // The spell ring around the crosshair: what it shows for ready, cooling down, charging, channelling and
    // the channel wind-up. Pure model, no UI.
    public class SpellRingModelTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void AReadySpellShowsAFullIdleRing()
        {
            var s = SpellRingModel.Evaluate(default);
            Assert.AreEqual(SpellRingMode.Ready, s.Mode);
            Assert.AreEqual(1f, s.Fraction, Eps);
            Assert.IsFalse(s.Full);
        }

        [Test]
        public void ACoolingSpellShowsTheRemainingCooldownFraction()
        {
            var s = SpellRingModel.Evaluate(new SpellRingInput { CooldownFraction = 0.4f });
            Assert.AreEqual(SpellRingMode.Cooldown, s.Mode);
            Assert.AreEqual(0.4f, s.Fraction, Eps);
        }

        [Test]
        public void ACooldownFractionIsClampedAndNonFiniteMeansReady()
        {
            Assert.AreEqual(1f, SpellRingModel.Evaluate(new SpellRingInput { CooldownFraction = 3f }).Fraction, Eps);
            Assert.AreEqual(SpellRingMode.Ready, SpellRingModel.Evaluate(new SpellRingInput { CooldownFraction = float.NaN }).Mode);
            Assert.AreEqual(SpellRingMode.Ready, SpellRingModel.Evaluate(new SpellRingInput { CooldownFraction = -1f }).Mode);
        }

        [Test]
        public void ChargingShowsTheChargeLevelAndIsFullOnlyAtFullCharge()
        {
            var half = SpellRingModel.Evaluate(new SpellRingInput { Charging = true, ChargeLevel = 0.5f });
            Assert.AreEqual(SpellRingMode.Charging, half.Mode);
            Assert.AreEqual(0.5f, half.Fraction, Eps);
            Assert.IsFalse(half.Full);

            var full = SpellRingModel.Evaluate(new SpellRingInput { Charging = true, ChargeLevel = 1f });
            Assert.AreEqual(1f, full.Fraction, Eps);
            Assert.IsTrue(full.Full);
        }

        [Test]
        public void ChargingBeatsTheCooldown()
        {
            var s = SpellRingModel.Evaluate(new SpellRingInput { Charging = true, ChargeLevel = 0.2f, CooldownFraction = 0.9f });
            Assert.AreEqual(SpellRingMode.Charging, s.Mode);
        }

        [Test]
        public void AChannelShowsItsRemainingTime()
        {
            var s = SpellRingModel.Evaluate(new SpellRingInput { Channelling = true, ChannelRemaining = 0.25f });
            Assert.AreEqual(SpellRingMode.Channelling, s.Mode);
            Assert.AreEqual(0.25f, s.Fraction, Eps);
        }

        [Test]
        public void TheChannelWindUpShowsAnEmptyArmingState()
        {
            var s = SpellRingModel.Evaluate(new SpellRingInput { Channelling = true, WindingUp = true, ChannelRemaining = 1f });
            Assert.AreEqual(SpellRingMode.WindingUp, s.Mode);
            Assert.AreEqual(0f, s.Fraction, Eps);
        }

        [Test]
        public void NonFiniteChargeAndChannelValuesAreClamped()
        {
            Assert.AreEqual(0f, SpellRingModel.Evaluate(new SpellRingInput { Charging = true, ChargeLevel = float.NaN }).Fraction, Eps);
            Assert.AreEqual(1f, SpellRingModel.Evaluate(new SpellRingInput { Charging = true, ChargeLevel = 7f }).Fraction, Eps);
            Assert.AreEqual(0f, SpellRingModel.Evaluate(new SpellRingInput { Channelling = true, ChannelRemaining = float.NaN }).Fraction, Eps);
        }
    }
}

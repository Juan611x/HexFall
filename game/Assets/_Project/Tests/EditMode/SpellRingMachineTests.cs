using HexFall.Player.Actions;
using HexFall.Player.Spells;
using HexFall.UI;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // The domain side of the spell ring: the caster's cooldown fraction, the channel's remaining time and what
    // the action machine reports as the ring at a given time.
    public class SpellRingMachineTests
    {
        private const float Eps = 1e-4f;

        private static CastConfig Single(float cooldown = 1f) =>
            new CastConfig { Pattern = CastPattern.Single, Damage = 10, CooldownSeconds = cooldown };

        private static CastConfig Fire() => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = 1.2f, BaseDamage = 18, ChargeMultiplier = 2f,
        };

        private static CastConfig Water(float windUp = 0f) => new CastConfig
        {
            Pattern = CastPattern.Channel, CooldownSeconds = 0.5f, ChannelDurationSeconds = 1.5f,
            ChannelTicksPerSecond = 10f, ChannelDamagePerTick = 4, ChannelCooldownPerSecond = 0.6f,
            ChannelWindUpTimeoutSeconds = windUp,
        };

        private static ActionMachine Machine(CastConfig config) => new ActionMachine(new ActionSettings
        {
            boltDelaySeconds = 0.2f, faceAimHoldSeconds = 0.3f, shieldCharges = 3, chargeRechargeSeconds = 6f,
            perfectParrySeconds = 0.15f, guardSeconds = 0.4f, partialBlock = 0.5f,
        }, new SpellCaster(config));

        // ---- caster cooldown ----

        [Test]
        public void ACasterThatNeverCastHasNoCooldownToShow()
        {
            Assert.AreEqual(0f, new SpellCaster(Single()).CooldownFraction(5f), Eps);
        }

        [Test]
        public void TheCooldownFractionDrainsFromOneToZero()
        {
            var caster = new SpellCaster(Single(2f));
            caster.Press(10f);
            Assert.AreEqual(1f, caster.CooldownFraction(10f), Eps);
            Assert.AreEqual(0.5f, caster.CooldownFraction(11f), Eps);
            Assert.AreEqual(0f, caster.CooldownFraction(12f), Eps);
            Assert.AreEqual(0f, caster.CooldownFraction(30f), Eps);
        }

        [Test]
        public void ABadTimeShowsNoCooldown()
        {
            var caster = new SpellCaster(Single(2f));
            caster.Press(10f);
            Assert.AreEqual(0f, caster.CooldownFraction(float.NaN), Eps);
        }

        [Test]
        public void ASwapKeepsTheRunningCooldownFraction()
        {
            var machine = Machine(Single(2f));
            machine.Tick(new ActionIntent(true, false), 10f);
            machine.SetCaster(new SpellCaster(Single(0.5f)), 11f);
            Assert.AreEqual(0.5f, machine.CooldownFraction(11f), Eps);
        }

        // ---- channel remaining ----

        [Test]
        public void AChannelReportsItsRemainingFraction()
        {
            var caster = new SpellCaster(Water());
            caster.Press(1f);
            Assert.AreEqual(1f, caster.ChannelRemainingFraction(1f), Eps);
            Assert.AreEqual(0.5f, caster.ChannelRemainingFraction(1.75f), Eps);
            Assert.AreEqual(0f, caster.ChannelRemainingFraction(5f), Eps);
        }

        [Test]
        public void AnIdleOrWindingUpChannelHasNoRemainingTime()
        {
            var caster = new SpellCaster(Water(0.6f));
            Assert.AreEqual(0f, caster.ChannelRemainingFraction(1f), Eps);
            caster.Press(1f);
            Assert.AreEqual(0f, caster.ChannelRemainingFraction(1.1f), Eps);
        }

        // ---- machine ring ----

        [Test]
        public void ReadyMachineShowsTheIdleRing()
        {
            Assert.AreEqual(SpellRingMode.Ready, Machine(Single()).SpellRing(0f).Mode);
        }

        [Test]
        public void AfterACastTheRingDrainsWithTheCooldown()
        {
            var machine = Machine(Single(2f));
            machine.Tick(new ActionIntent(true, false), 10f);
            var ring = machine.SpellRing(11f);
            Assert.AreEqual(SpellRingMode.Cooldown, ring.Mode);
            Assert.AreEqual(0.5f, ring.Fraction, Eps);
            Assert.AreEqual(SpellRingMode.Ready, machine.SpellRing(12.5f).Mode);
        }

        [Test]
        public void ChargingFillsTheRingAndFlagsFullCharge()
        {
            var machine = Machine(Fire());
            machine.Tick(new ActionIntent(true, false), 5f);
            var mid = machine.SpellRing(5.6f);
            Assert.AreEqual(SpellRingMode.Charging, mid.Mode);
            Assert.AreEqual(0.5f, mid.Fraction, Eps);
            Assert.IsFalse(mid.Full);
            Assert.IsTrue(machine.SpellRing(7f).Full);
        }

        [Test]
        public void ReleasingTheChargeSwitchesToTheCooldown()
        {
            var machine = Machine(Fire());
            machine.Tick(new ActionIntent(true, false), 5f);
            machine.Tick(new ActionIntent(false, false, true), 5.6f);
            Assert.AreEqual(SpellRingMode.Cooldown, machine.SpellRing(5.7f).Mode);
        }

        [Test]
        public void AWindingUpChannelShowsTheArmingStateThenTheDrainingChannel()
        {
            var machine = Machine(Water(0.6f));
            machine.Tick(new ActionIntent(true, false), 1f);
            Assert.AreEqual(SpellRingMode.WindingUp, machine.SpellRing(1.1f).Mode);

            machine.PoseReached(1.2f);
            var ring = machine.SpellRing(1.95f);
            Assert.AreEqual(SpellRingMode.Channelling, ring.Mode);
            Assert.AreEqual(0.5f, ring.Fraction, Eps);
        }

        [Test]
        public void AParryCancelledChargeShowsNoChargeRing()
        {
            var machine = Machine(Fire());
            machine.Tick(new ActionIntent(true, false), 5f);
            machine.Tick(new ActionIntent(false, true), 5.3f);
            Assert.AreNotEqual(SpellRingMode.Charging, machine.SpellRing(5.4f).Mode);
        }
    }
}

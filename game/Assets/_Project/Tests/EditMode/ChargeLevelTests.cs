using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // The charge level (0..1) of the hold in progress, read by the orb, the animator and the HUD.
    public class ChargeLevelTests
    {
        private static CastConfig Charged(float max = 1.2f) => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = max, BaseDamage = 18, ChargeMode = ChargeDamageMode.Curve, ChargeCurve = ChargeDamage.Bake(x => 1f + 1.5f * x),
        };

        [Test]
        public void ACasterThatIsNotChargingHasNoCharge()
        {
            var caster = new SpellCaster(Charged());
            Assert.AreEqual(0f, caster.ChargeLevel(1f));
        }

        [Test]
        public void TheLevelRisesWithTheHoldAndCapsAtOne()
        {
            var caster = new SpellCaster(Charged(1.2f));
            caster.Press(1f);
            Assert.AreEqual(0f, caster.ChargeLevel(1f), 1e-5f);
            Assert.AreEqual(0.5f, caster.ChargeLevel(1.6f), 1e-5f);
            Assert.AreEqual(1f, caster.ChargeLevel(2.2f), 1e-5f);
            Assert.AreEqual(1f, caster.ChargeLevel(9f), 1e-5f);
        }

        [Test]
        public void ReleaseAndCancelEndTheCharge()
        {
            var released = new SpellCaster(Charged());
            released.Press(1f);
            released.Release(1.6f);
            Assert.AreEqual(0f, released.ChargeLevel(1.7f));

            var cancelled = new SpellCaster(Charged());
            cancelled.Press(1f);
            cancelled.Cancel(1.3f);
            Assert.AreEqual(0f, cancelled.ChargeLevel(1.4f));
        }

        [Test]
        public void ALevelMatchesTheChargeOfTheShotItProduces()
        {
            var caster = new SpellCaster(Charged(1.2f));
            var charge = -1f;
            caster.ProjectileFired += s => charge = s.Charge;
            caster.Press(1f);
            var level = caster.ChargeLevel(1.9f);
            caster.Release(1.9f);
            Assert.AreEqual(level, charge, 1e-5f);
        }

        [Test]
        public void ANonPositiveMaxChargeIsFullWhileHeld()
        {
            var caster = new SpellCaster(Charged(0f));
            caster.Press(1f);
            Assert.AreEqual(1f, caster.ChargeLevel(1f));
        }

        [Test]
        public void InvalidTimesReadAsNoCharge()
        {
            var caster = new SpellCaster(Charged());
            caster.Press(1f);
            Assert.AreEqual(0f, caster.ChargeLevel(float.NaN));
            Assert.AreEqual(0f, caster.ChargeLevel(float.PositiveInfinity));
        }

        [TestCase(CastPattern.Single)]
        [TestCase(CastPattern.Burst)]
        [TestCase(CastPattern.Channel)]
        public void OtherPatternsNeverCharge(CastPattern pattern)
        {
            var caster = new SpellCaster(new CastConfig
            {
                Pattern = pattern, Damage = 8, CooldownSeconds = 0.5f, BurstCount = 3, BurstIntervalSeconds = 0.08f,
                ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f, ChannelDamagePerTick = 4,
            });
            caster.Press(1f);
            Assert.AreEqual(0f, caster.ChargeLevel(1.1f));
        }

        private static ActionMachine Machine(CastConfig config) =>
            new ActionMachine(new ActionSettings(), new SpellCaster(config));

        [Test]
        public void TheMachineReportsTheLevelOnlyWhileCharging()
        {
            var m = Machine(Charged(1.2f));
            Assert.AreEqual(0f, m.ChargeLevel(1f));
            m.Tick(new ActionIntent(true, false), 1f);
            Assert.AreEqual(ActionStateId.Charging, m.Current);
            Assert.AreEqual(0.5f, m.ChargeLevel(1.6f), 1e-5f);
            m.Tick(new ActionIntent(false, false, true), 1.6f);
            Assert.AreEqual(ActionStateId.Attack, m.Current);
            Assert.AreEqual(0f, m.ChargeLevel(1.7f));
        }

        [Test]
        public void AParryDropsTheLevel()
        {
            var m = Machine(Charged());
            m.Tick(new ActionIntent(true, false), 1f);
            m.Tick(new ActionIntent(false, true), 1.3f);
            Assert.AreEqual(0f, m.ChargeLevel(1.4f));
        }

        [Test]
        public void ALostReleaseDropsTheLevel()
        {
            var m = Machine(Charged());
            m.Tick(new ActionIntent(true, false), 1f);
            m.Tick(new ActionIntent(false, false, false, true), 1.3f);
            Assert.AreEqual(0f, m.ChargeLevel(1.4f));
        }

        [Test]
        public void ASpellSwapDropsTheLevel()
        {
            var m = Machine(Charged());
            m.Tick(new ActionIntent(true, false), 1f);
            m.SetCaster(new SpellCaster(Charged()), 1.3f);
            Assert.AreEqual(0f, m.ChargeLevel(1.4f));
        }

        [Test]
        public void AChannelHasNoChargeLevel()
        {
            var m = Machine(new CastConfig
            {
                Pattern = CastPattern.Channel, CooldownSeconds = 0.5f,
                ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f, ChannelDamagePerTick = 4,
            });
            m.Tick(new ActionIntent(true, false), 1f);
            Assert.AreEqual(ActionStateId.Channelling, m.Current);
            Assert.AreEqual(0f, m.ChargeLevel(1.5f));
        }
    }
}

using System;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // Charged damage = round(baseDamage x multiplier), multiplier from a constant (full charge only) or a baked curve.
    public class ChargeDamageTests
    {
        private const float Eps = 1e-4f;

        private static CastConfig Constant(float multiplier = 2.5f, int baseDamage = 18) => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = 1.2f,
            ChargeMode = ChargeDamageMode.Constant, BaseDamage = baseDamage, ChargeMultiplier = multiplier,
        };

        private static CastConfig Curve(Func<float, float> f) => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = 1.2f,
            ChargeMode = ChargeDamageMode.Curve, BaseDamage = 18, ChargeCurve = ChargeDamage.Bake(f),
        };

        private static int Shoot(CastConfig config, float held)
        {
            var caster = new SpellCaster(config);
            var damage = -1;
            caster.ProjectileFired += s => damage = s.Damage;
            caster.Press(1f);
            caster.Release(1f + held);
            return damage;
        }

        // ---- Constant ----

        [TestCase(0f, 18)]
        [TestCase(0.6f, 18)]
        [TestCase(1.1f, 18)]
        public void ConstantBelowFullChargeKeepsTheBaseDamage(float held, int expected) =>
            Assert.AreEqual(expected, Shoot(Constant(), held));

        [Test]
        public void ConstantAtFullChargeMultipliesTheBaseDamage() =>
            Assert.AreEqual(45, Shoot(Constant(), 1.2f));

        [Test]
        public void ConstantHeldPastTheMaximumStillMultiplies() =>
            Assert.AreEqual(45, Shoot(Constant(), 9f));

        [Test]
        public void ConstantFullChargeWithinTheEpsilonCounts() =>
            Assert.AreEqual(45, Shoot(Constant(), 1.2f - 1e-5f));

        [Test]
        public void ConstantRoundsHalvesAwayFromZero() =>
            Assert.AreEqual(23, Shoot(Constant(1.25f), 1.2f)); // 22.5 -> 23

        [Test]
        public void ConstantMultiplierBelowOneReducesTheFullCharge() =>
            Assert.AreEqual(9, Shoot(Constant(0.5f), 1.2f));

        // ---- Curve ----

        [TestCase(0f, 18)]
        [TestCase(0.3f, 25)]  // charge .25 -> x1.375 -> 24.75
        [TestCase(0.6f, 32)]  // charge .5 -> x1.75 -> 31.5 (same as the old lerp)
        [TestCase(0.9f, 38)]  // charge .75 -> x2.125 -> 38.25
        [TestCase(1.2f, 45)]
        [TestCase(5f, 45)]
        public void LinearCurveMatchesTheOldLerp(float held, int expected)
        {
            var charge = Math.Min(1f, held / 1.2f);
            var oldLerp = (int)Math.Round(18 + (45 - 18) * charge, MidpointRounding.AwayFromZero);
            Assert.AreEqual(expected, oldLerp, "test table agrees with the old formula");
            Assert.AreEqual(expected, Shoot(Curve(x => 1f + 1.5f * x), held));
        }

        [Test]
        public void NonLinearCurveGivesTheExpectedValues()
        {
            var cfg = Curve(x => 1f + 1.5f * x * x); // ease-in
            Assert.AreEqual(18, Shoot(cfg, 0f));
            Assert.AreEqual(25, Shoot(cfg, 0.6f)); // x1.375 -> 24.75
            Assert.AreEqual(45, Shoot(cfg, 1.2f));
        }

        [Test]
        public void CurveBelowOneIsAllowed() =>
            Assert.AreEqual(9, Shoot(Curve(x => 1f - 0.5f * x), 1.2f));

        [Test]
        public void ChargeLevelOnTheShotIsUnchanged()
        {
            var caster = new SpellCaster(Curve(x => 1f + x));
            var charge = -1f;
            caster.ProjectileFired += s => charge = s.Charge;
            caster.Press(1f);
            caster.Release(1.6f);
            Assert.AreEqual(0.5f, charge, Eps);
        }

        // ---- Bake / sample ----

        [Test]
        public void BakeSamplesTheFunctionAcrossZeroToOne()
        {
            var table = ChargeDamage.Bake(x => 3f * x, 5);
            Assert.AreEqual(5, table.Length);
            Assert.AreEqual(0f, table[0], Eps);
            Assert.AreEqual(0.75f, table[1], Eps);
            Assert.AreEqual(3f, table[4], Eps);
        }

        [Test]
        public void SampleInterpolatesLinearlyAndClampsTheEnds()
        {
            var table = new[] { 1f, 2f, 4f };
            Assert.AreEqual(1f, ChargeDamage.Sample(table, -1f), Eps);
            Assert.AreEqual(1.5f, ChargeDamage.Sample(table, 0.25f), Eps);
            Assert.AreEqual(2f, ChargeDamage.Sample(table, 0.5f), Eps);
            Assert.AreEqual(3f, ChargeDamage.Sample(table, 0.75f), Eps);
            Assert.AreEqual(4f, ChargeDamage.Sample(table, 7f), Eps);
        }

        [Test]
        public void SampleOfALinearBakeIsExactEverywhere()
        {
            var table = ChargeDamage.Bake(x => 1f + 1.5f * x);
            for (var i = 0; i <= 20; i++)
            {
                var x = i / 20f;
                Assert.AreEqual(1f + 1.5f * x, ChargeDamage.Sample(table, x), 1e-4f);
            }
        }

        [Test]
        public void SampleOfASmoothBakeStaysCloseToTheCurve()
        {
            var table = ChargeDamage.Bake(x => 1f + 1.5f * x * x);
            for (var i = 0; i <= 40; i++)
            {
                var x = i / 40f;
                Assert.AreEqual(1f + 1.5f * x * x, ChargeDamage.Sample(table, x), 2e-3f);
            }
        }

        [Test]
        public void SampleOfNullEmptyOrSingleTableIsFlat()
        {
            Assert.AreEqual(1f, ChargeDamage.Sample(null, 0.5f), Eps);
            Assert.AreEqual(1f, ChargeDamage.Sample(new float[0], 0.5f), Eps);
            Assert.AreEqual(3f, ChargeDamage.Sample(new[] { 3f }, 0.5f), Eps);
        }

        [Test]
        public void SampleOfANonFiniteChargeUsesTheFirstSample() =>
            Assert.AreEqual(1f, ChargeDamage.Sample(new[] { 1f, 2f }, float.NaN), Eps);

        // ---- Sanitize ----

        [Test]
        public void SanitizeReplacesANullOrEmptyCurveWithAFlatOne()
        {
            var a = new CastConfig { ChargeMode = ChargeDamageMode.Curve, ChargeCurve = null }.Sanitized();
            var b = new CastConfig { ChargeMode = ChargeDamageMode.Curve, ChargeCurve = new float[0] }.Sanitized();
            foreach (var c in new[] { a, b })
            {
                Assert.GreaterOrEqual(c.ChargeCurve.Length, 2);
                foreach (var v in c.ChargeCurve) Assert.AreEqual(1f, v, Eps);
            }
        }

        [Test]
        public void SanitizeReplacesNegativeAndNonFiniteSamplesAndMultipliers()
        {
            var c = new CastConfig
            {
                ChargeMultiplier = float.NaN, BaseDamage = -4,
                ChargeCurve = new[] { 1f, -2f, float.PositiveInfinity, float.NaN, 2f },
            }.Sanitized();
            Assert.AreEqual(1f, c.ChargeMultiplier, Eps);
            Assert.AreEqual(0, c.BaseDamage);
            Assert.AreEqual(new[] { 1f, 1f, 1f, 1f, 2f }, c.ChargeCurve);
            Assert.AreEqual(1f, new CastConfig { ChargeMultiplier = -3f }.Sanitized().ChargeMultiplier, Eps);
            Assert.AreEqual(1f, new CastConfig { ChargeMultiplier = float.PositiveInfinity }.Sanitized().ChargeMultiplier, Eps);
        }

        // ---- T10 advisories: unsanitized configs must not misbehave ----

        [Test]
        public void ANonFiniteMultiplierOnAnUnsanitizedConfigCountsAsX1()
        {
            Assert.AreEqual(18, ChargeDamage.Damage(Constant(float.NaN), 1f));
            Assert.AreEqual(18, ChargeDamage.Damage(Constant(float.PositiveInfinity), 1f));
            Assert.AreEqual(18, ChargeDamage.Damage(Constant(float.NegativeInfinity), 1f));
            var curve = new CastConfig
            {
                ChargeMode = ChargeDamageMode.Curve, BaseDamage = 18, ChargeCurve = new[] { 1f, float.NaN, float.PositiveInfinity },
            };
            Assert.AreEqual(18, ChargeDamage.Damage(curve, 0.5f));
            Assert.AreEqual(18, ChargeDamage.Damage(curve, 1f));
        }

        [Test]
        public void AHugeProductSaturatesAtIntMaxValue()
        {
            Assert.AreEqual(int.MaxValue, ChargeDamage.Damage(Constant(3f, int.MaxValue), 1f));
            Assert.AreEqual(int.MaxValue, ChargeDamage.Damage(Constant(1e30f), 1f));
            Assert.AreEqual(int.MaxValue, ChargeDamage.Damage(Curve(x => 1e30f), 0.5f));
            Assert.AreEqual(int.MaxValue, ChargeDamage.Damage(Constant(1f, int.MaxValue), 1f));
        }

        [Test]
        public void ACodeBuiltConfigNeverDealsZeroAtFullChargeInConstantMode()
        {
            // A default struct leaves ChargeMultiplier at 0; 0 means "unset" and acts as x1, never as zero damage.
            var built = new CastConfig { Pattern = CastPattern.Charged, MaxChargeSeconds = 1.2f, BaseDamage = 18 };
            Assert.AreEqual(ChargeDamageMode.Constant, built.ChargeMode);
            Assert.AreEqual(18, ChargeDamage.Damage(built, 1f));
            Assert.AreEqual(18, ChargeDamage.Damage(built.Sanitized(), 1f));
            Assert.AreEqual(1f, built.Sanitized().ChargeMultiplier, Eps);
            Assert.AreEqual(18, Shoot(built, 1.2f));
        }

        [Test]
        public void AnExplicitMultiplierIsStillHonoured()
        {
            Assert.AreEqual(45, ChargeDamage.Damage(Constant(2.5f), 1f));
            Assert.AreEqual(2.5f, Constant(2.5f).Sanitized().ChargeMultiplier, Eps);
        }

        [Test]
        public void SanitizeDoesNotMutateTheSourceCurve()
        {
            var src = new[] { 1f, -2f };
            new CastConfig { ChargeCurve = src }.Sanitized();
            Assert.AreEqual(-2f, src[1], Eps);
        }
    }
}

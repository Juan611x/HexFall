using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class SpellDefinitionTests
    {
        private SpellDefinition _spell;

        [SetUp]
        public void SetUp() => _spell = ScriptableObject.CreateInstance<SpellDefinition>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_spell);

        // Writes through SerializedObject so the tests need no setters on the data-only SO.
        private void Set(string path, System.Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(_spell);
            assign(so.FindProperty(path));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void DefaultsAreValidAndEffectSlotsAreEmpty()
        {
            Assert.AreEqual(CastPattern.Single, _spell.Pattern);
            Assert.Greater(_spell.Damage, 0);
            Assert.Greater(_spell.Projectile.speed, 0f);
            Assert.IsNull(_spell.Effects.castEffect);
            Assert.IsNull(_spell.Effects.impactClip);
        }

        [Test]
        public void ToCastConfigMapsEveryPatternField()
        {
            Set("pattern", p => p.enumValueIndex = (int)CastPattern.Channel);
            Set("damage", p => p.intValue = 9);
            Set("cooldownSeconds", p => p.floatValue = 0.5f);
            Set("charge.maxChargeSeconds", p => p.floatValue = 1.2f);
            Set("charge.damageMode", p => p.enumValueIndex = (int)ChargeDamageMode.Constant);
            Set("charge.baseDamage", p => p.intValue = 18);
            Set("charge.constantMultiplier", p => p.floatValue = 2.5f);
            Set("burst.count", p => p.intValue = 3);
            Set("burst.intervalSeconds", p => p.floatValue = 0.08f);
            Set("burst.spreadDegrees", p => p.floatValue = 3f);
            Set("channel.durationSeconds", p => p.floatValue = 1.5f);
            Set("channel.ticksPerSecond", p => p.floatValue = 10f);
            Set("channel.damagePerTick", p => p.intValue = 4);
            Set("channel.cooldownPerChannelSecond", p => p.floatValue = 0.6f);

            var c = _spell.ToCastConfig();

            Assert.AreEqual(CastPattern.Channel, c.Pattern);
            Assert.AreEqual(9, c.Damage);
            Assert.AreEqual(0.5f, c.CooldownSeconds, 1e-4f);
            Assert.AreEqual(1.2f, c.MaxChargeSeconds, 1e-4f);
            Assert.AreEqual(ChargeDamageMode.Constant, c.ChargeMode);
            Assert.AreEqual(18, c.BaseDamage);
            Assert.AreEqual(2.5f, c.ChargeMultiplier, 1e-4f);
            Assert.AreEqual(3, c.BurstCount);
            Assert.AreEqual(0.08f, c.BurstIntervalSeconds, 1e-4f);
            Assert.AreEqual(3f, c.BurstSpreadDegrees, 1e-4f);
            Assert.AreEqual(1.5f, c.ChannelDurationSeconds, 1e-4f);
            Assert.AreEqual(10f, c.ChannelTicksPerSecond, 1e-4f);
            Assert.AreEqual(4, c.ChannelDamagePerTick);
            Assert.AreEqual(0.6f, c.ChannelCooldownPerSecond, 1e-4f);
        }

        [Test]
        public void SanitizeClampsInvalidValues()
        {
            Set("cooldownSeconds", p => p.floatValue = -2f);
            Set("damage", p => p.intValue = -5);
            Set("burst.count", p => p.intValue = 0);
            Set("burst.intervalSeconds", p => p.floatValue = -1f);
            Set("charge.baseDamage", p => p.intValue = -30);
            Set("charge.constantMultiplier", p => p.floatValue = -2f);
            Set("channel.ticksPerSecond", p => p.floatValue = 0f);
            Set("projectile.lifetimeSeconds", p => p.floatValue = -1f);

            _spell.Sanitize();
            var c = _spell.ToCastConfig();

            Assert.AreEqual(0f, c.CooldownSeconds);
            Assert.AreEqual(0, c.Damage);
            Assert.AreEqual(1, c.BurstCount);
            Assert.AreEqual(0f, c.BurstIntervalSeconds);
            Assert.AreEqual(0, c.BaseDamage);
            Assert.AreEqual(1f, c.ChargeMultiplier, 1e-4f);
            Assert.Greater(c.ChannelTicksPerSecond, 0f);
            Assert.Greater(_spell.Projectile.lifetimeSeconds, 0f);
        }

        private static int ChargedDamage(SpellDefinition spell, float charge) =>
            ChargeDamage.Damage(spell.ToCastConfig().Sanitized(), charge);

        [Test]
        public void DefaultChargeDamageIsABaseDamageWithAFlatCurve()
        {
            Assert.AreEqual(18, _spell.Charge.baseDamage);
            Assert.AreEqual(ChargeDamageMode.Constant, _spell.Charge.damageMode);
            Assert.AreEqual(1f, _spell.Charge.damageCurve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(1f, _spell.Charge.damageCurve.Evaluate(1f), 1e-4f);
            Assert.AreEqual(1f, _spell.Charge.constantMultiplier, 1e-4f);
            Assert.AreEqual(18, ChargedDamage(_spell, 1f));
        }

        [Test]
        public void ToCastConfigBakesTheDamageCurveForTheDomain()
        {
            Set("charge.damageMode", p => p.enumValueIndex = (int)ChargeDamageMode.Curve);
            Set("charge.damageCurve", p => p.animationCurveValue = AnimationCurve.Linear(0f, 1f, 1f, 2.5f));

            var c = _spell.ToCastConfig();

            Assert.AreEqual(ChargeDamageMode.Curve, c.ChargeMode);
            Assert.GreaterOrEqual(c.ChargeCurve.Length, 2);
            Assert.AreEqual(1f, c.ChargeCurve[0], 1e-4f);
            Assert.AreEqual(2.5f, c.ChargeCurve[c.ChargeCurve.Length - 1], 1e-4f);
            Assert.AreEqual(18, ChargedDamage(_spell, 0f));
            Assert.AreEqual(32, ChargedDamage(_spell, 0.5f));
            Assert.AreEqual(45, ChargedDamage(_spell, 1f));
        }

        [Test]
        public void BakedCurveFollowsANonLinearAuthoredCurve()
        {
            var ease = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 3f, 4f, 0f));
            Set("charge.damageMode", p => p.enumValueIndex = (int)ChargeDamageMode.Curve);
            Set("charge.damageCurve", p => p.animationCurveValue = ease);

            var c = _spell.ToCastConfig();

            for (var i = 0; i <= 10; i++)
            {
                var x = i / 10f;
                Assert.AreEqual(ease.Evaluate(x), ChargeDamage.Sample(c.ChargeCurve, x), 0.02f);
            }
        }

        [Test]
        public void SanitizeReplacesAnEmptyOrNonFiniteCurveWithAFlatOne()
        {
            Set("charge.damageCurve", p => p.animationCurveValue = new AnimationCurve());
            _spell.Sanitize();
            Assert.AreEqual(1f, _spell.Charge.damageCurve.Evaluate(0.3f), 1e-4f);

            Set("charge.damageCurve", p => p.animationCurveValue = new AnimationCurve(
                new Keyframe(0f, float.NaN), new Keyframe(1f, 2f)));
            _spell.Sanitize();
            // Unity may drop the NaN key itself; either way the baked table must be finite.
            foreach (var v in _spell.ToCastConfig().Sanitized().ChargeCurve)
                Assert.IsTrue(!float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f);
        }

        [Test]
        public void ANegativeAuthoredCurveSampleBakesAsTheNeutralMultiplier()
        {
            Set("charge.damageCurve", p => p.animationCurveValue = AnimationCurve.Linear(0f, -1f, 1f, 2f));
            _spell.Sanitize();
            Assert.AreEqual(2f, _spell.Charge.damageCurve.Evaluate(1f), 1e-4f, "authored curve kept");
            Assert.AreEqual(1f, _spell.ToCastConfig().Sanitized().ChargeCurve[0], 1e-4f);
        }

        [Test]
        public void SanitizeClampsNonFiniteValues()
        {
            Set("cooldownSeconds", p => p.floatValue = float.PositiveInfinity);
            Set("projectile.speed", p => p.floatValue = float.NaN);
            Set("projectile.weight", p => p.floatValue = float.PositiveInfinity);
            Set("projectile.drag", p => p.floatValue = float.NaN);
            Set("projectile.curveDegreesPerSecond", p => p.floatValue = float.NaN);
            Set("projectile.sizeScale", p => p.floatValue = float.NaN);
            Set("projectile.lifetimeSeconds", p => p.floatValue = float.PositiveInfinity);
            Set("charge.maxChargeSeconds", p => p.floatValue = float.NaN);
            Set("burst.intervalSeconds", p => p.floatValue = float.PositiveInfinity);
            Set("burst.spreadDegrees", p => p.floatValue = float.NaN);
            Set("channel.durationSeconds", p => p.floatValue = float.PositiveInfinity);
            Set("channel.ticksPerSecond", p => p.floatValue = float.NaN);
            Set("channel.rangeMeters", p => p.floatValue = float.PositiveInfinity);
            Set("channel.cooldownPerChannelSecond", p => p.floatValue = float.NaN);

            _spell.Sanitize();

            foreach (var v in new[]
            {
                _spell.CooldownSeconds, _spell.Projectile.speed, _spell.Projectile.weight, _spell.Projectile.drag,
                _spell.Projectile.curveDegreesPerSecond, _spell.Projectile.sizeScale,
                _spell.Projectile.lifetimeSeconds, _spell.Charge.maxChargeSeconds,
                _spell.Burst.intervalSeconds, _spell.Burst.spreadDegrees,
                _spell.Channel.durationSeconds, _spell.Channel.ticksPerSecond,
                _spell.Channel.rangeMeters, _spell.Channel.cooldownPerChannelSecond,
            })
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
            }
            Assert.Greater(_spell.Projectile.sizeScale, 0f);
            Assert.Greater(_spell.Projectile.lifetimeSeconds, 0f);
            Assert.Greater(_spell.Channel.ticksPerSecond, 0f);
        }

        [Test]
        public void SanitizeClampsNegativeWeightAndDragToZero()
        {
            Set("projectile.weight", p => p.floatValue = -3f);
            Set("projectile.drag", p => p.floatValue = -0.5f);

            _spell.Sanitize();

            Assert.AreEqual(0f, _spell.Projectile.weight);
            Assert.AreEqual(0f, _spell.Projectile.drag);
        }

        [Test]
        public void SanitizeKeepsAValidWeightAndDrag()
        {
            Set("projectile.weight", p => p.floatValue = 1.5f);
            Set("projectile.drag", p => p.floatValue = 0.3f);

            _spell.Sanitize();

            Assert.AreEqual(1.5f, _spell.Projectile.weight, 1e-5f);
            Assert.AreEqual(0.3f, _spell.Projectile.drag, 1e-5f);
        }
    }
}

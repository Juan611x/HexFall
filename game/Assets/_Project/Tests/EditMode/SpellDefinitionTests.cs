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
            Set("charge.minDamage", p => p.intValue = 18);
            Set("charge.maxDamage", p => p.intValue = 45);
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
            Assert.AreEqual(18, c.MinDamage);
            Assert.AreEqual(45, c.MaxDamage);
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
            Set("charge.minDamage", p => p.intValue = 30);
            Set("charge.maxDamage", p => p.intValue = 10);
            Set("channel.ticksPerSecond", p => p.floatValue = 0f);
            Set("projectile.lifetimeSeconds", p => p.floatValue = -1f);

            _spell.Sanitize();
            var c = _spell.ToCastConfig();

            Assert.AreEqual(0f, c.CooldownSeconds);
            Assert.AreEqual(0, c.Damage);
            Assert.AreEqual(1, c.BurstCount);
            Assert.AreEqual(0f, c.BurstIntervalSeconds);
            Assert.GreaterOrEqual(c.MaxDamage, c.MinDamage);
            Assert.Greater(c.ChannelTicksPerSecond, 0f);
            Assert.Greater(_spell.Projectile.lifetimeSeconds, 0f);
        }
    }
}

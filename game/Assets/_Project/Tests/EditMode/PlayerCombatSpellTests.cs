using System.Linq;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The wizard prefab ships with the Base spell, the Attack input reports press and release, and a
    // spread shot is a yaw offset around the aim.
    public class PlayerCombatSpellTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string BaseSpellPath = "Assets/_Project/Spells/Spell_Base.asset";
        private const string ControlsPath = "Assets/_Project/Input/HexFallControls.inputactions";

        private static PlayerCombat Combat() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<PlayerCombat>();

        [Test]
        public void BaseSpellAssetExistsWithTheShippedBoltValues()
        {
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(BaseSpellPath);
            Assert.IsNotNull(spell);
            Assert.AreEqual(CastPattern.Single, spell.Pattern);
            Assert.AreEqual(18, spell.Damage);
            Assert.AreEqual(0.5f, spell.CooldownSeconds, 1e-4f);
            Assert.AreEqual(30f, spell.Projectile.speed, 1e-4f);
            Assert.AreEqual(0f, spell.Projectile.gravity, 1e-4f);
            Assert.AreEqual(0f, spell.Projectile.curveDegreesPerSecond, 1e-4f);
            Assert.AreEqual(1f, spell.Projectile.sizeScale, 1e-4f);
            Assert.AreEqual(3f, spell.Projectile.lifetimeSeconds, 1e-4f);
        }

        [Test]
        public void ThePlayerPrefabHoldsTheBaseSpell()
        {
            var combat = Combat();
            Assert.IsNotNull(combat);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<SpellDefinition>(BaseSpellPath), combat.ActiveSpell);
        }

        [Test]
        public void TheAttackInputReportsPressAndRelease()
        {
            // behavior=2 is PressAndRelease: performed fires on the press and again on the release.
            var attackLine = System.IO.File.ReadAllLines(ControlsPath)
                .Single(l => l.Contains("\"name\": \"Attack\"") && l.Contains("\"type\": \"Button\""));
            StringAssert.Contains("\"interactions\": \"Press(behavior=2)\"", attackLine);
        }

        [Test]
        public void ChargingAndChannellingReuseTheAttackAnimation()
        {
            var bridge = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<ActionAnimatorBridge>();
            Assert.AreEqual("Attack", bridge.StateNameFor(ActionStateId.Charging));
            Assert.AreEqual("Attack", bridge.StateNameFor(ActionStateId.Channelling));
        }

        [Test]
        public void ZeroSpreadKeepsTheAimDirection()
        {
            var dir = new Vector3(0.3f, -0.2f, 0.9f);
            Assert.AreEqual(0f, Vector3.Distance(dir, ShotAim.Offset(dir, 0f)), 1e-5f);
        }

        [Test]
        public void SpreadTurnsTheAimAroundTheVerticalAxis()
        {
            var turned = ShotAim.Offset(Vector3.forward, 90f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.right, turned), 1e-4f);
        }

        [Test]
        public void SpreadKeepsThePitchOfTheAim()
        {
            var dir = new Vector3(0f, 0.5f, 1f).normalized;
            var turned = ShotAim.Offset(dir, 3f);
            Assert.AreEqual(dir.y, turned.y, 1e-5f);
            Assert.AreEqual(3f, Mathf.Abs(Vector3.SignedAngle(
                new Vector3(dir.x, 0f, dir.z), new Vector3(turned.x, 0f, turned.z), Vector3.up)), 1e-3f);
        }
    }
}

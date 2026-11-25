using System.Linq;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The authored spells (Fire, Earth, Water next to Base) and the three spell pickups in the Sandbox.
    public class SpellAssetsTests
    {
        private const string Folder = "Assets/_Project/Spells";
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox.unity";
        private const string PickupPrefab = "Assets/_Project/Prefabs/Pickups/Pickup_Spell.prefab";

        private static SpellDefinition Load(string name)
        {
            var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>($"{Folder}/Spell_{name}.asset");
            Assert.IsNotNull(spell, $"Spell_{name}.asset missing.");
            return spell;
        }

        [TestCase("Fire")]
        [TestCase("Earth")]
        [TestCase("Water")]
        public void EachSpellHasItsNameAndEmptyEffectSlots(string name)
        {
            var spell = Load(name);
            Assert.AreEqual(name, spell.DisplayName);
            Assert.IsNull(spell.Effects.castEffect);
            Assert.IsNull(spell.Effects.impactEffect);
            Assert.IsNull(spell.Effects.enemyHitEffect);
            Assert.IsNull(spell.Effects.surfaceImpactParticles);
            Assert.IsNull(spell.Effects.surfaceDecal);
            Assert.IsNull(spell.Effects.castClip);
            Assert.IsNull(spell.Effects.impactClip);
            Assert.IsNull(spell.Effects.chargeOrLoopClip);
        }

        [Test]
        public void FireIsAChargedOrangeRedBolt()
        {
            var fire = Load("Fire");
            Assert.AreEqual(CastPattern.Charged, fire.Pattern);
            Assert.AreEqual(1.2f, fire.Charge.maxChargeSeconds, 1e-4f);
            Assert.AreEqual(18, fire.Charge.minDamage);
            Assert.AreEqual(45, fire.Charge.maxDamage);
            Assert.AreEqual(0.6f, fire.CooldownSeconds, 1e-4f);
            Assert.AreEqual(26f, fire.Projectile.speed, 1e-4f);
            Assert.That(fire.Projectile.gravity, Is.InRange(0.5f, 4f), "a slight drop");
            Assert.AreEqual(0f, fire.Projectile.curveDegreesPerSecond);
            Assert.AreEqual(1f, fire.Projectile.sizeScale, 1e-4f, "the size comes from the charge feedback");
            Assert.AreEqual(3f, fire.Projectile.lifetimeSeconds, 1e-4f);
            Assert.Greater(fire.Color.r, 0.8f);
            Assert.Less(fire.Color.b, 0.3f);
        }

        [Test]
        public void EarthIsAThreeShotBurstOfSmallBrownBolts()
        {
            var earth = Load("Earth");
            Assert.AreEqual(CastPattern.Burst, earth.Pattern);
            Assert.AreEqual(3, earth.Burst.count);
            Assert.AreEqual(0.08f, earth.Burst.intervalSeconds, 1e-4f);
            Assert.AreEqual(3f, earth.Burst.spreadDegrees, 1e-4f);
            Assert.AreEqual(8, earth.Damage);
            Assert.AreEqual(0.6f, earth.Projectile.sizeScale, 1e-4f);
            Assert.AreEqual(0.7f, earth.CooldownSeconds, 1e-4f);
            Assert.AreEqual(34f, earth.Projectile.speed, 1e-4f);
            Assert.That(earth.Projectile.gravity, Is.InRange(3f, 10f), "a drop");
            Assert.AreEqual(2.5f, earth.Projectile.lifetimeSeconds, 1e-4f);
            Assert.Greater(earth.Color.r, earth.Color.g);
            Assert.Greater(earth.Color.g, earth.Color.b);
        }

        [Test]
        public void WaterIsABlueChannelledBeamWithACooldownThatScalesWithTheChannel()
        {
            var water = Load("Water");
            Assert.AreEqual(CastPattern.Channel, water.Pattern);
            Assert.AreEqual(1.5f, water.Channel.durationSeconds, 1e-4f);
            Assert.AreEqual(10f, water.Channel.ticksPerSecond, 1e-4f);
            Assert.AreEqual(4, water.Channel.damagePerTick);
            Assert.AreEqual(20f, water.Channel.rangeMeters, 1e-4f);
            Assert.AreEqual(0.5f, water.CooldownSeconds, 1e-4f);
            Assert.AreEqual(0.6f, water.Channel.cooldownPerChannelSecond, 1e-4f);
            Assert.Greater(water.Color.b, water.Color.r);
            Assert.Greater(water.Color.b, 0.7f);
        }

        [Test]
        public void TheFourSpellsHaveDistinctColours()
        {
            var colours = new[] { "Base", "Fire", "Earth", "Water" }.Select(n => Load(n).Color).ToArray();
            Assert.AreEqual(4, colours.Distinct().Count());
        }

        private static void WithSandbox(System.Action<Scene> check)
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try { check(scene); }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        private static SpellPickup Pickup(Scene scene, string name)
        {
            var t = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(t, $"{name} missing from the Sandbox.");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            Assert.AreEqual(PickupPrefab, AssetDatabase.GetAssetPath(source), $"{name} is not a Pickup_Spell instance.");
            var pickup = t.GetComponent<SpellPickup>();
            Assert.IsNotNull(pickup);
            return pickup;
        }

        [TestCase("Fire")]
        [TestCase("Earth")]
        [TestCase("Water")]
        public void TheSandboxHasASpellPickupHoldingEachSpell(string name)
        {
            WithSandbox(scene => Assert.AreSame(Load(name), Pickup(scene, $"SpellPickup_{name}").Spell));
        }

        [Test]
        public void TheSpellPickupsLieNearTheSpawnApartFromEachOther()
        {
            WithSandbox(scene =>
            {
                var spawn = scene.GetRootGameObjects().First(r => r.name == "PlayerWizard").transform.position;
                var positions = new[] { "Fire", "Earth", "Water" }
                    .Select(n => Pickup(scene, $"SpellPickup_{n}").transform.position).ToArray();
                foreach (var p in positions)
                    Assert.Less(Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(spawn.x, 0, spawn.z)), 10f);
                for (var i = 0; i < positions.Length; i++)
                    for (var j = i + 1; j < positions.Length; j++)
                        Assert.GreaterOrEqual(Vector3.Distance(positions[i], positions[j]), 2f);
            });
        }

        [Test]
        public void ATrainingDummyIsWithinRangeOfTheSpellPickups()
        {
            WithSandbox(scene =>
            {
                var dummy = scene.GetRootGameObjects().First(r => r.name == "TrainingDummy").transform.position;
                foreach (var n in new[] { "Fire", "Earth", "Water" })
                    Assert.Less(Vector3.Distance(Pickup(scene, $"SpellPickup_{n}").transform.position, dummy), 12f);
            });
        }
    }
}

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

        [TestCase(0f, 18)]
        [TestCase(0.5f, 27)]
        [TestCase(1f, 36)]
        public void FireCurveGivesTheTunedChargedDamage(float charge, int expected) =>
            Assert.AreEqual(expected, ChargeDamage.Damage(Load("Fire").ToCastConfig().Sanitized(), charge));

        [Test]
        public void FireThroughTheActionMachineDoes18OnATapAnd36AtFullCharge()
        {
            var damages = new System.Collections.Generic.List<int>();
            var machine = new HexFall.Player.Actions.ActionMachine(
                new HexFall.Player.Actions.ActionSettings(), new SpellCaster(Load("Fire").ToCastConfig()));
            machine.ShotFired += s => damages.Add(s.Damage);
            var press = new HexFall.Player.Actions.ActionIntent(true, false);
            var release = new HexFall.Player.Actions.ActionIntent(false, false, true);
            var none = new HexFall.Player.Actions.ActionIntent(false, false);

            machine.Tick(press, 1f);
            machine.Tick(release, 1f);
            for (var i = 1; i <= 100; i++) machine.Tick(none, 1f + i * 0.02f);

            machine.Tick(press, 4f);
            for (var i = 1; i <= 60; i++) machine.Tick(none, 4f + i * 0.02f);
            machine.Tick(release, 5.4f);
            for (var i = 1; i <= 100; i++) machine.Tick(none, 5.4f + i * 0.02f);

            CollectionAssert.AreEqual(new[] { 18, 36 }, damages);
        }

        [Test]
        public void FireIsAChargedOrangeRedBolt()
        {
            var fire = Load("Fire");
            Assert.AreEqual(CastPattern.Charged, fire.Pattern);
            Assert.AreEqual(1.2f, fire.Charge.maxChargeSeconds, 1e-4f);
            Assert.AreEqual(ChargeDamageMode.Curve, fire.Charge.damageMode);
            Assert.AreEqual(18, fire.Charge.baseDamage);
            Assert.AreEqual(2.5f, fire.Charge.constantMultiplier, 1e-4f);
            Assert.AreEqual(1f, fire.Charge.damageCurve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(1.5f, fire.Charge.damageCurve.Evaluate(0.5f), 1e-3f);
            Assert.AreEqual(2f, fire.Charge.damageCurve.Evaluate(1f), 1e-4f);
            Assert.AreEqual(0.6f, fire.CooldownSeconds, 1e-4f);
            Assert.AreEqual(42f, fire.Projectile.speed, 1e-4f);
            Assert.AreEqual(0.1f, fire.Projectile.weight, 1e-4f, "a slight drop");
            Assert.AreEqual(0f, fire.Projectile.drag, 1e-4f);
            Assert.AreEqual(1.4f, fire.Charge.speedScaleAtMax, 1e-4f);
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
            Assert.AreEqual(20f, earth.Projectile.speed, 1e-4f);
            Assert.AreEqual(1f, earth.Projectile.weight, 1e-4f, "falls like a normal object");
            Assert.AreEqual(0.3f, earth.Projectile.drag, 1e-4f);
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
        public void ProjectileSpeedAndWeightOrderTheSpellsFireFastestEarthSlowestAndHeaviest()
        {
            var fire = Load("Fire").Projectile;
            var baseBolt = Load("Base").Projectile;
            var earth = Load("Earth").Projectile;
            Assert.Greater(fire.speed, baseBolt.speed);
            Assert.Greater(baseBolt.speed, earth.speed);
            Assert.Greater(earth.weight, fire.weight);
            Assert.Greater(fire.weight, 0f);
            Assert.AreEqual(0f, baseBolt.weight, 1e-6f);
            Assert.AreEqual(30f, baseBolt.speed, 1e-4f);
            Assert.AreEqual(0f, baseBolt.drag, 1e-6f);
        }

        [Test]
        public void WaterIsABeamSoItsProjectileSettingsStayWeightless()
        {
            var water = Load("Water");
            Assert.AreEqual(CastPattern.Channel, water.Pattern);
            Assert.AreEqual(0f, water.Projectile.weight, 1e-6f);
            Assert.AreEqual(0f, water.Projectile.drag, 1e-6f);
        }

        [Test]
        public void EarthStonesStillReachFifteenToTwentyMetersBeforeTheyExpire()
        {
            var p = Load("Earth").Projectile;
            var flight = new ProjectileFlight(Vector3.zero, new Vector3(0f, 0f, p.speed), p.weight * 9.81f, 0f, p.lifetimeSeconds, p.drag);
            var steps = (int)(p.lifetimeSeconds / 0.02f) + 2;
            var at15 = float.NaN;
            for (var i = 0; i < steps && !flight.IsExpired; i++)
            {
                flight.Step(0.02f);
                if (float.IsNaN(at15) && flight.Position.z >= 15f) at15 = flight.Age;
            }
            Assert.IsFalse(float.IsNaN(at15), "reaches 15 m of horizontal travel");
            Assert.Less(at15, p.lifetimeSeconds, "before the lifetime ends");
            Assert.Greater(flight.Position.z, 20f, "and carries past 20 m if nothing stops it");
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

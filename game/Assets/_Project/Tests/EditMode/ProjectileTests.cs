using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The bolt flies the spell's flight, is pooled, tinted and hands a full HitInfo to what it hits.
    public class ProjectileTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            BoltPool.Clear();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

        private Bolt NewPrefab()
        {
            var go = Track(new GameObject("BoltPrefab"));
            go.transform.localScale = Vector3.one * 0.25f;
            go.AddComponent<MeshRenderer>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.widthMultiplier = 1f;
            go.SetActive(false);
            return go.AddComponent<Bolt>();
        }

        private SpellDefinition NewSpell(Color color, SpellDefinition.SpellEffects effects = default)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "color", color);
            Set(spell, "effects", effects);
            return spell;
        }

        private static SpellDefinition.ProjectileSettings Settings(float speed = 30f, float gravity = 0f, float curve = 0f,
            float size = 1f, float lifetime = 3f) => new SpellDefinition.ProjectileSettings
        {
            speed = speed, gravity = gravity, curveDegreesPerSecond = curve, sizeScale = size, lifetimeSeconds = lifetime,
        };

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public void TakeHit(HitInfo hit) => Hits.Add(hit);
        }

        private Target NewTarget(Vector3 at)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.position = at;
            Physics.SyncTransforms();
            return go.AddComponent<Target>();
        }

        // ---- HitInfo / effect plan ----

        [Test]
        public void HitInfoCarriesWhatTheHitKnows()
        {
            var spell = NewSpell(Color.red);
            var source = Track(new GameObject("src"));
            var hit = new HitInfo(12, source, new Vector3(1, 2, 3), Vector3.up, spell);
            Assert.AreEqual(12, hit.Damage);
            Assert.AreSame(source, hit.Source);
            Assert.AreEqual(new Vector3(1, 2, 3), hit.Point);
            Assert.AreEqual(Vector3.up, hit.Normal);
            Assert.AreSame(spell, hit.Spell);
        }

        [Test]
        public void NoEffectsMeansNothingToSpawn()
        {
            Assert.IsEmpty(HitEffectPlan.Select(true, default));
            Assert.IsEmpty(HitEffectPlan.Select(false, default));
        }

        [Test]
        public void ADamageableHitSpawnsTheImpactAndTheEnemyHitEffect()
        {
            var impact = Track(new GameObject("impact"));
            var enemy = Track(new GameObject("enemy"));
            var surface = Track(new GameObject("surface"));
            var effects = new SpellDefinition.SpellEffects
            {
                impactEffect = impact, enemyHitEffect = enemy, surfaceImpactParticles = surface,
            };
            CollectionAssert.AreEqual(new[] { impact, enemy }, HitEffectPlan.Select(true, effects));
        }

        [Test]
        public void ASurfaceHitSpawnsTheImpactTheParticlesAndTheDecal()
        {
            var impact = Track(new GameObject("impact"));
            var enemy = Track(new GameObject("enemy"));
            var particles = Track(new GameObject("particles"));
            var decal = Track(new GameObject("decal"));
            var effects = new SpellDefinition.SpellEffects
            {
                impactEffect = impact, enemyHitEffect = enemy, surfaceImpactParticles = particles, surfaceDecal = decal,
            };
            CollectionAssert.AreEqual(new[] { impact, particles, decal }, HitEffectPlan.Select(false, effects));
        }

        // ---- Pool ----

        [Test]
        public void ReleasedBoltsAreReusedNotRecreated()
        {
            var prefab = NewPrefab();
            var first = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(first.gameObject);
            Assert.IsTrue(first.gameObject.activeSelf);
            BoltPool.Release(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            var second = BoltPool.Get(prefab, Vector3.one, Quaternion.identity);
            Assert.AreSame(first, second);
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.AreEqual(Vector3.one, second.transform.position);
        }

        [Test]
        public void ADestroyedPooledBoltIsSkipped()
        {
            var prefab = NewPrefab();
            var first = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            BoltPool.Release(first);
            Object.DestroyImmediate(first.gameObject);
            var second = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(second.gameObject);
            Assert.IsNotNull(second);
            Assert.IsTrue(second.gameObject.activeSelf);
        }

        [Test]
        public void DifferentPrefabsKeepSeparatePools()
        {
            var a = NewPrefab();
            var b = NewPrefab();
            var fromA = BoltPool.Get(a, Vector3.zero, Quaternion.identity);
            Track(fromA.gameObject);
            BoltPool.Release(fromA);
            var fromB = BoltPool.Get(b, Vector3.zero, Quaternion.identity);
            Track(fromB.gameObject);
            Assert.AreNotSame(fromA, fromB);
        }

        [Test]
        public void ReleasingABoltThatDidNotComeFromThePoolDestroysIt()
        {
            var loose = NewPrefab();
            loose.gameObject.SetActive(true);
            BoltPool.Release(loose);
            Assert.IsTrue(loose == null, "a loose bolt (DummyShooter) is destroyed, not pooled");
        }

        // ---- Launch: size and tint ----

        [Test]
        public void LaunchScalesTheVisualAndResetsItOnReuse()
        {
            var prefab = NewPrefab();
            var bolt = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            bolt.Launch(null, Vector3.forward, Settings(size: 2f), 10, null);
            Assert.AreEqual(0.5f, bolt.transform.localScale.x, 1e-4f);
            BoltPool.Release(bolt);
            var again = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            again.Launch(null, Vector3.forward, Settings(size: 1f), 10, null);
            Assert.AreEqual(0.25f, again.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void ASpellTintsTheMeshAndTheTrailAndABaseLaunchClearsIt()
        {
            var prefab = NewPrefab();
            var bolt = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            var spell = NewSpell(new Color(1f, 0.4f, 0.1f));
            bolt.Launch(null, Vector3.forward, Settings(), 10, spell);

            var block = new MaterialPropertyBlock();
            bolt.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.AreEqual(0.4f, block.GetColor("_BaseColor").g, 1e-4f);
            Assert.AreEqual(0.4f, block.GetColor("_Color").g, 1e-4f);
            var keys = bolt.GetComponent<TrailRenderer>().colorGradient.colorKeys;
            Assert.AreEqual(0.4f, keys[0].color.g, 1e-3f);

            bolt.Launch(null, Vector3.forward, Settings(), 10, null);
            bolt.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.IsTrue(block.isEmpty, "a launch without a spell keeps the prefab look");
        }

        // ---- Flight and hits ----

        [Test]
        public void AdvanceFliesTheSpellsFlightAndDropsWithGravity()
        {
            var prefab = NewPrefab();
            var b = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(b.gameObject);
            b.Launch(null, Vector3.forward, Settings(speed: 10f, gravity: 10f), 5, null);
            for (var i = 0; i < 10; i++) b.Advance(0.1f);
            Assert.AreEqual(10f, b.transform.position.z, 1e-3f);
            Assert.AreEqual(-5.5f, b.transform.position.y, 1e-3f);
        }

        [Test]
        public void ABoltPastItsLifetimeGoesBackToThePool()
        {
            var prefab = NewPrefab();
            var b = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(b.gameObject);
            b.Launch(null, Vector3.forward, Settings(lifetime: 0.2f), 5, null);
            b.Advance(0.1f);
            Assert.IsTrue(b.gameObject.activeSelf);
            b.Advance(0.1f);
            b.Advance(0.1f);
            Assert.IsFalse(b.gameObject.activeSelf);
        }

        [Test]
        public void AHitDeliversDamageSourcePointNormalAndSpellThenReturnsTheBolt()
        {
            var target = NewTarget(new Vector3(0f, 0f, 2f)); // cube spans z 1.5 .. 2.5
            var owner = Track(new GameObject("owner"));
            var spell = NewSpell(Color.cyan);
            var prefab = NewPrefab();
            var bolt = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            bolt.Launch(owner, Vector3.forward, Settings(speed: 30f), 18, spell);
            bolt.Advance(0.1f);

            Assert.AreEqual(1, target.Hits.Count);
            var hit = target.Hits[0];
            Assert.AreEqual(18, hit.Damage);
            Assert.AreSame(owner, hit.Source);
            Assert.AreSame(spell, hit.Spell);
            Assert.AreEqual(1.5f, hit.Point.z, 0.05f);
            Assert.Less(hit.Normal.z, -0.9f);
            Assert.IsFalse(bolt.gameObject.activeSelf);
        }

        [Test]
        public void ADummyShooterBoltHasNoSpellInItsHit()
        {
            var target = NewTarget(new Vector3(0f, 0f, 2f));
            var prefab = NewPrefab();
            var bolt = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            bolt.Launch(null, Vector3.forward, 30f, 10);
            bolt.Advance(0.1f);
            Assert.AreEqual(1, target.Hits.Count);
            Assert.IsNull(target.Hits[0].Spell);
            Assert.AreEqual(10, target.Hits[0].Damage);
        }

        [Test]
        public void TheSizeScaleWidensTheSweep()
        {
            var target = NewTarget(new Vector3(0.75f, 0f, 2f)); // its near face (x 0.25) is 0.1 m clear of a 0.15 radius sweep
            var prefab = NewPrefab();
            var thin = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(thin.gameObject);
            thin.Launch(null, Vector3.forward, Settings(size: 1f), 5, null);
            thin.Advance(0.1f);
            Assert.AreEqual(0, target.Hits.Count);

            var wide = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(wide.gameObject);
            wide.Launch(null, Vector3.forward, Settings(size: 3f), 5, null);
            wide.Advance(0.1f);
            Assert.AreEqual(1, target.Hits.Count);
        }

        [Test]
        public void AHitSpawnsTheSpellsImpactEffectAtThePoint()
        {
            var target = NewTarget(new Vector3(0f, 0f, 2f));
            var marker = Track(new GameObject("ImpactMarker"));
            var spell = NewSpell(Color.white, new SpellDefinition.SpellEffects { impactEffect = marker });
            var prefab = NewPrefab();
            var bolt = BoltPool.Get(prefab, Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            bolt.Launch(null, Vector3.forward, Settings(), 5, spell);
            bolt.Advance(0.1f);
            Assert.AreEqual(1, target.Hits.Count);
            var clones = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>())
            {
                if (go.name != "ImpactMarker(Clone)") continue;
                clones++;
                Track(go);
                Assert.AreEqual(1.5f, go.transform.position.z, 0.05f);
            }
            Assert.AreEqual(1, clones);
        }
    }
}

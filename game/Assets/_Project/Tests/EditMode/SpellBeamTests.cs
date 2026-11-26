using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The channelled beam: a ray from the muzzle capped at the spell range that deals each tick's damage
    // to the first thing it meets, and a stretched visual tinted with the spell colour.
    public class SpellBeamTests
    {
        private readonly List<Object> _made = new List<Object>();
        private SpellBeam _beam;
        private Transform _pivot;
        private MeshRenderer _visual;
        private GameObject _owner;
        private SpellDefinition _spell;

        private T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _owner = Track(new GameObject("Owner"));
            var go = Track(new GameObject("Beam"));
            _pivot = Track(new GameObject("Pivot")).transform;
            _pivot.SetParent(go.transform, false);
            var cyl = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(_pivot, false);
            _pivot.gameObject.SetActive(false); // the prefab ships it hidden
            _visual = cyl.GetComponent<MeshRenderer>();
            _beam = go.AddComponent<SpellBeam>();
            Set(_beam, "pivot", _pivot);
            Set(_beam, "visual", _visual);
            _spell = NewSpell(new Color(0.2f, 0.4f, 1f), 20f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private SpellDefinition NewSpell(Color color, float range, SpellDefinition.SpellEffects effects = default)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "color", color);
            Set(spell, "effects", effects);
            Set(spell, "channel", new SpellDefinition.ChannelSettings
            {
                durationSeconds = 1.5f, ticksPerSecond = 10f, damagePerTick = 4, rangeMeters = range, cooldownPerChannelSecond = 0.6f,
            });
            return spell;
        }

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public void TakeHit(HitInfo hit) => Hits.Add(hit);
        }

        private GameObject Cube(Vector3 at)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.position = at;
            Physics.SyncTransforms();
            return go;
        }

        private Target DamageableCube(Vector3 at) => Cube(at).AddComponent<Target>();

        private static readonly Vector3 Muzzle = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 Ahead = new Vector3(0f, 0f, 50f);

        // ---- Damage ----

        [Test]
        public void ATickDamagesTheTargetUnderTheBeamWithTheSpellPointAndNormal()
        {
            var target = DamageableCube(new Vector3(0f, 0f, 5f)); // near face at z 4.5
            _beam.Begin(_owner, _spell);
            _beam.Pulse(Muzzle, Ahead, 4);

            Assert.AreEqual(1, target.Hits.Count);
            var hit = target.Hits[0];
            Assert.AreEqual(4, hit.Damage);
            Assert.AreSame(_owner, hit.Source);
            Assert.AreSame(_spell, hit.Spell);
            Assert.AreEqual(4.5f, hit.Point.z, 0.01f);
            Assert.Less(hit.Normal.z, -0.9f);
        }

        [Test]
        public void EveryTickDamagesAgain()
        {
            var target = DamageableCube(new Vector3(0f, 0f, 5f));
            _beam.Begin(_owner, _spell);
            _beam.Pulse(Muzzle, Ahead, 4);
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(2, target.Hits.Count);
        }

        [Test]
        public void ATargetBeyondTheRangeIsNotHit()
        {
            var target = DamageableCube(new Vector3(0f, 0f, 25f));
            _beam.Begin(_owner, _spell); // range 20
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(0, target.Hits.Count);
        }

        [Test]
        public void TheFirstSurfaceStopsTheBeamSoNothingBehindItIsHit()
        {
            Cube(new Vector3(0f, 0f, 3f));
            var behind = DamageableCube(new Vector3(0f, 0f, 8f));
            _beam.Begin(_owner, _spell);
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(0, behind.Hits.Count);
        }

        [Test]
        public void TheOwnersCollidersAreIgnored()
        {
            var own = Cube(new Vector3(0f, 0f, 2f));
            own.transform.SetParent(_owner.transform, true);
            Physics.SyncTransforms();
            var target = DamageableCube(new Vector3(0f, 0f, 6f));
            _beam.Begin(_owner, _spell);
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(1, target.Hits.Count);
        }

        [Test]
        public void ATickOnAWorldSurfaceDealsNothingAndSpawnsTheSurfaceEffects()
        {
            Cube(new Vector3(0f, 0f, 5f));
            var impact = Track(new GameObject("ImpactMarker"));
            var enemy = Track(new GameObject("EnemyMarker"));
            var surface = Track(new GameObject("SurfaceMarker"));
            var spell = NewSpell(Color.blue, 20f, new SpellDefinition.SpellEffects
            {
                impactEffect = impact, enemyHitEffect = enemy, surfaceImpactParticles = surface,
            });
            _beam.Begin(_owner, spell);
            _beam.Pulse(Muzzle, Ahead, 4);

            Assert.AreEqual(1, Clones("ImpactMarker(Clone)"));
            Assert.AreEqual(1, Clones("SurfaceMarker(Clone)"));
            Assert.AreEqual(0, Clones("EnemyMarker(Clone)"));
        }

        [Test]
        public void ATickOnADamageableSpawnsTheImpactAndEnemyHitEffects()
        {
            DamageableCube(new Vector3(0f, 0f, 5f));
            var impact = Track(new GameObject("ImpactMarker"));
            var enemy = Track(new GameObject("EnemyMarker"));
            var surface = Track(new GameObject("SurfaceMarker"));
            var spell = NewSpell(Color.blue, 20f, new SpellDefinition.SpellEffects
            {
                impactEffect = impact, enemyHitEffect = enemy, surfaceImpactParticles = surface,
            });
            _beam.Begin(_owner, spell);
            _beam.Pulse(Muzzle, Ahead, 4);

            Assert.AreEqual(1, Clones("ImpactMarker(Clone)"));
            Assert.AreEqual(1, Clones("EnemyMarker(Clone)"));
            Assert.AreEqual(0, Clones("SurfaceMarker(Clone)"));
        }

        [Test]
        public void ATickWithNothingInRangeDealsNothingAndSpawnsNothing()
        {
            var impact = Track(new GameObject("ImpactMarker"));
            var spell = NewSpell(Color.blue, 20f, new SpellDefinition.SpellEffects { impactEffect = impact });
            _beam.Begin(_owner, spell);
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(0, Clones("ImpactMarker(Clone)"));
        }

        [Test]
        public void AnInactiveBeamIgnoresTicks()
        {
            var target = DamageableCube(new Vector3(0f, 0f, 5f));
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(0, target.Hits.Count);

            _beam.Begin(_owner, _spell);
            _beam.End();
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(0, target.Hits.Count);
        }

        // ---- Visual ----

        [Test]
        public void TheVisualIsHiddenUntilTheChannelBeginsAndHiddenAgainWhenItEnds()
        {
            Assert.IsFalse(_beam.IsActive);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
            _beam.Begin(_owner, _spell);
            Assert.IsTrue(_beam.IsActive);
            Assert.IsTrue(_pivot.gameObject.activeSelf);
            _beam.End();
            Assert.IsFalse(_beam.IsActive);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
        }

        [Test]
        public void RefreshStretchesTheVisualToTheRangeOrToTheSurface()
        {
            _beam.Begin(_owner, _spell);
            _beam.Refresh(Muzzle, Ahead);
            Assert.AreEqual(20f, _beam.Length, 1e-3f);
            Assert.AreEqual(20f, _pivot.lossyScale.z, 1e-3f);
            Assert.AreEqual(Vector3.zero, _pivot.position);

            Cube(new Vector3(0f, 0f, 5f));
            _beam.Refresh(Muzzle, Ahead);
            Assert.AreEqual(4.5f, _beam.Length, 0.01f);
            Assert.AreEqual(4.5f, _pivot.lossyScale.z, 0.01f);
        }

        [Test]
        public void RefreshPointsTheVisualAlongTheAim()
        {
            _beam.Begin(_owner, _spell);
            _beam.Refresh(new Vector3(1f, 2f, 3f), new Vector3(11f, 2f, 3f));
            var forward = _pivot.rotation * Vector3.forward;
            Assert.AreEqual(1f, forward.x, 1e-4f);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _pivot.position);
        }

        // The beam is drawn after the frame's animation, facing, locomotion and camera: it reads the
        // origin and aim from its sources at that moment, not from values captured earlier in the frame.
        [Test]
        public void TheBeamFollowsItsSourcesAtTheEndOfTheFrame()
        {
            var anchor = Track(new GameObject("Anchor")).transform;
            var aim = new Vector3(10f, 0f, 0f);
            _beam.Begin(_owner, _spell, () => anchor.position, () => aim);

            anchor.position = new Vector3(0f, 2f, 0f); // animator and locomotion moved it after Update
            aim = new Vector3(0f, 2f, 10f);            // the camera moved after Update
            _beam.UpdateToFinalPose();

            Assert.AreEqual(new Vector3(0f, 2f, 0f), _pivot.position);
            Assert.AreEqual(1f, (_pivot.rotation * Vector3.forward).z, 1e-4f);
        }

        [Test]
        public void QueuedPulsesHitAlongTheFinalPoseOfTheFrame()
        {
            var enemy = Track(new GameObject("Enemy"));
            enemy.transform.position = new Vector3(0f, 0f, 5f);
            enemy.AddComponent<BoxCollider>();
            var target = enemy.AddComponent<Target>();
            var origin = Vector3.zero;
            var aim = new Vector3(10f, 0f, 0f); // last frame's pose misses the enemy
            _beam.Begin(_owner, _spell, () => origin, () => aim);

            _beam.QueuePulse(4);                // the tick fires in Update
            aim = new Vector3(0f, 0f, 10f);     // the final pose points at it
            _beam.UpdateToFinalPose();

            Assert.AreEqual(1, target.Hits.Count);
            Assert.AreEqual(4, target.Hits[0].Damage);

            _beam.UpdateToFinalPose();          // the queue is spent
            Assert.AreEqual(1, target.Hits.Count);
        }

        private Target EnemyAt5()
        {
            var enemy = Track(new GameObject("Enemy"));
            enemy.transform.position = new Vector3(0f, 0f, 5f);
            enemy.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            return enemy.AddComponent<Target>();
        }

        // A tick the machine fired in the frame the channel ends (expiry, release, parry, swap) was emitted,
        // so it lands: End plays the queued ticks along the latest pose, then hides the beam.
        [Test]
        public void ATickFiredInTheFrameTheChannelEndsStillLands()
        {
            var target = EnemyAt5();
            var aim = new Vector3(0f, 0f, 10f);
            _beam.Begin(_owner, _spell, () => Vector3.zero, () => aim);

            _beam.QueuePulse(4);
            _beam.End();

            Assert.AreEqual(1, target.Hits.Count);
            Assert.AreEqual(4, target.Hits[0].Damage);
            Assert.IsFalse(_beam.IsActive);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
            _beam.UpdateToFinalPose();
            Assert.AreEqual(1, target.Hits.Count, "the queue is spent");
        }

        // ---- Teardown: the sources may already be gone and nothing may be dealt ----

        private void Invoke(string method) =>
            typeof(SpellBeam).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(_beam, null);

        [Test]
        public void EndAfterTheSourceTransformIsDestroyedDoesNotThrowAndDealsNothing()
        {
            var target = EnemyAt5();
            var anchor = Track(new GameObject("Anchor")).transform;
            var aim = new Vector3(0f, 0f, 10f);
            _beam.Begin(_owner, _spell, () => anchor.position, () => aim);
            _beam.QueuePulse(4);

            Object.DestroyImmediate(anchor.gameObject);

            Assert.DoesNotThrow(() => _beam.End());
            Assert.AreEqual(0, target.Hits.Count);
            Assert.IsFalse(_beam.IsActive);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
        }

        [Test]
        public void EndAfterTheOwnerIsDestroyedHidesWithoutPulsing()
        {
            var target = EnemyAt5();
            var aim = new Vector3(0f, 0f, 10f);
            _beam.Begin(_owner, _spell, () => Vector3.zero, () => aim);
            _beam.QueuePulse(4);

            Object.DestroyImmediate(_owner);

            Assert.DoesNotThrow(() => _beam.End());
            Assert.AreEqual(0, target.Hits.Count);
            Assert.IsFalse(_beam.IsActive);
        }

        [Test]
        public void TheEndOfFrameUpdateWithADestroyedSourceDoesNotThrow()
        {
            var anchor = Track(new GameObject("Anchor")).transform;
            _beam.Begin(_owner, _spell, () => anchor.position, () => Vector3.forward);
            Object.DestroyImmediate(anchor.gameObject);

            Assert.DoesNotThrow(() => _beam.UpdateToFinalPose());
        }

        [Test]
        public void DisablingTheBeamMidChannelHidesItWithoutThrowingOrDealingDamage()
        {
            var target = EnemyAt5();
            var aim = new Vector3(0f, 0f, 10f);
            _beam.Begin(_owner, _spell, () => Vector3.zero, () => aim);
            _pivot.gameObject.SetActive(true);
            _beam.QueuePulse(4);

            Assert.DoesNotThrow(() => Invoke("OnDisable"));

            Assert.AreEqual(0, target.Hits.Count, "teardown flushes nothing");
            Assert.IsFalse(_beam.IsActive);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
        }

        [Test]
        public void AFullChannelDealsOneHitPerTickFired()
        {
            var target = EnemyAt5();
            var aim = new Vector3(0f, 0f, 10f);
            _beam.Begin(_owner, _spell, () => Vector3.zero, () => aim);
            for (var i = 0; i < 14; i++)
            {
                _beam.QueuePulse(4);
                _beam.UpdateToFinalPose();
            }
            _beam.QueuePulse(4); // the 15th tick fires in the same step as the end
            _beam.End();
            Assert.AreEqual(15, target.Hits.Count);
        }

        // Two-argument Begin has no sources: a tick pulses at once along the last pose it was refreshed with
        // and never piles up in a queue nobody drains.
        [Test]
        public void WithoutSourcesATickPulsesAtOnceAlongTheLastRefreshedPose()
        {
            var target = EnemyAt5();
            _beam.Begin(_owner, _spell);
            _beam.Refresh(Muzzle, new Vector3(0f, 0f, 10f));

            _beam.QueuePulse(4);

            Assert.AreEqual(1, target.Hits.Count);
            _beam.UpdateToFinalPose();
            _beam.End();
            Assert.AreEqual(1, target.Hits.Count, "nothing was left queued");
        }

        [Test]
        public void WithoutSourcesAndWithoutAPoseATickIsDroppedNotQueued()
        {
            var target = EnemyAt5();
            _beam.Begin(_owner, _spell);
            _beam.QueuePulse(4);
            _beam.Refresh(Muzzle, new Vector3(0f, 0f, 10f));
            _beam.End();
            Assert.AreEqual(0, target.Hits.Count);
        }

        [Test]
        public void TheVisualIsTintedWithTheSpellColour()
        {
            _beam.Begin(_owner, _spell);
            var block = new MaterialPropertyBlock();
            _visual.GetPropertyBlock(block);
            Assert.AreEqual(0.4f, block.GetColor("_BaseColor").g, 1e-4f);
            Assert.AreEqual(1f, block.GetColor("_Color").b, 1e-4f);
        }

        [Test]
        public void RefreshWhileInactiveDoesNothing()
        {
            _beam.Refresh(Muzzle, Ahead);
            Assert.IsFalse(_pivot.gameObject.activeSelf);
            Assert.AreEqual(0f, _beam.Length);
        }

        [Test]
        public void ABeamWithoutAVisualStillDamages()
        {
            Set(_beam, "pivot", null);
            Set(_beam, "visual", null);
            var target = DamageableCube(new Vector3(0f, 0f, 5f));
            _beam.Begin(_owner, _spell);
            _beam.Refresh(Muzzle, Ahead);
            _beam.Pulse(Muzzle, Ahead, 4);
            Assert.AreEqual(1, target.Hits.Count);
            _beam.End();
        }

        private int Clones(string name)
        {
            var count = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>())
            {
                if (go.name != name) continue;
                count++;
                Track(go);
            }
            return count;
        }
    }
}

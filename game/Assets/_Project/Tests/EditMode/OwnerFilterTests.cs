using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The owner filter of bolts and beams: everything under the owner is ignored, but a sibling of the
    // owner under a shared scene container is a valid target (the old root-based check skipped it).
    public class OwnerFilterTests
    {
        private readonly List<Object> _made = new List<Object>();
        private GameObject _container;
        private GameObject _owner;

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
            _container = Track(new GameObject("Level"));
            _owner = Track(new GameObject("Owner"));
            _owner.transform.SetParent(_container.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            BoltPool.Clear();
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public void TakeHit(HitInfo hit) => Hits.Add(hit);
        }

        private GameObject Cube(Vector3 at, Transform parent)
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            Physics.SyncTransforms();
            return go;
        }

        // ---- The rule itself ----

        [Test]
        public void TheOwnerItsChildrenAndTheirChildrenAreOwned()
        {
            var child = Track(new GameObject("Child"));
            child.transform.SetParent(_owner.transform, false);
            var grandchild = Track(new GameObject("Grandchild"));
            grandchild.transform.SetParent(child.transform, false);
            Assert.IsTrue(OwnerFilter.Owns(_owner, _owner.transform));
            Assert.IsTrue(OwnerFilter.Owns(_owner, child.transform));
            Assert.IsTrue(OwnerFilter.Owns(_owner, grandchild.transform));
        }

        [Test]
        public void ASiblingUnderTheSameContainerIsNotOwned()
        {
            var sibling = Track(new GameObject("Sibling"));
            sibling.transform.SetParent(_container.transform, false);
            Assert.IsFalse(OwnerFilter.Owns(_owner, sibling.transform));
            Assert.IsFalse(OwnerFilter.Owns(_owner, _container.transform));
        }

        [Test]
        public void NoOwnerOwnsNothing()
        {
            Assert.IsFalse(OwnerFilter.Owns(null, _owner.transform));
        }

        // ---- Beam ----

        private SpellBeam NewBeam(out SpellDefinition spell)
        {
            var go = Track(new GameObject("Beam"));
            go.transform.SetParent(_owner.transform, false);
            var beam = go.AddComponent<SpellBeam>();
            spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "channel", new SpellDefinition.ChannelSettings
            {
                durationSeconds = 1.5f, ticksPerSecond = 10f, damagePerTick = 4, rangeMeters = 20f, cooldownPerChannelSecond = 0.6f,
            });
            return beam;
        }

        [Test]
        public void ABeamHitsASiblingOfTheOwnerUnderASharedContainer()
        {
            var beam = NewBeam(out var spell);
            Cube(new Vector3(0f, 0f, 2f), _owner.transform); // the owner's own body is in front
            var sibling = Cube(new Vector3(0f, 0f, 6f), _container.transform).AddComponent<Target>();
            beam.Begin(_owner, spell);
            beam.Pulse(Vector3.zero, new Vector3(0f, 0f, 50f), 4);
            Assert.AreEqual(1, sibling.Hits.Count);
        }

        // ---- Bolt ----

        private Bolt NewBoltPrefab()
        {
            var go = Track(new GameObject("BoltPrefab"));
            go.transform.localScale = Vector3.one * 0.25f;
            go.AddComponent<MeshRenderer>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.widthMultiplier = 1f;
            go.SetActive(false);
            return go.AddComponent<Bolt>();
        }

        [Test]
        public void ABoltHitsASiblingOfTheOwnerUnderASharedContainerButNotTheOwnersBody()
        {
            Cube(new Vector3(0f, 0f, 2f), _owner.transform);
            var sibling = Cube(new Vector3(0f, 0f, 6f), _container.transform).AddComponent<Target>();
            var bolt = BoltPool.Get(NewBoltPrefab(), Vector3.zero, Quaternion.identity);
            Track(bolt.gameObject);
            bolt.Launch(_owner, Vector3.forward, new SpellDefinition.ProjectileSettings
            {
                speed = 60f, sizeScale = 1f, lifetimeSeconds = 3f,
            }, 18, null);
            bolt.Advance(0.1f);
            Assert.AreEqual(1, sibling.Hits.Count);
        }
    }
}

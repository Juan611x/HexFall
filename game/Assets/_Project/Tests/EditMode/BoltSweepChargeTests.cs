using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // Decision 2026-11-25: the charge scale also widens the bolt's hit sweep (a bigger orb hits a wider area).
    // Pinned here: the sweep radius is radius x sizeScale x charge scale, and a low-charge bolt misses a target
    // that a full-charge bolt hits.
    public class BoltSweepChargeTests
    {
        private readonly List<Object> _made = new List<Object>();

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public int Hits;
            public void TakeHit(HitInfo hit) => Hits++;
        }

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

        // Charge scale 0.5 at no charge, 2 at full charge.
        private SpellDefinition Fire()
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "pattern", CastPattern.Charged);
            Set(spell, "charge", new SpellDefinition.ChargeSettings
            {
                maxChargeSeconds = 1.2f, baseDamage = 18, minSizeScale = 0.5f, maxSizeScale = 2f, fullChargeColor = Color.white, speedScaleAtMax = 1f,
            });
            return spell;
        }

        private static SpellDefinition.ProjectileSettings Flat(float sizeScale = 1f) => new SpellDefinition.ProjectileSettings
        {
            speed = 10f, weight = 0f, drag = 0f, curveDegreesPerSecond = 0f, sizeScale = sizeScale, lifetimeSeconds = 3f,
        };

        private Bolt NewBolt()
        {
            var go = Track(new GameObject("Bolt"));
            go.AddComponent<MeshRenderer>();
            go.AddComponent<TrailRenderer>();
            return go.AddComponent<Bolt>();
        }

        [Test]
        public void TheSweepRadiusIsTheBaseRadiusTimesTheSizeScaleTimesTheChargeScale()
        {
            var spell = Fire();
            var bolt = NewBolt();
            var radius = (float)typeof(Bolt).GetField("radius", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bolt);

            bolt.Launch(null, Vector3.forward, Flat(1f), 18, spell, 0f);
            Assert.AreEqual(radius * 0.5f, bolt.SweepRadius, 1e-4f);

            bolt.Launch(null, Vector3.forward, Flat(1f), 45, spell, 1f);
            Assert.AreEqual(radius * 2f, bolt.SweepRadius, 1e-4f);

            bolt.Launch(null, Vector3.forward, Flat(1.5f), 45, spell, 1f);
            Assert.AreEqual(radius * 1.5f * 2f, bolt.SweepRadius, 1e-4f);
        }

        // A box whose near face is 0.2 m beside the bolt's path: inside a full-charge sweep, outside a low one.
        private Target SideTarget()
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.transform.position = new Vector3(0.7f, 0f, 5f);
            go.transform.localScale = new Vector3(1f, 2f, 2f);
            Physics.SyncTransforms();
            return go.AddComponent<Target>();
        }

        [Test]
        public void ALowChargeBoltMissesATargetAFullChargeBoltHits()
        {
            var spell = Fire();
            var target = SideTarget();
            var lowBolt = NewBolt();
            lowBolt.Launch(null, Vector3.forward, Flat(), 18, spell, 0f);
            lowBolt.Advance(1f);
            Assert.AreEqual(0, target.Hits, "radius 0.075 m passes 0.2 m beside the box");

            var fullBolt = NewBolt();
            fullBolt.Launch(null, Vector3.forward, Flat(), 45, spell, 1f);
            fullBolt.Advance(1f);
            Assert.AreEqual(1, target.Hits, "radius 0.3 m reaches the box");
        }
    }
}

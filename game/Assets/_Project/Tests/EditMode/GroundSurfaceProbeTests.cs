using System.Collections.Generic;
using HexFall.Player;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class GroundSurfaceProbeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private SurfaceRegistry _registry;
        private SurfaceDefinition _stone;
        private SurfaceDefinition _fallback;
        private Texture2D _stoneTex;
        private GameObject _floor;
        private GameObject _owner;
        private GroundSurfaceProbe _probe;

        [SetUp]
        public void SetUp()
        {
            _registry = Track(ScriptableObject.CreateInstance<SurfaceRegistry>());
            _stone = Track(ScriptableObject.CreateInstance<SurfaceDefinition>());
            _fallback = Track(ScriptableObject.CreateInstance<SurfaceDefinition>());
            _stoneTex = Track(new Texture2D(1, 1));
            _registry.SetEntries(new[] { new SurfaceTextureEntry { texture = _stoneTex, surface = _stone } }, _fallback);

            Shader shader = null;
            foreach (var shaderName in new[] { "Universal Render Pipeline/Lit", "HexFall/PSX Lit", "Sprites/Default" })
            {
                shader = Shader.Find(shaderName);
                if (shader != null) break;
            }
            var material = Track(new Material(shader));
            material.mainTexture = _stoneTex;

            // Top face at y = 0, far from the origin so other test objects do not interfere.
            _floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            _floor.transform.position = new Vector3(1000f, -0.5f, 1000f);
            _floor.transform.localScale = new Vector3(10f, 1f, 10f);
            _floor.GetComponent<MeshRenderer>().sharedMaterial = material;

            _owner = Track(new GameObject("Owner"));
            _owner.transform.position = new Vector3(1000f, 0f, 1000f);
            Physics.SyncTransforms();

            _probe = new GroundSurfaceProbe(new MeshSurfaceResolver(_registry), _registry, 1, 0.3f, 0.6f, _owner.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        [Test]
        public void SurfaceAt_OnMappedFloor_ReturnsItsSurface()
        {
            Assert.AreSame(_stone, _probe.SurfaceAt(_owner.transform.position));
        }

        // A level container parenting both the wizard and the ground must not hide the ground.
        [Test]
        public void SurfaceAt_OwnerAndFloorUnderSharedRoot_StillFindsTheFloor()
        {
            var level = Track(new GameObject("Level"));
            _floor.transform.SetParent(level.transform, true);
            _owner.transform.SetParent(level.transform, true);
            Physics.SyncTransforms();

            Assert.AreSame(_stone, _probe.SurfaceAt(_owner.transform.position));
        }

        [Test]
        public void SurfaceAt_NothingBelow_ReturnsDefault()
        {
            Assert.AreSame(_fallback, _probe.SurfaceAt(_owner.transform.position + Vector3.up * 5f));
        }

        [Test]
        public void SurfaceAt_OwnColliderAboveFloor_IsIgnored()
        {
            var own = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            own.transform.SetParent(_owner.transform);
            own.transform.position = _owner.transform.position + Vector3.up * 0.15f;
            own.transform.localScale = Vector3.one * 0.1f;
            Physics.SyncTransforms();

            Assert.AreSame(_stone, _probe.SurfaceAt(_owner.transform.position));
        }

        [Test]
        public void SurfaceAt_LayerNotInMask_ReturnsDefault()
        {
            var other = new GroundSurfaceProbe(new MeshSurfaceResolver(_registry), _registry, 1 << 5, 0.3f, 0.6f, _owner.transform);
            Assert.AreSame(_fallback, other.SurfaceAt(_owner.transform.position));
        }
    }

    public class SurfaceTrackerTests
    {
        [Test]
        public void Update_FirstSurface_ReportsChange()
        {
            var a = ScriptableObject.CreateInstance<SurfaceDefinition>();
            var tracker = new SurfaceTracker();
            Assert.IsTrue(tracker.Update(a));
            Assert.AreSame(a, tracker.Current);
            Object.DestroyImmediate(a);
        }

        [Test]
        public void Update_SameSurface_ReportsNoChangeAndKeepsPrevious()
        {
            var a = ScriptableObject.CreateInstance<SurfaceDefinition>();
            var b = ScriptableObject.CreateInstance<SurfaceDefinition>();
            var tracker = new SurfaceTracker();
            tracker.Update(a);
            Assert.IsFalse(tracker.Update(a));
            Assert.IsTrue(tracker.Update(b));
            Assert.AreSame(a, tracker.Previous);
            Assert.AreSame(b, tracker.Current);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void Update_Null_IsIgnored()
        {
            var a = ScriptableObject.CreateInstance<SurfaceDefinition>();
            var tracker = new SurfaceTracker();
            tracker.Update(a);
            Assert.IsFalse(tracker.Update(null));
            Assert.AreSame(a, tracker.Current);
            Object.DestroyImmediate(a);
        }
    }

    public class PlayerSurfaceSensorPrefabTests
    {
        [Test]
        public void PlayerWizardPrefab_HasSurfaceSensor()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<PlayerSurfaceSensor>());
        }
    }
}

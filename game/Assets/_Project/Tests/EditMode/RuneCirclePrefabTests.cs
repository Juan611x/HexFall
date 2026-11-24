using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // Rune circle prefabs: a visual base with a trigger volume and no solid collider, plus a launch and a teleport variant.
    public class RuneCirclePrefabTests
    {
        private const string Folder = "Assets/_Project/Prefabs/RuneCircles";
        private const string Materials = "Assets/_Project/VFX/RuneCircles/Materials";
        private const int IgnoreRaycastLayer = 2;

        private static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name}.prefab not found in {Folder}.");
            return prefab;
        }

        private static Material Mat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat");
            Assert.IsNotNull(material, $"{name}.mat not found.");
            return material;
        }

        private static Renderer RendererOf(GameObject prefab, string child)
        {
            var t = prefab.transform.Find(child);
            Assert.IsNotNull(t, $"{prefab.name} has no child {child}.");
            return t.GetComponent<Renderer>();
        }

        [Test]
        public void BaseHasCircleSparksAndEmbersChildren()
        {
            var prefab = Load("RuneCircle");
            CollectionAssert.AreEquivalent(
                new[] { "Circle", "Sparks", "Embers" },
                prefab.transform.Cast<Transform>().Select(t => t.name).ToArray());
            Assert.IsNotNull(prefab.transform.Find("Sparks").GetComponent<ParticleSystem>());
            Assert.IsNotNull(prefab.transform.Find("Embers").GetComponent<ParticleSystem>());
            Assert.IsNotNull(prefab.transform.Find("Circle").GetComponent<MeshRenderer>());
        }

        [Test]
        public void BaseRootIsATriggerBoxCoveringTheFootprint()
        {
            var box = Load("RuneCircle").GetComponent<BoxCollider>();
            Assert.IsNotNull(box);
            Assert.IsTrue(box.isTrigger);
            Assert.GreaterOrEqual(box.size.x, 1.5f);
            Assert.GreaterOrEqual(box.size.z, 1.5f);
            Assert.GreaterOrEqual(box.size.y, 0.5f);
            Assert.AreEqual(0f, box.center.x, 0.001f);
            Assert.AreEqual(0f, box.center.z, 0.001f);
            Assert.Greater(box.center.y, 0f);
        }

        [Test]
        public void BaseHasNoSolidColliderAnywhere()
        {
            var prefab = Load("RuneCircle");
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, "Only the root trigger box is allowed.");
            Assert.That(colliders, Has.All.Matches<Collider>(c => c.isTrigger));
            Assert.IsNull(prefab.transform.Find("Circle").GetComponent<Collider>());
        }

        [Test]
        public void TriggerLivesOnALayerEveryProbeIgnores()
        {
            // Probes use QueryTriggerInteraction.Ignore; the camera filters Default only. A non-Default layer keeps both clear.
            foreach (var t in Load("RuneCircle").GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(IgnoreRaycastLayer, t.gameObject.layer, t.name);
        }

        [TestCase("RuneCircle_Launch", "Launch")]
        [TestCase("RuneCircle_Teleport", "Teleport")]
        public void VariantsAreVariantsOfTheBase(string name, string kind)
        {
            var prefab = Load(name);
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(prefab));
            Assert.AreEqual(Load("RuneCircle"), PrefabUtility.GetCorrespondingObjectFromSource(prefab));
            Assert.AreEqual(Mat($"M_RuneCircle_{kind}"), RendererOf(prefab, "Circle").sharedMaterial);
            Assert.AreEqual(Mat($"M_RuneSparks_{kind}"), RendererOf(prefab, "Sparks").sharedMaterial);
            Assert.AreEqual(Mat($"M_RuneEmbers_{kind}"), RendererOf(prefab, "Embers").sharedMaterial);
            Assert.IsTrue(prefab.GetComponent<BoxCollider>().isTrigger);
        }

        private static bool IsPurple(Color c) => c.b > c.g + 0.1f && c.r > c.g + 0.05f;
        private static bool IsOrange(Color c) => c.r > c.b + 0.2f && c.r > c.g;

        [TestCase("Sparks")]
        [TestCase("Embers")]
        public void TeleportParticlesReadPurple(string child)
        {
            var ps = Load("RuneCircle_Teleport").transform.Find(child).GetComponent<ParticleSystem>();
            Assert.IsTrue(IsPurple(ps.main.startColor.color), $"start color {ps.main.startColor.color}");
            var over = ps.colorOverLifetime;
            if (over.enabled)
                foreach (var key in over.color.gradient.colorKeys)
                    Assert.IsFalse(IsOrange(key.color), $"gradient key {key.color}"); // neutral white is fine: it only multiplies the purple start color
        }

        [TestCase("Sparks")]
        [TestCase("Embers")]
        public void LaunchParticlesStayWarm(string child)
        {
            var ps = Load("RuneCircle_Launch").transform.Find(child).GetComponent<ParticleSystem>();
            Assert.IsTrue(IsOrange(ps.main.startColor.color), $"start color {ps.main.startColor.color}");
        }
    }
}

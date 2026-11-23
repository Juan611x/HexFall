using System.Linq;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The Sandbox test patches: wood, grass, metal and carpet textures, their surfaces, and the patches that use them.
    public class SurfacePatchAssetsTests
    {
        private const string PackPath = "Assets/ThirdParty/Kenney/ImpactSounds";
        private const string SurfacesPath = "Assets/_Project/Surfaces";
        private const string TexturesPath = "Assets/_Project/Art/Environment/Textures";
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox.unity";

        private static SurfaceDefinition Surface(string name)
        {
            var surface = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>($"{SurfacesPath}/{name}.asset");
            Assert.IsNotNull(surface, $"{name} not found.");
            return surface;
        }

        private static Texture Texture(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TexturesPath}/{name}.png");
            Assert.IsNotNull(texture, $"{name} not found.");
            return texture;
        }

        private static void AssertEffect(string surfaceName, SurfaceImpact impact, string prefix, Vector2 volume)
        {
            var surface = Surface(surfaceName);
            Assert.IsTrue(surface.TryGetEffect(impact, out var effect), $"{surfaceName} has no {impact} effect.");
            var expected = Enumerable.Range(0, 5)
                .Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>($"{PackPath}/{prefix}_{i:000}.ogg"))
                .ToArray();
            Assert.That(expected, Has.All.Not.Null, $"Missing {prefix} clips in the pack.");
            CollectionAssert.AreEquivalent(expected, effect.clips);
            Assert.AreEqual(volume.x, effect.volumeRange.x, 1e-4f);
            Assert.AreEqual(volume.y, effect.volumeRange.y, 1e-4f);
            Assert.AreEqual(0.95f, effect.pitchRange.x, 1e-4f);
            Assert.AreEqual(1.05f, effect.pitchRange.y, 1e-4f);
        }

        private static readonly Vector2 StepVolume = new Vector2(0.8f, 1f);
        private static readonly Vector2 LandVolume = new Vector2(0.9f, 1f);

        private static SurfaceRegistry Registry() =>
            AssetDatabase.LoadAssetAtPath<SurfaceRegistry>($"{SurfacesPath}/SurfaceRegistry.asset");

        [TestCase("T_Wood", "Surface_Wood")]
        [TestCase("T_Grass", "Surface_Grass")]
        [TestCase("T_Metal", "Surface_Metal")]
        [TestCase("T_Carpet", "Surface_Carpet")]
        public void RegistryMapsPatchTexturesToTheirSurfaces(string texture, string surface)
        {
            Assert.AreEqual(Surface(surface), Registry().Resolve(Texture(texture)));
        }

        [Test]
        public void WoodUsesWoodSteps_AndLightWoodLandings()
        {
            AssertEffect("Surface_Wood", SurfaceImpact.Footstep, "footstep_wood", StepVolume);
            AssertEffect("Surface_Wood", SurfaceImpact.Landing, "impactWood_light", LandVolume);
        }

        [Test]
        public void GrassUsesGrassSteps_AndMediumSoftLandings()
        {
            AssertEffect("Surface_Grass", SurfaceImpact.Footstep, "footstep_grass", StepVolume);
            AssertEffect("Surface_Grass", SurfaceImpact.Landing, "impactSoft_medium", LandVolume);
        }

        [Test]
        public void MetalUsesLightMetalSteps_AndHeavyMetalLandings()
        {
            AssertEffect("Surface_Metal", SurfaceImpact.Footstep, "impactMetal_light", StepVolume);
            AssertEffect("Surface_Metal", SurfaceImpact.Landing, "impactMetal_heavy", LandVolume);
        }

        [Test]
        public void CarpetUsesCarpetSteps_AndMediumSoftLandings()
        {
            AssertEffect("Surface_Carpet", SurfaceImpact.Footstep, "footstep_carpet", StepVolume);
            AssertEffect("Surface_Carpet", SurfaceImpact.Landing, "impactSoft_medium", LandVolume);
        }

        [Test]
        public void CarpetScrapesQuieterAndLowerThanTheOtherSurfaces()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/OpenGameArt/Scrapes/SlideScrape.wav");
            Assert.IsTrue(Surface("Surface_Carpet").TryGetEffect(SurfaceImpact.Slide, out var effect));
            CollectionAssert.AreEqual(new[] { clip }, effect.clips);
            Assert.AreEqual(0.5f, effect.volumeRange.x, 1e-4f);
            Assert.AreEqual(0.7f, effect.volumeRange.y, 1e-4f);
            Assert.AreEqual(0.85f, effect.pitchRange.x, 1e-4f);
            Assert.AreEqual(0.95f, effect.pitchRange.y, 1e-4f);
            foreach (var other in new[] { "Surface_Grass", "Surface_Wood", "Surface_Stone", "Surface_Metal" })
            {
                Assert.IsTrue(Surface(other).TryGetEffect(SurfaceImpact.Slide, out var otherEffect));
                Assert.Less(effect.volumeRange.y, otherEffect.volumeRange.y + 1e-4f, other);
                Assert.Less(effect.pitchRange.y, otherEffect.pitchRange.y + 1e-4f, other);
            }
        }

        [Test]
        public void ThePatchesAndTheCrouchTunnelDoNotOverlap()
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToArray();
                var names = new[] { "Patch_Wood", "Patch_Grass", "Patch_Metal", "Patch_Carpet" };
                var patches = names.Select(n => all.FirstOrDefault(c => c.name == n)).ToArray();
                Assert.That(patches, Has.All.Not.Null);
                foreach (var patch in patches)
                {
                    var a = patch.bounds;
                    foreach (var other in all)
                    {
                        if (other == patch || other.name == "Ground") continue;
                        var b = other.bounds;
                        var overlapsFootprint = a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
                        Assert.IsFalse(overlapsFootprint, $"{patch.name} overlaps {other.name} on the ground plane.");
                    }
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("T_Wood")]
        [TestCase("T_Grass")]
        [TestCase("T_Metal")]
        [TestCase("T_Carpet")]
        public void PatchTexturesMatchTheGroundStyle(string name)
        {
            var path = $"{TexturesPath}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsNotNull(importer, $"{name} not found.");
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode);
            Assert.IsFalse(importer.mipmapEnabled);
            var texture = Texture(name);
            Assert.AreEqual(64, texture.width);
            Assert.AreEqual(64, texture.height);
        }

        [TestCase("Patch_Wood", "T_Wood")]
        [TestCase("Patch_Grass", "T_Grass")]
        [TestCase("Patch_Metal", "T_Metal")]
        [TestCase("Patch_Carpet", "T_Carpet")]
        public void SandboxHasTheFlatPatch(string patchName, string textureName)
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try
            {
                var patch = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == patchName);
                Assert.IsNotNull(patch, $"{patchName} not found in Sandbox.");
                Assert.IsNotNull(patch.parent);
                Assert.AreEqual("TestArena", patch.parent.name);
                Assert.IsNotNull(patch.GetComponent<BoxCollider>());
                Assert.IsFalse(patch.GetComponent<BoxCollider>().isTrigger);
                Assert.AreEqual(Texture(textureName), patch.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);
                Assert.AreEqual(0, patch.gameObject.layer);
                var top = patch.GetComponent<BoxCollider>().bounds.max.y;
                Assert.AreEqual(0.02f, top, 0.005f, "Patch top should sit 2 cm above the ground.");
                Assert.AreEqual(0.04f, patch.GetComponent<BoxCollider>().bounds.size.y, 0.005f);
                Assert.AreEqual(4f, patch.GetComponent<BoxCollider>().bounds.size.x, 0.1f);
                Assert.AreEqual(4f, patch.GetComponent<BoxCollider>().bounds.size.z, 0.1f);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

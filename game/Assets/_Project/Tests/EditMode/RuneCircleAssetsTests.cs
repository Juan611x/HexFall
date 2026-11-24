using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The rune circle VFX assets live under _Project/VFX/RuneCircles; the tutorial pack is credited under ThirdParty.
    public class RuneCircleAssetsTests
    {
        private const string Root = "Assets/_Project/VFX/RuneCircles";
        private const string PackPath = "Assets/ThirdParty/FlexUnit/MagicSpellCircle";

        private static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/{name}.mat");
            Assert.IsNotNull(material, $"{name}.mat not found.");
            return material;
        }

        private static Texture TextureOf(Material material, string property)
        {
            Assert.IsTrue(material.HasProperty(property), $"{material.name} has no {property}.");
            return material.GetTexture(property);
        }

        private static Texture LoadTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(texture, $"{path} not found.");
            return texture;
        }

        [TestCase("M_RuneCircle_Launch")]
        [TestCase("M_RuneCircle_Teleport")]
        public void CircleMaterialsUseTheRuneCircleShader(string materialName)
        {
            var material = LoadMaterial(materialName);
            var graph = AssetDatabase.LoadMainAssetAtPath($"{Root}/Shaders/SG_RuneCircle.shadergraph");
            Assert.IsNotNull(graph, "SG_RuneCircle.shadergraph not found.");
            Assert.IsNotNull(material.shader);
            Assert.AreEqual(AssetDatabase.GetAssetPath(graph), AssetDatabase.GetAssetPath(material.shader));
        }

        [Test]
        public void LaunchCircleUsesTheOrangeTexture()
        {
            var expected = LoadTexture($"{PackPath}/T_SpellCircle_Orange.png");
            Assert.AreEqual(expected, TextureOf(LoadMaterial("M_RuneCircle_Launch"), "_MainTextura"));
        }

        [Test]
        public void TeleportCircleUsesTheGeneratedTexture()
        {
            var expected = LoadTexture($"{Root}/Textures/T_RuneCircle_Teleport.png");
            Assert.AreEqual(expected, TextureOf(LoadMaterial("M_RuneCircle_Teleport"), "_MainTextura"));
        }

        [TestCase("M_RuneCircle_Launch")]
        [TestCase("M_RuneCircle_Teleport")]
        public void CirclesShareTheNoiseTexture(string materialName)
        {
            var expected = LoadTexture($"{PackPath}/T_SpellCircle_Noise.png");
            Assert.AreEqual(expected, TextureOf(LoadMaterial(materialName), "_NoiseTexture"));
        }

        [Test]
        public void TeleportCircleKeepsTheLaunchTuning()
        {
            var launch = LoadMaterial("M_RuneCircle_Launch");
            var teleport = LoadMaterial("M_RuneCircle_Teleport");
            foreach (var property in new[] { "_ColorIntensity", "_NoiseSpeed", "_RippleCount", "_RippleStrenght", "_RotationSpeed", "_TwilrSpeed" })
                Assert.AreEqual(launch.GetFloat(property), teleport.GetFloat(property), 1e-5f, property);
        }

        [TestCase("M_RuneSparks_Launch")]
        [TestCase("M_RuneEmbers_Launch")]
        [TestCase("M_RuneSparks_Teleport")]
        [TestCase("M_RuneEmbers_Teleport")]
        public void ParticleMaterialsExist(string materialName)
        {
            Assert.IsNotNull(LoadMaterial(materialName));
        }

        [TestCase("M_RuneSparks_Teleport", "M_RuneSparks_Launch")]
        [TestCase("M_RuneEmbers_Teleport", "M_RuneEmbers_Launch")]
        public void TeleportParticlesAreBluerThanTheLaunchOnes(string teleportName, string launchName)
        {
            var teleport = LoadMaterial(teleportName).GetColor("_EmissionColor");
            var launch = LoadMaterial(launchName).GetColor("_EmissionColor");
            Assert.Greater(teleport.b, teleport.g, "Purple emission has more blue than green.");
            Assert.Greater(teleport.b, launch.b, "Purple emission is bluer than the orange one.");
        }

        [Test]
        public void NoNonCommercialCircleImagesRemainUnderAssets()
        {
            var offenders = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .Where(p => p.Contains("Circle 2") || p.Contains("Circle 3") || p.Contains("Spell Circle/"))
                .ToArray();
            CollectionAssert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        [Test]
        public void TheTutorialPackCarriesItsLicense()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), PackPath, "LICENSE.txt")));
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath($"{PackPath}/Readme.pdf"));
        }
    }
}

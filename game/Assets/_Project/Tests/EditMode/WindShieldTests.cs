using System.Linq;
using System.Reflection;
using HexFall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexFall.UI.Tests
{
    // The air-sphere look of the parry shield: shader compiles and exposes the colour MagicShield animates,
    // the material wires shader and streak texture, the player prefab uses it, and the streak texture tiles.
    public class WindShieldTests
    {
        private const string ShaderName = "HexFall/WindShield";
        private const string MaterialPath = "Assets/_Project/VFX/WindShield/Materials/M_WindShield.mat";
        private const string TexturePath = "Assets/_Project/VFX/WindShield/Textures/T_WindStreaks.png";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        [Test]
        public void Shader_exists_and_compiles_without_errors()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader, "shader " + ShaderName);
            Assert.IsTrue(shader.isSupported, "supported");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "ShaderHasError");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error).ToArray();
            Assert.IsEmpty(errors, string.Join("\n", errors.Select(e => e.message)));
        }

        [Test]
        public void Shader_exposes_the_properties_the_shield_and_material_use()
        {
            var shader = Shader.Find(ShaderName);
            Assert.IsNotNull(shader);
            foreach (var name in new[] { "_BaseColor", "_StreakTex", "_RimPower", "_RimStrength", "_CoreAlpha",
                "_Layer1Speed", "_Layer1Tilt", "_Layer1Scale", "_Layer2Speed", "_Layer2Tilt", "_Layer2Scale" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(name), 0, name);
        }

        [Test]
        public void Material_uses_the_shader_and_the_streak_texture()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath);
            Assert.AreEqual(ShaderName, material.shader.name);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath), material.GetTexture("_StreakTex"));
            Assert.AreEqual(RenderQueue.Transparent, (RenderQueue)material.renderQueue);
        }

        [Test]
        public void Player_prefab_shield_renders_with_the_wind_material()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var shield = prefab.GetComponentInChildren<MagicShield>(true);
            Assert.IsNotNull(shield);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath), shield.GetComponent<Renderer>().sharedMaterial);
        }

        [Test]
        public void MagicShield_still_drives_the_base_colour_alpha_of_the_wind_material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var shield = go.AddComponent<MagicShield>();
                Invoke(shield, "Awake"); // EditMode does not run Awake on a runtime-only component
                shield.Flash(1f);
                Invoke(shield, "Update");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var baseAlpha = material.GetColor("_BaseColor").a;
                Assert.AreEqual(baseAlpha, block.GetColor("_BaseColor").a, 0.001f);

                shield.Deflect();
                Invoke(shield, "Update");
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(Mathf.Min(1f, baseAlpha * 2.5f), block.GetColor("_BaseColor").a, 0.001f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Streak_texture_tiles_horizontally_and_imports_as_a_repeating_mask()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            Assert.IsNotNull(importer, TexturePath);
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapModeV);
            Assert.IsFalse(importer.sRGBTexture, "mask is linear data");
            Assert.IsTrue(importer.mipmapEnabled);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(TexturePath)));
                var w = texture.width;
                var seam = 0f;
                var interior = 0f; // largest step between neighbouring columns away from the seam
                for (var y = 0; y < texture.height; y++)
                {
                    seam = Mathf.Max(seam, Mathf.Abs(texture.GetPixel(w - 1, y).r - texture.GetPixel(0, y).r));
                    interior = Mathf.Max(interior, Mathf.Abs(texture.GetPixel(w / 2 + 1, y).r - texture.GetPixel(w / 2, y).r));
                }
                Assert.LessOrEqual(seam, 0.15f, "wrap seam step");
                Assert.IsTrue(seam <= interior + 0.1f, "seam " + seam + " vs interior " + interior);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void Invoke(MagicShield shield, string method) =>
            typeof(MagicShield).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shield, null);
    }
}

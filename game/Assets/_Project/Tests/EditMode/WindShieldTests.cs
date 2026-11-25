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
    // The air-sphere look of the parry shield (PSX style, DD-003): a ~1000 triangle UV sphere with its poles on the
    // vertical axis and a Shader Graph with procedural, pixelated wind veins that MagicShield builds up with _Build.
    public class WindShieldTests
    {
        private const string GraphPath = "Assets/_Project/VFX/WindShield/Shaders/SG_WindShield.shadergraph";
        private const string IncludePath = "Assets/_Project/VFX/WindShield/Shaders/WindShieldCore.hlsl";
        private const string OldShaderPath = "Assets/_Project/VFX/WindShield/Shaders/WindShield.shader";
        private const string MaterialPath = "Assets/_Project/VFX/WindShield/Materials/M_WindShield.mat";
        private const string OldTexturePath = "Assets/_Project/VFX/WindShield/Textures/T_AirShield.png";
        private const string TextureDir = "Assets/_Project/VFX/WindShield/Textures/";
        private const string KenneyDir = "Assets/ThirdParty/Kenney/ParticlePack/";
        private const string MeshPath = "Assets/_Project/VFX/WindShield/Models/AirShield.fbx";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private static Shader LoadGraph()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            Assert.IsNotNull(shader, GraphPath);
            return shader;
        }

        [Test]
        public void Shader_graph_exists_and_compiles_without_errors()
        {
            var shader = LoadGraph();
            Assert.IsTrue(shader.isSupported, "supported");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "ShaderHasError");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == ShaderCompilerMessageSeverity.Error).ToArray();
            Assert.IsEmpty(errors, string.Join("\n", errors.Select(e => e.message)));
        }

        [Test]
        public void Shader_graph_math_lives_in_a_shared_include()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(IncludePath), IncludePath);
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(IncludePath), System.IO.File.ReadAllText(GraphPath),
                "the graph's Custom Function nodes reference the include by GUID");
        }

        [Test]
        public void Shader_graph_exposes_the_properties_the_shield_and_material_use()
        {
            var shader = LoadGraph();
            foreach (var name in new[] { "_BaseColor", "_Build", "_Density", "_AlphaSteps", "_NoiseCells", "_FlowSpeed",
                "_RimPower", "_CoreAlpha", "_PoleFade", "_SnapResolution", "_BuildSpin",
                "_TwirlTexA", "_TwirlTexB", "_TwirlTilingA", "_TwirlTilingB", "_WarpAmount" })
                Assert.GreaterOrEqual(shader.FindPropertyIndex(name), 0, name);
        }

        [Test]
        public void Material_uses_the_shader_graph_with_a_light_airy_look()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath);
            Assert.AreEqual(LoadGraph(), material.shader);
            Assert.AreEqual(RenderQueue.Transparent, (RenderQueue)material.renderQueue);
            Assert.Less(material.GetFloat("_AlphaSteps"), 2f, "no hard posterisation: it read as flat blocks");
            Assert.AreEqual(1f, material.GetFloat("_Build"), "complete in the editor");
            Assert.Less(material.GetFloat("_Density"), 0.5f, "low density: mostly transparent");
            Assert.Greater(material.GetColor("_BaseColor").a, 0f);
        }

        [TestCase("T_AirTwirl_A.png", "twirl_03.png")]
        [TestCase("T_AirTwirl_B.png", "twirl_02.png")]
        [TestCase("T_AirTrace.png", "trace_04.png")]
        public void Derived_wind_textures_are_low_res_point_filtered_with_a_real_alpha(string derived, string original)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyDir + original), "original " + original);
            var path = TextureDir + derived;
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, "point filter");
            Assert.IsFalse(importer.mipmapEnabled, "no mipmaps");
            Assert.IsTrue(importer.alphaIsTransparency, "alpha is transparency");
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);

            var image = new Texture2D(2, 2);
            Assert.IsTrue(image.LoadImage(System.IO.File.ReadAllBytes(path)), "decode " + path);
            try
            {
                Assert.That(image.width, Is.InRange(64, 128), "width");
                Assert.That(image.height, Is.InRange(64, 128), "height");
                var pixels = image.GetPixels32();
                Assert.Greater(pixels.Count(p => p.a < 8), pixels.Length / 4, "a transparent background");
                Assert.Greater(pixels.Max(p => p.a), 200, "visible strokes");
                Assert.Greater(pixels.Select(p => (int)p.a).Distinct().Count(), 24, "soft internal gradient, not posterised");
                Assert.IsTrue(pixels.Where(p => p.a > 0).All(p => p.r >= 250 && p.g >= 250 && p.b >= 250), "RGB stays white");
            }
            finally { Object.DestroyImmediate(image); }
        }

        [Test]
        public void Kenney_particle_pack_license_travels_with_the_originals()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(KenneyDir + "License.txt"));
        }

        [Test]
        public void Material_assigns_the_twirl_textures_with_whole_tile_counts_around_the_sphere()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "T_AirTwirl_A.png"), material.GetTexture("_TwirlTexA"));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "T_AirTwirl_B.png"), material.GetTexture("_TwirlTexB"));
            foreach (var name in new[] { "_TwirlTilingA", "_TwirlTilingB" })
            {
                var tiling = material.GetVector(name);
                Assert.Greater(tiling.x, 0f, name);
                Assert.AreEqual(Mathf.Round(tiling.x), tiling.x, 1e-4f, name + ": whole tiles hide the atan2 seam");
            }
        }

        [Test]
        public void Old_shader_and_photo_texture_are_gone()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(OldShaderPath), OldShaderPath);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(OldTexturePath), OldTexturePath);
        }

        [Test]
        public void Player_prefab_shield_renders_with_the_wind_material()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, PlayerPrefab);
            var shield = prefab.GetComponentInChildren<MagicShield>(true);
            Assert.IsNotNull(shield, "MagicShield");
            var renderer = shield.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "shield Renderer");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath), renderer.sharedMaterial);
        }

        [Test]
        public void Shield_uses_a_dense_uv_sphere_with_poles_on_the_vertical_axis()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var shield = prefab.GetComponentInChildren<MagicShield>(true);
            var mesh = shield.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "mesh");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath), mesh, "AirShield mesh");
            Assert.That(mesh.triangles.Length / 3, Is.InRange(900, 1100), "triangles");

            Assert.AreEqual(0.5f, mesh.bounds.extents.y, 0.01f, "radius along Y");
            Assert.AreEqual(0.5f, mesh.bounds.extents.x, 0.01f, "radius along X");
            Assert.AreEqual(0.5f, mesh.bounds.extents.z, 0.01f, "radius along Z");
            var vertices = mesh.vertices;
            foreach (var sign in new[] { 1f, -1f })
                Assert.IsTrue(vertices.Any(v => Mathf.Abs(v.x) < 1e-3f && Mathf.Abs(v.z) < 1e-3f && Mathf.Abs(v.y - 0.5f * sign) < 1e-3f),
                    "a pole vertex on the Y axis at " + sign);
        }

        [Test]
        public void MagicShield_still_drives_the_base_colour_alpha_of_the_wind_material()
        {
            var (go, shield, renderer) = NewShield();
            try
            {
                var material = renderer.sharedMaterial;
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

        [TestCase(0.06f)]
        [TestCase(0.5f)]
        [TestCase(1.1f)]
        [TestCase(1.25f)]
        public void MagicShield_sets_the_build_from_ShieldBuild_while_visible(float elapsed)
        {
            var (go, shield, renderer) = NewShield();
            try
            {
                shield.Flash(1f);
                Assert.IsTrue(shield.Apply(elapsed), "visible");
                Assert.IsTrue(renderer.enabled, "renderer stays on");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(new ShieldBuild(1f).Progress(elapsed), block.GetFloat("_Build"), 1e-5f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MagicShield_assembles_through_a_partial_build_and_disassembles_after_the_hold()
        {
            var (go, shield, renderer) = NewShield();
            try
            {
                shield.Flash(1f);
                var block = new MaterialPropertyBlock();
                shield.Apply(0.06f);
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Build"), Is.GreaterThan(0f).And.LessThan(1f), "assembling");
                shield.Apply(0.5f);
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetFloat("_Build"), 1e-5f, "held");
                shield.Apply(1.2f);
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Build"), Is.GreaterThan(0f).And.LessThan(1f), "disassembling past the guard window");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MagicShield_hides_the_renderer_only_when_the_build_is_done()
        {
            var (go, shield, renderer) = NewShield();
            try
            {
                shield.Flash(1f);
                Assert.IsTrue(shield.Apply(1.34f), "still disassembling");
                Assert.IsFalse(shield.Apply(1.36f), "done");
                Assert.IsFalse(renderer.enabled);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MagicShield_keeps_its_full_scale_while_building()
        {
            var (go, shield, _) = NewShield();
            try
            {
                var scale = go.transform.localScale;
                shield.Flash(1f);
                shield.Apply(0.01f);
                Assert.AreEqual(scale, go.transform.localScale, "the build replaces the old pop-scale");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static (GameObject go, MagicShield shield, Renderer renderer) NewShield()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shield = go.AddComponent<MagicShield>();
            Invoke(shield, "Awake"); // EditMode does not run Awake on a runtime-only component
            return (go, shield, renderer);
        }

        private static void Invoke(MagicShield shield, string method) =>
            typeof(MagicShield).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shield, null);
    }
}

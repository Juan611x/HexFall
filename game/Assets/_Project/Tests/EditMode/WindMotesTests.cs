using System.Reflection;
using HexFall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The air motes of the parry shield: prefab setup (soft sprites, never stretched bars) and the MagicShield
    // driving (drift in on assemble, light orbit while held, blow out on disassemble, restart on Flash).
    public class WindMotesTests
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string MoteMaterialPath = "Assets/_Project/VFX/WindShield/Materials/M_WindMote.mat";
        private const string ShieldMaterialPath = "Assets/_Project/VFX/WindShield/Materials/M_WindShield.mat";

        private static MagicShield PrefabShield()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, PlayerPrefab);
            var shield = prefab.GetComponentInChildren<MagicShield>(true);
            Assert.IsNotNull(shield, "MagicShield");
            return shield;
        }

        private static ParticleSystem Child(string name)
        {
            var t = PrefabShield().transform.Find(name);
            Assert.IsNotNull(t, name);
            var ps = t.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, name + " ParticleSystem");
            return ps;
        }

        [Test]
        public void No_shield_particle_system_is_a_stretched_billboard()
        {
            var systems = PrefabShield().GetComponentsInChildren<ParticleSystem>(true);
            Assert.GreaterOrEqual(systems.Length, 2);
            foreach (var ps in systems)
                Assert.AreNotEqual(ParticleSystemRenderMode.Stretch, ps.GetComponent<ParticleSystemRenderer>().renderMode, ps.name);
        }

        [Test]
        public void The_old_wisps_are_gone()
        {
            Assert.IsNull(PrefabShield().transform.Find("WindWisps"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/WindShield/Materials/M_WindWisp.mat"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/VFX/WindShield/Textures/T_AirWisp.png"));
        }

        [Test]
        public void Mote_material_is_additive_particles_unlit_near_white_and_does_not_write_depth()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);
            Assert.IsNotNull(material, MoteMaterialPath);
            Assert.AreEqual("Universal Render Pipeline/Particles/Unlit", material.shader.name);
            Assert.AreEqual(1f, material.GetFloat("_Surface"), "transparent");
            Assert.AreEqual(1f, material.GetFloat("_DstBlend"), "additive");
            Assert.AreEqual(0f, material.GetFloat("_ZWrite"));
            Assert.AreEqual(3000, material.renderQueue);
            AssertNotGreen(material.GetColor("_BaseColor"));
        }

        private static void AssertNotGreen(Color c)
        {
            Assert.GreaterOrEqual(Mathf.Min(c.r, c.g, c.b), 0.9f, "near white");
            Assert.LessOrEqual(c.g - Mathf.Max(c.r, c.b), 0.05f, "no green tint");
        }

        [Test]
        public void Mote_material_uses_the_small_point_filtered_flipbook_cut_from_the_kenney_art()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath).GetTexture("_BaseMap");
            Assert.IsNotNull(texture, "_BaseMap");
            Assert.AreEqual("T_AirMote", texture.name);
            Assert.LessOrEqual(texture.width, 128);
            Assert.LessOrEqual(texture.height, 32);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled, "no mipmaps");
            Assert.IsTrue(importer.alphaIsTransparency);
        }

        [TestCase("WindMotes")]
        [TestCase("WindBurst")]
        public void Shield_particle_systems_are_billboard_motes_with_the_mote_material(string child)
        {
            var ps = Child(child);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath), renderer.sharedMaterial);
            Assert.AreEqual(ParticleSystemRenderMode.Billboard, renderer.renderMode);
            AssertNotGreen(ps.main.startColor.color);
            Assert.IsTrue(ps.textureSheetAnimation.enabled, "random flipbook frame");
            Assert.IsTrue(ps.colorOverLifetime.enabled, "alpha fades in and out");
        }

        [TestCase("WindMotes", 60)]
        [TestCase("WindBurst", 40)]
        public void Mote_particles_follow_the_wizard_and_stay_within_the_webgl_budget(string child, int cap)
        {
            var main = Child(child).main;
            Assert.AreEqual(ParticleSystemSimulationSpace.Local, main.simulationSpace);
            Assert.IsFalse(main.playOnAwake);
            Assert.Greater(main.maxParticles, 0);
            Assert.LessOrEqual(main.maxParticles, cap);
        }

        [Test]
        public void Motes_loop_with_a_radial_velocity_module_driven_per_phase()
        {
            var ps = Child("WindMotes");
            Assert.IsTrue(ps.main.loop);
            Assert.IsTrue(ps.velocityOverLifetime.enabled);
            Assert.IsTrue(ps.shape.enabled);
            Assert.AreEqual(ParticleSystemShapeType.Sphere, ps.shape.shapeType);
        }

        [Test]
        public void Burst_is_a_small_puff_of_motes()
        {
            var ps = Child("WindBurst");
            Assert.AreEqual(14f, ps.emission.GetBurst(0).count.constant);
            Assert.LessOrEqual(ps.main.maxParticles, 20);
        }

        [Test]
        public void Motes_system_is_capped_for_the_halved_amount()
        {
            Assert.LessOrEqual(Child("WindMotes").main.maxParticles, 40);
            Assert.AreEqual(50f, ShieldMotes.For(ShieldBuildPhase.Assembling).Rate);
        }

        private static float[,] MoteFrame(int frame)
        {
            var path = System.IO.Path.GetFullPath("Assets/_Project/VFX/WindShield/Textures/T_AirMote.png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path)));
            Assert.AreEqual(64, tex.width);
            Assert.AreEqual(16, tex.height);
            var a = new float[16, 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    a[y, x] = tex.GetPixel(frame * 16 + x, y).a;
            Object.DestroyImmediate(tex);
            return a;
        }

        // Closed ring / full swirl proxy: the bright pixels (alpha >= 0.4), seen from their own centroid, reach
        // out in many directions. An open arc or comma only covers a few of the 8 octants.
        private static int BrightOctants(float[,] a)
        {
            float sx = 0, sy = 0, n = 0;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (a[y, x] >= 0.4f) { sx += x; sy += y; n++; }
            if (n == 0) return 0;
            float cx = sx / n, cy = sy / n;
            var seen = new bool[8];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (a[y, x] < 0.4f || dx * dx + dy * dy < 4f) continue;
                    int octant = Mathf.FloorToInt((Mathf.Atan2(dy, dx) + Mathf.PI) / (Mathf.PI / 4f)) % 8;
                    seen[octant] = true;
                }
            int count = 0;
            foreach (var s in seen) if (s) count++;
            return count;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Mote_frames_are_open_wisps_never_a_closed_ring(int frame)
        {
            var a = MoteFrame(frame);
            Assert.LessOrEqual(BrightOctants(a), 5, "bright pixels surround their centre like a ring");
            float max = 0, border = 0;
            int bright = 0;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    max = Mathf.Max(max, a[y, x]);
                    if (a[y, x] >= 0.4f) bright++;
                    if (x == 0 || y == 0 || x == 15 || y == 15) border = Mathf.Max(border, a[y, x]);
                }
            Assert.GreaterOrEqual(max, 0.9f, "has a bright core");
            Assert.LessOrEqual(bright, 80, "a thin fragment, not a filled blob");
            Assert.GreaterOrEqual(bright, 8, "readable at 16 px");
            Assert.LessOrEqual(border, 0.25f, "soft edge, no hard crop line");
        }

        [Test]
        public void Burst_is_a_one_shot_outward_ring_emitted_at_time_zero()
        {
            var ps = Child("WindBurst");
            Assert.IsFalse(ps.main.loop);
            Assert.AreEqual(0f, ps.emission.rateOverTime.constant);
            Assert.GreaterOrEqual(ps.emission.burstCount, 1);
            Assert.AreEqual(0f, ps.emission.GetBurst(0).time);
            Assert.Greater(ps.emission.GetBurst(0).count.constant, 0f);
            Assert.Greater(ps.velocityOverLifetime.radial.constant, 0f, "outward");
        }

        [Test]
        public void Shield_references_both_particle_systems()
        {
            var so = new SerializedObject(PrefabShield());
            Assert.AreEqual(Child("WindMotes"), so.FindProperty("motes").objectReferenceValue);
            Assert.AreEqual(Child("WindBurst"), so.FindProperty("burst").objectReferenceValue);
            Assert.IsNull(so.FindProperty("wisps"), "wisps field removed");
        }

        // --- driving ---

        private GameObject _go;
        private MagicShield _shield;
        private ParticleSystem _motes, _burst;

        [SetUp]
        public void SetUp()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShieldMaterialPath);
            _motes = NewSystem("m", _go.transform);
            _burst = NewSystem("b", _go.transform);
            _shield = _go.AddComponent<MagicShield>();
            var so = new SerializedObject(_shield);
            so.FindProperty("motes").objectReferenceValue = _motes;
            so.FindProperty("burst").objectReferenceValue = _burst;
            so.ApplyModifiedPropertiesWithoutUndo();
            Invoke("Awake");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static ParticleSystem NewSystem(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            return ps;
        }

        private void Invoke(string method) =>
            typeof(MagicShield).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_shield, null);

        private void SetStartedAt(float seconds) =>
            typeof(MagicShield).GetField("_startedAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_shield, seconds);

        private float Serialized(string field) =>
            new SerializedObject(_shield).FindProperty(field).floatValue;

        [Test]
        public void Flash_starts_the_motes_drifting_inward()
        {
            Assert.IsFalse(_motes.isEmitting);
            _shield.Flash(1f);
            Assert.IsTrue(_motes.isEmitting);
            Assert.Less(_motes.velocityOverLifetime.radial.constant, 0f);
            Assert.Greater(_motes.emission.rateOverTime.constant, 0f);
            Assert.Greater(_motes.shape.radius, ShieldMotes.SurfaceRadius);
        }

        [Test]
        public void Motes_orbit_on_the_surface_while_the_shield_is_held()
        {
            _shield.Flash(1f);
            _shield.Apply(Serialized("assembleSeconds") + 0.05f);
            Assert.IsTrue(_motes.isEmitting);
            Assert.AreEqual(0f, _motes.velocityOverLifetime.radial.constant);
            Assert.AreEqual(ShieldMotes.SurfaceRadius, _motes.shape.radius, 0.05f);
            Assert.AreNotEqual(0f, _motes.velocityOverLifetime.orbitalY.constant);
        }

        [Test]
        public void Disassembling_stops_the_emission_puffs_outward_and_keeps_the_live_motes()
        {
            _shield.Flash(0.3f);
            _shield.Apply(0.31f);
            Assert.IsFalse(_motes.isEmitting);
            Assert.Greater(_motes.velocityOverLifetime.radial.constant, 0f);
            Assert.GreaterOrEqual(_motes.particleCount, ShieldMotes.PuffCount(ShieldBuildPhase.Holding, ShieldBuildPhase.Disassembling));
        }

        [Test]
        public void Update_keeps_driving_the_motes_after_the_sphere_is_gone_while_some_are_alive()
        {
            _shield.Flash(0.3f);
            SetStartedAt(Time.time - (0.3f + Serialized("disassembleSeconds") + 0.05f)); // already Done, never saw Disassembling
            _go.GetComponent<Renderer>().enabled = false; // the sphere is gone: only the live motes keep Update going
            _motes.Emit(3);
            Assert.IsTrue(_motes.IsAlive(true));
            Invoke("Update");
            Assert.Greater(_motes.velocityOverLifetime.radial.constant, 0f, "blowing out, not stuck inward");
            Assert.IsFalse(_motes.isEmitting);
        }

        [Test]
        public void A_new_Flash_during_the_linger_restarts_the_motes_cleanly()
        {
            _shield.Flash(0.3f);
            _shield.Apply(0.31f);
            Assert.IsFalse(_motes.isEmitting);
            _shield.Flash(0.3f);
            Assert.AreEqual(0, _motes.particleCount, "old motes cleared");
            Assert.IsTrue(_motes.isEmitting);
            Assert.Less(_motes.velocityOverLifetime.radial.constant, 0f, "inward again");
            // the puff fires again on the next disassembly
            _shield.Apply(0.31f);
            Assert.Greater(_motes.particleCount, 0);
        }

        [Test]
        public void Deflect_plays_the_burst()
        {
            _shield.Flash(1f);
            Assert.IsFalse(_burst.isPlaying);
            _shield.Deflect();
            Assert.IsTrue(_burst.isPlaying);
        }

        [Test]
        public void Shield_without_particle_systems_still_works()
        {
            var so = new SerializedObject(_shield);
            so.FindProperty("motes").objectReferenceValue = null;
            so.FindProperty("burst").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.DoesNotThrow(() => { _shield.Flash(0.3f); Invoke("Update"); _shield.Apply(0.31f); _shield.Deflect(); });
        }
    }
}

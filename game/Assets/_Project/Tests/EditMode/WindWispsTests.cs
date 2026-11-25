using System.Reflection;
using HexFall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The wind particles of the parry shield: prefab setup (wisps orbiting while it is up, a burst on a deflect)
    // and the MagicShield driving (start on Flash, stop emitting when the hold ends, burst on Deflect).
    public class WindWispsTests
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string WispMaterialPath = "Assets/_Project/VFX/WindShield/Materials/M_WindWisp.mat";
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
        public void Wisp_material_is_additive_particles_unlit_and_does_not_write_depth()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WispMaterialPath);
            Assert.IsNotNull(material, WispMaterialPath);
            Assert.AreEqual("Universal Render Pipeline/Particles/Unlit", material.shader.name);
            Assert.AreEqual(1f, material.GetFloat("_Surface"), "transparent");
            Assert.AreEqual(1f, material.GetFloat("_DstBlend"), "additive");
            Assert.AreEqual(0f, material.GetFloat("_ZWrite"));
            Assert.AreEqual(3000, material.renderQueue);
        }

        [TestCase("WindWisps")]
        [TestCase("WindBurst")]
        public void Shield_has_a_wind_particle_system_with_the_wisp_material(string child)
        {
            var ps = Child(child);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Material>(WispMaterialPath), renderer.sharedMaterial);
            Assert.AreEqual(ParticleSystemRenderMode.Stretch, renderer.renderMode);
        }

        [TestCase("WindWisps", 40)]
        [TestCase("WindBurst", 40)]
        public void Wind_particles_follow_the_wizard_and_stay_within_the_webgl_budget(string child, int cap)
        {
            var main = Child(child).main;
            Assert.AreEqual(ParticleSystemSimulationSpace.Local, main.simulationSpace);
            Assert.IsFalse(main.playOnAwake);
            Assert.Greater(main.maxParticles, 0);
            Assert.LessOrEqual(main.maxParticles, cap);
        }

        [Test]
        public void Wisps_loop_and_orbit_around_the_vertical_axis_with_a_fade()
        {
            var ps = Child("WindWisps");
            Assert.IsTrue(ps.main.loop);
            Assert.IsTrue(ps.velocityOverLifetime.enabled);
            Assert.AreNotEqual(0f, ps.velocityOverLifetime.orbitalY.constant, "orbital Y");
            Assert.IsTrue(ps.colorOverLifetime.enabled, "alpha fades in and out");
            Assert.Greater(ps.emission.rateOverTime.constant, 0f);
        }

        [Test]
        public void Burst_is_a_one_shot_that_emits_a_burst_at_time_zero()
        {
            var ps = Child("WindBurst");
            Assert.IsFalse(ps.main.loop);
            Assert.AreEqual(0f, ps.emission.rateOverTime.constant);
            Assert.GreaterOrEqual(ps.emission.burstCount, 1);
            Assert.AreEqual(0f, ps.emission.GetBurst(0).time);
            Assert.Greater(ps.emission.GetBurst(0).count.constant, 0f);
        }

        [Test]
        public void Shield_references_both_particle_systems()
        {
            var shield = PrefabShield();
            var so = new SerializedObject(shield);
            Assert.AreEqual(Child("WindWisps"), so.FindProperty("wisps").objectReferenceValue);
            Assert.AreEqual(Child("WindBurst"), so.FindProperty("burst").objectReferenceValue);
        }

        [TestCase(0f, 0.3f, true)]
        [TestCase(0.29f, 0.3f, true)]
        [TestCase(0.3f, 0.3f, false)]
        [TestCase(0.45f, 0.3f, false)]
        [TestCase(-1f, 0.3f, false)]
        public void Wisps_emit_only_while_the_shield_is_held(float elapsed, float hold, bool expected) =>
            Assert.AreEqual(expected, MagicShield.WispsEmitting(elapsed, hold));

        // --- driving ---

        private GameObject _go;
        private MagicShield _shield;
        private ParticleSystem _wisps, _burst;

        [SetUp]
        public void SetUp()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ShieldMaterialPath);
            _wisps = NewSystem("w", _go.transform);
            _burst = NewSystem("b", _go.transform);
            _shield = _go.AddComponent<MagicShield>();
            var so = new SerializedObject(_shield);
            so.FindProperty("wisps").objectReferenceValue = _wisps;
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
            return ps;
        }

        private void Invoke(string method) =>
            typeof(MagicShield).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_shield, null);

        private void SetStartedAt(float seconds) =>
            typeof(MagicShield).GetField("_startedAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_shield, seconds);

        [Test]
        public void Wisp_material_uses_the_tiny_point_filtered_photo_sprite()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WispMaterialPath);
            var texture = material.GetTexture("_BaseMap");
            Assert.IsNotNull(texture, "_BaseMap");
            Assert.AreEqual("T_AirWisp", texture.name);
            Assert.LessOrEqual(texture.width, 16);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled, "no mipmaps");
        }

        [Test]
        public void Flash_starts_the_wisps()
        {
            Assert.IsFalse(_wisps.isEmitting);
            _shield.Flash(1f);
            Assert.IsTrue(_wisps.isEmitting);
        }

        [Test]
        public void Wisps_stop_emitting_when_the_hold_ends()
        {
            _shield.Flash(0.3f);
            SetStartedAt(Time.time - 0.35f); // held 0.3 s, now fading
            Invoke("Update");
            Assert.IsFalse(_wisps.isEmitting);
        }

        [Test]
        public void Wisps_keep_emitting_while_the_shield_is_held()
        {
            _shield.Flash(1f);
            Invoke("Update");
            Assert.IsTrue(_wisps.isEmitting);
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
            so.FindProperty("wisps").objectReferenceValue = null;
            so.FindProperty("burst").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.DoesNotThrow(() => { _shield.Flash(0.3f); Invoke("Update"); _shield.Deflect(); });
        }
    }
}

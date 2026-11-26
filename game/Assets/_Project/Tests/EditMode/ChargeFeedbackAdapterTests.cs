using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The charge feedback in the engine: the SpellDefinition settings, the bolt that grows and shifts colour
    // with its charge, and the charging orb at the muzzle that PlayerCombat shows while a charge is held.
    public class ChargeFeedbackAdapterTests
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<Object> _made = new List<Object>();

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

        private static SpellDefinition.ChargeSettings FireCharge() => new SpellDefinition.ChargeSettings
        {
            maxChargeSeconds = 1.2f, baseDamage = 18, damageMode = ChargeDamageMode.Curve,
            minSizeScale = 0.5f, maxSizeScale = 2f, fullChargeColor = new Color(1f, 1f, 0.5f), colorBlend = 1f,
            speedScaleAtMax = 1.5f, showOrb = true, orbSize = 0.4f, windUpState = "Charge", windUpHoldNormalized = 0.5f,
        };

        private SpellDefinition NewSpell(CastPattern pattern = CastPattern.Charged, bool feedback = true)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "color", new Color(1f, 0.2f, 0f));
            Set(spell, "pattern", pattern);
            if (feedback) Set(spell, "charge", FireCharge());
            return spell;
        }

        private static SpellDefinition.ProjectileSettings Flat(float speed = 10f) => new SpellDefinition.ProjectileSettings
        {
            speed = speed, weight = 0f, drag = 0f, curveDegreesPerSecond = 0f, sizeScale = 1f, lifetimeSeconds = 3f,
        };

        // ---- settings ----

        [Test]
        public void NewSpellsStartWithNeutralFeedbackAndAnOrbAndTheChargeAnimation()
        {
            var c = Track(ScriptableObject.CreateInstance<SpellDefinition>()).Charge;
            Assert.AreEqual(1f, c.minSizeScale);
            Assert.AreEqual(1f, c.maxSizeScale);
            Assert.AreEqual(0f, c.colorBlend);
            Assert.AreEqual(1f, c.speedScaleAtMax);
            Assert.IsTrue(c.showOrb);
            Assert.Greater(c.orbSize, 0f);
            Assert.AreEqual("Charge", c.windUpState);
            Assert.That(c.windUpHoldNormalized, Is.InRange(0f, 1f));
        }

        [Test]
        public void TheFeedbackConfigMirrorsTheSettings()
        {
            var cfg = NewSpell().ToFeedbackConfig();
            Assert.AreEqual(0.5f, cfg.MinScale);
            Assert.AreEqual(2f, cfg.MaxScale);
            Assert.AreEqual(1f, cfg.ColorBlend);
            Assert.AreEqual(1.5f, cfg.SpeedScaleAtMax);
            Assert.AreEqual(0.5f, cfg.FullColor.B, 1e-5f);
        }

        [Test]
        public void OnlyAChargedSpellChangesWithCharge()
        {
            var burst = NewSpell(CastPattern.Burst);
            var look = burst.LookAtCharge(1f);
            Assert.AreEqual(1f, look.Scale);
            Assert.AreEqual(1f, look.SpeedScale);
            Assert.AreEqual(burst.Color.g, look.Color.G, 1e-5f);

            var fire = NewSpell();
            Assert.AreEqual(2f, fire.LookAtCharge(1f).Scale, 1e-5f);
        }

        [Test]
        public void SanitizeClampsTheFeedbackSettings()
        {
            var spell = NewSpell();
            var c = FireCharge();
            c.minSizeScale = -1f;
            c.maxSizeScale = float.NaN;
            c.colorBlend = 5f;
            c.speedScaleAtMax = 0f;
            c.orbSize = -2f;
            c.windUpHoldNormalized = 3f;
            Set(spell, "charge", c);

            spell.Sanitize();

            Assert.Greater(spell.Charge.minSizeScale, 0f);
            Assert.IsFalse(float.IsNaN(spell.Charge.maxSizeScale));
            Assert.Greater(spell.Charge.maxSizeScale, 0f);
            Assert.AreEqual(1f, spell.Charge.colorBlend);
            Assert.Greater(spell.Charge.speedScaleAtMax, 0f);
            Assert.GreaterOrEqual(spell.Charge.orbSize, 0f);
            Assert.AreEqual(1f, spell.Charge.windUpHoldNormalized);
        }

        // ---- bolt ----

        private Bolt NewBolt()
        {
            var go = Track(new GameObject("Bolt"));
            go.transform.localScale = Vector3.one * 0.25f;
            go.AddComponent<MeshRenderer>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.widthMultiplier = 1f;
            return go.AddComponent<Bolt>();
        }

        private static Color Tint(Bolt bolt)
        {
            var block = new MaterialPropertyBlock();
            bolt.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetColor(BaseColorId);
        }

        [Test]
        public void ABoltFiredAtNoChargeIsSmallAndKeepsTheSpellColour()
        {
            var bolt = NewBolt();
            bolt.Launch(null, Vector3.forward, Flat(), 18, NewSpell(), 0f);
            Assert.AreEqual(0.25f * 0.5f, bolt.transform.localScale.x, 1e-4f);
            Assert.AreEqual(1f, Tint(bolt).r, 1e-4f);
            Assert.AreEqual(0.2f, Tint(bolt).g, 1e-4f);
        }

        [Test]
        public void ABoltFiredAtFullChargeIsBigBrightAndFaster()
        {
            var bolt = NewBolt();
            bolt.Launch(null, Vector3.forward, Flat(10f), 45, NewSpell(), 1f);
            Assert.AreEqual(0.25f * 2f, bolt.transform.localScale.x, 1e-4f);
            Assert.AreEqual(0.5f, Tint(bolt).b, 1e-4f);
            Assert.AreEqual(1f, Tint(bolt).g, 1e-4f);

            bolt.Advance(0.1f);
            Assert.AreEqual(1.5f, bolt.transform.position.z, 1e-3f, "10 m/s x 1.5 for 0.1 s");
        }

        [Test]
        public void ABoltInBetweenIsInBetween()
        {
            var bolt = NewBolt();
            bolt.Launch(null, Vector3.forward, Flat(), 30, NewSpell(), 0.5f);
            Assert.AreEqual(0.25f * 1.25f, bolt.transform.localScale.x, 1e-4f);
            Assert.AreEqual(0.6f, Tint(bolt).g, 1e-4f);
        }

        [Test]
        public void TheSpellSizeScaleStillMultipliesTheChargeScale()
        {
            var bolt = NewBolt();
            var p = Flat();
            p.sizeScale = 2f;
            bolt.Launch(null, Vector3.forward, p, 45, NewSpell(), 1f);
            Assert.AreEqual(0.25f * 2f * 2f, bolt.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void ChargeMeansNothingToASpellThatDoesNotCharge()
        {
            var bolt = NewBolt();
            bolt.Launch(null, Vector3.forward, Flat(), 8, NewSpell(CastPattern.Burst), 1f);
            Assert.AreEqual(0.25f, bolt.transform.localScale.x, 1e-4f);
            Assert.AreEqual(0.2f, Tint(bolt).g, 1e-4f);
        }

        [Test]
        public void ALaunchWithoutAChargeKeepsTheOldBehaviour()
        {
            var bolt = NewBolt();
            bolt.Launch(null, Vector3.forward, Flat(), 18, NewSpell());
            Assert.AreEqual(0.25f * 0.5f, bolt.transform.localScale.x, 1e-4f, "charge 0 is the smallest bolt");
            var plain = NewBolt();
            plain.Launch(null, Vector3.forward, Flat(), 18, null);
            Assert.AreEqual(0.25f, plain.transform.localScale.x, 1e-4f);
        }

        // ---- orb ----

        private sealed class OrbRig
        {
            public ChargeOrb Orb;
            public Transform Visual;
            public MeshRenderer Renderer;
        }

        private OrbRig NewOrb()
        {
            var go = Track(new GameObject("Orb"));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.SetActive(false); // the prefab ships it hidden
            var orb = go.AddComponent<ChargeOrb>();
            Set(orb, "visual", visual.transform);
            Set(orb, "visualRenderer", visual.GetComponent<MeshRenderer>());
            return new OrbRig { Orb = orb, Visual = visual.transform, Renderer = visual.GetComponent<MeshRenderer>() };
        }

        private static Color OrbTint(OrbRig rig)
        {
            var block = new MaterialPropertyBlock();
            rig.Renderer.GetPropertyBlock(block);
            return block.GetColor(BaseColorId);
        }

        [Test]
        public void TheOrbIsHiddenUntilShown()
        {
            var rig = NewOrb();
            Assert.IsFalse(rig.Orb.IsVisible);
            Assert.IsFalse(rig.Visual.gameObject.activeSelf);
        }

        [Test]
        public void TheOrbAppearsAtTheMuzzleScaledAndTintedByTheCharge()
        {
            var rig = NewOrb();
            var spell = NewSpell();
            rig.Orb.Show(spell, 0f, 1f, new Vector3(1, 2, 3));
            Assert.IsTrue(rig.Orb.IsVisible);
            Assert.IsTrue(rig.Visual.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(1, 2, 3), rig.Visual.position);
            Assert.AreEqual(0.4f * 0.5f, rig.Visual.localScale.x, 1e-4f);
            Assert.AreEqual(0.2f, OrbTint(rig).g, 1e-4f);

            rig.Orb.Show(spell, 1f, 2.2f, new Vector3(1, 2, 3));
            Assert.AreEqual(0.4f * 2f, rig.Visual.localScale.x, 1e-4f);
            Assert.AreEqual(1f, OrbTint(rig).g, 1e-4f);
            Assert.AreEqual(0.5f, OrbTint(rig).b, 1e-4f);
        }

        [Test]
        public void AFullOrbPulses()
        {
            var rig = NewOrb();
            var spell = NewSpell();
            rig.Orb.Show(spell, 1f, 2f, Vector3.zero); // first full frame: pulse phase 0
            var start = rig.Visual.localScale.x;
            rig.Orb.Show(spell, 1f, 2.04f, Vector3.zero); // 40 ms later, near the crest of a 6 Hz pulse
            var later = rig.Visual.localScale.x;
            Assert.AreEqual(0.8f, start, 1e-4f);
            Assert.Greater(later, start);
            Assert.LessOrEqual(later, 0.8f * 1.081f);
        }

        [Test]
        public void HideHidesTheOrbAndTheNextChargeStartsItsPulseAgain()
        {
            var rig = NewOrb();
            var spell = NewSpell();
            rig.Orb.Show(spell, 1f, 2f, Vector3.zero);
            rig.Orb.Hide();
            Assert.IsFalse(rig.Orb.IsVisible);
            Assert.IsFalse(rig.Visual.gameObject.activeSelf);
            rig.Orb.Show(spell, 1f, 9f, Vector3.zero);
            Assert.AreEqual(0.8f, rig.Visual.localScale.x, 1e-4f, "phase restarts at the first full frame");
        }

        [Test]
        public void ASpellWithTheOrbOffOrThatDoesNotChargeShowsNothing()
        {
            var rig = NewOrb();
            var off = NewSpell();
            var c = FireCharge();
            c.showOrb = false;
            Set(off, "charge", c);
            rig.Orb.Show(off, 0.5f, 1f, Vector3.zero);
            Assert.IsFalse(rig.Orb.IsVisible);

            rig.Orb.Show(NewSpell(CastPattern.Burst), 0.5f, 1f, Vector3.zero);
            Assert.IsFalse(rig.Orb.IsVisible);

            rig.Orb.Show(null, 0.5f, 1f, Vector3.zero);
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void AnOrbWithoutItsVisualDoesNotThrow()
        {
            var go = Track(new GameObject("Bare"));
            var orb = go.AddComponent<ChargeOrb>();
            Assert.DoesNotThrow(() => orb.Show(NewSpell(), 0.5f, 1f, Vector3.zero));
            Assert.DoesNotThrow(() => orb.Hide());
        }

        // ---- PlayerCombat drives the orb ----

        private PlayerCombat NewCombat(out OrbRig rig, SpellDefinition spell)
        {
            var go = Track(new GameObject("Wizard"));
            var combat = go.AddComponent<PlayerCombat>();
            rig = NewOrb();
            rig.Orb.transform.SetParent(go.transform, false);
            Set(combat, "chargeOrb", rig.Orb);
            typeof(PlayerCombat).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(combat, null);
            combat.SetSpell(spell);
            return combat;
        }

        private static float Now => Time.time;

        [Test]
        public void HoldingAChargeShowsTheOrbAtTheCurrentLevel()
        {
            var combat = NewCombat(out var rig, NewSpell());
            Assert.IsTrue(combat.Attack());
            combat.Step(Now + 0.6f, true);
            Assert.IsTrue(rig.Orb.IsVisible);
            Assert.AreEqual(0.5f, rig.Orb.Level, 1e-3f);
        }

        [Test]
        public void ReleasingHidesTheOrb()
        {
            var combat = NewCombat(out var rig, NewSpell());
            combat.Attack();
            combat.Step(Now + 0.6f, true);
            combat.ReleaseAttack();
            combat.Step(Now + 0.7f, true);
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void AParryHidesTheOrb()
        {
            var combat = NewCombat(out var rig, NewSpell());
            combat.Attack();
            combat.Step(Now + 0.3f, true);
            combat.Parry();
            combat.Step(Now + 0.4f, true);
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void ASpellSwapHidesTheOrb()
        {
            var combat = NewCombat(out var rig, NewSpell());
            combat.Attack();
            combat.Step(Now + 0.3f, true);
            Assert.IsTrue(rig.Orb.IsVisible);
            combat.SetSpell(NewSpell(CastPattern.Burst));
            combat.Step(Now + 0.4f, true);
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void ALostReleaseHidesTheOrb()
        {
            var combat = NewCombat(out var rig, NewSpell());
            combat.AttackHeldSource = () => true;
            Assert.IsTrue(combat.StartAttackFromInput());
            combat.Step(Now + 0.3f, true);
            Assert.IsTrue(rig.Orb.IsVisible);
            combat.Step(Now + 0.4f, false); // input no longer held
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void ASpellThatDoesNotChargeNeverShowsTheOrb()
        {
            var combat = NewCombat(out var rig, NewSpell(CastPattern.Channel));
            combat.Attack();
            combat.Step(Now + 0.3f, true);
            Assert.IsFalse(rig.Orb.IsVisible);
        }

        [Test]
        public void TheFiredShotCarriesTheChargeOfTheRelease()
        {
            var combat = NewCombat(out _, NewSpell());
            ProjectileShot? shot = null;
            combat.Machine.ShotFired += s => shot = s;
            combat.Attack();
            combat.Machine.Tick(new ActionIntent(false, false, true), Now + 0.6f);
            Assert.IsTrue(shot.HasValue);
            Assert.AreEqual(0.5f, shot.Value.Charge, 1e-3f);
        }

        // ---- prefab wiring ----

        [Test]
        public void ThePlayerWizardCarriesAHiddenOrbBuiltFromTheBoltLook()
        {
            const string prefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
            var wizard = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var orb = wizard.GetComponentInChildren<ChargeOrb>(true);
            Assert.IsNotNull(orb, "PlayerWizard needs a ChargeOrb");
            var so = new SerializedObject(wizard.GetComponent<PlayerCombat>());
            Assert.AreSame(orb, so.FindProperty("chargeOrb").objectReferenceValue);

            var visual = (Transform)new SerializedObject(orb).FindProperty("visual").objectReferenceValue;
            Assert.IsNotNull(visual);
            Assert.IsFalse(visual.gameObject.activeSelf, "hidden until a charge is held");
            var bolt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Combat/Bolt_Player.prefab");
            Assert.AreSame(bolt.GetComponent<MeshFilter>().sharedMesh, visual.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreSame(bolt.GetComponent<MeshRenderer>().sharedMaterial, visual.GetComponent<MeshRenderer>().sharedMaterial);
            Assert.IsNull(visual.GetComponent<Collider>());
        }

        // ---- Fire asset ----

        [Test]
        public void FireShipsItsChargeFeedback()
        {
            var fire = AssetDatabase.LoadAssetAtPath<SpellDefinition>("Assets/_Project/Spells/Spell_Fire.asset");
            var c = fire.Charge;
            Assert.AreEqual(0.7f, c.minSizeScale, 1e-4f);
            Assert.AreEqual(1.8f, c.maxSizeScale, 1e-4f);
            Assert.Greater(c.colorBlend, 0.5f);
            Assert.Greater(c.fullChargeColor.g, fire.Color.g + 0.3f, "toward yellow-white");
            Assert.Greater(c.fullChargeColor.b, fire.Color.b);
            Assert.Greater(c.speedScaleAtMax, 1f);
            Assert.IsTrue(c.showOrb);
            Assert.Greater(c.orbSize, 0f);
            Assert.AreEqual("Charge", c.windUpState);
            Assert.That(c.windUpHoldNormalized, Is.InRange(0.1f, 0.9f));
        }
    }
}

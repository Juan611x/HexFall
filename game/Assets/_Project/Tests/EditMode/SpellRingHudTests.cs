using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The spell ring in the engine: the HUD element, PlayerHud as its view and the adapter on the wizard that
    // pushes the ring state and the spell colour each frame.
    public class SpellRingHudTests
    {
        private const string HudUxmlPath = "Assets/_Project/UI/Hud/Hud.uxml";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private readonly List<Object> _made = new List<Object>();

        private sealed class FakeView : ISpellRingView
        {
            public SpellRingState State;
            public Color Color;
            public int Calls;

            public void SetSpellRing(SpellRingState state, Color color)
            {
                State = state;
                Color = color;
                Calls++;
            }
        }

        private T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(Component c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, null);

        // ---- the HUD ----

        [Test]
        public void TheRingElementIsInTheHudAroundTheCrosshairAndNeverBlocksThePointer()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            var root = new VisualElement();
            tree.CloneTree(root);
            var ring = root.Q<SpellRingElement>("spell-ring");
            Assert.IsNotNull(ring);
            Assert.AreEqual(PickingMode.Ignore, ring.pickingMode);
            Assert.IsNotNull(root.Q<HexCrosshairElement>("crosshair"));
        }

        [Test]
        public void TheElementKeepsTheStateAndColourItWasGiven()
        {
            var ring = new SpellRingElement();
            ring.SetState(new SpellRingState(SpellRingMode.Cooldown, 0.4f, false), Color.red);
            Assert.AreEqual(SpellRingMode.Cooldown, ring.State.Mode);
            Assert.AreEqual(0.4f, ring.State.Fraction, 1e-5f);
            Assert.AreEqual(Color.red, ring.RingColor);
        }

        [Test]
        public void PlayerHudIsTheRingView()
        {
            Assert.IsTrue(typeof(ISpellRingView).IsAssignableFrom(typeof(PlayerHud)));
        }

        // ---- the adapter ----

        private SpellDefinition Spell(CastPattern pattern, Color color, float cooldown = 1f)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            Set(spell, "color", color);
            Set(spell, "pattern", pattern);
            Set(spell, "cooldownSeconds", cooldown);
            Set(spell, "damage", 10);
            Set(spell, "charge", new SpellDefinition.ChargeSettings
            {
                maxChargeSeconds = 1.2f, baseDamage = 18, minSizeScale = 0.7f, maxSizeScale = 1.8f,
                fullChargeColor = new Color(1f, 1f, 0.5f), colorBlend = 1f, speedScaleAtMax = 1f,
            });
            return spell;
        }

        private (PlayerCombat combat, SpellRingHud adapter, FakeView view, float[] clock) NewWizard(SpellDefinition spell)
        {
            var wizard = Track(new GameObject("Wizard"));
            var combat = wizard.AddComponent<PlayerCombat>();
            Set(combat, "activeSpell", spell);
            Invoke(combat, "Awake");
            var clock = new[] { 10f };
            combat.ClockOverride = () => clock[0];
            var adapter = wizard.AddComponent<SpellRingHud>();
            var view = new FakeView();
            adapter.Bind(view);
            return (combat, adapter, view, clock);
        }

        [Test]
        public void ABoundAdapterShowsTheIdleRingInTheSpellColourAtOnce()
        {
            var colour = new Color(0.2f, 0.5f, 1f);
            var (_, _, view, _) = NewWizard(Spell(CastPattern.Single, colour));
            Assert.AreEqual(SpellRingMode.Ready, view.State.Mode);
            Assert.AreEqual(colour.r, view.Color.r, 1e-4f);
            Assert.AreEqual(colour.b, view.Color.b, 1e-4f);
        }

        [Test]
        public void AfterACastTheAdapterPushesTheDrainingCooldown()
        {
            var (combat, adapter, view, clock) = NewWizard(Spell(CastPattern.Single, Color.cyan, 2f));
            combat.Attack();
            clock[0] = 11f;
            adapter.Refresh();
            Assert.AreEqual(SpellRingMode.Cooldown, view.State.Mode);
            Assert.AreEqual(0.5f, view.State.Fraction, 1e-3f);
        }

        [Test]
        public void ChargingPushesTheChargeLevelAndBlendsTheColourTowardTheFullChargeColour()
        {
            var (combat, adapter, view, clock) = NewWizard(Spell(CastPattern.Charged, new Color(1f, 0.2f, 0f)));
            combat.Attack();
            clock[0] = 10.6f;
            adapter.Refresh();
            Assert.AreEqual(SpellRingMode.Charging, view.State.Mode);
            Assert.AreEqual(0.5f, view.State.Fraction, 1e-3f);
            var half = view.Color.g;

            clock[0] = 12f;
            adapter.Refresh();
            Assert.IsTrue(view.State.Full);
            Assert.AreEqual(1f, view.Color.g, 1e-3f, "full charge colour");
            Assert.Greater(view.Color.g, half);
        }

        [Test]
        public void WithoutASpellTheRingUsesTheBaseColour()
        {
            var (_, _, view, _) = NewWizard(null);
            Assert.AreEqual(SpellRingMode.Ready, view.State.Mode);
            Assert.AreEqual(SpellRingHud.BaseColor, view.Color);
        }

        [Test]
        public void AnUnboundAdapterDoesNothing()
        {
            var wizard = Track(new GameObject("Wizard"));
            wizard.AddComponent<PlayerCombat>();
            var adapter = wizard.AddComponent<SpellRingHud>();
            Assert.DoesNotThrow(() => adapter.Refresh());
        }

        [Test]
        public void ThePlayerPrefabHasTheRingAdapter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<SpellRingHud>());
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The "E" prompt: the HUD element (hidden by default, shows a key and a label) and the player-side
    // adapter that shows it from SpellInteractor.PromptChanged.
    public class InteractPromptHudTests
    {
        private const string HudUxmlPath = "Assets/_Project/UI/Hud/Hud.uxml";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private readonly List<Object> _made = new List<Object>();

        private sealed class FakeView : IInteractPromptView
        {
            public bool Visible;
            public string Key;
            public string Text;
            public int Calls;

            public void ShowPrompt(string key, string text)
            {
                Visible = true;
                Key = key;
                Text = text;
                Calls++;
            }

            public void HidePrompt()
            {
                Visible = false;
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

        private static VisualElement CloneHud()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            Assert.IsNotNull(tree, HudUxmlPath);
            var root = new VisualElement();
            tree.CloneTree(root);
            return root;
        }

        // ---- The HUD element ----

        [Test]
        public void ThePromptElementIsInTheHudAndHiddenByDefault()
        {
            var prompt = CloneHud().Q<InkPromptElement>("interact-prompt");
            Assert.IsNotNull(prompt);
            Assert.IsFalse(prompt.IsShown);
            Assert.AreEqual(DisplayStyle.None, prompt.style.display.value);
        }

        [Test]
        public void ShowingThePromptFillsTheKeyAndTheLabelAndMakesItVisible()
        {
            var root = CloneHud();
            var prompt = root.Q<InkPromptElement>("interact-prompt");
            prompt.Show("E", "Swap to Fire");

            Assert.IsTrue(prompt.IsShown);
            Assert.AreEqual(DisplayStyle.Flex, prompt.style.display.value);
            Assert.AreEqual("E", root.Q<Label>("interact-key").text);
            Assert.AreEqual("Swap to Fire", root.Q<Label>("interact-text").text);
        }

        [Test]
        public void HidingThePromptHidesItAgain()
        {
            var prompt = CloneHud().Q<InkPromptElement>("interact-prompt");
            prompt.Show("E", "Swap spell");
            prompt.Hide();
            Assert.IsFalse(prompt.IsShown);
            Assert.AreEqual(DisplayStyle.None, prompt.style.display.value);
        }

        [Test]
        public void ThePromptNeverBlocksThePointer()
        {
            Assert.AreEqual(PickingMode.Ignore, CloneHud().Q<InkPromptElement>("interact-prompt").pickingMode);
        }

        // ---- The adapter on the wizard ----

        private (GameObject wizard, SpellInteractor interactor, InteractPromptHud adapter, PlayerCombat combat) NewWizard(SpellDefinition held)
        {
            var wizard = Track(new GameObject("Wizard"));
            var combat = wizard.AddComponent<PlayerCombat>();
            typeof(PlayerCombat).GetField("activeSpell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(combat, held);
            Invoke(combat, "Awake");
            var interactor = wizard.AddComponent<SpellInteractor>();
            Invoke(interactor, "Awake");
            var adapter = wizard.AddComponent<InteractPromptHud>();
            return (wizard, interactor, adapter, combat);
        }

        private static void Invoke(Component c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, null);

        private SpellDefinition Spell(string name)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            spell.name = name;
            typeof(SpellDefinition).GetField("displayName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spell, name);
            return spell;
        }

        private SpellPickup Pickup(SpellDefinition spell)
        {
            var go = Track(new GameObject("Pickup_" + spell.name));
            go.AddComponent<SphereCollider>();
            var pickup = go.AddComponent<SpellPickup>();
            typeof(SpellPickup).GetField("spell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pickup, spell);
            Invoke(pickup, "Awake");
            return pickup;
        }

        [Test]
        public void ThePromptShowsTheKeyAndTheSpellNameWhenAPickupNeedsInteract()
        {
            var baseSpell = Spell("Base");
            var (wizard, _, adapter, _) = NewWizard(baseSpell);
            var view = new FakeView();
            adapter.Bind(view);
            Pickup(Spell("Fire")).Reach(wizard); // holding Base: taken at once, no prompt
            Assert.IsFalse(view.Visible);

            Pickup(Spell("Water")).Reach(wizard);
            Assert.IsTrue(view.Visible);
            Assert.AreEqual("E", view.Key);
            Assert.AreEqual("Swap to Water", view.Text);
        }

        [Test]
        public void ThePromptHidesWhenThePickupLeavesReach()
        {
            var (wizard, _, adapter, _) = NewWizard(Spell("Base"));
            var view = new FakeView();
            adapter.Bind(view);
            Pickup(Spell("Fire")).Reach(wizard);
            var water = Pickup(Spell("Water"));
            water.Reach(wizard);
            water.Leave(wizard);
            Assert.IsFalse(view.Visible);
        }

        [Test]
        public void BindingShowsTheCurrentPromptAtOnce()
        {
            var (wizard, _, adapter, _) = NewWizard(Spell("Base"));
            Pickup(Spell("Fire")).Reach(wizard);
            Pickup(Spell("Water")).Reach(wizard);

            var view = new FakeView();
            adapter.Bind(view);
            Assert.IsTrue(view.Visible);
            Assert.AreEqual("Swap to Water", view.Text);
        }

        [Test]
        public void ThePromptIsNotPushedAgainWhileItDoesNotChange()
        {
            var (wizard, interactor, adapter, _) = NewWizard(Spell("Base"));
            var view = new FakeView();
            adapter.Bind(view);
            Pickup(Spell("Fire")).Reach(wizard);
            Pickup(Spell("Water")).Reach(wizard);
            var calls = view.Calls;
            interactor.Refresh();
            interactor.Refresh();
            Assert.AreEqual(calls, view.Calls);
        }

        [Test]
        public void ASpellWithoutANameGetsTheGenericLabel()
        {
            var (wizard, _, adapter, _) = NewWizard(Spell("Base"));
            var view = new FakeView();
            adapter.Bind(view);
            Pickup(Spell("Fire")).Reach(wizard);
            var nameless = Spell("");
            Pickup(nameless).Reach(wizard);
            Assert.AreEqual("Swap spell", view.Text);
        }

        [Test]
        public void RebindingStopsListeningToTheOldView()
        {
            var (wizard, _, adapter, _) = NewWizard(Spell("Base"));
            var first = new FakeView();
            var second = new FakeView();
            adapter.Bind(first);
            adapter.Bind(second);
            Pickup(Spell("Fire")).Reach(wizard);
            Pickup(Spell("Water")).Reach(wizard);
            Assert.IsFalse(first.Visible);
            Assert.IsTrue(second.Visible);
        }

        [Test]
        public void ThePlayerPrefabCarriesTheAdapter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<InteractPromptHud>());
        }
    }
}

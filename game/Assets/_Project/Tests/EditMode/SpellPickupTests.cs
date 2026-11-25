using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HexFall.UI.Tests
{
    // The spell loadout adapter and the spell pickups: Base swaps on touch, another spell needs Interact,
    // the nearest pickup in reach is the target, and the prompt state tells the HUD when to show.
    public class SpellPickupTests
    {
        private const string PickupPrefabPath = "Assets/_Project/Prefabs/Pickups/Pickup_Spell.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string ControlsPath = "Assets/_Project/Input/HexFallControls.inputactions";

        private readonly List<Object> _made = new List<Object>();
        private GameObject _wizard;
        private PlayerCombat _combat;
        private SpellInteractor _interactor;
        private SpellDefinition _base;
        private SpellDefinition _fire;
        private SpellDefinition _water;
        private readonly List<InteractPrompt> _prompts = new List<InteractPrompt>();

        private T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        private static FieldInfo Field(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        private static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);

        // Awake does not run on AddComponent outside play mode.
        private static void Awake(Component c) =>
            c.GetType().GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(c, null);

        private static void Invoke(Component c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, null);

        private SpellDefinition NewSpell(string name, Color color)
        {
            var spell = Track(ScriptableObject.CreateInstance<SpellDefinition>());
            spell.name = name;
            Set(spell, "displayName", name);
            Set(spell, "color", color);
            return spell;
        }

        [SetUp]
        public void SetUp()
        {
            _base = NewSpell("Base", Color.cyan);
            _fire = NewSpell("Fire", Color.red);
            _water = NewSpell("Water", Color.blue);
            _wizard = Track(new GameObject("Wizard"));
            _combat = _wizard.AddComponent<PlayerCombat>();
            Set(_combat, "activeSpell", _base);
            Awake(_combat);
            _interactor = _wizard.AddComponent<SpellInteractor>();
            Awake(_interactor);
            _interactor.PromptChanged += _prompts.Add;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            _prompts.Clear();
        }

        private SpellPickup NewPickup(SpellDefinition spell, Vector3 at)
        {
            var go = Track(new GameObject("Pickup_" + (spell != null ? spell.name : "empty")));
            go.transform.position = at;
            go.AddComponent<SphereCollider>();
            var pickup = go.AddComponent<SpellPickup>();
            var visual = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            Set(pickup, "visual", visual.transform);
            Set(pickup, "tint", visual.GetComponent<Renderer>());
            Set(pickup, "spell", spell);
            Awake(pickup);
            return pickup;
        }

        private static Color TintOf(SpellPickup pickup)
        {
            var renderer = (Renderer)Field(pickup, "tint").GetValue(pickup);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        // A pickup the wizard already holds the spell of, so the next ones need Interact.
        private void HoldFire() => NewPickup(_fire, Vector3.zero).Reach(_wizard);

        // ---- Holding Base: touching swaps at once ----

        [Test]
        public void TouchingAPickupWhileHoldingBaseSwapsAtOnceWithoutAPrompt()
        {
            var pickup = NewPickup(_fire, Vector3.zero);
            pickup.Reach(_wizard);

            Assert.AreSame(_fire, _combat.ActiveSpell);
            Assert.IsTrue(pickup.Consumed);
            Assert.IsFalse(pickup.gameObject.activeSelf);
            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.AreEqual(0, _prompts.Count);
        }

        [Test]
        public void ACollectorWithoutAnInteractorIsIgnored()
        {
            var pickup = NewPickup(_fire, Vector3.zero);
            var stranger = Track(new GameObject("Stranger"));
            pickup.Reach(stranger);
            Assert.IsFalse(pickup.Consumed);
            Assert.AreSame(_base, _combat.ActiveSpell);
        }

        [Test]
        public void TheCollectorMayBeAChildOfTheWizard()
        {
            var pickup = NewPickup(_fire, Vector3.zero);
            var child = Track(new GameObject("Hand"));
            child.transform.SetParent(_wizard.transform, false);
            pickup.Reach(child);
            Assert.AreSame(_fire, _combat.ActiveSpell);
        }

        // ---- Holding another spell: Interact ----

        [Test]
        public void TouchingAPickupWhileHoldingAnotherSpellShowsThePromptAndSwapsNothing()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);

            Assert.AreSame(_fire, _combat.ActiveSpell);
            Assert.IsFalse(water.Consumed);
            Assert.IsTrue(_interactor.Prompt.Visible);
            Assert.AreSame(_water, _interactor.Prompt.Spell);
            Assert.AreEqual(1, _prompts.Count);
        }

        [Test]
        public void InteractSwapsAndTheHeldSpellStaysOnTheGroundInThePickup()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);

            Assert.IsTrue(_interactor.Interact());
            Assert.AreSame(_water, _combat.ActiveSpell);
            Assert.AreSame(_fire, water.Spell);
            Assert.IsFalse(water.Consumed);
            Assert.AreEqual(1f, TintOf(water).r, 1e-4f);
            Assert.AreEqual(0f, TintOf(water).b, 1e-4f);
        }

        [Test]
        public void ThePromptFollowsTheSwapThenTheDroppedSpellCanBeTakenBack()
        {
            HoldFire();
            var pickup = NewPickup(_water, Vector3.zero);
            pickup.Reach(_wizard);
            _interactor.Interact();

            Assert.IsTrue(_interactor.Prompt.Visible);
            Assert.AreSame(_fire, _interactor.Prompt.Spell);
            Assert.IsTrue(_interactor.Interact());
            Assert.AreSame(_fire, _combat.ActiveSpell);
            Assert.AreSame(_water, pickup.Spell);
        }

        [Test]
        public void LeavingThePickupClearsThePrompt()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);
            water.Leave(_wizard);

            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.IsNull(_interactor.Prompt.Spell);
            Assert.IsFalse(_interactor.Interact());
            Assert.AreSame(_fire, _combat.ActiveSpell);
        }

        [Test]
        public void InteractWithNothingInReachDoesNothing()
        {
            Assert.IsFalse(_interactor.Interact());
            Assert.AreSame(_base, _combat.ActiveSpell);
        }

        [Test]
        public void APickupHoldingTheSpellAlreadyHeldNeedsNoPrompt()
        {
            HoldFire();
            var same = NewPickup(_fire, Vector3.zero);
            same.Reach(_wizard);
            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.IsFalse(_interactor.Interact());
            Assert.IsFalse(same.Consumed);
        }

        [Test]
        public void ThePromptOnlyRaisesWhenItChanges()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);
            water.Reach(_wizard);
            _interactor.Refresh();
            Assert.AreEqual(1, _prompts.Count);
        }

        // ---- Several pickups in reach ----

        [Test]
        public void TheNearestPickupInReachIsTheTarget()
        {
            HoldFire();
            var far = NewPickup(_water, new Vector3(5f, 0f, 0f));
            var earth = NewSpell("Earth", Color.green);
            var near = NewPickup(earth, new Vector3(1f, 0f, 0f));
            far.Reach(_wizard);
            near.Reach(_wizard);

            Assert.AreSame(earth, _interactor.Prompt.Spell);
            _interactor.Interact();
            Assert.AreSame(earth, _combat.ActiveSpell);
            Assert.AreSame(_fire, near.Spell);
            Assert.AreSame(_water, far.Spell);
        }

        [Test]
        public void MovingNextToAnotherPickupRetargetsThePrompt()
        {
            HoldFire();
            var a = NewPickup(_water, new Vector3(-3f, 0f, 0f));
            var earth = NewSpell("Earth", Color.green);
            var b = NewPickup(earth, new Vector3(3f, 0f, 0f));
            a.Reach(_wizard);
            b.Reach(_wizard);
            _wizard.transform.position = new Vector3(-2f, 0f, 0f);
            _interactor.Refresh();
            Assert.AreSame(_water, _interactor.Prompt.Spell);
            _wizard.transform.position = new Vector3(2f, 0f, 0f);
            _interactor.Refresh();
            Assert.AreSame(earth, _interactor.Prompt.Spell);
        }

        [Test]
        public void ADisabledPickupIsNoLongerInReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);
            water.gameObject.SetActive(false);
            _interactor.Refresh();
            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        // ---- Visual ----

        [Test]
        public void ThePickupIsTintedWithItsSpellColour()
        {
            var pickup = NewPickup(_fire, Vector3.zero);
            Assert.AreEqual(1f, TintOf(pickup).r, 1e-4f);
            Assert.AreEqual(0f, TintOf(pickup).g, 1e-4f);
        }

        [Test]
        public void APickupWithoutASpellOffersNothing()
        {
            var empty = NewPickup(null, Vector3.zero);
            empty.Reach(_wizard);
            Assert.AreSame(_base, _combat.ActiveSpell);
            Assert.IsFalse(empty.Consumed);
        }

        // ---- Cooldown across a swap, through the wizard ----

        [Test]
        public void SwappingSpellsThroughAPickupKeepsTheRunningCooldown()
        {
            var slow = NewSpell("Slow", Color.white);
            Set(slow, "cooldownSeconds", 100f);
            NewPickup(slow, Vector3.zero).Reach(_wizard); // Base -> Slow at once
            Assert.IsTrue(_combat.Attack());
            _combat.Machine.Tick(default, Time.time + 1f); // the attack is over, the cooldown runs

            var fire = NewPickup(_fire, Vector3.zero);
            fire.Reach(_wizard);
            Assert.IsTrue(_interactor.Interact());
            Assert.AreSame(_fire, _combat.ActiveSpell);
            Assert.IsFalse(_combat.Attack(), "Fire is not castable before the old cooldown ends");
        }

        // ---- A refused press never becomes a hold (T4 advisory b) ----

        [Test]
        public void APressTheMachineRefusesDoesNotMarkARunningScriptedHoldAsAnInputCast()
        {
            var charged = NewSpell("Charged", Color.yellow);
            Set(charged, "pattern", CastPattern.Charged);
            _combat.SetSpell(charged);
            Assert.IsTrue(_combat.Attack()); // scripted hold, not from the input
            Assert.IsFalse(_combat.StartAttackFromInput(), "the machine refuses a press while it holds");

            _combat.Step(Time.time + 0.1f, false); // the attack input is not held
            Assert.IsTrue(_combat.Machine.IsHolding, "a refused press must not turn the scripted hold into one that can be lost");
        }

        // ---- Reach is counted per collider (T5 advisory a) ----

        private Collider ColliderOnWizard(string name)
        {
            var go = Track(new GameObject(name));
            go.transform.SetParent(_wizard.transform, false);
            return go.AddComponent<SphereCollider>();
        }

        [Test]
        public void ThePickupStaysInReachUntilTheLastColliderOfTheWizardLeaves()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            var foot = ColliderOnWizard("Foot");
            water.Reach(hand);
            water.Reach(foot);

            water.Leave(hand);
            Assert.IsTrue(_interactor.Prompt.Visible, "the foot is still inside");
            Assert.IsTrue(_interactor.Interact());
        }

        [Test]
        public void TheLastColliderLeavingEndsTheReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            var foot = ColliderOnWizard("Foot");
            water.Reach(hand);
            water.Reach(foot);
            water.Leave(hand);
            water.Leave(foot);

            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.IsFalse(_interactor.Interact());
        }

        [Test]
        public void ADestroyedColliderInsideTheTriggerNoLongerHoldsTheReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            var foot = ColliderOnWizard("Foot");
            water.Reach(hand);
            water.Reach(foot);
            Object.DestroyImmediate(hand.gameObject);
            Object.DestroyImmediate(foot.gameObject);

            Invoke(water, "Update");

            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.IsFalse(_interactor.Interact());
        }

        [Test]
        public void ADisabledColliderInsideTheTriggerNoLongerHoldsTheReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            water.Reach(hand);
            hand.enabled = false;

            Invoke(water, "Update");

            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        [Test]
        public void ADeactivatedColliderObjectInsideTheTriggerNoLongerHoldsTheReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            water.Reach(hand);
            hand.gameObject.SetActive(false);

            Invoke(water, "Update");

            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        [Test]
        public void AStaleColliderDoesNotKeepTheReachWhenTheLastRealOneLeaves()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            var foot = ColliderOnWizard("Foot");
            water.Reach(hand);
            water.Reach(foot);
            hand.enabled = false; // never raises an exit

            water.Leave(foot);

            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        [Test]
        public void ALiveColliderKeepsTheReachWhileAnotherWasDestroyed()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            var foot = ColliderOnWizard("Foot");
            water.Reach(hand);
            water.Reach(foot);
            Object.DestroyImmediate(hand.gameObject);

            Invoke(water, "Update");

            Assert.IsTrue(_interactor.Prompt.Visible);
        }

        [Test]
        public void ACollidersRepeatedEnterCountsOnce()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            var hand = ColliderOnWizard("Hand");
            water.Reach(hand);
            water.Reach(hand);
            water.Leave(hand);
            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        // ---- The loadout follows PlayerCombat (T5 advisory b) ----

        [Test]
        public void TheSpellSetBySomethingElseBecomesTheHeldOneAndBaseStaysBase()
        {
            _combat.SetSpell(_fire); // not through the interactor, before any pickup was touched
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);

            Assert.AreSame(_fire, _combat.ActiveSpell, "holding Fire, so Water needs Interact");
            Assert.AreSame(_water, _interactor.Prompt.Spell);

            _interactor.Interact();
            Assert.AreSame(_water, _combat.ActiveSpell);
            Assert.AreSame(_fire, water.Spell, "Fire was dropped; Base was not the held one");
        }

        [Test]
        public void ReturningToBaseThroughSetSpellMakesTheNextPickupSwapAtOnce()
        {
            HoldFire();
            _combat.SetSpell(_base);
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);

            Assert.AreSame(_water, _combat.ActiveSpell);
            Assert.IsTrue(water.Consumed);
            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        [Test]
        public void AnExternalSpellChangeRetargetsThePromptOnRefresh()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);
            _combat.SetSpell(_water);
            _interactor.Refresh();
            Assert.IsFalse(_interactor.Prompt.Visible, "the pickup holds the spell that is now held");
        }

        // ---- Disabling and re-enabling the interactor (T5 advisory d) ----

        [Test]
        public void DisablingTheInteractorHidesThePromptAndEnablingItRestoresTheReach()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);

            Invoke(_interactor, "OnDisable");
            Assert.IsFalse(_interactor.Prompt.Visible);
            Assert.IsFalse(_interactor.Interact(), "a disabled interactor does not swap");

            Invoke(_interactor, "OnEnable");
            Assert.IsTrue(_interactor.Prompt.Visible);
            Assert.AreSame(_water, _interactor.Prompt.Spell);
            Assert.IsTrue(_interactor.Interact());
            Assert.AreSame(_water, _combat.ActiveSpell);
        }

        [Test]
        public void APickupTouchedWhileTheInteractorIsDisabledIsOfferedWhenItComesBack()
        {
            var fire = NewPickup(_fire, Vector3.zero);
            Invoke(_interactor, "OnDisable");
            fire.Reach(_wizard);
            Assert.AreSame(_base, _combat.ActiveSpell);
            Assert.IsFalse(fire.Consumed);

            Invoke(_interactor, "OnEnable");
            Assert.AreSame(_fire, _combat.ActiveSpell);
            Assert.IsTrue(fire.Consumed);
        }

        [Test]
        public void LeavingWhileTheInteractorIsDisabledIsRemembered()
        {
            HoldFire();
            var water = NewPickup(_water, Vector3.zero);
            water.Reach(_wizard);
            Invoke(_interactor, "OnDisable");
            water.Leave(_wizard);
            Invoke(_interactor, "OnEnable");
            Assert.IsFalse(_interactor.Prompt.Visible);
        }

        // ---- Assets ----

        [Test]
        public void TheSpellPickupPrefabIsATriggerWithAVisualAndNoSpellYet()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPrefabPath);
            Assert.IsNotNull(prefab);
            var pickup = prefab.GetComponent<SpellPickup>();
            Assert.IsNotNull(pickup);
            Assert.IsTrue(prefab.GetComponent<Collider>().isTrigger);
            Assert.IsNotNull(Field(pickup, "visual").GetValue(pickup));
            Assert.IsNotNull(Field(pickup, "tint").GetValue(pickup));
        }

        [Test]
        public void ThePlayerPrefabHasTheSpellInteractor()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab.GetComponent<SpellInteractor>());
        }

        [Test]
        public void TheControlsBindInteractToEAndAGamepadButton()
        {
            var lines = System.IO.File.ReadAllLines(ControlsPath);
            Assert.IsTrue(System.Array.Exists(lines, l => l.Contains("\"name\": \"Interact\"") && l.Contains("\"type\": \"Button\"")));
            var keyboard = System.Array.Find(lines, l => l.Contains("\"action\": \"Interact\"") && l.Contains("<Keyboard>/e\""));
            var pad = System.Array.Find(lines, l => l.Contains("\"action\": \"Interact\"") && l.Contains("<Gamepad>/"));
            Assert.IsNotNull(keyboard);
            StringAssert.Contains("\"groups\": \"KeyboardMouse\"", keyboard);
            Assert.IsNotNull(pad);
            StringAssert.Contains("\"groups\": \"Gamepad\"", pad);
        }
    }
}

using System.Linq;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The charge pose: a Charge state on the UpperBody layer whose speed is a parameter, so the bridge can
    // play the wind-up and then freeze it while the charge is held, and let the Attack state throw on release.
    public class ChargeAnimationTests
    {
        private const string ControllerPath = "Assets/_Project/Animation/AC_WizardThirdPerson.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string FirePath = "Assets/_Project/Spells/Spell_Fire.asset";

        private Scene _scene;
        private GameObject _wizard;
        private PlayerCombat _combat;
        private ActionAnimatorBridge _bridge;
        private Animator _animator;
        private int _layer;
        private SpellDefinition _fire;

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            _wizard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            _combat = _wizard.GetComponent<PlayerCombat>();
            _bridge = _wizard.GetComponent<ActionAnimatorBridge>();
            _animator = _wizard.GetComponent<Animator>();
            _animator.Rebind();
            foreach (var shield in _wizard.GetComponentsInChildren<MagicShield>(true)) Invoke(shield, "Awake");
            Invoke(_combat, "Awake");
            Invoke(_bridge, "Awake");
            Invoke(_bridge, "Subscribe");
            _layer = _animator.GetLayerIndex(ActionAnimatorBridge.UpperBodyLayerName);
            _fire = AssetDatabase.LoadAssetAtPath<SpellDefinition>(FirePath);
            _combat.SetSpell(_fire);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wizard);
            // A released charge fires a real bolt into the open scene; leave none behind.
            foreach (var bolt in Object.FindObjectsByType<Bolt>(FindObjectsInactive.Include)) Object.DestroyImmediate(bolt.gameObject);
            BoltPool.Clear();
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        private static float Now => Time.time;

        // The state the layer is on, or heading to while it cross-fades.
        private int CurrentOrNextState() =>
            (_animator.IsInTransition(_layer) ? _animator.GetNextAnimatorStateInfo(_layer) : _animator.GetCurrentAnimatorStateInfo(_layer))
                .shortNameHash;

        // Frames of 50 ms, the bridge's Update after each, like the game loop.
        private void Advance(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += 0.05f)
            {
                _animator.Update(0.05f);
                _bridge.HoldWindUp();
            }
        }

        private float ChargeStateTime() =>
            (_animator.IsInTransition(_layer) ? _animator.GetNextAnimatorStateInfo(_layer) : _animator.GetCurrentAnimatorStateInfo(_layer))
                .normalizedTime;

        // ---- controller ----

        [Test]
        public void TheUpperBodyLayerHasAChargeStateDrivenByASpeedParameter()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var upper = controller.layers.First(l => l.name == ActionAnimatorBridge.UpperBodyLayerName).stateMachine;
            var charge = upper.states.Select(s => s.state).FirstOrDefault(s => s.name == "Charge");
            Assert.IsNotNull(charge, "UpperBody needs a Charge state");
            Assert.IsNotNull(charge.motion);
            Assert.IsTrue(charge.speedParameterActive);
            Assert.AreEqual(ActionAnimatorBridge.ChargeSpeedParameter, charge.speedParameter);
            var parameter = controller.parameters.First(p => p.name == ActionAnimatorBridge.ChargeSpeedParameter);
            Assert.AreEqual(AnimatorControllerParameterType.Float, parameter.type);
            Assert.AreEqual(1f, parameter.defaultFloat);
        }

        [Test]
        public void ChargingMapsToTheChargeStateAndChannellingStillToAttack()
        {
            Assert.AreEqual("Charge", _bridge.StateNameFor(ActionStateId.Charging));
            Assert.AreEqual("Attack", _bridge.StateNameFor(ActionStateId.Channelling));
            Assert.AreEqual("Attack", _bridge.StateNameFor(ActionStateId.Attack));
        }

        // ---- the bridge ----

        [Test]
        public void StartingAChargePlaysTheChargeState()
        {
            Assert.IsTrue(_combat.Attack());
            _animator.Update(0.0f);
            Assert.AreEqual(Animator.StringToHash("Charge"), CurrentOrNextState());
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter), "the wind-up plays");
        }

        [Test]
        public void TheWindUpFreezesAtTheHoldTimeWhileTheChargeIsHeld()
        {
            _combat.Attack();
            Advance(0.3f);
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter), "still winding up");

            Advance(1.0f);
            Assert.AreEqual(0f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter), "pose held");
            _animator.Update(0f);
            Assert.AreEqual(_fire.Charge.windUpHoldNormalized, ChargeStateTime(), 0.03f);

            Advance(2.0f);
            _animator.Update(0f);
            Assert.AreEqual(_fire.Charge.windUpHoldNormalized, ChargeStateTime(), 0.03f, "it stays on the pose");
        }

        [Test]
        public void ReleasingPlaysTheThrowAndResumesTheSpeed()
        {
            _combat.Attack();
            Advance(1.2f);
            Assert.AreEqual(0f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));

            _combat.ReleaseAttack();
            _animator.Update(0.0f);
            Assert.AreEqual(ActionStateId.Attack, _combat.Machine.Current);
            Assert.AreEqual(Animator.StringToHash("Attack"), CurrentOrNextState());
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
        }

        [Test]
        public void AParryResumesTheSpeed()
        {
            _combat.Attack();
            Advance(1.2f);
            Assert.AreEqual(0f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
            _combat.Parry();
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
        }

        [Test]
        public void ALostReleaseResumesTheSpeed()
        {
            _combat.Attack();
            Advance(1.2f);
            Assert.AreEqual(0f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
            _combat.Machine.Tick(new ActionIntent(false, false, false, true), Now + 1f);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
        }

        [Test]
        public void TheHoldDoesNothingOutsideACharge()
        {
            _bridge.HoldWindUp();
            Assert.AreEqual(1f, _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter));
        }

        [Test]
        public void ASpellCanPickAnotherWindUpStateAndFallsBackWhenItDoesNotExist()
        {
            var spell = Object.Instantiate(_fire);
            try
            {
                var charge = spell.Charge;
                charge.windUpState = "Attack";
                typeof(SpellDefinition).GetField("charge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spell, charge);
                _combat.SetSpell(spell);
                _combat.Attack();
                _animator.Update(0.0f);
                Assert.AreEqual(Animator.StringToHash("Attack"), CurrentOrNextState());

                _combat.Machine.Tick(new ActionIntent(false, false, false, true), Now + 1f); // drop the hold
                charge.windUpState = "NoSuchState";
                typeof(SpellDefinition).GetField("charge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spell, charge);
                _combat.Machine.Tick(new ActionIntent(false, false), Now + 5f);
                _combat.Attack();
                _animator.Update(0.0f);
                Assert.AreEqual(Animator.StringToHash("Charge"), CurrentOrNextState(), "unknown state: the mapped Charge");
            }
            finally
            {
                Object.DestroyImmediate(spell);
            }
        }
    }
}

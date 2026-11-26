using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HexFall.UI.Tests
{
    // Press, hold and release the real Attack action (HexFallControls) on a test mouse and deliver it the way
    // PlayerInput's Send Messages does (performed only, an InputValue built from the callback context).
    public class FireChargeInputTests : InputTestFixture
    {
        private GameObject _go;
        private PlayerCombat _combat;
        private InputActionAsset _asset;
        private Mouse _mouse;
        private SpellDefinition _fire;
        private readonly List<ProjectileShot> _shots = new List<ProjectileShot>();

        public override void Setup()
        {
            base.Setup();
            _shots.Clear();
            _fire = AssetDatabase.LoadAssetAtPath<SpellDefinition>("Assets/_Project/Spells/Spell_Fire.asset");
            _go = new GameObject("Wizard");
            _combat = _go.AddComponent<PlayerCombat>();
            // Awake does not run on AddComponent outside play mode.
            typeof(PlayerCombat).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_combat, null);
            _combat.Machine.ShotFired += _shots.Add;
            _combat.SetSpell(_fire); // picked up from the ground, as in the Sandbox

            _mouse = InputSystem.AddDevice<Mouse>();
            _asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/_Project/Input/HexFallControls.inputactions"));
            _asset.devices = new InputDevice[] { _mouse };
            var attack = _asset.FindAction("Attack");
            attack.performed += OnPerformed;
            attack.Enable();
            typeof(PlayerCombat).GetField("_attackAction", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_combat, attack);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_asset);
            base.TearDown();
        }

        // What PlayerInput does for SendMessages: performed only, an InputValue over the callback context.
        private void OnPerformed(InputAction.CallbackContext ctx)
        {
            var value = (InputValue)System.Activator.CreateInstance(typeof(InputValue), true);
            typeof(InputValue).GetField("m_Context", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, ctx);
            _combat.OnAttack(value);
        }

        private void Frame() =>
            typeof(PlayerCombat).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_combat, null);

        [Test]
        public void HoldingTheAttackButtonKeepsFireCharging()
        {
            Press(_mouse.leftButton);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "press starts the charge");
            for (var i = 0; i < 5; i++)
            {
                Frame();
                Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "still charging on frame " + i);
            }
            Release(_mouse.leftButton);
            Assert.AreEqual(1, _shots.Count, "the release fires the charged shot");
        }

        [Test]
        public void ATapOnFireStillFiresTheMinimumShotOnRelease()
        {
            Press(_mouse.leftButton);
            Release(_mouse.leftButton);
            Assert.AreEqual(1, _shots.Count);
            Assert.AreEqual(_fire.Charge.baseDamage, _shots[0].Damage);
        }

        [Test]
        public void ALostReleaseStillDropsTheChargeWithoutAShot()
        {
            Press(_mouse.leftButton);
            // Focus lost: the button state resets with no release delivered to the action.
            _asset.FindAction("Attack").Disable();
            Frame();
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.AreEqual(0, _shots.Count);
        }
    }
}

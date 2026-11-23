using System;
using System.Linq;
using HexFall.Player;
using HexFall.Player.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The UpperBody layer is code-driven by the action machine: these checks keep AC_WizardThirdPerson
    // in step with the ActionAnimatorBridge that ships on the PlayerWizard prefab.
    public class ActionAnimatorTests
    {
        private const string ControllerPath = "Assets/_Project/Animation/AC_WizardThirdPerson.controller";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private static readonly string[] RetiredTriggers = { "Attack", "Parry" };

        private AnimatorController _controller;
        private ActionAnimatorBridge _bridge;

        [SetUp]
        public void Load()
        {
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(_controller);
            Assert.IsNotNull(player);
            _bridge = player.GetComponent<ActionAnimatorBridge>();
        }

        private AnimatorStateMachine UpperBody =>
            _controller.layers.First(l => l.name == ActionAnimatorBridge.UpperBodyLayerName).stateMachine;

        [Test]
        public void PlayerPrefabHasTheActionBridge()
        {
            Assert.IsNotNull(_bridge);
        }

        [Test]
        public void EveryActionStateIsMapped()
        {
            foreach (ActionStateId id in Enum.GetValues(typeof(ActionStateId)))
                Assert.IsNotNull(_bridge.StateNameFor(id), id.ToString());
        }

        [Test]
        public void EveryMappedStateExistsOnTheUpperBodyLayer()
        {
            var names = UpperBody.states.Select(s => s.state.name).ToList();
            foreach (var required in _bridge.RequiredAnimatorStates())
                CollectionAssert.Contains(names, required);
        }

        [Test]
        public void UpperBodyLayerHasNoTriggerDrivenTransitions()
        {
            Assert.IsEmpty(UpperBody.anyStateTransitions);
            foreach (var entry in UpperBody.states)
                Assert.IsEmpty(entry.state.transitions, $"{entry.state.name} must only be left by code");
        }

        [Test]
        public void ActionTriggerParametersAreRetired()
        {
            var names = _controller.parameters.Select(p => p.name).ToList();
            foreach (var trigger in RetiredTriggers)
                CollectionAssert.DoesNotContain(names, trigger);
        }
    }
}

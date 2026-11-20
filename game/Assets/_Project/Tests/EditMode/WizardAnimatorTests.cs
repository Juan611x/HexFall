using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The controller is code-driven: these checks keep AC_WizardThirdPerson in step with
    // PlayerAnimatorBridge and the locomotion states.
    public class WizardAnimatorTests
    {
        private const string ControllerPath = "Assets/_Project/Animation/AC_WizardThirdPerson.controller";
        private static readonly string[] DrivenParameters = { "Jump", "Grounded", "FreeFall" };

        private AnimatorController _controller;
        private GameObject _host;
        private PlayerAnimatorBridge _bridge;

        [SetUp]
        public void Load()
        {
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            _host = new GameObject("BridgeHost", typeof(Animator));
            _bridge = _host.AddComponent<PlayerAnimatorBridge>();
        }

        [TearDown]
        public void Clean() => Object.DestroyImmediate(_host);

        private AnimatorStateMachine Base => _controller.layers[0].stateMachine;

        [Test]
        public void EverySelectorKeyIsMapped()
        {
            foreach (var key in new[] { AnimatorStateSelector.Locomotion, AnimatorStateSelector.JumpStart, AnimatorStateSelector.InAir, AnimatorStateSelector.JumpLand })
                Assert.IsNotNull(_bridge.StateNameFor(key), key);
        }

        [Test]
        public void EveryMappedStateExistsOnTheBaseLayer()
        {
            var names = Base.states.Select(s => s.state.name).ToList();
            foreach (var required in _bridge.RequiredAnimatorStates())
                CollectionAssert.Contains(names, required);
        }

        [Test]
        public void LocomotionBlendTreeGoesIdleWalkJogSprint()
        {
            var locomotion = Base.states.First(s => s.state.name == _bridge.StateNameFor(AnimatorStateSelector.Locomotion)).state;
            var tree = (BlendTree)locomotion.motion;
            var clips = tree.children.OrderBy(c => c.threshold).Select(c => c.motion.name).ToArray();
            CollectionAssert.AreEqual(new[] { "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop" }, clips);
            var settings = new LocomotionSettings();
            var thresholds = tree.children.OrderBy(c => c.threshold).Select(c => c.threshold).ToArray();
            CollectionAssert.AreEqual(new[] { 0f, settings.walkSpeed, settings.jogSpeed, settings.sprintSpeed }, thresholds);
        }

        [Test]
        public void NoBaseLayerTransitionDependsOnLocomotionParameters()
        {
            var transitions = Base.anyStateTransitions.Cast<AnimatorStateTransition>()
                .Concat(Base.states.SelectMany(s => s.state.transitions));
            foreach (var transition in transitions)
                foreach (var condition in transition.conditions)
                    CollectionAssert.DoesNotContain(DrivenParameters, condition.parameter,
                        "The locomotion state machine drives these states; the Animator must not.");
        }
    }
}

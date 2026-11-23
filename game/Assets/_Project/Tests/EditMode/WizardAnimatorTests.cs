using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The controller is code-driven: these checks keep AC_WizardThirdPerson in step with the
    // PlayerAnimatorBridge and PlayerMotor settings that ship on the PlayerWizard prefab.
    public class WizardAnimatorTests
    {
        private const string ControllerPath = "Assets/_Project/Animation/AC_WizardThirdPerson.controller";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private static readonly string[] DrivenParameters = { "Jump", "Grounded", "FreeFall" };

        private AnimatorController _controller;
        private GameObject _player;
        private PlayerAnimatorBridge _bridge;

        [SetUp]
        public void Load()
        {
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            _player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(_player, $"PlayerWizard prefab not found at {PlayerPrefabPath}");
            Assert.IsNotNull(_controller, $"Animator controller not found at {ControllerPath}");
            _bridge = _player.GetComponent<PlayerAnimatorBridge>();
        }

        // Walk, jog and sprint speeds as serialized on the prefab's PlayerMotor.
        private float[] PrefabSpeeds()
        {
            var settings = new SerializedObject(_player.GetComponent<PlayerMotor>()).FindProperty("settings");
            return new[]
            {
                settings.FindPropertyRelative("walkSpeed").floatValue,
                settings.FindPropertyRelative("jogSpeed").floatValue,
                settings.FindPropertyRelative("sprintSpeed").floatValue,
            };
        }

        [Test]
        public void PlayerPrefabUsesTheControllerAndTheBridge()
        {
            Assert.IsNotNull(_bridge);
            Assert.AreSame(_controller, _player.GetComponent<Animator>().runtimeAnimatorController);
        }

        private AnimatorStateMachine Base => _controller.layers[0].stateMachine;

        // PlayerIKDriver reads the animated (pre-IK) foot goals in OnAnimatorIK; without the IK pass
        // it would read the feet after the rig moved them and pin them in place.
        [Test]
        public void BaseLayerRunsTheIkPassForFootPlacement()
        {
            Assert.IsTrue(_controller.layers[0].iKPass);
        }

        [Test]
        public void EverySelectorKeyIsMapped()
        {
            foreach (var key in new[] { AnimatorStateSelector.Locomotion, AnimatorStateSelector.JumpStart, AnimatorStateSelector.InAir, AnimatorStateSelector.JumpLand, AnimatorStateSelector.Crouch, AnimatorStateSelector.Slide })
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
            var speeds = PrefabSpeeds();
            var thresholds = tree.children.OrderBy(c => c.threshold).Select(c => c.threshold).ToArray();
            CollectionAssert.AreEqual(new[] { 0f, speeds[0], speeds[1], speeds[2] }, thresholds);
        }

        private AnimatorState StateFor(string key) =>
            Base.states.First(s => s.state.name == _bridge.StateNameFor(key)).state;

        [Test]
        public void CrouchBlendTreeGoesIdleToForwardAtTheCrouchSpeed()
        {
            var tree = (BlendTree)StateFor(AnimatorStateSelector.Crouch).motion;
            Assert.AreEqual("Speed", tree.blendParameter);
            var ordered = tree.children.OrderBy(c => c.threshold).ToArray();
            CollectionAssert.AreEqual(new[] { "Crouch_Idle_Loop", "Crouch_Fwd_Loop" }, ordered.Select(c => c.motion.name).ToArray());
            var crouchSpeed = new SerializedObject(_player.GetComponent<PlayerMotor>())
                .FindProperty("settings").FindPropertyRelative("crouchSpeed").floatValue;
            Assert.AreEqual(0f, ordered[0].threshold, 1e-5f);
            Assert.AreEqual(crouchSpeed, ordered[1].threshold, 1e-5f);
        }

        private const string Ual2Path = SlideAnimationImportTests.FbxPath;

        [Test]
        public void SlideStartsWithTheUal2StartClip()
        {
            var clip = StateFor(AnimatorStateSelector.Slide).motion;
            Assert.IsNotNull(clip);
            Assert.AreEqual("Slide_Start", clip.name);
            Assert.AreEqual(Ual2Path, AssetDatabase.GetAssetPath(clip));
        }

        // The bridge only enters the "Slide" state; the clip chain runs inside the animator by itself.
        [Test]
        public void SlideStartHandsOverToTheLoopWithoutParameters()
        {
            var start = StateFor(AnimatorStateSelector.Slide);
            Assert.AreEqual(1, start.transitions.Length);
            var transition = start.transitions[0];
            Assert.IsTrue(transition.hasExitTime);
            Assert.IsEmpty(transition.conditions);
            Assert.IsNotNull(transition.destinationState);
            var loop = transition.destinationState.motion as AnimationClip;
            Assert.IsNotNull(loop);
            Assert.AreEqual("Slide_Loop", loop.name);
            Assert.AreEqual(Ual2Path, AssetDatabase.GetAssetPath(loop));
        }

        [Test]
        public void SlideLoopRepeatsUntilTheLocomotionStateLeaves()
        {
            var loopState = StateFor(AnimatorStateSelector.Slide).transitions[0].destinationState;
            Assert.IsTrue(((AnimationClip)loopState.motion).isLooping);
            Assert.IsEmpty(loopState.transitions, "Only the bridge may leave the slide loop.");
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

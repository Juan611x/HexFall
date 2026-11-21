using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The climb plays ClimbUp_1m from Quaternius' Universal Animation Library 2 (CC0), imported as a
    // humanoid clip next to the slide clips. The motor drives the position, so the clip has no root motion.
    public class ClimbAnimationImportTests
    {
        private const string ClipName = "ClimbUp_1m";

        private static ModelImporter Importer => (ModelImporter)AssetImporter.GetAtPath(SlideAnimationImportTests.FbxPath);

        private static ModelImporterClipAnimation Definition =>
            Importer.clipAnimations.FirstOrDefault(c => c.name == ClipName);

        [Test]
        public void ClimbUpIsDefinedFromTheLibraryTake()
        {
            var definition = Definition;
            Assert.IsNotNull(definition);
            Assert.AreEqual("Armature|ClimbUp_1m", definition.takeName);
        }

        [Test]
        public void ClimbUpPlaysOnceWithoutLooping()
        {
            Assert.IsFalse(Definition.loopTime);
            Assert.IsFalse(Definition.loopPose);
        }

        [Test]
        public void ClimbUpIsAnAnimationClipOfTheLibrary()
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(SlideAnimationImportTests.FbxPath)
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == ClipName);
            Assert.IsNotNull(clip);
            Assert.IsTrue(clip.humanMotion);
            Assert.Greater(clip.length, 0.3f);
        }

        // The take is one seamless climbing step (0.67 s, the last pose equals the first): the hands reach
        // their highest at frame 10, then the body comes upright. The scripted path plays once over the clip:
        // reach until the hands grab (frame 10), pull-up for the rest.
        private const float GrabFrame = 10f;
        private const float FrameRate = 30f;

        private static AnimationClip Clip => AssetDatabase.LoadAllAssetsAtPath(SlideAnimationImportTests.FbxPath)
            .OfType<AnimationClip>().First(c => c.name == ClipName);

        [Test]
        public void TheDefaultPathMatchesTheClipLengthSplitWhereTheHandsGrab()
        {
            var climb = new ClimbSettings();
            Assert.AreEqual(Clip.length, climb.reachSeconds + climb.pullUpSeconds, 0.02f);
            Assert.AreEqual(GrabFrame / FrameRate, climb.reachSeconds, 0.02f);
        }

        [Test]
        public void ThePlayerPrefabPathMatchesTheClipLength()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            var climb = new SerializedObject(prefab.GetComponent<PlayerMotor>())
                .FindProperty("settings").FindPropertyRelative("climb");
            var reach = climb.FindPropertyRelative("reachSeconds").floatValue;
            var pullUp = climb.FindPropertyRelative("pullUpSeconds").floatValue;
            Assert.AreEqual(Clip.length, reach + pullUp, 0.02f);
            Assert.AreEqual(GrabFrame / FrameRate, reach, 0.02f);
        }

        [Test]
        public void ClimbUpUsesTheSameRootSettingsAsTheSlide()
        {
            var climb = Definition;
            var slide = Importer.clipAnimations.First(c => c.name == "Slide_Start");
            Assert.AreEqual(slide.keepOriginalPositionY, climb.keepOriginalPositionY);
            Assert.AreEqual(slide.keepOriginalPositionXZ, climb.keepOriginalPositionXZ);
        }
    }
}

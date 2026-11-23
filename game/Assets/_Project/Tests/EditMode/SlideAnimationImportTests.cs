using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The slide plays real clips from Quaternius' Universal Animation Library 2 (CC0), imported as a
    // humanoid so they retarget onto the wizard like the UAL1 clips do.
    public class SlideAnimationImportTests
    {
        public const string FbxPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";
        private const string LicensePath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/License.txt";

        private static ModelImporter Importer => (ModelImporter)AssetImporter.GetAtPath(FbxPath);

        [Test]
        public void TheLibraryShipsWithItsLicense()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(LicensePath));
        }

        [Test]
        public void TheLibraryIsImportedAsAHumanoid()
        {
            Assert.AreEqual(ModelImporterAnimationType.Human, Importer.animationType);
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FbxPath);
            Assert.IsNotNull(avatar);
            Assert.IsTrue(avatar.isValid);
            Assert.IsTrue(avatar.isHuman);
        }

        [Test]
        public void TheSlideClimbAndHopClipsAreDefined()
        {
            var names = Importer.clipAnimations.Select(c => c.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Slide_Exit", "Slide_Loop", "Slide_Start", "ClimbUp_1m", "NinjaJump_Start", "NinjaJump_Idle_Loop", "NinjaJump_Land" }, names);
        }

        // The hop clips are authored in place like the rest of the library.
        [TestCase("NinjaJump_Start")]
        [TestCase("NinjaJump_Idle_Loop")]
        [TestCase("NinjaJump_Land")]
        public void TheHopClipsAreInPlaceHumanoidClips(string clipName)
        {
            var clip = Importer.clipAnimations.First(c => c.name == clipName);
            Assert.AreEqual("Armature|" + clipName, clip.takeName);
            Assert.IsTrue(clip.keepOriginalPositionY, clipName);
            Assert.IsTrue(clip.keepOriginalPositionXZ, clipName);
        }

        // The first frames of the ninja jump start are the crouched wind-up on the ground; the hop starts
        // in the air, so the clip is trimmed to begin once the body has left the ground.
        [Test]
        public void TheHopStartSkipsTheGroundWindUp()
        {
            var clip = Importer.clipAnimations.First(c => c.name == "NinjaJump_Start");
            Assert.That(clip.firstFrame, Is.InRange(3f, 8f));
            Assert.AreEqual(29f, clip.lastFrame);
        }

        [TestCase("Slide_Loop", true)]
        [TestCase("NinjaJump_Idle_Loop", true)]
        [TestCase("NinjaJump_Start", false)]
        [TestCase("NinjaJump_Land", false)]
        [TestCase("ClimbUp_1m", false)]
        [TestCase("Slide_Start", false)]
        [TestCase("Slide_Exit", false)]
        public void OnlyTheLoopClipsLoop(string clipName, bool loops)
        {
            var clip = Importer.clipAnimations.First(c => c.name == clipName);
            Assert.AreEqual(loops, clip.loopTime);
        }

        // Retargeted onto the shorter wizard the low slide pose sinks its seat a few centimetres into the
        // floor, so the slide clips are lifted a little.
        [TestCase("Slide_Start")]
        [TestCase("Slide_Loop")]
        [TestCase("Slide_Exit")]
        public void TheSlideClipsAreLiftedClearOfTheFloor(string clipName)
        {
            var clip = Importer.clipAnimations.First(c => c.name == clipName);
            // Unity applies the clip offset as a downward shift, so lifting the pose takes a negative value.
            Assert.That(clip.heightOffset, Is.InRange(-0.15f, -0.05f), clipName);
        }

        [TestCase("Slide_Start")]
        [TestCase("Slide_Loop")]
        [TestCase("Slide_Exit")]
        [TestCase("NinjaJump_Start")]
        [TestCase("NinjaJump_Idle_Loop")]
        [TestCase("NinjaJump_Land")]
        public void EverySlideClipIsAnAnimationClipOfTheLibrary(string clipName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            Assert.IsNotNull(clip, clipName);
            Assert.Greater(clip.length, 0.1f, clipName);
        }
    }
}

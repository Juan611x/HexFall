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
        public void TheSlideAndClimbClipsAreDefined()
        {
            var names = Importer.clipAnimations.Select(c => c.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Slide_Exit", "Slide_Loop", "Slide_Start", "ClimbUp_1m" }, names);
        }

        [TestCase("Slide_Loop", true)]
        [TestCase("Slide_Start", false)]
        [TestCase("Slide_Exit", false)]
        public void OnlyTheSlideLoopLoops(string clipName, bool loops)
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
        public void EverySlideClipIsAnAnimationClipOfTheLibrary(string clipName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            Assert.IsNotNull(clip, clipName);
            Assert.Greater(clip.length, 0.1f, clipName);
        }
    }
}

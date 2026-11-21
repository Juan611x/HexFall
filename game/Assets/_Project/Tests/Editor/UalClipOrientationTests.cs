using HexFall.Editor.Import;
using NUnit.Framework;
using UnityEditor;

namespace HexFall.Editor.Build.Tests
{
    public class UalClipOrientationTests
    {
        private static ModelImporterClipAnimation Clip(string name) =>
            new ModelImporterClipAnimation { name = name, keepOriginalOrientation = false, rotationOffset = 27.5f };

        [TestCase("Walk_Loop")]
        [TestCase("Jog_Fwd_Loop")]
        [TestCase("Sprint_Loop")]
        [TestCase("Run_Loop")]
        [TestCase("Crouch_Fwd_Loop")]
        public void TravellingClipsFaceTheirAuthoredDirectionTurnedToUnityForward(string name)
        {
            var clip = Clip(name);

            UalClipOrientation.Apply(clip);

            Assert.IsTrue(clip.keepOriginalOrientation);
            Assert.AreEqual(180f, clip.rotationOffset);
            Assert.IsTrue(clip.lockRootRotation);
        }

        [TestCase("Idle_Loop")]
        [TestCase("Crouch_Idle_Loop")]
        [TestCase("Jump_Start")]
        [TestCase("Spell_Simple_Shoot")]
        public void InPlaceClipsKeepUnitysBodyOrientation(string name)
        {
            var clip = Clip(name);

            UalClipOrientation.Apply(clip);

            Assert.IsFalse(clip.keepOriginalOrientation);
            Assert.AreEqual(0f, clip.rotationOffset);
            Assert.IsTrue(clip.lockRootRotation);
        }

        [Test]
        public void OnlyTheUniversalAnimationLibraryIsAffected()
        {
            Assert.IsTrue(UalClipOrientation.AppliesTo("Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx"));
            Assert.IsFalse(UalClipOrientation.AppliesTo("Assets/_Project/Art/Characters/Wizard/WizardHumanoid.fbx"));
        }
    }
}

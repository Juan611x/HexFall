using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The wizard sounds are synthesized by art/audio/compose_wizard_sfx.py into Audio/Sfx, except Snap, a recorded
    // finger snap derived by art/audio/derive_finger_snap.py from ThirdParty/FingerSnaps.
    public class WizardSfxImportTests
    {
        private static readonly string[] Clips =
        {
            "WizardCast", "BoltImpact", "ShieldRaise", "ParryDeflect", "ParryPerfect", "Snap", "Huff", "Jump",
        };

        [TestCaseSource(nameof(Clips))]
        public void ClipImportsAsAnAudioClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/Sfx/{name}.wav");
            Assert.IsNotNull(clip, name);
            Assert.Greater(clip.length, 0.1f);
        }

        [Test]
        public void SnapIsARealRecordedFingerSnap()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Sfx/Snap.wav");
            Assert.IsNotNull(clip);
            Assert.AreEqual(1, clip.channels, "mono");
            Assert.AreEqual(44100, clip.frequency, "kept at the source rate");
            Assert.That(clip.length, Is.InRange(0.15f, 0.35f), "a short snap with its natural ring");
        }

        [Test]
        public void SnapSourceAndLicenseAreInTheRepo()
        {
            Assert.IsTrue(File.Exists("Assets/ThirdParty/FingerSnaps/177496_snapper4298.wav"));
            var license = File.ReadAllText("Assets/ThirdParty/FingerSnaps/LICENSE.txt");
            StringAssert.Contains("177496", license);
            StringAssert.Contains("public domain", license.ToLowerInvariant());
        }
    }
}

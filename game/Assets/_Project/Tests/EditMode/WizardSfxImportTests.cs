using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The wizard sounds are synthesized by art/audio/compose_wizard_sfx.py into Audio/Sfx.
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
    }
}

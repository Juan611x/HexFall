using HexFall.Player;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class LandingLoudnessTests
    {
        private const float Min = 2f;
        private const float Full = 9f;

        [Test]
        public void BelowTheMinimumSpeedIsSilent()
        {
            Assert.AreEqual(0f, LandingLoudness.For(1.9f, Min, Full));
            Assert.AreEqual(0f, LandingLoudness.For(0f, Min, Full));
            Assert.AreEqual(0f, LandingLoudness.For(-5f, Min, Full));
        }

        [Test]
        public void AtTheMinimumSpeedIsAlreadyAudible()
        {
            Assert.AreEqual(LandingLoudness.SoftestLoudness, LandingLoudness.For(Min, Min, Full), 1e-5f);
            Assert.Greater(LandingLoudness.SoftestLoudness, 0f);
        }

        [Test]
        public void GrowsLinearlyUpToTheFullSpeed()
        {
            var middle = LandingLoudness.For((Min + Full) * 0.5f, Min, Full);
            Assert.AreEqual(LandingLoudness.SoftestLoudness + (1f - LandingLoudness.SoftestLoudness) * 0.5f, middle, 1e-5f);
            Assert.AreEqual(1f, LandingLoudness.For(Full, Min, Full), 1e-5f);
        }

        [Test]
        public void ClampsAboveTheFullSpeed()
        {
            Assert.AreEqual(1f, LandingLoudness.For(40f, Min, Full), 1e-5f);
        }

        [Test]
        public void NaNAndInvalidRangesAreSilent()
        {
            Assert.AreEqual(0f, LandingLoudness.For(float.NaN, Min, Full));
            Assert.AreEqual(0f, LandingLoudness.For(5f, float.NaN, Full));
            Assert.AreEqual(0f, LandingLoudness.For(5f, Min, float.NaN));
        }

        [Test]
        public void ARangeWithoutSpanActsAsAStep()
        {
            Assert.AreEqual(0f, LandingLoudness.For(2.9f, 3f, 3f));
            Assert.AreEqual(1f, LandingLoudness.For(3f, 3f, 3f), 1e-5f);
        }
    }

    public class PlayerLandingsPrefabTests
    {
        [Test]
        public void TheWizardPrefabHasTheLandingPlayer()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<PlayerLandings>());
        }
    }
}

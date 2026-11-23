using HexFall.Player;
using HexFall.Player.Locomotion;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class SlideScrapeLevelsTests
    {
        [Test]
        public void SpeedFactorGrowsLinearlyUpToTheFullSpeed()
        {
            Assert.AreEqual(0.5f, SlideScrapeLevels.SpeedFactor(3f, 6f), 1e-5f);
            Assert.AreEqual(1f, SlideScrapeLevels.SpeedFactor(6f, 6f), 1e-5f);
        }

        [Test]
        public void SpeedFactorClampsAboveTheFullSpeedAndBelowZero()
        {
            Assert.AreEqual(1f, SlideScrapeLevels.SpeedFactor(12f, 6f), 1e-5f);
            Assert.AreEqual(0f, SlideScrapeLevels.SpeedFactor(-2f, 6f), 1e-5f);
        }

        [Test]
        public void SpeedFactorIsZeroForNaNSpeed()
        {
            Assert.AreEqual(0f, SlideScrapeLevels.SpeedFactor(float.NaN, 6f));
        }

        [Test]
        public void AFullSpeedWithoutSpanActsAsFullLoudness()
        {
            Assert.AreEqual(1f, SlideScrapeLevels.SpeedFactor(2f, 0f));
            Assert.AreEqual(1f, SlideScrapeLevels.SpeedFactor(2f, float.NaN));
        }

        [Test]
        public void FadeGainFallsLinearlyFromOneToZero()
        {
            Assert.AreEqual(1f, SlideScrapeLevels.FadeGain(0f, 0.12f), 1e-5f);
            Assert.AreEqual(0.5f, SlideScrapeLevels.FadeGain(0.06f, 0.12f), 1e-5f);
            Assert.AreEqual(0f, SlideScrapeLevels.FadeGain(0.12f, 0.12f), 1e-5f);
        }

        [Test]
        public void FadeGainStaysInsideZeroAndOne()
        {
            Assert.AreEqual(0f, SlideScrapeLevels.FadeGain(5f, 0.12f));
            Assert.AreEqual(1f, SlideScrapeLevels.FadeGain(-1f, 0.12f));
        }

        [Test]
        public void ANonPositiveFadeTimeCutsImmediately()
        {
            Assert.AreEqual(0f, SlideScrapeLevels.FadeGain(0f, 0f));
            Assert.AreEqual(0f, SlideScrapeLevels.FadeGain(0.1f, -1f));
            Assert.AreEqual(0f, SlideScrapeLevels.FadeGain(float.NaN, 0.12f));
        }
    }

    public class SlideScrapeAssetsTests
    {
        private const string SurfacesPath = "Assets/_Project/Surfaces";
        private const string ClipPath = "Assets/ThirdParty/OpenGameArt/Scrapes/SlideScrape.wav";

        private static SurfaceEffect SlideEffect(string surfaceName)
        {
            var surface = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>($"{SurfacesPath}/{surfaceName}.asset");
            Assert.IsNotNull(surface, $"{surfaceName} not found.");
            Assert.IsTrue(surface.TryGetEffect(SurfaceImpact.Slide, out var effect), $"{surfaceName} has no Slide effect.");
            return effect;
        }

        [Test]
        public void SlideIsAppendedAfterTheExistingImpacts()
        {
            Assert.AreEqual(0, (int)SurfaceImpact.Footstep);
            Assert.AreEqual(1, (int)SurfaceImpact.Landing);
            Assert.AreEqual(2, (int)SurfaceImpact.Slide);
        }

        [Test]
        public void TheScrapeClipIsImportedAsMono()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            Assert.IsNotNull(clip, "SlideScrape.wav not found.");
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(1.14f, clip.length, 0.02f);
        }

        [TestCase("Surface_Brick")]
        [TestCase("Surface_Default")]
        [TestCase("Surface_Grass")]
        [TestCase("Surface_Metal")]
        [TestCase("Surface_Stone")]
        [TestCase("Surface_Wood")]
        public void EverySurfaceScrapesWithTheSlideClip(string surfaceName)
        {
            var effect = SlideEffect(surfaceName);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            Assert.AreEqual(1, effect.clips.Length);
            Assert.AreEqual(clip, effect.clips[0]);
        }

        [TestCase("Surface_Brick", 0.8f, 1f, 0.95f, 1.05f)]
        [TestCase("Surface_Default", 0.8f, 1f, 0.95f, 1.05f)]
        [TestCase("Surface_Stone", 0.8f, 1f, 0.95f, 1.05f)]
        [TestCase("Surface_Grass", 0.6f, 0.8f, 0.9f, 1f)]
        [TestCase("Surface_Wood", 0.7f, 0.9f, 0.92f, 1.02f)]
        [TestCase("Surface_Metal", 0.8f, 1f, 1.05f, 1.2f)]
        public void SlideLevelsAreTunedPerSurface(string surfaceName, float volMin, float volMax, float pitchMin, float pitchMax)
        {
            var effect = SlideEffect(surfaceName);
            Assert.AreEqual(volMin, effect.volumeRange.x, 1e-4f);
            Assert.AreEqual(volMax, effect.volumeRange.y, 1e-4f);
            Assert.AreEqual(pitchMin, effect.pitchRange.x, 1e-4f);
            Assert.AreEqual(pitchMax, effect.pitchRange.y, 1e-4f);
        }

        [Test]
        public void TheWizardPrefabHasTheSlideScrapePlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<PlayerSlideScrape>());
        }
    }
}

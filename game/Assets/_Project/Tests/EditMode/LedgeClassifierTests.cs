using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class LedgeClassifierTests
    {
        private static ClimbSettings Settings() => new ClimbSettings();

        // A ledge straight ahead (+Z) with a free landing behind it; override what each test cares about.
        private static LedgeMeasurement Ledge(
            float height = 1f,
            float depth = 2f,
            float headroom = 3f,
            bool landingClear = true,
            bool hasLedge = true) =>
            new LedgeMeasurement(hasLedge, height, new Vector3(0f, height, 1f), Vector3.forward, depth, headroom, landingClear, 0f);

        // Height window (inclusive on both ends)

        [Test]
        public void NoLedgeIsNone()
        {
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(hasLedge: false), Settings()));
        }

        [Test]
        public void DefaultMeasurementHasNoLedge()
        {
            Assert.IsFalse(default(LedgeMeasurement).HasLedge);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(default, Settings()));
        }

        [Test]
        public void BelowTheMinimumHeightIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(height: s.climbMinHeight - 0.01f), s));
        }

        [Test]
        public void ExactlyTheMinimumHeightIsInclusive()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(height: s.climbMinHeight), s));
        }

        [Test]
        public void ExactlyTheMaximumHeightIsInclusive()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(height: s.climbMaxHeight), s));
        }

        [Test]
        public void AboveTheMaximumHeightIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(height: s.climbMaxHeight + 0.01f), s));
        }

        // Climb up: deep enough to stand with headroom

        [Test]
        public void DeepTopWithHeadroomIsClimbUp()
        {
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(depth: 2f, headroom: 3f), Settings()));
        }

        [Test]
        public void ExactlyTheStandDepthAndHeadroomIsInclusive()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(depth: s.standDepth, headroom: s.minHeadroom), s));
        }

        [Test]
        public void AnEndlessTopIsClimbUp()
        {
            Assert.AreEqual(ClimbKind.ClimbUp,
                LedgeClassifier.Classify(Ledge(depth: float.PositiveInfinity, headroom: float.PositiveInfinity), Settings()));
        }

        [Test]
        public void DeepTopWithoutHeadroomIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(depth: 2f, headroom: s.minHeadroom - 0.01f), s));
        }

        [Test]
        public void ClimbUpDoesNotNeedALandingBehind()
        {
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(depth: 2f, landingClear: false), Settings()));
        }

        // Climb over: thin top with a free landing

        [Test]
        public void ThinTopWithFreeLandingAndNoHeadroomIsClimbOver()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbOver,
                LedgeClassifier.Classify(Ledge(depth: s.standDepth - 0.2f, headroom: 0f, landingClear: true), s));
        }

        [Test]
        public void ThinTopWithFreeLandingButNotStandableIsClimbOver()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbOver,
                LedgeClassifier.Classify(Ledge(depth: 0.2f, headroom: 3f, landingClear: true), s));
        }

        [Test]
        public void NoHeadroomButThinWithFreeLandingIsClimbOver()
        {
            var s = Settings();
            // Deep enough to stand (>= standDepth) but no headroom, still within the over thickness.
            Assert.AreEqual(ClimbKind.ClimbOver,
                LedgeClassifier.Classify(Ledge(depth: s.standDepth, headroom: s.minHeadroom - 0.5f, landingClear: true), s));
        }

        [Test]
        public void ExactlyTheOverThicknessIsInclusive()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.ClimbOver,
                LedgeClassifier.Classify(Ledge(depth: s.overMaxThickness, headroom: 0f, landingClear: true), s));
        }

        [Test]
        public void JustThickerThanTheOverThicknessWithoutHeadroomIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None,
                LedgeClassifier.Classify(Ledge(depth: s.overMaxThickness + 0.01f, headroom: 0f, landingClear: true), s));
        }

        [Test]
        public void ThinTopWithBlockedLandingAndTooThinToStandIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None,
                LedgeClassifier.Classify(Ledge(depth: s.standDepth - 0.2f, landingClear: false), s));
        }

        [Test]
        public void ThinTopWithBlockedLandingAndNoHeadroomIsNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None,
                LedgeClassifier.Classify(Ledge(depth: s.standDepth, headroom: 0f, landingClear: false), s));
        }

        // Invalid input

        [Test]
        public void NanInputsAreNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(height: float.NaN), s));
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(depth: float.NaN), s));
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(headroom: float.NaN), s));
        }

        [Test]
        public void NegativeInputsAreNone()
        {
            var s = Settings();
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(height: -1f), s));
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(depth: -0.1f), s));
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(headroom: -0.1f), s));
        }

        [Test]
        public void ThresholdsComeFromTheSettings()
        {
            var s = Settings();
            s.climbMaxHeight = 2f;
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(Ledge(height: 1.5f), s));
            s.climbMinHeight = 1.6f;
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(Ledge(height: 1.5f), s));
        }

        [Test]
        public void SettingsAreReachableFromLocomotionSettings()
        {
            Assert.IsNotNull(new LocomotionSettings().climb);
        }
    }
}

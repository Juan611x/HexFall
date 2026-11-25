using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // The maths behind the charge feedback: how far a hold has charged, and what size, colour and speed a
    // shot or the charging orb gets at that level.
    public class ChargeFeedbackTests
    {
        private static readonly Rgb Red = new Rgb(1f, 0.2f, 0f);
        private static readonly Rgb White = new Rgb(1f, 1f, 0.8f);

        private static ChargeFeedbackConfig Fire() => new ChargeFeedbackConfig
        {
            MinScale = 0.7f, MaxScale = 1.8f, ColorBlend = 1f, FullColor = White, SpeedScaleAtMax = 1.5f,
        };

        [TestCase(0f, 1.2f, 0f)]
        [TestCase(0.6f, 1.2f, 0.5f)]
        [TestCase(1.2f, 1.2f, 1f)]
        [TestCase(5f, 1.2f, 1f)]
        [TestCase(-1f, 1.2f, 0f)]
        public void LevelIsTheHeldFractionOfTheMaxCharge(float held, float max, float expected)
        {
            Assert.AreEqual(expected, ChargeFeedback.Level(held, max), 1e-5f);
        }

        [Test]
        public void ANonPositiveMaxChargeIsFullAtOnce()
        {
            Assert.AreEqual(1f, ChargeFeedback.Level(0f, 0f));
            Assert.AreEqual(1f, ChargeFeedback.Level(0.1f, -2f));
        }

        [Test]
        public void ANonFiniteHoldChargesNothingAndAnInfiniteOneIsFull()
        {
            Assert.AreEqual(0f, ChargeFeedback.Level(float.NaN, 1.2f));
            Assert.AreEqual(1f, ChargeFeedback.Level(float.PositiveInfinity, 1.2f));
            Assert.AreEqual(1f, ChargeFeedback.Level(0.5f, float.NaN));
        }

        [Test]
        public void NoChargeLooksLikeTheSpellAtItsMinimumSize()
        {
            var look = ChargeFeedback.Evaluate(Fire(), Red, 0f);
            Assert.AreEqual(0.7f, look.Scale, 1e-5f);
            Assert.AreEqual(Red.R, look.Color.R, 1e-5f);
            Assert.AreEqual(Red.G, look.Color.G, 1e-5f);
            Assert.AreEqual(Red.B, look.Color.B, 1e-5f);
            Assert.AreEqual(1f, look.SpeedScale, 1e-5f);
        }

        [Test]
        public void FullChargeIsTheBiggestFastestAndBrightestShot()
        {
            var look = ChargeFeedback.Evaluate(Fire(), Red, 1f);
            Assert.AreEqual(1.8f, look.Scale, 1e-5f);
            Assert.AreEqual(White.R, look.Color.R, 1e-5f);
            Assert.AreEqual(White.G, look.Color.G, 1e-5f);
            Assert.AreEqual(White.B, look.Color.B, 1e-5f);
            Assert.AreEqual(1.5f, look.SpeedScale, 1e-5f);
        }

        [Test]
        public void HalfChargeIsHalfwayBetweenTheTwoLooks()
        {
            var look = ChargeFeedback.Evaluate(Fire(), Red, 0.5f);
            Assert.AreEqual(1.25f, look.Scale, 1e-5f);
            Assert.AreEqual(1f, look.Color.R, 1e-5f);
            Assert.AreEqual(0.6f, look.Color.G, 1e-5f);
            Assert.AreEqual(0.4f, look.Color.B, 1e-5f);
            Assert.AreEqual(1.25f, look.SpeedScale, 1e-5f);
        }

        [Test]
        public void ColourBlendLimitsHowFarTheColourTravels()
        {
            var cfg = Fire();
            cfg.ColorBlend = 0.5f;
            var look = ChargeFeedback.Evaluate(cfg, Red, 1f);
            Assert.AreEqual(0.6f, look.Color.G, 1e-5f);
            cfg.ColorBlend = 0f;
            Assert.AreEqual(Red.G, ChargeFeedback.Evaluate(cfg, Red, 1f).Color.G, 1e-5f);
        }

        [Test]
        public void ALevelOutsideZeroToOneIsClamped()
        {
            Assert.AreEqual(1.8f, ChargeFeedback.Evaluate(Fire(), Red, 7f).Scale, 1e-5f);
            Assert.AreEqual(0.7f, ChargeFeedback.Evaluate(Fire(), Red, -3f).Scale, 1e-5f);
            Assert.AreEqual(0.7f, ChargeFeedback.Evaluate(Fire(), Red, float.NaN).Scale, 1e-5f);
        }

        [Test]
        public void TheNeutralConfigChangesNothing()
        {
            var look = ChargeFeedback.Evaluate(ChargeFeedbackConfig.Neutral, Red, 1f);
            Assert.AreEqual(1f, look.Scale, 1e-5f);
            Assert.AreEqual(Red.G, look.Color.G, 1e-5f);
            Assert.AreEqual(1f, look.SpeedScale, 1e-5f);
        }

        [Test]
        public void BadConfigValuesFallBackToSafeOnes()
        {
            var cfg = new ChargeFeedbackConfig
            {
                MinScale = float.NaN, MaxScale = -2f, ColorBlend = float.PositiveInfinity,
                FullColor = White, SpeedScaleAtMax = 0f,
            };
            var look = ChargeFeedback.Evaluate(cfg, Red, 1f);
            Assert.Greater(look.Scale, 0f);
            Assert.Greater(look.SpeedScale, 0f);
            Assert.IsFalse(float.IsNaN(look.Color.R) || float.IsNaN(look.Color.G) || float.IsNaN(look.Color.B));
        }

        [TestCase(0.49f, 0.5f, false)]
        [TestCase(0.5f, 0.5f, true)]
        [TestCase(2.3f, 0.5f, true)]
        [TestCase(0f, 0f, true)]
        [TestCase(0.9f, 1.7f, false)]
        [TestCase(1f, 1.7f, true)]
        public void TheWindUpIsReachedAtTheHoldTime(float normalizedTime, float hold, bool reached)
        {
            Assert.AreEqual(reached, ChargeFeedback.WindUpReached(normalizedTime, hold));
        }

        [Test]
        public void ABadHoldTimeOrAnimatorTimeNeverReadsAsReachedByAccident()
        {
            Assert.IsFalse(ChargeFeedback.WindUpReached(float.NaN, 0.5f));
            Assert.IsTrue(ChargeFeedback.WindUpReached(0.5f, float.NaN), "no usable hold time: hold at the start");
        }

        [Test]
        public void PulseDoesNothingBeforeFullCharge()
        {
            Assert.AreEqual(1f, ChargeFeedback.Pulse(0.99f, 3f));
            Assert.AreEqual(1f, ChargeFeedback.Pulse(0f, 0f));
        }

        [Test]
        public void PulseOscillatesAroundOneOnceFull()
        {
            // 5 Hz, amplitude 0.1: a quarter period (0.05 s) is the crest, three quarters the trough.
            Assert.AreEqual(1f, ChargeFeedback.Pulse(1f, 0f, 5f, 0.1f), 1e-5f);
            Assert.AreEqual(1.1f, ChargeFeedback.Pulse(1f, 0.05f, 5f, 0.1f), 1e-4f);
            Assert.AreEqual(0.9f, ChargeFeedback.Pulse(1f, 0.15f, 5f, 0.1f), 1e-4f);
        }

        [Test]
        public void PulseStaysWithinItsAmplitudeAndIgnoresBadInput()
        {
            for (var i = 0; i < 50; i++)
                Assert.That(ChargeFeedback.Pulse(1f, i * 0.037f, 5f, 0.1f), Is.InRange(0.9f - 1e-4f, 1.1f + 1e-4f));
            Assert.AreEqual(1f, ChargeFeedback.Pulse(1f, 1f, 5f, 0f));
            Assert.AreEqual(1f, ChargeFeedback.Pulse(1f, -1f, 5f, 0.1f));
            Assert.AreEqual(1f, ChargeFeedback.Pulse(1f, float.NaN, 5f, 0.1f));
        }
    }
}

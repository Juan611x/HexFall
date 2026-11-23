using HexFall.Surfaces;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class NoRepeatPickerTests
    {
        [Test]
        public void NeverReturnsThePreviousIndex()
        {
            for (var previous = 0; previous < 5; previous++)
            for (var step = 0; step <= 100; step++)
            {
                var index = SurfaceEffectPicker.PickIndexAvoiding(5, previous, step / 100f);
                Assert.AreNotEqual(previous, index, $"previous={previous} random={step / 100f}");
                Assert.That(index, Is.InRange(0, 4));
            }
        }

        [Test]
        public void CoversEveryOtherIndex()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var step = 0; step <= 100; step++)
                seen.Add(SurfaceEffectPicker.PickIndexAvoiding(5, 2, step / 100f));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3, 4 }, seen);
        }

        [Test]
        public void TwoClipsAlternate()
        {
            Assert.AreEqual(1, SurfaceEffectPicker.PickIndexAvoiding(2, 0, 0.3f));
            Assert.AreEqual(0, SurfaceEffectPicker.PickIndexAvoiding(2, 1, 0.9f));
        }

        [Test]
        public void SingleClipRepeatsAndEmptyHasNone()
        {
            Assert.AreEqual(0, SurfaceEffectPicker.PickIndexAvoiding(1, 0, 0.5f));
            Assert.AreEqual(-1, SurfaceEffectPicker.PickIndexAvoiding(0, 0, 0.5f));
            Assert.AreEqual(-1, SurfaceEffectPicker.PickIndexAvoiding(-3, 1, 0.5f));
        }

        [Test]
        public void OutOfRangePreviousBehavesLikePickIndex()
        {
            foreach (var previous in new[] { -1, 5, 99 })
                Assert.AreEqual(SurfaceEffectPicker.PickIndex(5, 0.7f), SurfaceEffectPicker.PickIndexAvoiding(5, previous, 0.7f));
        }

        [Test]
        public void NaNAndOutOfRangeRandomAreSafe()
        {
            Assert.AreNotEqual(1, SurfaceEffectPicker.PickIndexAvoiding(5, 1, float.NaN));
            Assert.AreNotEqual(4, SurfaceEffectPicker.PickIndexAvoiding(5, 4, 7f));
            Assert.AreNotEqual(0, SurfaceEffectPicker.PickIndexAvoiding(5, 0, -3f));
        }

        [Test]
        public void VariationNeverRepeatsAcrossManyPicks()
        {
            var effect = new SurfaceEffect { clips = new AudioClip[4] };
            var variation = new ClipVariation();
            var rng = new System.Random(7);
            var last = -2;
            for (var i = 0; i < 500; i++)
            {
                var index = variation.Pick(effect, (float)rng.NextDouble());
                Assert.AreNotEqual(last, index);
                last = index;
            }
        }

        [Test]
        public void VariationRemembersEachEffectSeparately()
        {
            var a = new SurfaceEffect { clips = new AudioClip[3] };
            var b = new SurfaceEffect { clips = new AudioClip[3] };
            var variation = new ClipVariation();
            var first = variation.Pick(a, 0f);
            variation.Pick(b, 0f);
            Assert.AreNotEqual(first, variation.Pick(a, 0f));
        }

        [Test]
        public void VariationHandlesEffectsWithoutClips()
        {
            var variation = new ClipVariation();
            Assert.AreEqual(-1, variation.Pick(null, 0.5f));
            Assert.AreEqual(-1, variation.Pick(new SurfaceEffect { clips = null }, 0.5f));
            Assert.AreEqual(-1, variation.Pick(new SurfaceEffect { clips = new AudioClip[0] }, 0.5f));
        }
    }
}

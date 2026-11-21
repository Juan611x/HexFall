using HexFall.Surfaces;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class SurfaceEffectPickerTests
    {
        // Index selection maps a 0..1 random value onto the clip list.
        [TestCase(0, 0.5f, -1)]
        [TestCase(1, 0.99f, 0)]
        [TestCase(4, 0f, 0)]
        [TestCase(4, 0.26f, 1)]
        [TestCase(4, 0.99f, 3)]
        [TestCase(4, 1f, 3)]
        [TestCase(4, -0.5f, 0)]
        public void PickIndex_MapsRandomToClip(int count, float random, int expected)
        {
            Assert.AreEqual(expected, SurfaceEffectPicker.PickIndex(count, random));
        }

        [Test]
        public void PickIndex_NaNRandom_TreatedAsZero()
        {
            Assert.AreEqual(0, SurfaceEffectPicker.PickIndex(4, float.NaN));
            Assert.AreEqual(-1, SurfaceEffectPicker.PickIndex(0, float.NaN));
        }

        [Test]
        public void Lerp_NaNRandom_ReturnsRangeStart()
        {
            Assert.AreEqual(0.8f, SurfaceEffectPicker.Lerp(new Vector2(0.8f, 1f), float.NaN), 1e-5f);
        }

        [Test]
        public void Lerp_InterpolatesAndClampsRandom()
        {
            var range = new Vector2(0.8f, 1f);
            Assert.AreEqual(0.8f, SurfaceEffectPicker.Lerp(range, 0f), 1e-5f);
            Assert.AreEqual(0.9f, SurfaceEffectPicker.Lerp(range, 0.5f), 1e-5f);
            Assert.AreEqual(1f, SurfaceEffectPicker.Lerp(range, 2f), 1e-5f);
            Assert.AreEqual(0.8f, SurfaceEffectPicker.Lerp(range, -1f), 1e-5f);
        }
    }
}

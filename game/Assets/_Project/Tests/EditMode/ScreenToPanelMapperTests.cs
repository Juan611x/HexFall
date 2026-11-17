using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class ScreenToPanelMapperTests
    {
        private static readonly Vector2 Screen = new Vector2(1280f, 720f);
        private static readonly Vector2 Target = new Vector2(640f, 360f);

        [Test]
        public void ScreenBottomLeftMapsToPanelBottomLeft()
        {
            var p = ScreenToPanelMapper.Map(new Vector2(0f, 0f), Screen, Target);
            Assert.AreEqual(0f, p.x, 1e-4f);
            Assert.AreEqual(360f, p.y, 1e-4f);
        }

        [Test]
        public void ScreenTopRightMapsToPanelTopRight()
        {
            var p = ScreenToPanelMapper.Map(new Vector2(1280f, 720f), Screen, Target);
            Assert.AreEqual(640f, p.x, 1e-4f);
            Assert.AreEqual(0f, p.y, 1e-4f);
        }

        [Test]
        public void CenterMapsToCenter()
        {
            var p = ScreenToPanelMapper.Map(new Vector2(640f, 360f), Screen, Target);
            Assert.AreEqual(320f, p.x, 1e-4f);
            Assert.AreEqual(180f, p.y, 1e-4f);
        }

        [Test]
        public void YAxisIsFlippedAndScaled()
        {
            var p = ScreenToPanelMapper.Map(new Vector2(100f, 200f), Screen, Target);
            Assert.AreEqual(50f, p.x, 1e-4f);
            Assert.AreEqual((720f - 200f) * 0.5f, p.y, 1e-4f);
        }

        [TestCase(-1f, 100f)]
        [TestCase(100f, -1f)]
        [TestCase(1281f, 100f)]
        [TestCase(100f, 721f)]
        public void OutOfBoundsIsInvalid(float x, float y)
        {
            var p = ScreenToPanelMapper.Map(new Vector2(x, y), Screen, Target);
            Assert.IsTrue(float.IsNaN(p.x) && float.IsNaN(p.y));
        }

        [Test]
        public void ZeroScreenSizeIsInvalid()
        {
            var p = ScreenToPanelMapper.Map(new Vector2(1f, 1f), Vector2.zero, Target);
            Assert.IsTrue(float.IsNaN(p.x) && float.IsNaN(p.y));
        }
    }
}

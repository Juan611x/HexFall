using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class CrosshairLayoutTests
    {
        private const float Tolerance = 0.01f;

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
        }

        [Test]
        public void RingIsAPointyTopHexagonRunningClockwiseFromTheTop()
        {
            var layout = CrosshairLayout.Compute(new Vector2(24f, 24f), 48f);

            Assert.AreEqual(6, layout.Ring.Length);
            AssertNear(new Vector2(24f, 11f), layout.Ring[0]);
            AssertNear(new Vector2(35.258f, 17.5f), layout.Ring[1]);
            AssertNear(new Vector2(35.258f, 30.5f), layout.Ring[2]);
            AssertNear(new Vector2(24f, 37f), layout.Ring[3]);
            AssertNear(new Vector2(12.742f, 30.5f), layout.Ring[4]);
            AssertNear(new Vector2(12.742f, 17.5f), layout.Ring[5]);
        }

        [Test]
        public void RingScalesWithSizeAroundTheCentre()
        {
            var layout = CrosshairLayout.Compute(new Vector2(100f, 50f), 96f);

            AssertNear(new Vector2(100f, 24f), layout.Ring[0]);
            AssertNear(new Vector2(100f, 76f), layout.Ring[3]);
        }

        [Test]
        public void DotIsCentredWithAOnePixelOutlineAtDesignSize()
        {
            var layout = CrosshairLayout.Compute(new Vector2(24f, 24f), 48f);

            Assert.AreEqual(new Rect(22f, 22f, 4f, 4f), layout.Dot);
            Assert.AreEqual(new Rect(21f, 21f, 6f, 6f), layout.DotOutline);
        }

        [Test]
        public void StrokeWidthsFollowTheDesign()
        {
            var layout = CrosshairLayout.Compute(Vector2.zero, 48f);

            Assert.AreEqual(2f, layout.RingWidth, Tolerance);
            Assert.AreEqual(5f, layout.OutlineWidth, Tolerance);
        }

        [Test]
        public void ZeroOrNegativeSizeCollapsesToTheCentre()
        {
            var layout = CrosshairLayout.Compute(new Vector2(10f, 10f), -5f);

            AssertNear(new Vector2(10f, 10f), layout.Ring[0]);
            Assert.AreEqual(0f, layout.RingWidth);
        }
    }
}

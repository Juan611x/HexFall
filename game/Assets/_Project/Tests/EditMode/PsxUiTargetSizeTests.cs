using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class PsxUiTargetSizeTests
    {
        [Test]
        public void HalfScaleHalvesTheScreen()
        {
            Assert.AreEqual(new Vector2Int(640, 360), PsxUiTargetSize.Compute(1280, 720, 0.5f));
        }

        [Test]
        public void FractionalSizeIsTruncatedLikeUrp()
        {
            Assert.AreEqual(new Vector2Int(500, 281), PsxUiTargetSize.Compute(1001, 563, 0.5f));
        }

        [Test]
        public void SizeNeverDropsBelowOnePixel()
        {
            Assert.AreEqual(new Vector2Int(1, 1), PsxUiTargetSize.Compute(1, 1, 0.1f));
        }

        [Test]
        public void ScaleIsClampedToUrpRange()
        {
            Assert.AreEqual(new Vector2Int(400, 200), PsxUiTargetSize.Compute(200, 100, 5f));
        }
    }
}

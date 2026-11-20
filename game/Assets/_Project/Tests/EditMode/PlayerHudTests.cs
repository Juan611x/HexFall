using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class PlayerHudFormatTests
    {
        [Test]
        public void HealthFractionIsHealthOverMax()
        {
            Assert.AreEqual(0.72f, PlayerHudFormat.HealthFraction(72, 100), 0.0001f);
        }

        [Test]
        public void HealthFractionIsClampedToZeroAndOne()
        {
            Assert.AreEqual(0f, PlayerHudFormat.HealthFraction(-5, 100));
            Assert.AreEqual(1f, PlayerHudFormat.HealthFraction(150, 100));
        }

        [Test]
        public void HealthFractionIsZeroWithoutAMaximum()
        {
            Assert.AreEqual(0f, PlayerHudFormat.HealthFraction(10, 0));
        }

        [Test]
        public void HealthLabelShowsClampedHealthOverMax()
        {
            Assert.AreEqual("72 / 100", PlayerHudFormat.HealthLabel(72, 100));
            Assert.AreEqual("0 / 100", PlayerHudFormat.HealthLabel(-3, 100));
        }

        [Test]
        public void SealsListIntactChargesFirst()
        {
            CollectionAssert.AreEqual(
                new[] { SealState.Intact, SealState.Intact, SealState.Spent },
                PlayerHudFormat.Seals(2, 3));
        }

        [Test]
        public void SealsClampChargesToTheMaximum()
        {
            CollectionAssert.AreEqual(new[] { SealState.Intact, SealState.Intact }, PlayerHudFormat.Seals(5, 2));
            CollectionAssert.AreEqual(new[] { SealState.Spent, SealState.Spent }, PlayerHudFormat.Seals(-1, 2));
        }

        [Test]
        public void ShieldLabelShowsChargesOverMax()
        {
            Assert.AreEqual("Shield 2 / 3", PlayerHudFormat.ShieldLabel(2, 3));
        }
    }

    public class InkOutlineTests
    {
        private static readonly Rect Area = new Rect(0f, 0f, 400f, 120f);

        [Test]
        public void SameSeedGivesTheSameOutline()
        {
            CollectionAssert.AreEqual(InkOutline.Blob(Area, 48, 8f, 7), InkOutline.Blob(Area, 48, 8f, 7));
        }

        [Test]
        public void DifferentSeedsGiveDifferentOutlines()
        {
            CollectionAssert.AreNotEqual(InkOutline.Blob(Area, 48, 8f, 7), InkOutline.Blob(Area, 48, 8f, 8));
        }

        [Test]
        public void OutlineHasTheRequestedPointCount()
        {
            Assert.AreEqual(48, InkOutline.Blob(Area, 48, 8f, 1).Length);
        }

        [Test]
        public void PointsStayWithinTheAreaGrownByTheRoughness()
        {
            var grown = new Rect(Area.x - 8f, Area.y - 8f, Area.width + 16f, Area.height + 16f);
            foreach (var p in InkOutline.Blob(Area, 64, 8f, 3))
                Assert.IsTrue(grown.Contains(p), p.ToString());
        }

        [Test]
        public void ZeroRoughnessTracesTheEllipseInsideTheArea()
        {
            foreach (var p in InkOutline.Blob(Area, 16, 0f, 3))
            {
                var nx = (p.x - Area.center.x) / (Area.width / 2f);
                var ny = (p.y - Area.center.y) / (Area.height / 2f);
                Assert.AreEqual(1f, nx * nx + ny * ny, 0.001f);
            }
        }
    }
}

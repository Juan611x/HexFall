using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The circle's real footprint: a disc of the given radius around the centre, within a height band along the circle's up axis.
    public class RuneCircleFootprintTests
    {
        private static readonly Vector3 Centre = new Vector3(10f, 0.01f, -4f);

        private static bool Contains(Vector3 offset, Quaternion? rotation = null) =>
            RuneCircleFootprint.Contains(Centre, rotation ?? Quaternion.identity, Centre + offset, 0.9f, -0.2f, 1f);

        [Test]
        public void TheCentreIsInside()
        {
            Assert.IsTrue(Contains(Vector3.zero));
        }

        [Test]
        public void InsideTheRadiusIsInsideAndBeyondItIsOutside()
        {
            Assert.IsTrue(Contains(new Vector3(0.85f, 0f, 0f)));
            Assert.IsFalse(Contains(new Vector3(0.95f, 0f, 0f)));
        }

        [Test]
        public void TheHorizontalDistanceIsRadialNotAxisAligned()
        {
            // The corner of a 1.8 m square is 1.13 m from the centre at (0.8, 0.8): inside an AABB, outside the disc.
            Assert.IsFalse(Contains(new Vector3(0.8f, 0f, 0.8f)));
            Assert.IsTrue(Contains(new Vector3(0.6f, 0f, 0.6f)));
        }

        [Test]
        public void TheHeightBandBoundsTheFootprintVertically()
        {
            Assert.IsTrue(Contains(new Vector3(0f, 0.9f, 0f)));
            Assert.IsFalse(Contains(new Vector3(0f, 1.1f, 0f)));
            Assert.IsTrue(Contains(new Vector3(0f, -0.1f, 0f)));
            Assert.IsFalse(Contains(new Vector3(0f, -0.3f, 0f)));
        }

        [Test]
        public void YawRotationDoesNotChangeTheDisc()
        {
            var yawed = Quaternion.Euler(0f, 37f, 0f);
            Assert.IsTrue(Contains(new Vector3(0.85f, 0f, 0f), yawed));
            Assert.IsFalse(Contains(new Vector3(0.95f, 0f, 0f), yawed));
        }

        [Test]
        public void ATiltedCircleUsesItsOwnUpAxisForTheBand()
        {
            // Rolled 90 degrees about Z, the circle's up axis points along world -X: the band runs along X.
            var rolled = Quaternion.Euler(0f, 0f, 90f);
            Assert.IsTrue(Contains(new Vector3(-0.8f, 0f, 0f), rolled));
            Assert.IsFalse(Contains(new Vector3(-1.5f, 0f, 0f), rolled));
            // The disc now spans world Y and Z.
            Assert.IsTrue(Contains(new Vector3(0f, 0.8f, 0f), rolled));
            Assert.IsFalse(Contains(new Vector3(0f, 0.95f, 0f), rolled));
        }
    }
}

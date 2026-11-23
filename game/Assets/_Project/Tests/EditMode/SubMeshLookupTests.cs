using HexFall.Surfaces;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SubMeshLookupTests
    {
        // Three submeshes of 2, 3 and 1 triangles: index ranges 0-5, 6-14, 15-17.
        static readonly (int indexStart, int indexCount)[] Ranges =
        {
            (0, 6), (6, 9), (15, 3),
        };

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        public void IndexOf_ReturnsContainingSubMesh(int triangle, int expected)
        {
            Assert.AreEqual(expected, SubMeshLookup.IndexOf(triangle, Ranges));
        }

        [TestCase(6)]
        [TestCase(100)]
        [TestCase(-1)]
        public void IndexOf_OutOfRange_ReturnsMinusOne(int triangle)
        {
            Assert.AreEqual(-1, SubMeshLookup.IndexOf(triangle, Ranges));
        }

        [Test]
        public void IndexOf_EmptyRanges_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SubMeshLookup.IndexOf(0, new (int, int)[0]));
        }
    }
}

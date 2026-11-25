using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The beam's geometry as plain maths: where it ends given the range and where something got in the way.
    public class BeamTraceTests
    {
        private static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);

        [Test]
        public void WithNothingInTheWayTheBeamReachesItsFullRange()
        {
            var beam = BeamTrace.Resolve(new Vector3(1f, 2f, 3f), Forward, 20f, null);
            Assert.IsFalse(beam.Hit);
            Assert.AreEqual(20f, beam.Length, 1e-4f);
            Assert.AreEqual(23f, beam.End.z, 1e-4f);
            Assert.AreEqual(1f, beam.End.x, 1e-4f);
            Assert.AreEqual(2f, beam.End.y, 1e-4f);
        }

        [Test]
        public void ASurfaceInsideTheRangeStopsTheBeamAndCountsAsAHit()
        {
            var beam = BeamTrace.Resolve(Vector3.zero, Forward, 20f, 7.5f);
            Assert.IsTrue(beam.Hit);
            Assert.AreEqual(7.5f, beam.Length, 1e-4f);
            Assert.AreEqual(7.5f, beam.End.z, 1e-4f);
        }

        [Test]
        public void ASurfaceBeyondTheRangeIsOutOfReach()
        {
            var beam = BeamTrace.Resolve(Vector3.zero, Forward, 20f, 25f);
            Assert.IsFalse(beam.Hit);
            Assert.AreEqual(20f, beam.Length, 1e-4f);
        }

        [Test]
        public void ASurfaceExactlyAtTheRangeIsStillAHit()
        {
            var beam = BeamTrace.Resolve(Vector3.zero, Forward, 20f, 20f);
            Assert.IsTrue(beam.Hit);
            Assert.AreEqual(20f, beam.Length, 1e-4f);
        }

        [Test]
        public void ASurfaceRightAtTheMuzzleIsAZeroLengthHit()
        {
            var beam = BeamTrace.Resolve(Vector3.zero, Forward, 20f, 0f);
            Assert.IsTrue(beam.Hit);
            Assert.AreEqual(0f, beam.Length);
            Assert.AreEqual(0f, beam.End.z);
        }

        [Test]
        public void ANegativeOrNonFiniteHitDistanceIsNoHit()
        {
            foreach (var bad in new float[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var beam = BeamTrace.Resolve(Vector3.zero, Forward, 20f, bad);
                Assert.IsFalse(beam.Hit, $"hit distance {bad}");
                Assert.AreEqual(20f, beam.Length, 1e-4f);
            }
        }

        [Test]
        public void ANonPositiveOrNonFiniteRangeGivesAnEmptyBeam()
        {
            foreach (var bad in new float[] { 0f, -5f, float.NaN, float.PositiveInfinity })
            {
                var beam = BeamTrace.Resolve(Vector3.zero, Forward, bad, 3f);
                Assert.IsFalse(beam.Hit, $"range {bad}");
                Assert.AreEqual(0f, beam.Length, $"range {bad}");
                Assert.AreEqual(0f, beam.End.z, $"range {bad}");
            }
        }

        [Test]
        public void ADegenerateDirectionGivesAnEmptyBeam()
        {
            var beam = BeamTrace.Resolve(new Vector3(1f, 1f, 1f), Vector3.zero, 20f, 5f);
            Assert.IsFalse(beam.Hit);
            Assert.AreEqual(0f, beam.Length);
            Assert.AreEqual(1f, beam.End.x);
        }

        [Test]
        public void TheDirectionPointsFromTheMuzzleToTheAimPointAsAUnitVector()
        {
            var dir = BeamTrace.DirectionTo(new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 11f));
            Assert.AreEqual(0f, dir.x, 1e-5f);
            Assert.AreEqual(0f, dir.y, 1e-5f);
            Assert.AreEqual(1f, dir.z, 1e-5f);

            var diagonal = BeamTrace.DirectionTo(Vector3.zero, new Vector3(3f, 0f, 4f));
            Assert.AreEqual(0.6f, diagonal.x, 1e-5f);
            Assert.AreEqual(0.8f, diagonal.z, 1e-5f);
        }

        [Test]
        public void AnAimPointOnTheMuzzleOrNonFiniteHasNoDirection()
        {
            var same = BeamTrace.DirectionTo(new Vector3(2f, 2f, 2f), new Vector3(2f, 2f, 2f));
            Assert.AreEqual(0f, same.x);
            Assert.AreEqual(0f, same.y);
            Assert.AreEqual(0f, same.z);

            var nan = BeamTrace.DirectionTo(Vector3.zero, new Vector3(float.NaN, 0f, 1f));
            Assert.AreEqual(0f, nan.x);
            Assert.AreEqual(0f, nan.z);
        }
    }
}

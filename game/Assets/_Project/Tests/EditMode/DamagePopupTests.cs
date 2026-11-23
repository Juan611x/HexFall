using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class DamagePopupMotionTests
    {
        private static readonly DamagePopupMotion Motion = new DamagePopupMotion(
            lifetimeSeconds: 1f, riseHeight: 0.8f, popScale: 1.6f, popSeconds: 0.2f, fadeSeconds: 0.4f, driftSpacing: 0.3f);

        [Test]
        public void RiseStartsAtTheHitAndEndsAtFullHeight()
        {
            Assert.AreEqual(0f, Motion.Rise(0f), 0.0001f);
            Assert.AreEqual(0.8f, Motion.Rise(1f), 0.0001f);
        }

        [Test]
        public void RiseEasesOutSoItClimbsFastestAtFirst()
        {
            Assert.Greater(Motion.Rise(0.5f), 0.4f);
        }

        [Test]
        public void RiseStopsAtFullHeightAfterTheLifetime()
        {
            Assert.AreEqual(0.8f, Motion.Rise(3f), 0.0001f);
        }

        [Test]
        public void ScalePunchesInAndSettles()
        {
            Assert.AreEqual(1.6f, Motion.Scale(0f), 0.0001f);
            Assert.AreEqual(1.3f, Motion.Scale(0.1f), 0.0001f);
            Assert.AreEqual(1f, Motion.Scale(0.2f), 0.0001f);
            Assert.AreEqual(1f, Motion.Scale(0.7f), 0.0001f);
        }

        [Test]
        public void AlphaHoldsThenFadesOverTheLastPart()
        {
            Assert.AreEqual(1f, Motion.Alpha(0.6f), 0.0001f);
            Assert.AreEqual(0.5f, Motion.Alpha(0.8f), 0.0001f);
            Assert.AreEqual(0f, Motion.Alpha(1f), 0.0001f);
            Assert.AreEqual(0f, Motion.Alpha(2f), 0.0001f);
        }

        [Test]
        public void IsDoneOnlyAfterTheLifetime()
        {
            Assert.IsFalse(Motion.IsDone(0.99f));
            Assert.IsTrue(Motion.IsDone(1f));
        }

        [Test]
        public void ConsecutiveHitsDriftCentreRightLeft()
        {
            Assert.AreEqual(0f, Motion.Drift(0), 0.0001f);
            Assert.AreEqual(0.3f, Motion.Drift(1), 0.0001f);
            Assert.AreEqual(-0.3f, Motion.Drift(2), 0.0001f);
            Assert.AreEqual(0f, Motion.Drift(3), 0.0001f);
        }
    }

    public class DamageFeedTests
    {
        private readonly List<(Vector3, int)> _seen = new List<(Vector3, int)>();

        private void Record(Vector3 at, int amount) => _seen.Add((at, amount));

        [SetUp]
        public void Subscribe()
        {
            _seen.Clear();
            DamageFeed.Dealt += Record;
        }

        [TearDown]
        public void Unsubscribe() => DamageFeed.Dealt -= Record;

        [Test]
        public void ReportedDamageReachesListeners()
        {
            DamageFeed.Report(new Vector3(1f, 2f, 3f), 18);

            Assert.AreEqual(1, _seen.Count);
            Assert.AreEqual((new Vector3(1f, 2f, 3f), 18), _seen[0]);
        }

        [Test]
        public void ZeroDamageIsNotReported()
        {
            DamageFeed.Report(Vector3.zero, 0);

            Assert.AreEqual(0, _seen.Count);
        }

        [Test]
        public void ReportingWithoutListenersDoesNothing()
        {
            DamageFeed.Dealt -= Record;

            Assert.DoesNotThrow(() => DamageFeed.Report(Vector3.zero, 5));
        }
    }
}

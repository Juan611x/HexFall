using System;
using System.Linq;
using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class WindCurrentSchedulerTests
    {
        private static WindCurrentScheduler Make(int seed = 7, int count = 3, float gap = 0.35f)
        {
            return new WindCurrentScheduler(new Random(seed), count, -0.6f, 0.6f, gap, 12f, 1.2f, 2.2f);
        }

        [Test]
        public void KeepsTheConfiguredNumberOfCurrents()
        {
            Assert.AreEqual(3, Make(count: 3).Currents.Count);
            Assert.AreEqual(4, Make(count: 4).Currents.Count);
        }

        [Test]
        public void RejectsACountOutsideThreeToFour()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(count: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(count: 5));
        }

        [Test]
        public void InitialCurrentsAreStaggered()
        {
            var scheduler = Make();

            for (int i = 0; i < scheduler.Currents.Count; i++)
                for (int j = i + 1; j < scheduler.Currents.Count; j++)
                    Assert.Greater(Math.Abs(scheduler.Currents[i].Progress - scheduler.Currents[j].Progress), 0.05f);
        }

        [Test]
        public void StatesStayInsideTheConfiguredRanges()
        {
            var scheduler = Make();

            for (int step = 0; step < 2000; step++)
            {
                scheduler.Tick(0.05f);
                foreach (var c in scheduler.Currents)
                {
                    Assert.GreaterOrEqual(c.Progress, 0f);
                    Assert.Less(c.Progress, 1f);
                    Assert.GreaterOrEqual(c.Height, -0.6f);
                    Assert.LessOrEqual(c.Height, 0.6f);
                    Assert.LessOrEqual(Math.Abs(c.TiltDegrees), 12f);
                    Assert.GreaterOrEqual(c.Lifetime, 1.2f);
                    Assert.LessOrEqual(c.Lifetime, 2.2f);
                }
            }
        }

        [Test]
        public void ProgressAdvancesWithTime()
        {
            var scheduler = Make();
            var before = scheduler.Currents[0];

            scheduler.Tick(0.1f);

            Assert.AreEqual(before.Progress + 0.1f / before.Lifetime, scheduler.Currents[0].Progress, 1e-4f);
        }

        [Test]
        public void RespawnedCurrentsKeepTheMinimumHeightGapAcrossManySeeds()
        {
            // Feasible gap (two others exclude at most 0.4 of the 1.2 band); tolerance covers the farthest-candidate fallback.
            for (int seed = 1; seed <= 20; seed++)
            {
                var scheduler = Make(seed: seed, gap: 0.1f);
                var last = new float[scheduler.Currents.Count];
                int respawns = 0;
                for (int step = 0; step < 600; step++)
                {
                    scheduler.Tick(0.05f);
                    for (int i = 0; i < last.Length; i++)
                    {
                        var now = scheduler.Currents[i];
                        if (now.Height == last[i] || step == 0) { last[i] = now.Height; continue; }
                        last[i] = now.Height;
                        respawns++;
                        for (int j = 0; j < last.Length; j++)
                            if (j != i)
                                Assert.GreaterOrEqual(Math.Abs(now.Height - scheduler.Currents[j].Height), 0.1f - 1e-4f, "seed " + seed);
                    }
                }
                Assert.Greater(respawns, 0, "seed " + seed + " respawned");
            }
        }

        [Test]
        public void PickIndexTakesTheFirstCandidateThatKeepsTheGap()
        {
            var candidates = new[] { 0.05f, 0.5f, 0.9f };
            var others = new[] { 0f, 1f };

            Assert.AreEqual(1, WindCurrentScheduler.PickIndex(candidates, others, 0.4f));
        }

        [Test]
        public void PickIndexFallsBackToTheFarthestCandidateWhenNoneKeepsTheGap()
        {
            var candidates = new[] { 0.1f, 0.45f, 0.2f };
            var others = new[] { 0f, 1f };

            Assert.AreEqual(1, WindCurrentScheduler.PickIndex(candidates, others, 5f));
        }

        [Test]
        public void PickIndexWithNoOthersTakesTheFirstCandidate()
        {
            Assert.AreEqual(0, WindCurrentScheduler.PickIndex(new[] { 0.3f, 0.6f }, new float[0], 1f));
        }

        [Test]
        public void ABigHitchedDeltaDoesNotRespawnEveryCurrentTogether()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var scheduler = Make(seed: seed, count: 4);

                scheduler.Tick(37.3f);

                var remaining = scheduler.Currents.Select(c => c.Lifetime * (1f - c.Progress)).OrderBy(x => x).ToArray();
                for (int i = 1; i < remaining.Length; i++)
                    Assert.Greater(remaining[i] - remaining[i - 1], 0.04f, "seed " + seed + ": expiries stay apart");
                foreach (var c in scheduler.Currents)
                    Assert.Greater(c.Progress, 0f, "leftover time was carried, not dropped (seed " + seed + ")");
            }
        }

        [Test]
        public void ExpiryCarriesTheLeftoverTimeIntoTheNewCurrent()
        {
            var scheduler = Make();
            var first = scheduler.Currents[0];
            float toExpiry = first.Lifetime * (1f - first.Progress);

            scheduler.Tick(toExpiry + 0.05f);

            var now = scheduler.Currents[0];
            Assert.AreEqual(0.05f / now.Lifetime, now.Progress, 1e-3f);
        }

        [Test]
        public void ExpiriesStayApartAfterLongRuns()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var scheduler = Make(seed: seed, count: 4);
                for (int step = 0; step < 4000; step++) scheduler.Tick(0.033f);

                var remaining = scheduler.Currents.Select(c => c.Lifetime * (1f - c.Progress)).OrderBy(x => x).ToArray();
                for (int i = 1; i < remaining.Length; i++)
                    Assert.Greater(remaining[i] - remaining[i - 1], 0.04f, "seed " + seed);
            }
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        public void NegativeOrNonFiniteDeltaTimeIsIgnored(float delta)
        {
            var scheduler = Make();
            var before = scheduler.Currents.ToArray();

            scheduler.Tick(delta);

            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].Progress, scheduler.Currents[i].Progress);
                Assert.AreEqual(before[i].Height, scheduler.Currents[i].Height);
            }
        }

        [Test]
        public void SameSeedGivesTheSameSequence()
        {
            var a = Make(seed: 3);
            var b = Make(seed: 3);

            for (int step = 0; step < 300; step++)
            {
                a.Tick(0.05f);
                b.Tick(0.05f);
            }

            for (int i = 0; i < a.Currents.Count; i++)
                Assert.AreEqual(a.Currents[i].Height, b.Currents[i].Height);
        }

        [Test]
        public void ImpossibleGapStillTerminatesAndStaysInBand()
        {
            var scheduler = Make(gap: 5f);

            for (int step = 0; step < 500; step++)
                scheduler.Tick(0.05f);

            foreach (var c in scheduler.Currents)
            {
                Assert.GreaterOrEqual(c.Height, -0.6f);
                Assert.LessOrEqual(c.Height, 0.6f);
            }
        }

        [Test]
        public void ALargeTickRespawnsWithoutOverflowingProgress()
        {
            var scheduler = Make();

            scheduler.Tick(100f);

            foreach (var c in scheduler.Currents)
            {
                Assert.GreaterOrEqual(c.Progress, 0f);
                Assert.Less(c.Progress, 1f);
            }
        }
    }
}

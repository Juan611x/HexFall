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
        public void RejectsACountOutsideThreeToEight()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(count: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(count: 9));
            Assert.AreEqual(6, Make(count: 6).Currents.Count);
            Assert.AreEqual(8, Make(count: 8).Currents.Count);
        }

        // ---- six currents over the wider band (T6c) ----------------------------------------------------------------

        [Test]
        public void DefaultsAreSixCurrentsOverTheWideBand()
        {
            var scheduler = new WindCurrentScheduler(new Random(2));
            Assert.AreEqual(6, scheduler.Currents.Count);

            float min = 0f, max = 0f;
            for (int step = 0; step < 3000; step++)
            {
                scheduler.Tick(0.05f);
                foreach (var c in scheduler.Currents)
                {
                    min = Math.Min(min, c.Height);
                    max = Math.Max(max, c.Height);
                }
            }

            Assert.LessOrEqual(max, 0.85f);
            Assert.GreaterOrEqual(min, -0.85f);
            Assert.Greater(max, 0.8f, "reaches the top of the band");
            Assert.Less(min, -0.8f, "reaches the bottom of the band");
        }

        // Bounds are measured over these 20 seeds (60 fps, 25 s each): the closest pair of heights ever seen is
        // 0.055 (the farthest-of-eight fallback has no hard guarantee), 73 % of the adjacent pairs keep the full
        // 0.22 gap, and the closest pair of expiries on any tick is 0.027 s (the configured target is 0.06 s).
        [Test]
        public void SixFastCurrentsKeepTheirHeightsApartAndTheirExpiriesApart()
        {
            int pairs = 0, pairsAtTheGap = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var scheduler = new WindCurrentScheduler(new Random(seed), 6, -0.85f, 0.85f, 0.22f, 12f, 0.6f, 0.8f, 0.06f);
                for (int step = 0; step < 1500; step++)
                {
                    scheduler.Tick(1f / 60f);
                    var heights = scheduler.Currents.Select(c => c.Height).OrderBy(x => x).ToArray();
                    for (int i = 1; i < heights.Length; i++)
                    {
                        pairs++;
                        if (heights[i] - heights[i - 1] >= 0.22f - 1e-4f) pairsAtTheGap++;
                        Assert.GreaterOrEqual(heights[i] - heights[i - 1], 0.05f, "seed " + seed + " step " + step + ": heights apart");
                    }

                    var remaining = scheduler.Currents.Select(c => c.Lifetime * (1f - c.Progress)).OrderBy(x => x).ToArray();
                    for (int i = 1; i < remaining.Length; i++)
                        Assert.GreaterOrEqual(remaining[i] - remaining[i - 1], 0.025f, "seed " + seed + " step " + step + ": expiries apart");
                }
            }

            Assert.GreaterOrEqual(pairsAtTheGap / (float)pairs, 0.65f, "most neighbours keep the configured gap, not only the floor");
        }

        [Test]
        public void RejectsANegativeOrNonFiniteMinExpiryGap()
        {
            foreach (var gap in new[] { -0.01f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new WindCurrentScheduler(new Random(1), 6, -0.85f, 0.85f, 0.22f, 12f, 0.6f, 0.8f, gap), "gap " + gap);
            Assert.DoesNotThrow(() => new WindCurrentScheduler(new Random(1), 6, -0.85f, 0.85f, 0.22f, 12f, 0.6f, 0.8f, 0f));
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

        // ---- fast currents, draining and the initial expiry gap (T6b) -------------------------------------------------

        private static WindCurrentScheduler MakeFast(int seed, int count = 4)
        {
            return new WindCurrentScheduler(new Random(seed), count, -0.6f, 0.6f, 0.3f, 12f, 0.6f, 0.8f);
        }

        [Test]
        public void InitialExpiriesStayApartUsingTheRemainingTimeNotTheFullLifetime()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var scheduler = MakeFast(seed);

                var remaining = scheduler.Currents.Select(c => c.Lifetime * (1f - c.Progress)).OrderBy(x => x).ToArray();
                for (int i = 1; i < remaining.Length; i++) // 0.1 s is the target; late phases leave little room to pick, so 0.05 s is enforced
                    Assert.GreaterOrEqual(remaining[i] - remaining[i - 1], 0.05f, "seed " + seed);
            }
        }

        [Test]
        public void FastLifetimesBirthSeveralCurrentsDuringTheGuardWindow()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var scheduler = MakeFast(seed);
                var heights = scheduler.Currents.Select(c => c.Height).ToArray();
                int births = 0;
                for (int step = 0; step < 24; step++) // 24 / 60 s = the 0.4 s guard
                {
                    scheduler.Tick(1f / 60f);
                    for (int i = 0; i < heights.Length; i++)
                    {
                        if (scheduler.Currents[i].Height == heights[i]) continue;
                        heights[i] = scheduler.Currents[i].Height;
                        births++;
                    }
                }

                Assert.GreaterOrEqual(births, 2, "seed " + seed);
            }
        }

        [Test]
        public void StopSpawningLetsLiveCurrentsFinishWithoutRespawning()
        {
            var scheduler = MakeFast(3);
            scheduler.Tick(0.1f);
            var heights = scheduler.Currents.Select(c => c.Height).ToArray();
            Assert.IsFalse(scheduler.Draining);
            Assert.IsFalse(scheduler.IsIdle);

            scheduler.StopSpawning();
            Assert.IsTrue(scheduler.Draining);
            float last = scheduler.Currents.Max(c => c.Progress);
            for (int step = 0; step < 120; step++)
            {
                scheduler.Tick(1f / 60f);
                for (int i = 0; i < heights.Length; i++)
                    Assert.AreEqual(heights[i], scheduler.Currents[i].Height, "no respawn of current " + i);
                Assert.GreaterOrEqual(scheduler.Currents.Max(c => c.Progress), last - 1e-6f);
                last = scheduler.Currents.Max(c => c.Progress);
            }

            Assert.IsTrue(scheduler.IsIdle, "everything expired after 2 s");
            Assert.IsTrue(scheduler.Currents.All(c => c.Progress == 1f));
        }

        [Test]
        public void ADrainingSchedulerIsNotIdleWhileAnyCurrentIsAlive()
        {
            var scheduler = MakeFast(5);
            scheduler.StopSpawning();

            scheduler.Tick(0.05f);

            Assert.IsFalse(scheduler.IsIdle);
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

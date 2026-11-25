using System;
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
        public void FinishedCurrentRespawnsWithANewHeightAwayFromTheOthers()
        {
            var scheduler = Make();
            bool respawned = false;
            var last = scheduler.Currents[0];

            for (int step = 0; step < 400 && !respawned; step++)
            {
                scheduler.Tick(0.05f);
                var now = scheduler.Currents[0];
                if (now.Progress < last.Progress)
                {
                    respawned = true;
                    for (int i = 1; i < scheduler.Currents.Count; i++)
                        Assert.GreaterOrEqual(Math.Abs(now.Height - scheduler.Currents[i].Height), 0.35f - 1e-4f);
                }
                last = now;
            }

            Assert.IsTrue(respawned);
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

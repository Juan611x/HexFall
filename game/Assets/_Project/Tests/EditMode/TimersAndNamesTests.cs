using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class TimeLabelTests
    {
        [TestCase(0, "0:00")]
        [TestCase(7, "0:07")]
        [TestCase(42, "0:42")]
        [TestCase(60, "1:00")]
        [TestCase(125, "2:05")]
        [TestCase(-3, "0:00")]
        public void FormatsMinutesAndSeconds(int seconds, string expected)
        {
            Assert.AreEqual(expected, TimeLabel.MinSec(seconds));
        }
    }

    public class CountdownTests
    {
        [Test]
        public void IsIdleUntilStarted()
        {
            var c = new Countdown(60);
            Assert.IsFalse(c.IsRunning);
            Assert.IsFalse(c.IsFinished);
        }

        [Test]
        public void StartBeginsAtFullDuration()
        {
            var c = new Countdown(60);
            c.Start();
            Assert.IsTrue(c.IsRunning);
            Assert.AreEqual(60, c.RemainingSeconds);
        }

        [Test]
        public void TickReducesRemainingRoundingUp()
        {
            var c = new Countdown(60);
            c.Start();
            c.Tick(0.4f);
            Assert.AreEqual(60, c.RemainingSeconds);
            c.Tick(0.7f);
            Assert.AreEqual(59, c.RemainingSeconds);
            c.Tick(18f);
            Assert.AreEqual(41, c.RemainingSeconds);
        }

        [Test]
        public void FinishesAtZeroAndStopsRunning()
        {
            var c = new Countdown(3);
            c.Start();
            c.Tick(5f);
            Assert.AreEqual(0, c.RemainingSeconds);
            Assert.IsTrue(c.IsFinished);
            Assert.IsFalse(c.IsRunning);
        }

        [Test]
        public void TickBeforeStartIsIgnored()
        {
            var c = new Countdown(3);
            c.Tick(1f);
            Assert.IsFalse(c.IsFinished);
        }

        [Test]
        public void StartAgainRestarts()
        {
            var c = new Countdown(3);
            c.Start();
            c.Tick(5f);
            c.Start();
            Assert.IsTrue(c.IsRunning);
            Assert.IsFalse(c.IsFinished);
            Assert.AreEqual(3, c.RemainingSeconds);
        }
    }

    public class MatchSearchTimerTests
    {
        [Test]
        public void StartsIdle()
        {
            var t = new MatchSearchTimer();
            Assert.IsFalse(t.IsSearching);
            Assert.AreEqual("0:00", t.Label);
        }

        [Test]
        public void StartBeginsSearchingAtZero()
        {
            var t = new MatchSearchTimer();
            t.Start();
            Assert.IsTrue(t.IsSearching);
            Assert.AreEqual(0, t.ElapsedSeconds);
        }

        [Test]
        public void TickAccumulatesWholeSeconds()
        {
            var t = new MatchSearchTimer();
            t.Start();
            t.Tick(3.6f);
            t.Tick(3.6f);
            Assert.AreEqual(7, t.ElapsedSeconds);
            Assert.AreEqual("0:07", t.Label);
        }

        [Test]
        public void TickWhileIdleIsIgnored()
        {
            var t = new MatchSearchTimer();
            t.Tick(10f);
            Assert.AreEqual(0, t.ElapsedSeconds);
        }

        [Test]
        public void CancelStopsAndResets()
        {
            var t = new MatchSearchTimer();
            t.Start();
            t.Tick(75f);
            Assert.AreEqual("1:15", t.Label);
            t.Cancel();
            Assert.IsFalse(t.IsSearching);
            Assert.AreEqual(0, t.ElapsedSeconds);
        }

        [Test]
        public void StartAgainResetsElapsed()
        {
            var t = new MatchSearchTimer();
            t.Start();
            t.Tick(9f);
            t.Start();
            Assert.AreEqual(0, t.ElapsedSeconds);
        }
    }

    public class GuestNameGeneratorTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new GuestNameGenerator(new Random(7));
            var b = new GuestNameGenerator(new Random(7));
            for (var i = 0; i < 5; i++) Assert.AreEqual(a.Generate(), b.Generate());
        }

        [Test]
        public void NamesFollowPatternAndRule()
        {
            var g = new GuestNameGenerator(new Random(1));
            const string pattern = "^(Ashen|Hex|Storm|Gloom|Ember|Frost|Void|Rune)_(Mage|Weaver|Caller|Warden|Sage|Binder)_[0-9]{3}$";
            for (var i = 0; i < 2000; i++)
            {
                var name = g.Generate();
                StringAssert.IsMatch(pattern, name);
                Assert.LessOrEqual(name.Length, WizardNameRule.MaxLength);
                Assert.IsTrue(WizardNameRule.IsValid(name), name);
            }
        }

        [Test]
        public void ProducesVariety()
        {
            var g = new GuestNameGenerator(new Random(3));
            var seen = new HashSet<string>();
            for (var i = 0; i < 50; i++) seen.Add(g.Generate());
            Assert.Greater(seen.Count, 40);
        }
    }
}

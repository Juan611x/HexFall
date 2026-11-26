using System;
using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // Reach is counted per collider: a pickup stays in reach until the last collider of the wizard leaves.
    public class TouchSetTests
    {
        [Test]
        public void TheFirstTouchIsReportedAndTheSetIsNoLongerEmpty()
        {
            var set = new TouchSet<string>();
            Assert.IsTrue(set.IsEmpty);
            Assert.IsTrue(set.Add("a"));
            Assert.IsFalse(set.IsEmpty);
        }

        [Test]
        public void TouchingTheSameKeyTwiceCountsOnce()
        {
            var set = new TouchSet<string>();
            set.Add("a");
            Assert.IsFalse(set.Add("a"));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void ItStaysTouchedUntilTheLastKeyLeaves()
        {
            var set = new TouchSet<string>();
            set.Add("a");
            set.Add("b");
            Assert.IsFalse(set.Remove("a"));
            Assert.IsFalse(set.IsEmpty);
            Assert.IsTrue(set.Remove("b"));
            Assert.IsTrue(set.IsEmpty);
        }

        [Test]
        public void RemovingAnUnknownKeyChangesNothing()
        {
            var set = new TouchSet<string>();
            set.Add("a");
            Assert.IsFalse(set.Remove("zzz"));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void RemovingFromAnEmptySetIsNotALastLeave()
        {
            Assert.IsFalse(new TouchSet<string>().Remove("a"));
        }

        [Test]
        public void ClearEmptiesTheSet()
        {
            var set = new TouchSet<string>();
            set.Add("a");
            set.Clear();
            Assert.IsTrue(set.IsEmpty);
        }

        // A collider disabled or destroyed inside the trigger never raises an exit; pruning drops those keys.
        [Test]
        public void PruneDropsTheKeysThatAreNoLongerAlive()
        {
            var set = new TouchSet<string>();
            set.Add("live");
            set.Add("dead");
            Assert.IsFalse(set.Prune(k => k == "live"), "a live key remains");
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void PruneReportsWhenTheLastKeyWasDropped()
        {
            var set = new TouchSet<string>();
            set.Add("a");
            set.Add("b");
            Assert.IsTrue(set.Prune(k => false));
            Assert.IsTrue(set.IsEmpty);
        }

        [Test]
        public void PruningAnEmptySetOrWhenAllAreAliveReportsNothing()
        {
            var set = new TouchSet<string>();
            Assert.IsFalse(set.Prune(k => false));
            set.Add("a");
            Assert.IsFalse(set.Prune(k => true));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void PruneRequiresAPredicate()
        {
            Assert.Throws<ArgumentNullException>(() => new TouchSet<string>().Prune(null));
        }
    }
}

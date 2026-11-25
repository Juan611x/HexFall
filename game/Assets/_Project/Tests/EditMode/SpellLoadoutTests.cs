using System.Collections.Generic;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The loadout rule: the Base spell is the default and never dropped; picking up a spell while holding
    // Base swaps at once; holding another spell, the pickup needs Interact and the held spell is left behind.
    public class SpellLoadoutTests
    {
        private const string BaseSpell = "base";

        private static SpellLoadout<string> NewLoadout() => new SpellLoadout<string>(BaseSpell);

        // ---- Sync with whatever else changes the held spell ----

        [Test]
        public void SyncFollowsAnExternalChangeAndKeepsBase()
        {
            var loadout = NewLoadout();
            loadout.Sync("fire");
            Assert.AreEqual("fire", loadout.Current);
            Assert.AreEqual(BaseSpell, loadout.Base);
            Assert.IsFalse(loadout.HoldsBase);
        }

        [Test]
        public void SyncingBackToBaseHoldsBaseAgain()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            loadout.Sync(BaseSpell);
            Assert.IsTrue(loadout.HoldsBase);
            Assert.AreEqual(OfferOutcome.Swapped, loadout.Offer("water"));
        }

        [Test]
        public void SyncingNothingMeansBase()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            loadout.Sync(null);
            Assert.IsTrue(loadout.HoldsBase);
        }

        [Test]
        public void SwapAfterAnExternalSyncDropsTheSyncedSpell()
        {
            var loadout = NewLoadout();
            loadout.Sync("fire");
            Assert.AreEqual("fire", loadout.Swap("water"));
            Assert.AreEqual("water", loadout.Current);
        }

        [Test]
        public void StartsHoldingTheBaseSpell()
        {
            var loadout = NewLoadout();
            Assert.AreEqual(BaseSpell, loadout.Base);
            Assert.AreEqual(BaseSpell, loadout.Current);
            Assert.IsTrue(loadout.HoldsBase);
        }

        [Test]
        public void OfferingASpellWhileHoldingBaseSwapsAtOnce()
        {
            var loadout = NewLoadout();
            Assert.AreEqual(OfferOutcome.Swapped, loadout.Offer("fire"));
            Assert.AreEqual("fire", loadout.Current);
            Assert.IsFalse(loadout.HoldsBase);
        }

        [Test]
        public void OfferingASpellWhileHoldingAnotherNeedsInteractAndChangesNothing()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            Assert.AreEqual(OfferOutcome.NeedsInteract, loadout.Offer("water"));
            Assert.AreEqual("fire", loadout.Current);
        }

        [Test]
        public void InteractSwapsAndReturnsTheHeldSpellAsDropped()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            Assert.AreEqual("fire", loadout.Swap("water"));
            Assert.AreEqual("water", loadout.Current);
        }

        [Test]
        public void TheDroppedSpellCanBePickedUpAgainByInteract()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            var dropped = loadout.Swap("water");
            Assert.AreEqual("water", loadout.Swap(dropped));
            Assert.AreEqual("fire", loadout.Current);
        }

        [Test]
        public void BaseIsNeverDropped()
        {
            var loadout = NewLoadout();
            Assert.IsNull(loadout.Swap("fire"));
            Assert.AreEqual("fire", loadout.Current);
        }

        [Test]
        public void OfferingTheSpellAlreadyHeldDoesNothing()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            Assert.AreEqual(OfferOutcome.None, loadout.Offer("fire"));
            Assert.AreEqual("fire", loadout.Current);
            Assert.IsNull(loadout.Swap("fire"));
            Assert.AreEqual("fire", loadout.Current);
        }

        [Test]
        public void OfferingBaseWhileHoldingBaseDoesNothing()
        {
            var loadout = NewLoadout();
            Assert.AreEqual(OfferOutcome.None, loadout.Offer(BaseSpell));
            Assert.IsTrue(loadout.HoldsBase);
        }

        [Test]
        public void OfferingNothingDoesNothing()
        {
            var loadout = NewLoadout();
            Assert.AreEqual(OfferOutcome.None, loadout.Offer(null));
            Assert.AreEqual(BaseSpell, loadout.Current);
            Assert.IsNull(loadout.Swap(null));
            Assert.AreEqual(BaseSpell, loadout.Current);
        }

        [Test]
        public void ABasePickupCanBeSwappedInByInteractAndTheHeldSpellIsDropped()
        {
            var loadout = NewLoadout();
            loadout.Offer("fire");
            Assert.AreEqual(OfferOutcome.NeedsInteract, loadout.Offer(BaseSpell));
            Assert.AreEqual("fire", loadout.Swap(BaseSpell));
            Assert.IsTrue(loadout.HoldsBase);
        }

        [Test]
        public void ABaseHolderNeverDropsAnythingWhenInteracting()
        {
            var loadout = NewLoadout();
            Assert.IsNull(loadout.Swap("earth"));
            Assert.AreEqual("earth", loadout.Current);
        }

        [Test]
        public void ConstructingWithoutABaseThrows()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SpellLoadout<string>(null));
        }
    }

    // Several pickups in reach: the nearest one is the interact target; a tie keeps the earliest.
    public class PickupTargetingTests
    {
        private static readonly Vector3 Origin = new Vector3(0f, 0f, 0f);

        [Test]
        public void NoPickupsMeansNoTarget()
        {
            Assert.AreEqual(-1, PickupTargeting.Nearest(Origin, new List<Vector3>()));
        }

        [Test]
        public void TheNearestPickupWins()
        {
            var points = new List<Vector3> { new Vector3(5f, 0f, 0f), new Vector3(0f, 0f, 2f), new Vector3(0f, 3f, 0f) };
            Assert.AreEqual(1, PickupTargeting.Nearest(Origin, points));
        }

        [Test]
        public void ADistanceTieKeepsTheEarliest()
        {
            var points = new List<Vector3> { new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f) };
            Assert.AreEqual(0, PickupTargeting.Nearest(Origin, points));
        }

        [Test]
        public void TheOriginMattersNotTheWorldOrigin()
        {
            var points = new List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(10f, 0f, 0f) };
            Assert.AreEqual(1, PickupTargeting.Nearest(new Vector3(9f, 0f, 0f), points));
        }
    }
}

using HexFall.Player;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class FootContactDetectorTests
    {
        private const float Dt = 1f / 60f;
        private static readonly FootContactSettings Settings = new FootContactSettings(0.10f, 0.06f, 0.18f);

        // A foot that is planted at `rest` for half of each cycle and lifted to rest + lift for the other half.
        private static float Height(float time, float period, float rest, float lift)
        {
            var phase = Mathf.Repeat(time / period, 1f);
            return phase < 0.5f ? rest + lift * Mathf.Sin(phase * 2f * Mathf.PI) : rest;
        }

        private static int Count(FootContactDetector detector, float seconds, float period, bool allowed)
        {
            var contacts = 0;
            for (var t = 0f; t < seconds; t += Dt)
                if (detector.Tick(Height(t, period, 0.03f, 0.25f), allowed, Dt)) contacts++;
            return contacts;
        }

        [Test]
        public void WalkCycleGivesOneContactPerStep()
        {
            Assert.AreEqual(4, Count(new FootContactDetector(Settings), 2f, 0.5f, true));
        }

        [Test]
        public void StandingStillGivesNoContacts()
        {
            Assert.AreEqual(0, Count(new FootContactDetector(Settings), 2f, 0.5f, false));
        }

        [Test]
        public void ContactFiresWhenTheFootCrossesDownThroughTheThreshold()
        {
            var detector = new FootContactDetector(Settings);
            Assert.IsFalse(detector.Tick(0.03f, true, Dt));
            Assert.IsFalse(detector.Tick(0.20f, true, Dt));
            Assert.IsFalse(detector.Tick(0.08f, true, Dt));
            Assert.IsTrue(detector.Tick(0.05f, true, Dt));
            Assert.IsFalse(detector.Tick(0.03f, true, Dt));
        }

        [Test]
        public void JitterAroundTheContactThresholdDoesNotRetrigger()
        {
            var detector = new FootContactDetector(Settings);
            detector.Tick(0.2f, true, Dt);
            Assert.IsTrue(detector.Tick(0.05f, true, Dt));
            var extra = 0;
            for (var i = 0; i < 120; i++)
                if (detector.Tick(i % 2 == 0 ? 0.05f : 0.075f, true, Dt)) extra++;
            Assert.AreEqual(0, extra);
        }

        [Test]
        public void RearmsAfterAFullLift()
        {
            var detector = new FootContactDetector(Settings);
            detector.Tick(0.2f, true, Dt);
            Assert.IsTrue(detector.Tick(0.03f, true, Dt));
            for (var i = 0; i < 20; i++) detector.Tick(0.03f, true, Dt);
            detector.Tick(0.2f, true, Dt);
            Assert.IsTrue(detector.Tick(0.03f, true, Dt));
        }

        [Test]
        public void ContactsCloserThanTheMinimumIntervalAreIgnored()
        {
            var detector = new FootContactDetector(Settings);
            var contacts = 0;
            for (var i = 0; i < 30; i++)
            {
                detector.Tick(0.2f, true, Dt);
                if (detector.Tick(0.03f, true, Dt)) contacts++;
            }
            Assert.That(contacts, Is.LessThanOrEqualTo(6));
            Assert.That(contacts, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void AirborneGivesNoContactAndNeedsANewLiftAfterLanding()
        {
            var detector = new FootContactDetector(Settings);
            detector.Tick(0.2f, true, Dt);
            Assert.IsFalse(detector.Tick(0.03f, false, Dt));
            Assert.IsFalse(detector.Tick(0.03f, true, Dt));
            detector.Tick(0.2f, true, Dt);
            Assert.IsTrue(detector.Tick(0.03f, true, Dt));
        }

        [Test]
        public void StepsAreAllowedOnlyWhenGroundedAndMoving()
        {
            Assert.IsTrue(FootContactDetector.StepsAllowed(true, 2f, 0.2f));
            Assert.IsFalse(FootContactDetector.StepsAllowed(true, 0.1f, 0.2f));
            Assert.IsFalse(FootContactDetector.StepsAllowed(false, 5f, 0.2f));
        }

        [Test]
        public void NaNHeightNeverFires()
        {
            var detector = new FootContactDetector(Settings);
            detector.Tick(0.2f, true, Dt);
            Assert.IsFalse(detector.Tick(float.NaN, true, Dt));
        }
    }

    public class PlayerFootstepsPrefabTests
    {
        [Test]
        public void TheWizardPrefabHasTheFootstepPlayer()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PlayerWizard.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<PlayerFootsteps>());
        }
    }
}

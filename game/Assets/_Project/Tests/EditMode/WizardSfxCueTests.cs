using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class WizardSfxCueTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private static readonly string[] BoltPrefabPaths =
        {
            "Assets/_Project/Prefabs/Combat/Bolt_Player.prefab",
            "Assets/_Project/Prefabs/Combat/Bolt_Dummy.prefab",
        };

        [Test]
        public void EnteringAttackIsSilentBecauseTheCastWaitsForTheBolt()
        {
            Assert.AreEqual(WizardSfxCue.None, WizardSfxCues.ForStateChange(ActionStateId.Ready, ActionStateId.Attack));
        }

        [Test]
        public void TheBoltLeavingPlaysCast()
        {
            Assert.AreEqual(WizardSfxCue.Cast, WizardSfxCues.ForBoltFired());
        }

        [Test]
        public void PressesPlayTheBodySounds()
        {
            Assert.AreEqual(WizardSfxCue.Huff, WizardSfxCues.ForAttackPress());
            Assert.AreEqual(WizardSfxCue.Snap, WizardSfxCues.ForParryPress());
        }

        [TestCase(LocomotionStateId.Grounded)]
        [TestCase(LocomotionStateId.Fall)]
        public void EnteringJumpPlaysJump(LocomotionStateId from)
        {
            Assert.AreEqual(WizardSfxCue.Jump, WizardSfxCues.ForLocomotionChange(from, LocomotionStateId.Jump));
        }

        [TestCase(LocomotionStateId.Jump, LocomotionStateId.Fall)]
        [TestCase(LocomotionStateId.Jump, LocomotionStateId.Grounded)]
        [TestCase(LocomotionStateId.Grounded, LocomotionStateId.Fall)]
        [TestCase(LocomotionStateId.Fall, LocomotionStateId.Grounded)]
        public void OtherLocomotionChangesAreSilent(LocomotionStateId from, LocomotionStateId to)
        {
            Assert.AreEqual(WizardSfxCue.None, WizardSfxCues.ForLocomotionChange(from, to));
        }

        [Test]
        public void EnteringParryPlaysShieldRaise()
        {
            Assert.AreEqual(WizardSfxCue.ShieldRaise, WizardSfxCues.ForStateChange(ActionStateId.Ready, ActionStateId.Parry));
        }

        [Test]
        public void ReturningToReadyIsSilent()
        {
            Assert.AreEqual(WizardSfxCue.None, WizardSfxCues.ForStateChange(ActionStateId.Attack, ActionStateId.Ready));
            Assert.AreEqual(WizardSfxCue.None, WizardSfxCues.ForStateChange(ActionStateId.Parry, ActionStateId.Ready));
        }

        [Test]
        public void PerfectParryPlaysParryPerfect()
        {
            Assert.AreEqual(WizardSfxCue.ParryPerfect, WizardSfxCues.ForHit(new HitResult(ParryGrade.Perfect, 0, 10)));
        }

        [Test]
        public void PartialParryPlaysParryDeflect()
        {
            Assert.AreEqual(WizardSfxCue.ParryDeflect, WizardSfxCues.ForHit(new HitResult(ParryGrade.Partial, 5, 5)));
        }

        [Test]
        public void UnparriedHitIsSilent()
        {
            Assert.AreEqual(WizardSfxCue.None, WizardSfxCues.ForHit(new HitResult(ParryGrade.None, 10, 0)));
        }

        [Test]
        public void ShippedPlayerHasWizardSfxWithEveryCueClip()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(player);
            var sfx = player.GetComponent<WizardSfx>();
            Assert.IsNotNull(sfx, "PlayerWizard needs a WizardSfx.");
            Assert.IsNotNull(sfx.Library, "WizardSfx needs a library.");
            foreach (var cue in new[] { WizardSfxCue.Cast, WizardSfxCue.ShieldRaise, WizardSfxCue.ParryDeflect, WizardSfxCue.ParryPerfect, WizardSfxCue.BoltImpact, WizardSfxCue.Snap, WizardSfxCue.Huff, WizardSfxCue.Jump })
            {
                Assert.IsTrue(sfx.Library.TryGet(cue, out var clip, out var volume), $"{cue} missing from the library.");
                Assert.IsNotNull(clip, $"{cue} has no clip.");
                Assert.Greater(volume, 0f, $"{cue} is silent.");
            }
        }

        [Test]
        public void ShippedBoltsHaveAnImpactClip()
        {
            foreach (var path in BoltPrefabPaths)
            {
                var bolt = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(bolt, path);
                Assert.IsNotNull(bolt.GetComponent<Bolt>().ImpactClip, $"{path} has no impact clip.");
            }
        }
    }
}

using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The channel wind-up through the real animator controller and the real wizard prefab: pressing Water plays
    // the wind-up state and freezes it at the hold point, and only then does the beam start. The animator is
    // stepped in bounded 50 ms frames, with the machine on a test clock.
    public class ChannelAnimationTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private const string WaterPath = "Assets/_Project/Spells/Spell_Water.asset";
        private const float Frame = 0.05f;

        private Scene _scene;
        private GameObject _wizard;
        private PlayerCombat _combat;
        private ActionAnimatorBridge _bridge;
        private Animator _animator;
        private SpellBeam _beam;
        private SpellDefinition _water;
        private int _layer;
        private float _clock = 10f;

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            _wizard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            _combat = _wizard.GetComponent<PlayerCombat>();
            _bridge = _wizard.GetComponent<ActionAnimatorBridge>();
            _animator = _wizard.GetComponent<Animator>();
            _beam = _wizard.GetComponentInChildren<SpellBeam>(true);
            _animator.Rebind();
            foreach (var shield in _wizard.GetComponentsInChildren<MagicShield>(true)) Invoke(shield, "Awake");
            Invoke(_combat, "Awake");
            Invoke(_bridge, "Awake");
            Invoke(_bridge, "Subscribe");
            _layer = _animator.GetLayerIndex(ActionAnimatorBridge.UpperBodyLayerName);
            _combat.ClockOverride = () => _clock;
            _combat.AimOverride = () => _wizard.transform.position + new Vector3(0f, 1.3f, 30f);
            _water = AssetDatabase.LoadAssetAtPath<SpellDefinition>(WaterPath);
            _combat.SetSpell(_water);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wizard);
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        private int CurrentOrNextState() =>
            (_animator.IsInTransition(_layer) ? _animator.GetNextAnimatorStateInfo(_layer) : _animator.GetCurrentAnimatorStateInfo(_layer))
                .shortNameHash;

        private float StateTime() =>
            (_animator.IsInTransition(_layer) ? _animator.GetNextAnimatorStateInfo(_layer) : _animator.GetCurrentAnimatorStateInfo(_layer))
                .normalizedTime;

        // Frames like the game loop: the machine steps, the animator advances, the bridge holds the pose.
        private void Advance(float seconds, bool animate = true)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
            {
                _clock += Frame;
                if (animate) _animator.Update(Frame);
                _combat.Step(_clock, true);
                if (animate) _bridge.HoldWindUp();
            }
        }

        private float Speed => _animator.GetFloat(ActionAnimatorBridge.ChargeSpeedParameter);

        private void AssertPoseFrozenAndChannelling(string cast)
        {
            Assert.AreEqual(ActionStateId.Channelling, _combat.Machine.Current, cast);
            Assert.AreEqual(Animator.StringToHash(_water.WindUp.State), CurrentOrNextState(), cast + ": wind-up state");
            Assert.AreEqual(0f, Speed, cast + ": the pose is frozen");
            _animator.Update(0f);
            Assert.AreEqual(_water.WindUp.HoldNormalized, StateTime(), 0.03f, cast + ": held at the hold point");
            Assert.IsFalse(_combat.Machine.IsWindingUp, cast + ": the channel started");
            Assert.IsTrue(_beam.IsActive, cast + ": the beam is up");
        }

        // The user's play-test bug: the first Water attack played the normal attack animation and only the
        // second one froze on the pose. Both casts must freeze, the first included.
        [Test]
        public void TwoConsecutiveWaterCastsBothFreezeOnThePoseFromTheFirstAttempt()
        {
            Assert.IsTrue(_combat.Attack(), "first press");
            _animator.Update(0f);
            Assert.AreEqual(Animator.StringToHash(_water.WindUp.State), CurrentOrNextState(), "first cast starts the wind-up state at once");
            Advance(1.0f);
            AssertPoseFrozenAndChannelling("first cast");

            _combat.ReleaseAttack();
            Advance(2.0f); // past the cooldown, back to Empty
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.AreEqual(1f, Speed, "the speed is back for the next cast");

            Assert.IsTrue(_combat.Attack(), "second press");
            Advance(1.0f);
            AssertPoseFrozenAndChannelling("second cast");
        }

        [Test]
        public void TheBeamAndTheChannelStartOnlyWhenThePoseIsReached()
        {
            _combat.Attack();
            Assert.IsTrue(_combat.Machine.IsWindingUp);
            Assert.IsFalse(_beam.IsActive, "no beam on press");

            Advance(0.1f);
            Assert.IsTrue(_combat.Machine.IsWindingUp, "still before the hold point");
            Assert.IsFalse(_beam.IsActive);

            Advance(1.0f);
            Assert.IsFalse(_combat.Machine.IsWindingUp);
            Assert.IsTrue(_beam.IsActive);
        }

        [Test]
        public void ReleasingBeforeThePoseCastsNothingAndRestoresThePlainSpeed()
        {
            _combat.Attack();
            Advance(0.1f);
            _combat.ReleaseAttack();

            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.IsFalse(_beam.IsActive);
            Assert.AreEqual(1f, Speed);
            Assert.IsTrue(_combat.Attack(), "no cooldown: it can start again at once");
        }

        [Test]
        public void AParryBeforeThePoseCastsNothing()
        {
            _combat.Attack();
            Advance(0.1f);
            _combat.Parry();

            Assert.AreEqual(ActionStateId.Parry, _combat.Machine.Current);
            Assert.IsFalse(_beam.IsActive);
            Advance(1.0f);
            Assert.IsFalse(_beam.IsActive, "the pose is never reported after the cancel");
        }

        // The fallback is only a safety net: the real pose (clip time of the hold point) must arrive well before it.
        [Test]
        public void ThePoseNotTheFallbackTimeoutStartsTheChannelInNormalPlay()
        {
            _combat.Attack();
            var elapsed = 0f;
            for (var frame = 0; frame < 40 && !_beam.IsActive; frame++)
            {
                Advance(Frame);
                elapsed += Frame;
            }
            Assert.IsTrue(_beam.IsActive, "the channel started");
            Assert.Greater(elapsed, 0.3f, "not before the pose");
            Assert.Less(elapsed, _water.WindUp.TimeoutSeconds - 0.2f, "well before the fallback timeout");
        }

        [Test]
        public void WhenTheAnimatorNeverReportsThePoseTheTimeoutStartsTheChannel()
        {
            _combat.Attack();
            Advance(0.5f, animate: false);
            Assert.IsFalse(_beam.IsActive, "inside the timeout");

            Advance(0.6f, animate: false);
            Assert.IsTrue(_beam.IsActive, "timeout passed (" + _water.WindUp.TimeoutSeconds + " s)");
            Assert.IsFalse(_combat.Machine.IsWindingUp);
        }

        [Test]
        public void ReleasingAfterThePoseEndsTheChannelAndHidesTheBeam()
        {
            _combat.Attack();
            Advance(1.0f);
            Assert.IsTrue(_beam.IsActive);

            _combat.ReleaseAttack();
            Assert.IsFalse(_beam.IsActive);
            Assert.AreEqual(1f, Speed);
        }

        // ---- cast anchor ----

        private Vector3 OrbPosition() => _wizard.GetComponent<CastAnchors>().Resolve(CastAnchors.StaffOrb).position;

        [Test]
        public void TheWaterBeamStartsAtTheStaffOrbNotTheMuzzle()
        {
            _combat.Attack();
            Advance(1.0f);
            Assert.IsTrue(_beam.IsActive);
            Invoke(_combat, "Update");
            _beam.UpdateToFinalPose(); // the beam draws at the end of the frame

            var pivot = _beam.transform.GetChild(0);
            var muzzle = _wizard.GetComponent<CastAnchors>().Resolve(CastAnchors.Muzzle).position;
            Assert.AreEqual(OrbPosition().x, pivot.position.x, 1e-3f);
            Assert.AreEqual(OrbPosition().y, pivot.position.y, 1e-3f);
            Assert.AreEqual(OrbPosition().z, pivot.position.z, 1e-3f);
            Assert.Greater(Vector3.Distance(pivot.position, muzzle), 0.05f, "not at the muzzle");
        }

        private SpellDefinition AnchoredClone(SpellDefinition source, string anchor)
        {
            var clone = Object.Instantiate(source);
            var so = new SerializedObject(clone);
            so.FindProperty("castAnchor").stringValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return clone;
        }

        [Test]
        public void TheChargeOrbFollowsTheSpellsAnchor()
        {
            var fire = AnchoredClone(AssetDatabase.LoadAssetAtPath<SpellDefinition>("Assets/_Project/Spells/Spell_Fire.asset"), CastAnchors.StaffOrb);
            try
            {
                _combat.SetSpell(fire);
                _combat.Attack();
                Advance(0.3f);
                var orb = _wizard.GetComponentInChildren<ChargeOrb>(true);
                var visual = (Transform)typeof(ChargeOrb).GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(orb);
                Assert.IsTrue(orb.IsVisible);
                Assert.AreEqual(0f, Vector3.Distance(visual.position, OrbPosition()), 1e-3f);
            }
            finally { Object.DestroyImmediate(fire); }
        }

        [Test]
        public void ABoltStartsAtTheSpellsAnchorAndOtherSpellsStayOnTheMuzzle()
        {
            var baseSpell = AssetDatabase.LoadAssetAtPath<SpellDefinition>("Assets/_Project/Spells/Spell_Base.asset");
            var anchored = AnchoredClone(baseSpell, CastAnchors.StaffOrb);
            try
            {
                var muzzle = _wizard.GetComponent<CastAnchors>().Resolve(CastAnchors.Muzzle).position;

                _combat.SetSpell(baseSpell);
                _combat.Attack();
                Advance(0.3f); // past the bolt delay: the bolt has left
                var onMuzzle = NewestBoltPosition();
                Assert.Less(Vector3.Distance(onMuzzle, muzzle), Vector3.Distance(onMuzzle, OrbPosition()), "Base keeps the muzzle");
                CleanBolts();

                Advance(1.0f);
                _combat.SetSpell(anchored);
                _combat.Attack();
                Advance(0.3f);
                var onOrb = NewestBoltPosition();
                Assert.Less(Vector3.Distance(onOrb, OrbPosition()), Vector3.Distance(onOrb, muzzle));
            }
            finally
            {
                CleanBolts();
                Object.DestroyImmediate(anchored);
            }
        }

        private static Vector3 NewestBoltPosition()
        {
            var bolts = Object.FindObjectsByType<Bolt>(FindObjectsInactive.Exclude);
            Assert.IsNotEmpty(bolts, "a bolt was fired");
            return bolts[0].transform.position;
        }

        private static void CleanBolts()
        {
            foreach (var bolt in Object.FindObjectsByType<Bolt>(FindObjectsInactive.Include)) Object.DestroyImmediate(bolt.gameObject);
            BoltPool.Clear();
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The MonoBehaviour adapter: attack-start counting, release, spell swap and the lost-release guard.
    public class PlayerCombatTests
    {
        private GameObject _go;
        private PlayerCombat _combat;
        private readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Wizard");
            _combat = _go.AddComponent<PlayerCombat>();
            // Awake does not run on AddComponent outside play mode.
            typeof(PlayerCombat).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_combat, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private SpellDefinition Spell(CastPattern pattern)
        {
            var spell = ScriptableObject.CreateInstance<SpellDefinition>();
            typeof(SpellDefinition).GetField("pattern", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spell, pattern);
            _made.Add(spell);
            return spell;
        }

        private static float Now => Time.time;

        [Test]
        public void AnAttackStartsOnceThenIsRefusedWhileItRuns()
        {
            Assert.IsTrue(_combat.Attack());
            Assert.AreEqual(ActionStateId.Attack, _combat.Machine.Current);
            Assert.IsFalse(_combat.Attack());
        }

        [Test]
        public void ChargingAndChannellingSpellsCountTheirPressAsAStart()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            Assert.IsTrue(_combat.Attack());
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);

            _combat.SetSpell(Spell(CastPattern.Channel));
            Assert.IsTrue(_combat.Attack());
            Assert.AreEqual(ActionStateId.Channelling, _combat.Machine.Current);
        }

        [Test]
        public void ReleasingAChargeFiresItAndDoesNotCountAsANewStart()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            Assert.IsTrue(_combat.Attack());
            var shots = 0;
            _combat.Machine.ShotFired += _ => shots++;
            _combat.ReleaseAttack();
            Assert.AreEqual(ActionStateId.Attack, _combat.Machine.Current);
            Assert.AreEqual(1, shots, "a released charge leaves at once");
            Assert.IsFalse(_combat.Attack(), "the release is not an attack start and the spell is cooling down");
        }

        [Test]
        public void ReleasingWithNothingHeldChangesNothing()
        {
            _combat.ReleaseAttack();
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
        }

        [Test]
        public void SetSpellKeepsTheSpellAndCancelsACastInProgress()
        {
            var fire = Spell(CastPattern.Charged);
            _combat.SetSpell(fire);
            _combat.Attack();
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);

            var earth = Spell(CastPattern.Burst);
            _combat.SetSpell(earth);
            Assert.AreSame(earth, _combat.ActiveSpell);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.IsTrue(_combat.Attack());
            Assert.AreEqual(ActionStateId.Attack, _combat.Machine.Current);
        }

        [Test]
        public void SetSpellToNullFallsBackToTheBaseCaster()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            _combat.SetSpell(null);
            Assert.IsNull(_combat.ActiveSpell);
            Assert.IsTrue(_combat.Attack());
            Assert.AreEqual(ActionStateId.Attack, _combat.Machine.Current);
        }

        [Test]
        public void AHoldIsDroppedWhenTheAttackInputIsNoLongerHeld()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            Assert.IsTrue(_combat.StartAttackFromInput());
            var shots = 0;
            _combat.Machine.ShotFired += _ => shots++;
            _combat.Step(Now, attackHeld: true);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
            _combat.Step(Now, attackHeld: false);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.AreEqual(0, shots);
        }

        [Test]
        public void ACastStartedByAScriptedAttackIsNotDroppedWhenTheInputIsNotHeld()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            Assert.IsTrue(_combat.Attack());
            _combat.Step(Now, attackHeld: false);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
        }

        [Test]
        public void TheInputGuardClearsOnceTheCastEndsSoALaterScriptedCastIsKept()
        {
            _combat.SetSpell(Spell(CastPattern.Charged));
            _combat.StartAttackFromInput();
            _combat.Step(Now, attackHeld: false);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);

            Assert.IsTrue(_combat.Attack());
            _combat.Step(Now, attackHeld: false);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
        }

        [Test]
        public void UpdateReadsTheHeldSourceAndDropsAnInputHoldWhoseReleaseWasLost()
        {
            var held = true;
            _combat.AttackHeldSource = () => held;
            _combat.SetSpell(Spell(CastPattern.Charged));
            _combat.StartAttackFromInput();

            Update();
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
            held = false;
            Update();
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
        }

        private void Update() =>
            typeof(PlayerCombat).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_combat, null);

        // ---- A click made while the spell is not ready stays pending while the button is held ----

        // A Fire-like spell fired by input at t = 1 (released at 1.2, cooling down until ~1.8), then clicked again at 1.4.
        private float _t;

        private void FireThenClickDuringTheCooldown()
        {
            _combat.ClockOverride = () => _t;
            _combat.SetSpell(Spell(CastPattern.Charged));
            _t = 1f;
            _combat.StartAttackFromInput();
            _t = 1.2f;
            _combat.ReleaseAttack();
            _t = 1.4f;
            Assert.IsFalse(_combat.StartAttackFromInput(), "refused: cooling down");
            Assert.IsTrue(_combat.Machine.HasPendingAttack);
        }

        [Test]
        public void AnInputPressDuringTheCooldownStartsTheCastWhenReadyAndCountsAsAnInputCast()
        {
            FireThenClickDuringTheCooldown();
            _combat.Step(1.5f, attackHeld: true);
            Assert.AreNotEqual(ActionStateId.Charging, _combat.Machine.Current, "still cooling down");
            _combat.Step(2f, attackHeld: true);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "the pending click started the charge");
            _combat.Step(2.1f, attackHeld: false);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current, "its release was lost: the input-cast guard drops it");
        }

        [Test]
        public void ARefusedInputPressStartedByAScriptedTickIsStillAnInputCast()
        {
            FireThenClickDuringTheCooldown();
            _t = 2f;
            Assert.IsTrue(_combat.Attack(), "the pending press starts on a tick made outside Step");
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);

            _combat.Step(2.1f, attackHeld: false);

            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current, "its release was lost: the input-cast guard drops it");
        }

        [Test]
        public void ARefusedInputPressStartedByAnIdleMachineTickIsStillAnInputCast()
        {
            FireThenClickDuringTheCooldown();
            _combat.Machine.Tick(new ActionIntent(false, false), 2f);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);

            _combat.Step(2.1f, attackHeld: false);

            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
        }

        [Test]
        public void AnInputPressReleasedWithoutAReleaseEventIsDroppedBeforeItStarts()
        {
            FireThenClickDuringTheCooldown();
            _combat.Step(1.5f, attackHeld: false);
            Assert.IsFalse(_combat.Machine.HasPendingAttack);
            _combat.Step(2f, attackHeld: true);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
        }

        [Test]
        public void ReleaseAttackDropsAPendingInputPress()
        {
            FireThenClickDuringTheCooldown();
            _combat.ReleaseAttack();
            _combat.Step(2f, attackHeld: true);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
        }

        [Test]
        public void ThePendingStartIsCountedAsAnAttackStart()
        {
            FireThenClickDuringTheCooldown();
            var starts = 0;
            _combat.Machine.StateChanged += (from, to) => { if (ActionStateRules.StartsCast(from, to)) starts++; };
            _combat.Step(2f, attackHeld: true);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
        }

        [Test]
        public void AScriptedPressThatWasRefusedIsNotDroppedByAnUnheldInput()
        {
            _combat.ClockOverride = () => _t;
            _combat.SetSpell(Spell(CastPattern.Charged));
            _t = 1f;
            _combat.Attack();
            _t = 1.2f;
            _combat.ReleaseAttack();
            _t = 1.4f;
            Assert.IsFalse(_combat.Attack());
            _combat.Step(2f, attackHeld: false);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "a scripted press has no input to lose");
            _combat.Step(2.1f, attackHeld: false);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "and its hold is not an input cast");
        }

        [Test]
        public void ARefusedInputPressDoesNotDropARunningScriptedHoldWhenTheInputIsNotHeld()
        {
            _combat.ClockOverride = () => _t;
            _combat.SetSpell(Spell(CastPattern.Charged));
            _t = 1f;
            Assert.IsTrue(_combat.Attack());
            Assert.IsFalse(_combat.StartAttackFromInput());
            _combat.Step(1.1f, attackHeld: false);
            Assert.IsFalse(_combat.Machine.HasPendingAttack, "the unheld input dropped its pending press");
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current, "but the scripted hold is untouched");
        }

        // ---- A launch that throws must not leak the pooled bolt ----

        [Test]
        public void ABoltWhoseLaunchThrowsGoesBackToThePool()
        {
            var prefabGo = new GameObject("BoltPrefab");
            _made.Add(prefabGo);
            prefabGo.SetActive(false);
            var prefab = prefabGo.AddComponent<Bolt>();
            typeof(PlayerCombat).GetField("boltPrefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_combat, prefab);
            _combat.AimOverride = () => new Vector3(0f, 1.3f, 50f);

            var spell = Spell(CastPattern.Single);
            var projectile = spell.Projectile;
            projectile.weight = float.NaN; // ProjectileFlight refuses it
            typeof(SpellDefinition).GetField("projectile", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spell, projectile);
            _combat.SetSpell(spell);

            var t = Now;
            _combat.Machine.Tick(new ActionIntent(true, false), t);
            Assert.Throws<System.ArgumentException>(() => _combat.Machine.Tick(new ActionIntent(false, false), t + 0.5f));

            var active = 0;
            foreach (var bolt in Object.FindObjectsByType<Bolt>()) if (bolt != prefab) active++;
            Assert.AreEqual(0, active, "no active bolt is left behind");
            foreach (var bolt in Object.FindObjectsByType<Bolt>(FindObjectsInactive.Include))
                if (bolt != prefab) _made.Add(bolt.gameObject);
            BoltPool.Clear();
        }

        // ---- The Water beam end to end ----

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public void TakeHit(HitInfo hit) => Hits.Add(hit);
        }

        private SpellBeam WireBeam(SpellDefinition spell, out Target target)
        {
            var beamGo = new GameObject("Beam");
            _made.Add(beamGo);
            var pivot = new GameObject("Pivot");
            _made.Add(pivot);
            pivot.transform.SetParent(beamGo.transform, false);
            pivot.SetActive(false);
            var beam = beamGo.AddComponent<SpellBeam>();
            typeof(SpellBeam).GetField("pivot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(beam, pivot.transform);
            typeof(PlayerCombat).GetField("beam", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_combat, beam);
            _combat.AimOverride = () => new Vector3(0f, 1.3f, 50f);
            _combat.SetSpell(spell);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(cube);
            cube.transform.position = new Vector3(0f, 1.3f, 5f);
            Physics.SyncTransforms();
            target = cube.AddComponent<Target>();
            return beam;
        }

        [Test]
        public void ChannellingDamagesTheTargetEachTickShowsTheBeamAndHidesItOnRelease()
        {
            var beam = WireBeam(Spell(CastPattern.Channel), out var target);
            var t = Now;
            _combat.Machine.Tick(new ActionIntent(true, false), t);
            Assert.IsTrue(beam.IsActive);

            _combat.Machine.Tick(new ActionIntent(false, false), t + 0.55f);
            beam.UpdateToFinalPose(); // ticks land in the end-of-frame pass
            Assert.AreEqual(5, target.Hits.Count, "10 ticks/s for 0.55 s");
            Assert.AreEqual(4, target.Hits[0].Damage);
            Assert.AreSame(_combat.ActiveSpell, target.Hits[0].Spell);

            _combat.Machine.Tick(new ActionIntent(false, false, true), t + 0.55f);
            Assert.IsFalse(beam.IsActive);
        }

        [Test]
        public void TheBeamFollowsTheCameraEveryFrameWhileChannelling()
        {
            var beam = WireBeam(Spell(CastPattern.Channel), out _);
            _combat.AttackHeldSource = () => true;
            _combat.StartAttackFromInput();
            Update();
            beam.UpdateToFinalPose();
            Assert.AreEqual(4.5f, beam.Length, 0.01f, "from the muzzle to the cube face at z 4.5");
        }

        [Test]
        public void AParryEndsTheChannelAndHidesTheBeam()
        {
            var beam = WireBeam(Spell(CastPattern.Channel), out _);
            var t = Now;
            _combat.Machine.Tick(new ActionIntent(true, false), t);
            Assert.IsTrue(beam.IsActive);
            _combat.Machine.Tick(new ActionIntent(false, true), t + 0.2f);
            Assert.IsFalse(beam.IsActive);
        }

        [Test]
        public void SwappingTheSpellMidChannelHidesTheBeam()
        {
            var beam = WireBeam(Spell(CastPattern.Channel), out _);
            _combat.Machine.Tick(new ActionIntent(true, false), Now);
            Assert.IsTrue(beam.IsActive);
            _combat.SetSpell(Spell(CastPattern.Single));
            Assert.IsFalse(beam.IsActive);
        }

        [Test]
        public void TheChannelCooldownIsTheBasePlusTheChannelledTimeScaled()
        {
            WireBeam(Spell(CastPattern.Channel), out _);
            var t = Now;
            _combat.Machine.Tick(new ActionIntent(true, false), t);
            _combat.Machine.Tick(new ActionIntent(false, false, true), t + 1f); // 1 s channelled: 0.5 + 1 x 0.6
            var starts = 0;
            _combat.Machine.AttackPressed += () => starts++;

            _combat.Machine.Tick(new ActionIntent(true, false), t + 1f + 1.0f);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current, "still cooling down 1.0 s after the release");
            _combat.Machine.Tick(new ActionIntent(true, false), t + 1f + 1.2f);
            Assert.AreEqual(ActionStateId.Channelling, _combat.Machine.Current, "ready after 1.1 s");
        }
    }
}

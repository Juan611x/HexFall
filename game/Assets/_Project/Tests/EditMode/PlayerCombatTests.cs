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
            _combat.Attack();
            var shots = 0;
            _combat.Machine.ShotFired += _ => shots++;
            _combat.Step(Now, attackHeld: true);
            Assert.AreEqual(ActionStateId.Charging, _combat.Machine.Current);
            _combat.Step(Now, attackHeld: false);
            Assert.AreEqual(ActionStateId.Ready, _combat.Machine.Current);
            Assert.AreEqual(0, shots);
        }
    }
}

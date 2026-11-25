using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // Swapping the spell keeps the running cooldown, and a press the machine refuses never turns into a
    // hold later (no buffered input).
    public class ActionMachineSwapTests
    {
        private static readonly ActionIntent None = default;
        private static readonly ActionIntent Press = new ActionIntent(true, false);
        private static readonly ActionIntent Release = new ActionIntent(false, false, true);

        private static CastConfig Single(float cooldown) => new CastConfig
        {
            Pattern = CastPattern.Single, Damage = 10, CooldownSeconds = cooldown,
        };

        private static CastConfig Charged() => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = 1.2f, BaseDamage = 18, ChargeMode = ChargeDamageMode.Curve, ChargeCurve = ChargeDamage.Bake(x => 1f + 1.5f * x),
        };

        private static CastConfig Burst() => new CastConfig
        {
            Pattern = CastPattern.Burst, Damage = 8, CooldownSeconds = 0.7f,
            BurstCount = 3, BurstIntervalSeconds = 0.08f, BurstSpreadDegrees = 3f,
        };

        // ---- Cooldown across a swap ----

        [Test]
        public void ASwapDuringTheCooldownKeepsWaitingForTheRemainingTime()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Single(2f)));
            machine.Tick(Press, 1f);
            machine.Tick(None, 1.6f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);

            machine.SetCaster(new SpellCaster(Single(0.5f)), 1.7f);
            machine.Tick(Press, 1.8f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current, "the old cast's cooldown (until 3.0) still runs");
            machine.Tick(Press, 2.99f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            machine.Tick(Press, 3f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
        }

        [Test]
        public void ASwapAfterTheCooldownDoesNotDelayTheNextCast()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Single(0.5f)));
            machine.Tick(Press, 1f);
            machine.Tick(None, 1.6f);
            machine.SetCaster(new SpellCaster(Single(0.5f)), 1.7f);
            machine.Tick(Press, 1.7f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
        }

        [Test]
        public void ASwapNeverShortensTheNewSpellsOwnCooldown()
        {
            var next = new SpellCaster(Single(5f));
            next.Press(0f);
            next.Tick(0.1f); // cooldown until 5.0 on the incoming caster
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Single(0.5f)));
            machine.SetCaster(next, 0.2f);
            machine.Tick(Press, 1f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ASwapDuringABurstCarriesTheCooldownOfTheCancelledCast()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Burst()));
            machine.Tick(Press, 1f);
            machine.Tick(None, 1.05f);
            machine.SetCaster(new SpellCaster(Single(0.1f)), 1.05f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            machine.Tick(Press, 1.5f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current, "cancelled burst: cooldown 0.7 from the swap");
            machine.Tick(Press, 1.76f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
        }

        [Test]
        public void ASwapAfterALostChargeKeepsNoCooldown()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Charged()));
            machine.Tick(Press, 1f);
            machine.SetCaster(new SpellCaster(Single(0.5f)), 1.2f);
            machine.Tick(Press, 1.2f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current, "a cancelled charge never started a cooldown");
        }

        [Test]
        public void ACasterReportsWhenItBecomesReady()
        {
            var caster = new SpellCaster(Single(2f));
            Assert.AreEqual(float.NegativeInfinity, caster.ReadyAt);
            caster.Press(1f);
            caster.Tick(1.5f);
            Assert.AreEqual(3f, caster.ReadyAt, 1e-4f);
        }

        [Test]
        public void InheritingANonFiniteCooldownIsIgnored()
        {
            var caster = new SpellCaster(Single(0.5f));
            caster.InheritCooldown(float.NaN);
            caster.InheritCooldown(float.PositiveInfinity);
            Assert.IsTrue(caster.IsReady(0f));
        }

        // ---- A refused press never becomes a hold ----

        [Test]
        public void APressDuringTheCooldownDoesNotStartAChargeNowOrLater()
        {
            var other = new ActionMachine(new ActionSettings(), new SpellCaster(Charged()));
            other.Tick(Press, 1f);
            other.Tick(Release, 1.3f);
            other.Tick(None, 1.85f);
            Assert.AreEqual(ActionStateId.Ready, other.Current);
            other.Tick(Press, 1.85f); // refused: still cooling down
            Assert.AreNotEqual(ActionStateId.Charging, other.Current);
            other.Tick(None, 1.86f);
            other.Tick(None, 2.5f);
            Assert.AreNotEqual(ActionStateId.Charging, other.Current, "no buffered press");
            Assert.IsFalse(other.IsHolding);
        }

        [Test]
        public void APressWhileAnAttackRunsIsRefusedAndNotBuffered()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Single(0.5f)));
            machine.Tick(Press, 1f);
            var presses = 0;
            machine.AttackPressed += () => presses++;
            machine.Tick(Press, 1.1f);
            Assert.AreEqual(1, presses, "the press was seen");
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            machine.Tick(None, 1.6f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current, "the refused press did not queue another attack");
            Assert.IsFalse(machine.IsHolding);
        }

        [Test]
        public void ARefusedPressDuringAParryNeverStartsAHoldWhenTheParryEnds()
        {
            var machine = new ActionMachine(new ActionSettings { guardSeconds = 0.4f, shieldCharges = 3, chargeRechargeSeconds = 6f, perfectParrySeconds = 0.15f, partialBlock = 0.5f },
                new SpellCaster(Charged()));
            machine.Tick(new ActionIntent(false, true), 1f);
            machine.Tick(Press, 1.1f);
            machine.Tick(None, 1.6f);
            Assert.IsFalse(machine.IsHolding);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }
    }
}

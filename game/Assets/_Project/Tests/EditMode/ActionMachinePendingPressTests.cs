using System.Collections.Generic;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // A press the machine refuses (cooldown, a cast in progress, a parry) stays pending while the attack
    // button is held: the first tick on which the wizard is ready starts the cast like a fresh press.
    // Releasing (or a lost release) drops it; a pending press is consumed once, so holding never repeats.
    public class ActionMachinePendingPressTests
    {
        private static readonly ActionIntent None = default;
        private static readonly ActionIntent Press = new ActionIntent(true, false);
        private static readonly ActionIntent Release = new ActionIntent(false, false, true);
        private static readonly ActionIntent Cancel = new ActionIntent(false, false, false, true);
        private static readonly ActionIntent Parry = new ActionIntent(false, true);
        private static readonly ActionIntent PressAndRelease = new ActionIntent(true, false, true);

        private static CastConfig Single(float cooldown = 0.5f) => new CastConfig
        {
            Pattern = CastPattern.Single, Damage = 10, CooldownSeconds = cooldown,
        };

        private static CastConfig Charged() => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f, MaxChargeSeconds = 1.2f, BaseDamage = 18,
            ChargeMode = ChargeDamageMode.Curve, ChargeCurve = ChargeDamage.Bake(x => 1f + 1.5f * x),
        };

        private static CastConfig Burst() => new CastConfig
        {
            Pattern = CastPattern.Burst, Damage = 8, CooldownSeconds = 0.7f,
            BurstCount = 3, BurstIntervalSeconds = 0.08f, BurstSpreadDegrees = 3f,
        };

        private static CastConfig Channel() => new CastConfig
        {
            Pattern = CastPattern.Channel, CooldownSeconds = 0.5f,
            ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f, ChannelWindUpTimeoutSeconds = 1f,
            ChannelDamagePerTick = 4, ChannelCooldownPerSecond = 0.6f,
        };

        private static ActionSettings Settings() => new ActionSettings
        {
            guardSeconds = 0.4f, shieldCharges = 3, chargeRechargeSeconds = 6f, perfectParrySeconds = 0.15f, partialBlock = 0.5f,
        };

        private static float _clock;

        // The machine plus the times (the tick clock) at which its shots left.
        private static ActionMachine Machine(CastConfig config, out List<float> shotTimes)
        {
            var machine = new ActionMachine(Settings(), new SpellCaster(config));
            var times = new List<float>();
            machine.ShotFired += _ => times.Add(_clock);
            shotTimes = times;
            return machine;
        }

        private static void Tick(ActionMachine machine, ActionIntent intent, float at)
        {
            _clock = at;
            machine.Tick(intent, at);
        }

        // ---- Single ----

        [Test]
        public void ASinglePressDuringTheAttackAndCooldownCastsOnceTheWizardIsReady()
        {
            var machine = Machine(Single(0.5f), out var shots);
            Tick(machine, Press, 1f);                // accepted, the bolt leaves at 1.2
            Tick(machine, Press, 1.3f);              // refused: the attack is running
            Assert.IsTrue(machine.HasPendingAttack);
            Tick(machine, None, 1.4f);
            for (var t = 1.5f; t < 3f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(2, shots.Count, "the pending press cast exactly once and never repeats while held");
            Assert.IsFalse(machine.HasPendingAttack);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void ThePendingCastStartsAtTheTickWhereTheWizardBecomesReadyNotBefore()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, None, 1.3f);               // the bolt left at 1.2; the cooldown runs until 3.0
            Tick(machine, Press, 1.6f);              // refused: cooling down
            Tick(machine, None, 2.9f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current, "still cooling down");
            Tick(machine, None, 3.1f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current, "the cast started at the first ready tick");
            Tick(machine, None, 3.5f);
            Assert.AreEqual(2, shots.Count);
            Assert.GreaterOrEqual(shots[1], 3.1f + 0.2f - 1e-4f, "its bolt leaves one wind-up after the tick that started it");
        }

        [Test]
        public void TheCastStartIsObservableLikeAnImmediateOne()
        {
            var machine = Machine(Single(0.5f), out _);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.1f);
            var changes = new List<(ActionStateId, ActionStateId)>();
            machine.StateChanged += (a, b) => changes.Add((a, b));
            for (var t = 1.2f; t < 2f; t += 0.01f) Tick(machine, None, t);
            Assert.IsTrue(changes.Contains((ActionStateId.Ready, ActionStateId.Attack)), "a Ready -> Attack change marks the pending start");
        }

        [Test]
        public void AttackPressedIsRaisedForTheClickOnlyNotAgainWhenThePendingCastStarts()
        {
            var machine = Machine(Single(0.5f), out _);
            var presses = 0;
            machine.AttackPressed += () => presses++;
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.1f);
            for (var t = 1.2f; t < 2f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(2, presses, "one per press the tick saw");
        }

        // ---- Charged ----

        [Test]
        public void AChargedPressDuringTheCooldownStartsChargingWhenItEndsAndKeepsChargingWhileHeld()
        {
            var machine = Machine(Charged(), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Release, 1.2f);            // fires at once; cooldown 0.6 until 1.8
            Tick(machine, Press, 1.4f);              // refused
            Tick(machine, None, 1.7f);
            Assert.AreNotEqual(ActionStateId.Charging, machine.Current, "still cooling down");
            Tick(machine, None, 1.9f);
            Assert.AreEqual(ActionStateId.Charging, machine.Current);
            Tick(machine, None, 2.5f);
            Assert.AreEqual(ActionStateId.Charging, machine.Current, "still held, still charging");
            Assert.AreEqual(0.6f / 1.2f, machine.ChargeLevel(2.5f), 1e-3f, "the charge counts from the tick that started it");
            Tick(machine, Release, 2.5f);
            Assert.AreEqual(2, shots.Count);
        }

        [Test]
        public void ARefusedPressWhileChargingIsDroppedByTheRelease()
        {
            var machine = Machine(Charged(), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.1f);              // the machine is Charging: refused
            Assert.IsTrue(machine.HasPendingAttack);
            Tick(machine, Release, 1.5f);
            Assert.IsFalse(machine.HasPendingAttack);
            for (var t = 1.6f; t < 3.5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
            Assert.AreNotEqual(ActionStateId.Charging, machine.Current, "the click was spent by the release, no second charge");
        }

        // ---- Burst and Channel ----

        [Test]
        public void ABurstPressDuringTheCooldownFiresTheWholeBurstWhenReady()
        {
            var machine = Machine(Burst(), out var shots);
            Tick(machine, Press, 1f);
            for (var t = 1.01f; t < 1.5f; t += 0.01f) Tick(machine, t < 1.3f ? None : Press, t);
            for (var t = 1.5f; t < 4f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(6, shots.Count, "two bursts of three");
        }

        [Test]
        public void AChannelPressDuringTheCooldownStartsTheChannelWhenReady()
        {
            var machine = Machine(Channel(), out _);
            var starts = 0;
            machine.ChannelStarted += () => starts++;
            Tick(machine, Press, 1f);
            machine.PoseReached(1.1f);
            Tick(machine, Release, 1.3f);            // channel ends; cooldown 0.5 + 0.2 x 0.6
            Tick(machine, Press, 1.4f);              // refused
            Tick(machine, None, 1.45f);
            Assert.AreNotEqual(ActionStateId.Channelling, machine.Current);
            Tick(machine, None, 2.5f);
            Assert.AreEqual(ActionStateId.Channelling, machine.Current);
            Assert.IsTrue(machine.IsWindingUp, "like a fresh press the channel waits for its pose");
            machine.PoseReached(2.6f);
            Assert.AreEqual(2, starts);
        }

        // ---- Drop ----

        [Test]
        public void ReleasingBeforeTheWizardIsReadyDropsThePress()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);
            Tick(machine, Release, 1.8f);
            Assert.IsFalse(machine.HasPendingAttack);
            for (var t = 1.9f; t < 5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void ALostReleaseDropsThePendingPress()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);
            Tick(machine, Cancel, 1.8f);
            Assert.IsFalse(machine.HasPendingAttack);
            for (var t = 1.9f; t < 5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void APressAndReleaseOnTheSameTickWhileBusyCastsNothingLater()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, PressAndRelease, 1.5f);
            Assert.IsFalse(machine.HasPendingAttack);
            for (var t = 1.6f; t < 5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void AReleaseOnTheTickTheWizardBecomesReadyStillDropsThePress()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);
            Tick(machine, Release, 3.1f);            // ready at 3.0, but the button was released first
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
            for (var t = 3.4f; t < 5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void AFreshPressWhileReadyBehavesAsBefore()
        {
            var machine = Machine(Single(0.5f), out _);
            Tick(machine, Press, 1f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current);
            Assert.IsFalse(machine.HasPendingAttack);
        }

        [Test]
        public void DroppingThePendingPressCastsNothingLaterAndLeavesTheRunningCastAlone()
        {
            var machine = Machine(Charged(), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.1f);              // refused while charging
            machine.DropPendingAttack();
            Assert.IsFalse(machine.HasPendingAttack);
            Assert.AreEqual(ActionStateId.Charging, machine.Current, "the hold itself is untouched");
            Tick(machine, Release, 1.5f);
            for (var t = 1.6f; t < 3f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(1, shots.Count);
        }

        // ---- Parry ----

        [Test]
        public void APendingPressSurvivesAParryAndCastsWhenTheParryEnds()
        {
            var machine = Machine(Single(0.5f), out var shots);
            Tick(machine, Parry, 1f);                // guard until 1.4
            Tick(machine, Press, 1.1f);              // refused during the parry
            Assert.IsTrue(machine.HasPendingAttack);
            Tick(machine, None, 1.3f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
            Tick(machine, None, 1.5f);
            Assert.AreEqual(ActionStateId.Attack, machine.Current, "the guard ended and the spell is ready");
            Tick(machine, None, 1.8f);
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void APressPendingBeforeAParryAlsoSurvivesIt()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);              // pending on the cooldown (ready at 3.0)
            Tick(machine, Parry, 1.6f);              // parry interrupts; the held button is still down
            Assert.IsTrue(machine.HasPendingAttack);
            for (var t = 1.7f; t < 4f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(2, shots.Count);
        }

        [Test]
        public void ReleasingDuringAParryDropsThePendingPress()
        {
            var machine = Machine(Single(0.5f), out var shots);
            Tick(machine, Parry, 1f);
            Tick(machine, Press, 1.1f);
            Tick(machine, Release, 1.2f);
            for (var t = 1.3f; t < 3f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(0, shots.Count);
        }

        [Test]
        public void TheParryPressItselfIsUnaffectedByAPendingAttack()
        {
            var machine = Machine(Single(2f), out _);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);
            Tick(machine, Parry, 1.6f);
            Assert.AreEqual(ActionStateId.Parry, machine.Current);
        }

        // ---- Swap ----

        [Test]
        public void ASwapKeepsThePendingPressAgainstTheNewSpell()
        {
            var machine = Machine(Single(2f), out var shots);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);              // pending against Single (ready at 3.0)
            machine.SetCaster(new SpellCaster(Charged()), 1.6f);
            Assert.IsTrue(machine.HasPendingAttack, "the pending press follows the held button, not the spell");
            Tick(machine, None, 2.9f);
            Assert.AreNotEqual(ActionStateId.Charging, machine.Current, "the inherited cooldown still runs");
            Tick(machine, None, 3.1f);
            Assert.AreEqual(ActionStateId.Charging, machine.Current, "it started the NEW spell's cast");
            Assert.AreEqual(1, shots.Count);
        }

        [Test]
        public void ASwapThenReleaseDropsThePress()
        {
            var machine = Machine(Single(2f), out _);
            Tick(machine, Press, 1f);
            Tick(machine, Press, 1.5f);
            machine.SetCaster(new SpellCaster(Charged()), 1.6f);
            Tick(machine, Release, 1.7f);
            for (var t = 1.8f; t < 5f; t += 0.01f) Tick(machine, None, t);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }
    }
}

using System.Collections.Generic;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // The action machine drives the active spell's caster: wind-up and aim hold stay in the machine,
    // cooldown, charge and channel timing come from the spell.
    public class ActionMachineSpellTests
    {
        private const float Frame = 0.01f;

        private static readonly ActionIntent None = default;
        private static readonly ActionIntent Press = new ActionIntent(true, false);
        private static readonly ActionIntent Release = new ActionIntent(false, false, true);
        private static readonly ActionIntent Parry = new ActionIntent(false, true);

        private static CastConfig Single(float cooldown = 0.5f, int damage = 18) => new CastConfig
        {
            Pattern = CastPattern.Single, Damage = damage, CooldownSeconds = cooldown,
        };

        private static CastConfig Charged() => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f,
            MaxChargeSeconds = 1.2f, MinDamage = 18, MaxDamage = 45,
        };

        private static CastConfig Burst() => new CastConfig
        {
            Pattern = CastPattern.Burst, Damage = 8, CooldownSeconds = 0.7f,
            BurstCount = 3, BurstIntervalSeconds = 0.08f, BurstSpreadDegrees = 3f,
        };

        private static CastConfig Channel() => new CastConfig
        {
            Pattern = CastPattern.Channel, CooldownSeconds = 0.5f,
            ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f,
            ChannelDamagePerTick = 4, ChannelCooldownPerSecond = 0.6f,
        };

        private sealed class Rig
        {
            public readonly ActionMachine Machine;
            public readonly List<(ProjectileShot Shot, float At)> Shots = new List<(ProjectileShot, float)>();
            public readonly List<int> ChannelTicks = new List<int>();
            public readonly List<float> ChannelEnds = new List<float>();
            public int ChannelStarts;
            public float Now;

            public Rig(CastConfig config, float start = 1f)
            {
                Machine = new ActionMachine(new ActionSettings(), new SpellCaster(config));
                Machine.ShotFired += s => Shots.Add((s, Now));
                Machine.ChannelStarted += () => ChannelStarts++;
                Machine.ChannelTicked += ChannelTicks.Add;
                Machine.ChannelEnded += ChannelEnds.Add;
                Now = start;
            }

            public ActionStateId State => Machine.Current;

            public void Tick(ActionIntent intent, float at)
            {
                Now = at;
                Machine.Tick(intent, at);
            }

            // Ticks every frame up to the given time, like the game loop does.
            public void RunTo(float to)
            {
                while (Now + Frame <= to + 1e-4f) Tick(None, Now + Frame);
            }
        }

        // ---- Single ----

        [Test]
        public void SingleFiresAfterTheBoltDelayWithTheSpellDamage()
        {
            var rig = new Rig(Single(damage: 22));
            rig.Tick(Press, 1f);
            rig.Tick(None, 1.19f);
            Assert.AreEqual(0, rig.Shots.Count);
            rig.Tick(None, 1.2f);
            Assert.AreEqual(1, rig.Shots.Count);
            Assert.AreEqual(22, rig.Shots[0].Shot.Damage);
        }

        // Regression: with no ShotFired listener the pending shot must still leave the queue.
        [Test]
        public void AShotWithNoListenerStillLeavesTheQueueAndTheAttackEnds()
        {
            var machine = new ActionMachine(new ActionSettings(), new SpellCaster(Single()));
            machine.Tick(Press, 1f);
            machine.Tick(None, 1.2f);
            machine.Tick(None, 1.51f);
            Assert.AreEqual(ActionStateId.Ready, machine.Current);
        }

        [Test]
        public void SingleAttackLastsBoltDelayPlusAimHold()
        {
            var rig = new Rig(Single());
            rig.Tick(Press, 1f);
            rig.Tick(None, 1.49f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
            rig.Tick(None, 1.51f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
        }

        [Test]
        public void SingleCooldownComesFromTheSpellNotTheSettings()
        {
            var rig = new Rig(Single(cooldown: 1f));
            rig.Tick(Press, 1f);
            rig.Tick(None, 1.51f);
            rig.Tick(Press, 1.9f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            rig.Tick(Press, 2f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
        }

        // ---- Burst ----

        [Test]
        public void BurstFirstShotAfterTheDelayThenAtTheBurstInterval()
        {
            var rig = new Rig(Burst());
            rig.Tick(Press, 1f);
            rig.RunTo(1.18f);
            Assert.AreEqual(0, rig.Shots.Count);
            rig.RunTo(1.22f);
            Assert.AreEqual(1, rig.Shots.Count);
            rig.RunTo(1.4f);
            Assert.AreEqual(3, rig.Shots.Count);
            Assert.AreEqual(1.2f, rig.Shots[0].At, 0.02f);
            Assert.AreEqual(0.08f, rig.Shots[1].At - rig.Shots[0].At, 0.02f);
            Assert.AreEqual(0.08f, rig.Shots[2].At - rig.Shots[1].At, 0.02f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[]
            {
                rig.Shots[0].Shot.Index, rig.Shots[1].Shot.Index, rig.Shots[2].Shot.Index,
            });
            Assert.AreEqual(-3f, rig.Shots[0].Shot.SpreadDegrees, 1e-3f);
            Assert.AreEqual(3f, rig.Shots[2].Shot.SpreadDegrees, 1e-3f);
        }

        [Test]
        public void BurstStaysInAttackUntilTheLastShotPlusTheAimHold()
        {
            var rig = new Rig(Burst());
            rig.Tick(Press, 1f);
            rig.RunTo(1.6f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
            rig.RunTo(1.7f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            Assert.AreEqual(3, rig.Shots.Count);
        }

        [Test]
        public void BurstCooldownRunsFromTheLastScheduledShot()
        {
            var rig = new Rig(Burst());
            rig.Tick(Press, 1f);
            rig.RunTo(1.8f);
            rig.Tick(Press, 1.81f);
            Assert.AreEqual(ActionStateId.Ready, rig.State, "cooldown 0.7 from the last shot at 1.16 ends at 1.86");
            rig.Tick(Press, 1.9f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
        }

        [Test]
        public void ParryDuringABurstDropsTheShotsNotYetFired()
        {
            var rig = new Rig(Burst());
            rig.Tick(Press, 1f);
            rig.RunTo(1.22f);
            Assert.AreEqual(1, rig.Shots.Count);
            rig.Tick(Parry, 1.23f);
            Assert.AreEqual(ActionStateId.Parry, rig.State);
            rig.RunTo(2f);
            Assert.AreEqual(1, rig.Shots.Count);
        }

        // ---- Charged ----

        [Test]
        public void ChargedPressChargesFacingAimWithoutFiring()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.RunTo(3f);
            Assert.AreEqual(ActionStateId.Charging, rig.State);
            Assert.IsTrue(rig.Machine.FacesAim);
            Assert.IsTrue(rig.Machine.IsActing);
            Assert.AreEqual(0, rig.Shots.Count);
        }

        [Test]
        public void ChargedReleaseFiresAtOnceWithTheChargedDamage()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Release, 1.6f);
            Assert.AreEqual(1, rig.Shots.Count, "no extra wind-up after the release");
            Assert.AreEqual(32, rig.Shots[0].Shot.Damage, "0.6 s of 1.2 s: 18 + 27 * 0.5 rounds to 32");
            Assert.AreEqual(1.6f, rig.Shots[0].At, 1e-4f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
        }

        [Test]
        public void ChargedFullChargeDoesFullDamage()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Release, 3f);
            Assert.AreEqual(45, rig.Shots[0].Shot.Damage);
        }

        [Test]
        public void ChargedHoldsTheAimThenReturnsToReady()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Release, 1.6f);
            rig.Tick(None, 1.89f);
            Assert.AreEqual(ActionStateId.Attack, rig.State);
            rig.Tick(None, 1.91f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
        }

        [Test]
        public void ChargedCooldownRunsFromTheRelease()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Release, 1.6f);
            rig.Tick(None, 1.95f);
            rig.Tick(Press, 2.1f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            rig.Tick(Press, 2.2f);
            Assert.AreEqual(ActionStateId.Charging, rig.State);
        }

        [Test]
        public void ReleaseWithoutAChargeDoesNothing()
        {
            var rig = new Rig(Charged());
            rig.Tick(Release, 1f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            Assert.AreEqual(0, rig.Shots.Count);
        }

        [Test]
        public void ChargedPressRefusedDuringTheCooldownIgnoresItsRelease()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Release, 1.1f);
            rig.Tick(None, 1.5f);
            rig.Tick(Press, 1.6f);
            rig.Tick(Release, 1.65f);
            Assert.AreEqual(1, rig.Shots.Count);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
        }

        [Test]
        public void ParryWhileChargingLosesTheChargeAndParries()
        {
            var rig = new Rig(Charged());
            rig.Tick(Press, 1f);
            rig.Tick(Parry, 1.5f);
            Assert.AreEqual(ActionStateId.Parry, rig.State);
            rig.Tick(Release, 1.6f);
            rig.RunTo(2.5f);
            Assert.AreEqual(0, rig.Shots.Count);
            rig.Tick(Press, 2.6f);
            Assert.AreEqual(ActionStateId.Charging, rig.State, "a lost charge costs no cooldown");
        }

        // ---- Channel ----

        [Test]
        public void ChannelPressStartsChannellingFacingAim()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            Assert.AreEqual(ActionStateId.Channelling, rig.State);
            Assert.IsTrue(rig.Machine.FacesAim);
            Assert.AreEqual(1, rig.ChannelStarts);
            Assert.AreEqual(0, rig.Shots.Count);
        }

        [Test]
        public void ChannelTicksAtTheSpellRate()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.RunTo(1.5f);
            Assert.AreEqual(5, rig.ChannelTicks.Count);
            Assert.AreEqual(4, rig.ChannelTicks[0]);
        }

        [Test]
        public void ChannelReleaseEndsItEarly()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.RunTo(1.55f);
            rig.Tick(Release, 1.55f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            Assert.AreEqual(1, rig.ChannelEnds.Count);
            Assert.AreEqual(0.55f, rig.ChannelEnds[0], 0.02f);
        }

        [Test]
        public void ChannelEndsByItselfAtTheDuration()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.RunTo(2.45f);
            Assert.AreEqual(ActionStateId.Channelling, rig.State);
            rig.RunTo(2.6f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            Assert.AreEqual(15, rig.ChannelTicks.Count);
            Assert.AreEqual(1.5f, rig.ChannelEnds[0], 0.02f);
        }

        [Test]
        public void ChannelCooldownScalesWithTheChannelledTime()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.RunTo(2.6f);
            rig.Tick(Press, 3.3f);
            Assert.AreEqual(ActionStateId.Ready, rig.State, "0.5 + 1.5 * 0.6 = 1.4 s from 2.5");
            rig.Tick(Press, 3.95f);
            Assert.AreEqual(ActionStateId.Channelling, rig.State);
        }

        [Test]
        public void ParryWhileChannellingEndsTheChannelAndParries()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.RunTo(1.3f);
            rig.Tick(Parry, 1.3f);
            Assert.AreEqual(ActionStateId.Parry, rig.State);
            Assert.AreEqual(1, rig.ChannelEnds.Count);
            rig.RunTo(2f);
            Assert.AreEqual(1, rig.ChannelEnds.Count);
            Assert.AreEqual(3, rig.ChannelTicks.Count);
        }

        [Test]
        public void ParryWithoutChargesWhileChannellingEntersParryEmpty()
        {
            var rig = new Rig(Channel(), 0f);
            rig.Tick(Parry, 0.1f);
            rig.Tick(Parry, 0.6f);
            rig.Tick(Parry, 1.1f);
            rig.RunTo(1.6f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            rig.Tick(Press, 1.7f);
            Assert.AreEqual(ActionStateId.Channelling, rig.State);
            rig.Tick(Parry, 1.8f);
            Assert.AreEqual(ActionStateId.ParryEmpty, rig.State);
            Assert.AreEqual(1, rig.ChannelEnds.Count);
        }

        // ---- Spell swap ----

        [Test]
        public void SetCasterCancelsAnActiveChannelAndUsesTheNewSpell()
        {
            var rig = new Rig(Channel());
            rig.Tick(Press, 1f);
            rig.Machine.SetCaster(new SpellCaster(Single(damage: 31)), 1.2f);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
            Assert.AreEqual(1, rig.ChannelEnds.Count);
            rig.Tick(Press, 1.3f);
            rig.RunTo(1.6f);
            Assert.AreEqual(31, rig.Shots[0].Shot.Damage);
        }

        [Test]
        public void SetCasterWhileIdleKeepsTheStateAndRaisesNothing()
        {
            var rig = new Rig(Single());
            var changes = 0;
            rig.Machine.StateChanged += (f, t) => changes++;
            rig.Machine.SetCaster(new SpellCaster(Burst()), 1f);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(ActionStateId.Ready, rig.State);
        }

        [Test]
        public void ADefaultMachineCastsTheBaseSpell()
        {
            var machine = new ActionMachine(new ActionSettings());
            var damage = -1;
            machine.ShotFired += s => damage = s.Damage;
            machine.Tick(Press, 1f);
            machine.Tick(None, 1.2f);
            Assert.AreEqual(18, damage);
        }
    }
}

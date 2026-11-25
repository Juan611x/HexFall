using System.Collections.Generic;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SpellCasterTests
    {
        private const float Eps = 1e-4f;

        private sealed class Recorder
        {
            public readonly List<ProjectileShot> Shots = new List<ProjectileShot>();
            public readonly List<int> ChannelTicks = new List<int>();
            public int ChannelStarts;
            public readonly List<float> ChannelEnds = new List<float>();

            public Recorder(SpellCaster caster)
            {
                caster.ProjectileFired += Shots.Add;
                caster.ChannelStarted += () => ChannelStarts++;
                caster.ChannelTicked += ChannelTicks.Add;
                caster.ChannelEnded += ChannelEnds.Add;
            }
        }

        private static CastConfig Single(int damage = 18, float cooldown = 0.5f) => new CastConfig
        {
            Pattern = CastPattern.Single, Damage = damage, CooldownSeconds = cooldown,
        };

        private static CastConfig Charged() => new CastConfig
        {
            Pattern = CastPattern.Charged, CooldownSeconds = 0.6f,
            MaxChargeSeconds = 1.2f, MinDamage = 18, MaxDamage = 45,
        };

        private static CastConfig Burst(int count = 3) => new CastConfig
        {
            Pattern = CastPattern.Burst, Damage = 8, CooldownSeconds = 0.7f,
            BurstCount = count, BurstIntervalSeconds = 0.08f, BurstSpreadDegrees = 3f,
        };

        private static CastConfig Channel() => new CastConfig
        {
            Pattern = CastPattern.Channel, CooldownSeconds = 0.5f,
            ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f,
            ChannelDamagePerTick = 4, ChannelCooldownPerSecond = 0.6f,
        };

        // ---- Single ----

        [Test]
        public void SinglePressFiresOneProjectileWithDamage()
        {
            var caster = new SpellCaster(Single());
            var rec = new Recorder(caster);

            Assert.IsTrue(caster.Press(1f));

            Assert.AreEqual(1, rec.Shots.Count);
            Assert.AreEqual(18, rec.Shots[0].Damage);
            Assert.AreEqual(0, rec.Shots[0].Index);
            Assert.AreEqual(0f, rec.Shots[0].SpreadDegrees, Eps);
            Assert.IsFalse(caster.IsBusy);
        }

        [Test]
        public void SingleCooldownBlocksUntilElapsed()
        {
            var caster = new SpellCaster(Single());
            var rec = new Recorder(caster);
            caster.Press(1f);

            Assert.IsFalse(caster.IsReady(1.49f));
            Assert.IsFalse(caster.Press(1.49f));
            Assert.IsTrue(caster.IsReady(1.5f));
            Assert.IsTrue(caster.Press(1.5f));
            Assert.AreEqual(2, rec.Shots.Count);
        }

        [Test]
        public void FreshCasterIsReady()
        {
            Assert.IsTrue(new SpellCaster(Single()).IsReady(0f));
        }

        // ---- Charged ----

        [Test]
        public void ChargedPressAloneFiresNothingAndIsBusy()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);

            Assert.IsTrue(caster.Press(1f));

            Assert.AreEqual(0, rec.Shots.Count);
            Assert.IsTrue(caster.IsBusy);
        }

        [Test]
        public void ChargedQuickTapFiresMinDamage()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Release(1f);

            Assert.AreEqual(1, rec.Shots.Count);
            Assert.AreEqual(18, rec.Shots[0].Damage);
            Assert.AreEqual(0f, rec.Shots[0].Charge, Eps);
        }

        [Test]
        public void ChargedHalfHeldInterpolatesDamage()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Release(1.6f);

            Assert.AreEqual(0.5f, rec.Shots[0].Charge, Eps);
            Assert.AreEqual(32, rec.Shots[0].Damage); // lerp(18,45,.5)=31.5 rounds away from zero
        }

        [Test]
        public void ChargedHoldingPastMaxStaysAtMax()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Release(9f);

            Assert.AreEqual(1f, rec.Shots[0].Charge, Eps);
            Assert.AreEqual(45, rec.Shots[0].Damage);
        }

        [Test]
        public void ChargedCooldownStartsAtRelease()
        {
            var caster = new SpellCaster(Charged());
            caster.Press(1f);
            caster.Release(2f);

            Assert.IsFalse(caster.IsReady(2.59f));
            Assert.IsTrue(caster.IsReady(2.6f));
            Assert.IsFalse(caster.IsBusy);
        }

        [Test]
        public void ChargedCancelFiresNothingAndHasNoCooldown()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Cancel(1.5f);

            Assert.AreEqual(0, rec.Shots.Count);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsTrue(caster.IsReady(1.5f));
        }

        [Test]
        public void ChargedReleaseWithoutPressIsIgnored()
        {
            var caster = new SpellCaster(Charged());
            var rec = new Recorder(caster);
            caster.Release(1f);

            Assert.AreEqual(0, rec.Shots.Count);
        }

        // ---- Burst ----

        [Test]
        public void BurstFiresFirstShotImmediately()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);

            Assert.AreEqual(1, rec.Shots.Count);
            Assert.AreEqual(8, rec.Shots[0].Damage);
            Assert.IsTrue(caster.IsBusy);
        }

        [Test]
        public void BurstRemainingShotsFollowTheInterval()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Tick(1.07f);
            Assert.AreEqual(1, rec.Shots.Count);
            caster.Tick(1.08f);
            Assert.AreEqual(2, rec.Shots.Count);
            caster.Tick(1.16f);
            Assert.AreEqual(3, rec.Shots.Count);
            Assert.IsFalse(caster.IsBusy);
        }

        [Test]
        public void BurstBigDeltaCatchesUpWithoutExceedingCount()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Tick(5f);
            caster.Tick(6f);

            Assert.AreEqual(3, rec.Shots.Count);
            Assert.AreEqual(0, rec.Shots[0].Index);
            Assert.AreEqual(1, rec.Shots[1].Index);
            Assert.AreEqual(2, rec.Shots[2].Index);
        }

        [Test]
        public void BurstSpreadIsSymmetricAndDeterministic()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Tick(2f);

            Assert.AreEqual(-3f, rec.Shots[0].SpreadDegrees, Eps);
            Assert.AreEqual(0f, rec.Shots[1].SpreadDegrees, Eps);
            Assert.AreEqual(3f, rec.Shots[2].SpreadDegrees, Eps);
        }

        [Test]
        public void BurstSingleShotHasNoSpreadAndFinishesAtOnce()
        {
            var caster = new SpellCaster(Burst(1));
            var rec = new Recorder(caster);
            caster.Press(1f);

            Assert.AreEqual(0f, rec.Shots[0].SpreadDegrees, Eps);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsFalse(caster.IsReady(1.69f));
            Assert.IsTrue(caster.IsReady(1.7f));
        }

        [Test]
        public void BurstCooldownStartsAfterLastShot()
        {
            var caster = new SpellCaster(Burst());
            caster.Press(1f);
            caster.Tick(1.16f); // last shot at 1.16, ready at 1.86

            Assert.IsFalse(caster.IsReady(1.85f));
            Assert.IsTrue(caster.IsReady(1.87f));
        }

        [Test]
        public void BurstPressWhileBusyIsIgnored()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);

            Assert.IsFalse(caster.Press(1.05f));
            Assert.AreEqual(1, rec.Shots.Count);
        }

        [Test]
        public void BurstCancelDropsRemainingShotsAndAppliesCooldown()
        {
            var caster = new SpellCaster(Burst());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Cancel(1.05f);
            caster.Tick(2f);

            Assert.AreEqual(1, rec.Shots.Count);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsFalse(caster.IsReady(1.74f));
            Assert.IsTrue(caster.IsReady(1.76f));
        }

        // ---- Channel ----

        [Test]
        public void ChannelPressStartsWithoutInstantDamage()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);

            Assert.AreEqual(1, rec.ChannelStarts);
            Assert.AreEqual(0, rec.ChannelTicks.Count);
            Assert.IsTrue(caster.IsBusy);
        }

        [Test]
        public void ChannelTicksAtRateWhileHeld()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Tick(1.099f);
            Assert.AreEqual(0, rec.ChannelTicks.Count);
            caster.Tick(1.1f);
            Assert.AreEqual(1, rec.ChannelTicks.Count);
            caster.Tick(1.5f);
            Assert.AreEqual(5, rec.ChannelTicks.Count);
            Assert.AreEqual(4, rec.ChannelTicks[0]);
        }

        [Test]
        public void ChannelBigDeltaCatchesUpTicks()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Tick(1.55f);

            Assert.AreEqual(5, rec.ChannelTicks.Count);
        }

        [Test]
        public void ChannelEndsAtMaxDurationWithAllTicks()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Tick(10f);

            Assert.AreEqual(15, rec.ChannelTicks.Count);
            Assert.AreEqual(1, rec.ChannelEnds.Count);
            Assert.AreEqual(1.5f, rec.ChannelEnds[0], Eps);
            Assert.IsFalse(caster.IsBusy);
        }

        [Test]
        public void ChannelFullDurationCooldownIsOnePointFourSeconds()
        {
            var caster = new SpellCaster(Channel());
            caster.Press(1f);
            caster.Tick(2.5f); // channel ends at 2.5; ready at 3.9

            Assert.IsFalse(caster.IsReady(3.89f));
            Assert.IsTrue(caster.IsReady(3.91f));
        }

        [Test]
        public void ChannelReleaseEndsAndCooldownIsProportional()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Release(1.5f); // 0.5 s channelled: 0.5 + 0.3 = 0.8

            Assert.AreEqual(5, rec.ChannelTicks.Count);
            Assert.AreEqual(0.5f, rec.ChannelEnds[0], Eps);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsFalse(caster.IsReady(2.29f));
            Assert.IsTrue(caster.IsReady(2.31f));
        }

        [Test]
        public void ChannelCancelEndsNowWithoutPendingTicks()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Tick(1.25f);
            caster.Cancel(1.38f);

            Assert.AreEqual(2, rec.ChannelTicks.Count);
            Assert.AreEqual(0.38f, rec.ChannelEnds[0], Eps);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsFalse(caster.IsReady(1.38f + 0.5f + 0.38f * 0.6f - 0.01f));
            Assert.IsTrue(caster.IsReady(1.38f + 0.5f + 0.38f * 0.6f + 0.01f));
        }

        [Test]
        public void ChannelReleaseWithoutPressIsIgnored()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Release(1f);

            Assert.AreEqual(0, rec.ChannelEnds.Count);
        }

        // ---- Defensive ----

        [Test]
        public void InvalidTimesAreIgnored()
        {
            var caster = new SpellCaster(Single());
            var rec = new Recorder(caster);

            Assert.IsFalse(caster.Press(float.NaN));
            Assert.IsFalse(caster.Press(float.PositiveInfinity));
            Assert.IsFalse(caster.Press(-1f));
            caster.Tick(float.NaN);
            caster.Release(float.NegativeInfinity);
            caster.Cancel(-3f);

            Assert.AreEqual(0, rec.Shots.Count);
            Assert.IsTrue(caster.Press(0f));
        }

        [Test]
        public void InvalidTimeDoesNotEndAnActiveChannel()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.Release(float.NaN);
            caster.Tick(float.PositiveInfinity);

            Assert.IsTrue(caster.IsBusy);
            Assert.AreEqual(0, rec.ChannelEnds.Count);
        }

        [Test]
        public void ConfigWithBadValuesIsSanitised()
        {
            var caster = new SpellCaster(new CastConfig
            {
                Pattern = CastPattern.Burst, Damage = 5, CooldownSeconds = -1f,
                BurstCount = 0, BurstIntervalSeconds = -1f, BurstSpreadDegrees = 2f,
            });
            var rec = new Recorder(caster);

            Assert.IsTrue(caster.Press(1f));
            Assert.AreEqual(1, rec.Shots.Count);
            Assert.IsFalse(caster.IsBusy);
            Assert.IsTrue(caster.IsReady(1f));
        }

        // ---- Advisories: state advances before the event, bad floats are clamped ----

        private static void Throwing(SpellCaster caster) =>
            caster.ProjectileFired += _ => throw new System.InvalidOperationException("subscriber");

        [Test]
        public void SingleCooldownStartsEvenWhenASubscriberThrows()
        {
            var caster = new SpellCaster(Single(cooldown: 0.5f));
            Throwing(caster);

            Assert.Throws<System.InvalidOperationException>(() => caster.Press(1f));

            Assert.IsFalse(caster.IsReady(1.2f));
            Assert.IsTrue(caster.IsReady(1.5f));
        }

        [Test]
        public void BurstDoesNotRefireTheSameShotWhenASubscriberThrows()
        {
            var caster = new SpellCaster(Burst());
            var calls = 0;
            caster.ProjectileFired += _ =>
            {
                calls++;
                if (calls == 1) throw new System.InvalidOperationException("subscriber");
            };

            Assert.Throws<System.InvalidOperationException>(() => caster.Press(1f));
            caster.Tick(1.2f);

            Assert.AreEqual(3, calls, "shots 0, 1 and 2 once each");
            Assert.IsFalse(caster.IsBusy);
        }

        [Test]
        public void ChargedCooldownStartsEvenWhenASubscriberThrows()
        {
            var caster = new SpellCaster(Charged());
            caster.Press(1f);
            Throwing(caster);

            Assert.Throws<System.InvalidOperationException>(() => caster.Release(1.6f));

            Assert.IsFalse(caster.IsBusy);
            Assert.IsFalse(caster.IsReady(1.9f));
            Assert.IsTrue(caster.IsReady(2.2f));
        }

        [Test]
        public void ChannelCooldownStartsEvenWhenTheEndSubscriberThrows()
        {
            var caster = new SpellCaster(Channel());
            caster.ChannelEnded += _ => throw new System.InvalidOperationException("subscriber");
            caster.Press(1f);

            Assert.Throws<System.InvalidOperationException>(() => caster.Release(1.5f));

            Assert.IsFalse(caster.IsReady(1.6f));
        }

        [Test]
        public void SanitizedClampsNonFiniteValuesToSafeOnes()
        {
            var inf = float.PositiveInfinity;
            var c = new CastConfig
            {
                Pattern = CastPattern.Channel,
                CooldownSeconds = inf, MaxChargeSeconds = float.NaN,
                BurstIntervalSeconds = inf, BurstSpreadDegrees = float.NegativeInfinity,
                ChannelDurationSeconds = inf, ChannelTicksPerSecond = inf,
                ChannelCooldownPerSecond = float.NaN,
            }.Sanitized();

            foreach (var v in new[]
            {
                c.CooldownSeconds, c.MaxChargeSeconds, c.BurstIntervalSeconds, c.BurstSpreadDegrees,
                c.ChannelDurationSeconds, c.ChannelTicksPerSecond, c.ChannelCooldownPerSecond,
            })
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                Assert.GreaterOrEqual(v, 0f);
            }
            Assert.Greater(c.ChannelTicksPerSecond, 0f);
        }

        [Test]
        public void ANonFiniteCooldownDoesNotLockTheSpell()
        {
            var caster = new SpellCaster(Single(cooldown: float.PositiveInfinity));

            Assert.IsTrue(caster.Press(1f));
            Assert.IsTrue(caster.IsReady(1.1f));
        }

        [Test]
        public void CasterExposesItsPattern()
        {
            Assert.AreEqual(CastPattern.Burst, new SpellCaster(Burst()).Pattern);
        }
    }
}

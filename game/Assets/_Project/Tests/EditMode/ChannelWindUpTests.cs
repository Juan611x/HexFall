using System.Collections.Generic;
using HexFall.Player.Actions;
using HexFall.Player.Spells;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // A channel with a wind-up: the press only starts the pose; the channel (beam, duration, ticks) starts when
    // the adapter reports the pose reached, or when the fallback timeout runs out. Pure domain, no animator.
    public class ChannelWindUpTests
    {
        private const float Eps = 1e-4f;

        private static CastConfig Channel(float windUp = 0.6f) => new CastConfig
        {
            Pattern = CastPattern.Channel, CooldownSeconds = 0.5f,
            ChannelDurationSeconds = 1.5f, ChannelTicksPerSecond = 10f,
            ChannelDamagePerTick = 4, ChannelCooldownPerSecond = 0.6f,
            ChannelWindUpTimeoutSeconds = windUp,
        };

        private sealed class Recorder
        {
            public int Starts;
            public readonly List<int> Ticks = new List<int>();
            public readonly List<float> Ends = new List<float>();

            public Recorder(SpellCaster caster)
            {
                caster.ChannelStarted += () => Starts++;
                caster.ChannelTicked += Ticks.Add;
                caster.ChannelEnded += Ends.Add;
            }
        }

        // ---- caster ----

        [Test]
        public void PressOnlyStartsTheWindUp()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);

            Assert.IsTrue(caster.Press(1f));
            Assert.IsTrue(caster.IsWindingUp);
            Assert.IsTrue(caster.IsBusy);
            Assert.AreEqual(0, rec.Starts, "no beam yet");
            caster.Tick(1.3f);
            Assert.AreEqual(0, rec.Ticks.Count, "no ticks while winding up");
        }

        [Test]
        public void ThePoseReachedStartsTheChannelFromThatMoment()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.PoseReached(1.2f);
            Assert.IsFalse(caster.IsWindingUp);
            Assert.AreEqual(1, rec.Starts);
            caster.Tick(1.5f);
            Assert.AreEqual(3, rec.Ticks.Count, "ticks count from the pose, not from the press");
            caster.Tick(2.7f);
            Assert.AreEqual(1, rec.Ends.Count, "the duration runs from the pose");
            Assert.AreEqual(1.5f, rec.Ends[0], Eps);
            Assert.AreEqual(15, rec.Ticks.Count);
        }

        [Test]
        public void TheFallbackTimeoutStartsTheChannelWhenThePoseNeverComes()
        {
            var caster = new SpellCaster(Channel(0.6f));
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Tick(1.5f);
            Assert.AreEqual(0, rec.Starts, "still within the timeout");
            caster.Tick(1.6f);
            Assert.AreEqual(1, rec.Starts, "timeout reached");
            Assert.IsFalse(caster.IsWindingUp);
        }

        [Test]
        public void ReleasingBeforeThePoseCastsNothingAndCostsNoCooldown()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Release(1.2f);
            Assert.AreEqual(0, rec.Starts);
            Assert.AreEqual(0, rec.Ends.Count, "nothing started, so nothing ended");
            Assert.IsFalse(caster.IsBusy);
            Assert.IsTrue(caster.IsReady(1.2f), "no cooldown");
        }

        [Test]
        public void CancellingBeforeThePoseCastsNothingAndCostsNoCooldown()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);

            caster.Cancel(1.2f);
            Assert.AreEqual(0, rec.Starts);
            Assert.AreEqual(0, rec.Ends.Count);
            Assert.IsTrue(caster.IsReady(1.2f));
        }

        [Test]
        public void TheCooldownCountsOnlyTheRealChannelTime()
        {
            var caster = new SpellCaster(Channel());
            caster.Press(1f);
            caster.PoseReached(1.4f);

            caster.Release(1.9f); // channelled 0.5 s
            Assert.AreEqual(1.9f + 0.5f + 0.5f * 0.6f, caster.ReadyAt, Eps);
        }

        [Test]
        public void ThePoseReportedTwiceOrOutsideAWindUpIsIgnored()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.PoseReached(0.5f); // nothing pressed
            Assert.AreEqual(0, rec.Starts);

            caster.Press(1f);
            caster.PoseReached(1.1f);
            caster.PoseReached(1.2f);
            Assert.AreEqual(1, rec.Starts);
        }

        [Test]
        public void ANonFiniteTimeIsIgnored()
        {
            var caster = new SpellCaster(Channel());
            var rec = new Recorder(caster);
            caster.Press(1f);
            caster.PoseReached(float.NaN);
            caster.PoseReached(float.PositiveInfinity);
            Assert.IsTrue(caster.IsWindingUp);
            Assert.AreEqual(0, rec.Starts);
        }

        [Test]
        public void WithoutAWindUpTheChannelStartsOnPress()
        {
            var caster = new SpellCaster(Channel(0f));
            var rec = new Recorder(caster);
            caster.Press(1f);
            Assert.IsFalse(caster.IsWindingUp);
            Assert.AreEqual(1, rec.Starts);
        }

        [Test]
        public void ABadTimeoutCountsAsNoWindUp()
        {
            var caster = new SpellCaster(Channel(float.NaN));
            var rec = new Recorder(caster);
            caster.Press(1f);
            Assert.AreEqual(1, rec.Starts);
        }

        [Test]
        public void OtherPatternsNeverWindUp()
        {
            var caster = new SpellCaster(new CastConfig
            {
                Pattern = CastPattern.Single, Damage = 18, CooldownSeconds = 0.5f, ChannelWindUpTimeoutSeconds = 0.6f,
            });
            caster.Press(1f);
            Assert.IsFalse(caster.IsWindingUp);
        }

        // ---- machine ----

        private sealed class Rig
        {
            public readonly ActionMachine Machine;
            public int Starts;
            public readonly List<int> Ticks = new List<int>();
            public readonly List<float> Ends = new List<float>();
            public float Now = 1f;

            public Rig(CastConfig config)
            {
                Machine = new ActionMachine(new ActionSettings(), new SpellCaster(config));
                Machine.ChannelStarted += () => Starts++;
                Machine.ChannelTicked += Ticks.Add;
                Machine.ChannelEnded += Ends.Add;
            }
        }

        [Test]
        public void ThePressEntersChannellingWindingUpWithoutStartingTheBeam()
        {
            var rig = new Rig(Channel());
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now);

            Assert.AreEqual(ActionStateId.Channelling, rig.Machine.Current);
            Assert.IsTrue(rig.Machine.IsWindingUp);
            Assert.AreEqual(0, rig.Starts);
        }

        [Test]
        public void ThePoseReachedStartsTheChannelOnTheMachine()
        {
            var rig = new Rig(Channel());
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now);

            rig.Machine.PoseReached(rig.Now + 0.2f);
            Assert.IsFalse(rig.Machine.IsWindingUp);
            Assert.AreEqual(1, rig.Starts);
            rig.Machine.Tick(default, rig.Now + 0.5f);
            Assert.AreEqual(3, rig.Ticks.Count);
        }

        [Test]
        public void ThePoseIsIgnoredWhenNotWindingUpAChannel()
        {
            var rig = new Rig(Channel());
            rig.Machine.PoseReached(rig.Now); // ready: nothing to do
            Assert.AreEqual(0, rig.Starts);
        }

        [Test]
        public void ReleasingBeforeThePoseReturnsToReadyWithNothingCast()
        {
            var rig = new Rig(Channel());
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now);
            rig.Machine.Tick(new ActionIntent(false, false, true), rig.Now + 0.1f);

            Assert.AreEqual(ActionStateId.Ready, rig.Machine.Current);
            Assert.AreEqual(0, rig.Starts);
            Assert.AreEqual(0, rig.Ends.Count);
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now + 0.12f);
            Assert.AreEqual(ActionStateId.Channelling, rig.Machine.Current, "no cooldown: it can cast again at once");
        }

        [Test]
        public void AParryBeforeThePoseCastsNothing()
        {
            var rig = new Rig(Channel());
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now);
            rig.Machine.Tick(new ActionIntent(false, true), rig.Now + 0.1f);

            Assert.AreEqual(ActionStateId.Parry, rig.Machine.Current);
            Assert.AreEqual(0, rig.Starts);
            Assert.AreEqual(0, rig.Ends.Count);
        }

        [Test]
        public void TheFallbackTimeoutStartsTheChannelThroughTheMachineTick()
        {
            var rig = new Rig(Channel(0.6f));
            rig.Machine.Tick(new ActionIntent(true, false), rig.Now);
            rig.Machine.Tick(default, rig.Now + 0.5f);
            Assert.AreEqual(0, rig.Starts);
            rig.Machine.Tick(default, rig.Now + 0.61f);
            Assert.AreEqual(1, rig.Starts);
            Assert.AreEqual(ActionStateId.Channelling, rig.Machine.Current);
        }
    }
}

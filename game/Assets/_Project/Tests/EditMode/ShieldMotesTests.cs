using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // Pure phase -> motes mapping of the air shield: drift in while it assembles, orbit lightly while held,
    // blow outward once it disassembles (and after it is gone, for the motes still alive).
    public class ShieldMotesTests
    {
        [Test]
        public void Assembling_motes_spawn_outside_the_surface_and_drift_inward()
        {
            var p = ShieldMotes.For(ShieldBuildPhase.Assembling);
            Assert.Greater(p.Rate, 0f);
            Assert.Less(p.Radial, 0f, "negative radial velocity = inward");
            Assert.Greater(p.SpawnRadius, ShieldMotes.SurfaceRadius);
        }

        [Test]
        public void Held_motes_orbit_on_the_surface_at_a_lower_rate_than_the_assembly()
        {
            var held = ShieldMotes.For(ShieldBuildPhase.Holding);
            Assert.Greater(held.Rate, 0f);
            Assert.Less(held.Rate, ShieldMotes.For(ShieldBuildPhase.Assembling).Rate);
            Assert.AreEqual(0f, held.Radial);
            Assert.AreEqual(ShieldMotes.SurfaceRadius, held.SpawnRadius, 0.05f);
            Assert.AreNotEqual(0f, held.OrbitalY);
        }

        [TestCase(ShieldBuildPhase.Disassembling)]
        [TestCase(ShieldBuildPhase.Done)]
        public void Disassembling_and_after_stop_emitting_and_blow_outward(ShieldBuildPhase phase)
        {
            var p = ShieldMotes.For(phase);
            Assert.AreEqual(0f, p.Rate);
            Assert.Greater(p.Radial, 0f, "positive radial velocity = outward");
        }

        [Test]
        public void Amounts_are_sparse_wisps_not_a_cloud()
        {
            Assert.AreEqual(50f, ShieldMotes.For(ShieldBuildPhase.Assembling).Rate);
            Assert.AreEqual(10f, ShieldMotes.For(ShieldBuildPhase.Holding).Rate);
            Assert.AreEqual(7, ShieldMotes.DisassemblePuff);
        }

        [Test]
        public void Not_started_emits_nothing()
        {
            var p = ShieldMotes.For(ShieldBuildPhase.NotStarted);
            Assert.AreEqual(0f, p.Rate);
            Assert.AreEqual(0f, p.Radial);
        }

        [TestCase(ShieldBuildPhase.Holding, ShieldBuildPhase.Disassembling, true)]
        [TestCase(ShieldBuildPhase.Assembling, ShieldBuildPhase.Disassembling, true)]
        [TestCase(ShieldBuildPhase.Holding, ShieldBuildPhase.Done, true)]
        [TestCase(ShieldBuildPhase.Disassembling, ShieldBuildPhase.Disassembling, false)]
        [TestCase(ShieldBuildPhase.Disassembling, ShieldBuildPhase.Done, false)]
        [TestCase(ShieldBuildPhase.Assembling, ShieldBuildPhase.Holding, false)]
        [TestCase(ShieldBuildPhase.NotStarted, ShieldBuildPhase.Assembling, false)]
        public void Puff_is_emitted_once_when_the_disassembly_starts(ShieldBuildPhase previous, ShieldBuildPhase current, bool puff) =>
            Assert.AreEqual(puff, ShieldMotes.PuffCount(previous, current) > 0);
    }
}

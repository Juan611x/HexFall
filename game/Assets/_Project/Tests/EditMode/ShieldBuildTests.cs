using HexFall.Player;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ShieldBuildTests
    {
        private const float Hold = 0.4f;
        private const float Eps = 1e-4f;

        private static ShieldBuild Make() => new ShieldBuild(Hold);

        [Test]
        public void NegativeElapsedIsNotStartedAndEmpty()
        {
            var build = Make();

            Assert.AreEqual(0f, build.Progress(-0.1f), Eps);
            Assert.AreEqual(ShieldBuildPhase.NotStarted, build.PhaseAt(-0.1f));
            Assert.IsFalse(build.IsDone(-0.1f));
        }

        [Test]
        public void StartsEmptyAndAssembling()
        {
            var build = Make();

            Assert.AreEqual(0f, build.Progress(0f), Eps);
            Assert.AreEqual(ShieldBuildPhase.Assembling, build.PhaseAt(0f));
        }

        [Test]
        public void AssemblyIsSmoothstepShaped()
        {
            var build = Make();

            Assert.AreEqual(0.5f, build.Progress(0.06f), Eps);
            Assert.Less(build.Progress(0.03f), 0.25f);
            Assert.Greater(build.Progress(0.09f), 0.75f);
        }

        [Test]
        public void IsFullyBuiltAfterAssembleAndHolds()
        {
            var build = Make();

            Assert.AreEqual(1f, build.Progress(0.12f), Eps);
            Assert.AreEqual(1f, build.Progress(0.3f), Eps);
            Assert.AreEqual(ShieldBuildPhase.Holding, build.PhaseAt(0.2f));
        }

        [Test]
        public void DisassemblesAfterTheHold()
        {
            var build = Make();

            Assert.AreEqual(ShieldBuildPhase.Disassembling, build.PhaseAt(Hold));
            Assert.AreEqual(1f, build.Progress(Hold), Eps);
            Assert.AreEqual(0.5f, build.Progress(Hold + 0.175f), Eps);
            Assert.AreEqual(0f, build.Progress(Hold + 0.35f), Eps);
        }

        [Test]
        public void IsDoneOnceDisassemblyEnds()
        {
            var build = Make();

            Assert.IsFalse(build.IsDone(Hold + 0.34f));
            Assert.IsTrue(build.IsDone(Hold + 0.35f));
            Assert.AreEqual(ShieldBuildPhase.Done, build.PhaseAt(Hold + 1f));
            Assert.AreEqual(0f, build.Progress(Hold + 1f), Eps);
        }

        [Test]
        public void ShortHoldDisassemblesFromTheProgressReached()
        {
            var build = new ShieldBuild(0.06f);
            float atHold = build.Progress(0.06f);

            Assert.AreEqual(0.5f, atHold, Eps);
            Assert.AreEqual(atHold, build.Progress(0.06f + 0.0001f), 1e-3f);
            Assert.AreEqual(ShieldBuildPhase.Assembling, build.PhaseAt(0.05f));
            Assert.AreEqual(ShieldBuildPhase.Disassembling, build.PhaseAt(0.06f));
            Assert.IsTrue(build.IsDone(0.06f + 0.35f));
        }

        [TestCase(float.NaN, 0.12f, 0.35f)]
        [TestCase(float.PositiveInfinity, 0.12f, 0.35f)]
        [TestCase(0.4f, float.NaN, 0.35f)]
        [TestCase(0.4f, 0.12f, float.NaN)]
        [TestCase(0.4f, float.PositiveInfinity, 0.35f)]
        [TestCase(0.4f, 0.12f, float.PositiveInfinity)]
        public void RejectsNonFiniteConstructorArguments(float hold, float assemble, float disassemble)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ShieldBuild(hold, assemble, disassemble));
        }

        [Test]
        public void NaNElapsedIsTreatedAsNotStarted()
        {
            var build = Make();

            Assert.AreEqual(ShieldBuildPhase.NotStarted, build.PhaseAt(float.NaN));
            Assert.AreEqual(0f, build.Progress(float.NaN), Eps);
            Assert.IsFalse(build.IsDone(float.NaN));
        }

        [Test]
        public void ProgressStaysInRange()
        {
            var build = Make();

            for (float t = -0.2f; t < 1.2f; t += 0.01f)
            {
                float p = build.Progress(t);
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
            }
        }
    }
}

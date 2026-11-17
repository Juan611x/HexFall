using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class SpellSoundWizardAnimatorTests
    {
        private sealed class Spy : IWizardAnimator, ISpellSfx
        {
            public int Casts, Sounds;
            public void Cast() => Casts++;
            public void PlayCast() => Sounds++;
        }

        [Test]
        public void CastTriggersAnimationAndSoundTogether()
        {
            var spy = new Spy();
            new SpellSoundWizardAnimator(spy, spy).Cast();
            Assert.AreEqual(1, spy.Casts);
            Assert.AreEqual(1, spy.Sounds);
        }

        [Test]
        public void EveryCastPlaysASound()
        {
            var spy = new Spy();
            var wizard = new SpellSoundWizardAnimator(spy, spy);
            wizard.Cast();
            wizard.Cast();
            wizard.Cast();
            Assert.AreEqual(3, spy.Casts);
            Assert.AreEqual(3, spy.Sounds);
        }
    }
}

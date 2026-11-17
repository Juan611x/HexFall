namespace HexFall.UI
{
    // Decorator: the sound shares the exact trigger path of the animation, so they cannot drift apart.
    public sealed class SpellSoundWizardAnimator : IWizardAnimator
    {
        private readonly IWizardAnimator _inner;
        private readonly ISpellSfx _sfx;

        public SpellSoundWizardAnimator(IWizardAnimator inner, ISpellSfx sfx)
        {
            _inner = inner;
            _sfx = sfx;
        }

        public void Cast()
        {
            _inner.Cast();
            _sfx.PlayCast();
        }
    }
}

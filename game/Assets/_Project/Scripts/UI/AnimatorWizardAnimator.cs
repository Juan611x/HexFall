using UnityEngine;

namespace HexFall.UI
{
    public sealed class AnimatorWizardAnimator : IWizardAnimator
    {
        private static readonly int CastTrigger = Animator.StringToHash("Cast");

        private readonly Animator _animator;

        public AnimatorWizardAnimator(Animator animator) => _animator = animator;

        public void Cast() => _animator.SetTrigger(CastTrigger);
    }
}

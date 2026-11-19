using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class MenuClickCastReaction
    {
        // Controls under an element with this class keep their click/hover sounds but never trigger a cast.
        public const string NoCastClass = "hf-no-cast";

        private readonly IWizardAnimator _wizard;

        public MenuClickCastReaction(IWizardAnimator wizard) => _wizard = wizard;

        public void OnClick(VisualElement target)
        {
            var onButton = false;
            for (var element = target; element != null; element = element.parent)
            {
                if (element.ClassListContains(NoCastClass))
                {
                    return;
                }

                onButton |= element is Button;
            }

            if (onButton)
            {
                _wizard.Cast();
            }
        }
    }
}

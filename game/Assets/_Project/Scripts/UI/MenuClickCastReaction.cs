using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class MenuClickCastReaction
    {
        private readonly IWizardAnimator _wizard;

        public MenuClickCastReaction(IWizardAnimator wizard) => _wizard = wizard;

        public void OnClick(VisualElement target)
        {
            for (var element = target; element != null; element = element.parent)
            {
                if (element is Button)
                {
                    _wizard.Cast();
                    return;
                }
            }
        }
    }
}

using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class MenuSoundReaction
    {
        public const string BackButtonName = "back-button";

        private readonly IMenuSfx _sfx;

        public MenuSoundReaction(IMenuSfx sfx) => _sfx = sfx;

        // PointerEnterEvent targets every element entered, so only the Button itself counts: its children
        // fire their own enter events while the pointer is already inside the button.
        public void OnPointerEnter(VisualElement target)
        {
            if (!(target is Button button) || !button.enabledInHierarchy)
            {
                return;
            }

            _sfx.PlayHover();
        }

        public void OnPress(VisualElement target)
        {
            var button = FindButton(target);
            if (button == null || !button.enabledInHierarchy)
            {
                return;
            }

            if (button.name == BackButtonName)
            {
                _sfx.PlayBack();
            }
            else
            {
                _sfx.PlayClick();
            }
        }

        private static Button FindButton(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element is Button button)
                {
                    return button;
                }
            }

            return null;
        }
    }
}

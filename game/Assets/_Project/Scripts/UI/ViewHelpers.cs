using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    internal static class ViewHelpers
    {
        public static void SetVisible(this VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public static void ShowText(this Label label, string text)
        {
            label.text = text;
            label.SetVisible(true);
        }

        public static void HideText(this Label label)
        {
            label.text = string.Empty;
            label.SetVisible(false);
        }

        public static void OnEnter(this TextField field, Action action)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopPropagation();
                action();
            });
        }
    }
}

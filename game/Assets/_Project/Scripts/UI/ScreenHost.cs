using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class ScreenHost : IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly IReadOnlyDictionary<ScreenId, VisualTreeAsset> _screens;
        private readonly VisualElement _content;
        private readonly Button _backButton;
        private ScreenId? _shown;

        public event Action<ScreenId, VisualElement> ScreenShown;
        public event Action<ScreenId> ScreenHidden;

        public ScreenHost(VisualElement layoutRoot, ScreenRouter router, IReadOnlyDictionary<ScreenId, VisualTreeAsset> screens)
        {
            _router = router;
            _screens = screens;
            _content = layoutRoot.Q<VisualElement>("screen-content");
            _backButton = layoutRoot.Q<Button>("back-button");
            _backButton.clicked += OnBackClicked;
            _router.ScreenChanged += OnScreenChanged;
        }

        public void ShowCurrent() => Show(_router.Current);

        public void Dispose()
        {
            _router.ScreenChanged -= OnScreenChanged;
            _backButton.clicked -= OnBackClicked;
        }

        private void OnBackClicked() => _router.Back();
        private void OnScreenChanged(ScreenId previous, ScreenId current) => Show(current);

        private void Show(ScreenId id)
        {
            if (_shown.HasValue) ScreenHidden?.Invoke(_shown.Value);
            _content.Clear();

            VisualElement screen;
            if (_screens.TryGetValue(id, out var asset) && asset != null)
            {
                screen = asset.Instantiate();
                screen.AddToClassList("hf-fill");
            }
            else
            {
                screen = new Label($"{id} — coming soon");
                screen.AddToClassList("hf-placeholder");
            }

            _content.Add(screen);
            _backButton.style.display = _router.CanGoBack ? DisplayStyle.Flex : DisplayStyle.None;
            _shown = id;
            ScreenShown?.Invoke(id, screen);
        }
    }
}

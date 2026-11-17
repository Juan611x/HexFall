using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuBootstrap : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset titleScreen;
        [SerializeField] private Animator wizardAnimator;

        private ScreenRouter _router;
        private ScreenHost _host;
        private IDisposable _presenter;
        private VisualElement _root;
        private MenuClickCastReaction _castReaction;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            var root = _root;
            _castReaction = new MenuClickCastReaction(new AnimatorWizardAnimator(wizardAnimator));
            // Pointer-down, not ClickEvent: a navigating button removes itself on pointer-up, so no click is raised for it.
            root.RegisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            // Keyboard/gamepad activation of a focused Button raises no ClickEvent.
            root.RegisterCallback<NavigationSubmitEvent>(OnPanelSubmit, TrickleDown.TrickleDown);
            _router = new ScreenRouter(ScreenId.Title);
            _host = new ScreenHost(root, _router, new Dictionary<ScreenId, VisualTreeAsset>
            {
                { ScreenId.Title, titleScreen },
            });
            _host.ScreenShown += OnScreenShown;
            _host.ScreenHidden += OnScreenHidden;
            _host.ShowCurrent();
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            _root?.UnregisterCallback<NavigationSubmitEvent>(OnPanelSubmit, TrickleDown.TrickleDown);
            _root = null;
            _presenter?.Dispose();
            _presenter = null;
            _host?.Dispose();
            _host = null;
        }

        private void OnPanelPointerDown(PointerDownEvent evt) => _castReaction.OnClick(evt.target as VisualElement);

        private void OnPanelSubmit(NavigationSubmitEvent evt) => _castReaction.OnClick(evt.target as VisualElement);

        private void OnScreenShown(ScreenId id, VisualElement screen)
        {
            if (id == ScreenId.Title)
                _presenter = new TitlePresenter(new TitleView(screen), _router);
        }

        private void OnScreenHidden(ScreenId id)
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}

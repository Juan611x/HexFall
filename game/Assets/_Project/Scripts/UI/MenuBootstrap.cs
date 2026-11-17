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
        [SerializeField] private VisualTreeAsset loginScreen;
        [SerializeField] private VisualTreeAsset registerScreen;
        [SerializeField] private VisualTreeAsset verifyScreen;
        [SerializeField] private VisualTreeAsset guestScreen;
        [SerializeField] private VisualTreeAsset lobbyScreen;
        [SerializeField] private Animator wizardAnimator;

        private ScreenRouter _router;
        private ScreenHost _host;
        private IDisposable _presenter;
        private VisualElement _root;
        private MenuClickCastReaction _castReaction;
        private IAuthService _auth;
        private SessionState _session;
        private GuestNameGenerator _guestNames;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            var root = _root;
            _castReaction = new MenuClickCastReaction(new AnimatorWizardAnimator(wizardAnimator));
            // Pointer-down, not ClickEvent: a navigating button removes itself on pointer-up, so no click is raised for it.
            root.RegisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            // Keyboard/gamepad activation of a focused Button raises no ClickEvent.
            root.RegisterCallback<NavigationSubmitEvent>(OnPanelSubmit, TrickleDown.TrickleDown);
            _auth = new FakeAuthService();
            _session = new SessionState();
            _guestNames = new GuestNameGenerator(new System.Random());
            _router = new ScreenRouter(ScreenId.Title);
            _host = new ScreenHost(root, _router, new Dictionary<ScreenId, VisualTreeAsset>
            {
                { ScreenId.Title, titleScreen },
                { ScreenId.Login, loginScreen },
                { ScreenId.Register, registerScreen },
                { ScreenId.Verify, verifyScreen },
                { ScreenId.Guest, guestScreen },
                { ScreenId.Lobby, lobbyScreen },
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

        private void Update() => (_presenter as ITickable)?.Tick(Time.unscaledDeltaTime);

        private void OnPanelPointerDown(PointerDownEvent evt) => _castReaction.OnClick(evt.target as VisualElement);

        private void OnPanelSubmit(NavigationSubmitEvent evt) => _castReaction.OnClick(evt.target as VisualElement);

        private void OnScreenShown(ScreenId id, VisualElement screen)
        {
            _presenter = id switch
            {
                ScreenId.Title => new TitlePresenter(new TitleView(screen), _router),
                ScreenId.Login => new LoginPresenter(new LoginView(screen), _router, _auth, _session),
                ScreenId.Register => new RegisterPresenter(new RegisterView(screen), _router, _auth, _session),
                ScreenId.Verify => new VerifyPresenter(new VerifyView(screen), _router, _auth, _session),
                ScreenId.Guest => new GuestPresenter(new GuestView(screen), _router, _auth, _session, _guestNames),
                ScreenId.Lobby => new LobbyPresenter(new LobbyView(screen), _router, _session),
                _ => null,
            };
        }

        private void OnScreenHidden(ScreenId id)
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}

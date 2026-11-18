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
        [SerializeField] private MenuAudioLibrary audioLibrary;
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioSource spellAudioSource;
        [SerializeField] private AudioSource themeAudioSource;
        [SerializeField] private AudioSource ambientAudioSource;

        private ScreenRouter _router;
        private ScreenHost _host;
        private IDisposable _presenter;
        private VisualElement _root;
        private MenuClickCastReaction _castReaction;
        private MenuSoundReaction _soundReaction;
        private MenuMusicPlayer _music;
        private IAuthService _auth;
        private SessionState _session;
        private GuestNameGenerator _guestNames;
        private SoundSettings _sound;
        private SoundSettingsSaver _soundSaver;
        private SoundMixerPresenter _mixer;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            var root = _root;
            var soundStore = new PlayerPrefsSoundSettingsStore();
            _sound = new SoundSettings(soundStore.Load());
            _soundSaver = new SoundSettingsSaver(_sound, soundStore);
            var canPlay = audioLibrary != null;
            IMenuSfx menuSfx = canPlay && uiAudioSource != null ? new UnityMenuSfx(uiAudioSource, audioLibrary, () => _sound.EffectsMultiplier) : new SilentSfx();
            ISpellSfx spellSfx = canPlay && spellAudioSource != null
                ? new UnitySpellSfx(spellAudioSource, audioLibrary, new SpellPicker(new System.Random().Next), () => _sound.EffectsMultiplier)
                : new SilentSfx();
            _soundReaction = new MenuSoundReaction(menuSfx);
            _castReaction = new MenuClickCastReaction(new SpellSoundWizardAnimator(new AnimatorWizardAnimator(wizardAnimator), spellSfx));
            _music = CreateMusic();
            _music?.Begin();
            // Pointer-down, not ClickEvent: a navigating button removes itself on pointer-up, so no click is raised for it.
            root.RegisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            // Keyboard/gamepad activation of a focused Button raises no ClickEvent.
            root.RegisterCallback<NavigationSubmitEvent>(OnPanelSubmit, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerEnterEvent>(OnPanelPointerEnter, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnPanelKeyDown, TrickleDown.TrickleDown);
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
            _mixer = new SoundMixerPresenter(new SoundMixerView(root.Q<VisualElement>("sound-mixer")), _sound);
        }

        private void OnDisable()
        {
            _root?.UnregisterCallback<PointerDownEvent>(OnPanelPointerDown, TrickleDown.TrickleDown);
            _root?.UnregisterCallback<NavigationSubmitEvent>(OnPanelSubmit, TrickleDown.TrickleDown);
            _root?.UnregisterCallback<PointerEnterEvent>(OnPanelPointerEnter, TrickleDown.TrickleDown);
            _root?.UnregisterCallback<KeyDownEvent>(OnPanelKeyDown, TrickleDown.TrickleDown);
            _root = null;
            _mixer?.Dispose();
            _mixer = null;
            _soundSaver?.Dispose();
            _soundSaver = null;
            _presenter?.Dispose();
            _presenter = null;
            _host?.Dispose();
            _host = null;
        }

        private void Update()
        {
            var delta = Time.unscaledDeltaTime;
            (_presenter as ITickable)?.Tick(delta);
            _music?.Tick(delta);
        }

        private MenuMusicPlayer CreateMusic()
        {
            if (audioLibrary == null || themeAudioSource == null || ambientAudioSource == null)
            {
                return null;
            }

            // Browsers keep audio locked until the first user gesture.
            var requireGesture = Application.platform == RuntimePlatform.WebGLPlayer;
            return new MenuMusicPlayer(
                new UnityMusicLayers(themeAudioSource, ambientAudioSource),
                new[] { audioLibrary.themeVolume, audioLibrary.ambientVolume },
                audioLibrary.musicFadeInSeconds,
                requireGesture,
                () => _sound.MusicMultiplier);
        }

        private void OnPanelKeyDown(KeyDownEvent evt) => _music?.OnUserGesture();

        private void OnPanelPointerDown(PointerDownEvent evt) => OnPress(evt.target as VisualElement);

        private void OnPanelSubmit(NavigationSubmitEvent evt) => OnPress(evt.target as VisualElement);

        private void OnPanelPointerEnter(PointerEnterEvent evt) =>
            _soundReaction.OnPointerEnter(evt.target as VisualElement);

        private void OnPress(VisualElement target)
        {
            _music?.OnUserGesture();
            _soundReaction.OnPress(target);
            _castReaction.OnClick(target);
        }

        private sealed class SilentSfx : IMenuSfx, ISpellSfx
        {
            public void PlayHover() { }
            public void PlayClick() { }
            public void PlayBack() { }
            public void PlayCast() { }
        }

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

using System;

namespace HexFall.UI
{
    public sealed class GuestPresenter : IDisposable
    {
        public const string InvalidNameMessage = "Use 3–16 letters, numbers or _ only.";
        public const string TakenNameMessage = "That wizard name is already taken.";
        public const string NetworkErrorMessage = "Can't reach the server. Check your connection.";
        public const string NoFreeNameMessage = "Couldn't find a free name. Try again.";

        // Random candidates tried per reroll before giving up.
        private const int MaxCandidates = 5;

        private readonly IGuestView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;
        private readonly GuestNameGenerator _generator;
        private readonly INameAvailability _names;
        private bool _busy;
        private bool _disposed;

        // `names` null means no check: guests enter with any valid name (old config without apiBaseUrl).
        public GuestPresenter(IGuestView view, ScreenRouter router, IAuthService auth, SessionState session, GuestNameGenerator generator, INameAvailability names = null)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _generator = generator;
            _names = names;
            _view.NameChanged += Validate;
            _view.RerollRequested += OnReroll;
            _view.EnterRequested += OnEnter;
            _view.CreateAccountRequested += OnCreateAccount;
            OnReroll();
        }

        public void Dispose()
        {
            _disposed = true;
            _view.NameChanged -= Validate;
            _view.RerollRequested -= OnReroll;
            _view.EnterRequested -= OnEnter;
            _view.CreateAccountRequested -= OnCreateAccount;
        }

        private void OnCreateAccount() => _router.Navigate(ScreenId.Register);

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _view.SetBusy(busy);
        }

        private void OnReroll()
        {
            if (_busy) return;
            if (_names == null)
            {
                _view.SetName(_generator.Generate());
                Validate();
                return;
            }

            SetBusy(true);
            TryCandidate(1);
        }

        // Generates a candidate and asks whether it is free; a taken one is replaced, up to MaxCandidates.
        private void TryCandidate(int attempt)
        {
            var candidate = _generator.Generate();
            _names.Check(candidate, result =>
            {
                if (_disposed) return;
                switch (result)
                {
                    case NameAvailability.Available:
                        SetBusy(false);
                        _view.SetName(candidate);
                        Validate();
                        break;
                    case NameAvailability.Taken when attempt < MaxCandidates:
                        TryCandidate(attempt + 1);
                        break;
                    case NameAvailability.Taken:
                        SetBusy(false);
                        _view.ShowError(NoFreeNameMessage);
                        break;
                    default:
                        // Unverified: keep it, Enter checks again.
                        SetBusy(false);
                        _view.SetName(candidate);
                        Validate();
                        _view.ShowError(NetworkErrorMessage);
                        break;
                }
            });
        }

        private void Validate()
        {
            var check = WizardNameRule.Evaluate(_view.Name);
            _view.SetEnterEnabled(check == NameCheck.Valid);
            if (check == NameCheck.Invalid) _view.ShowError(InvalidNameMessage);
            else _view.ClearError();
        }

        private void OnEnter()
        {
            if (_busy || !WizardNameRule.IsValid(_view.Name)) return;
            if (_names == null)
            {
                EnterAsGuest();
                return;
            }

            _view.ClearError();
            SetBusy(true);
            _names.Check(_view.Name, result =>
            {
                if (_disposed) return;
                if (result == NameAvailability.Available)
                {
                    EnterAsGuest();
                    return;
                }

                SetBusy(false);
                _view.ShowError(result switch
                {
                    NameAvailability.Taken => TakenNameMessage,
                    NameAvailability.Invalid => InvalidNameMessage,
                    _ => NetworkErrorMessage,
                });
            });
        }

        private void EnterAsGuest()
        {
            _auth.Guest(_view.Name, result =>
            {
                if (_disposed) return;
                SetBusy(false);
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    return;
                }

                _session.Current = result.Session;
                _router.ResetTo(ScreenId.Lobby);
            });
        }
    }
}

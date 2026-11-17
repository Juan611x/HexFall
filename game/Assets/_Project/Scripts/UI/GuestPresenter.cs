using System;

namespace HexFall.UI
{
    public sealed class GuestPresenter : IDisposable
    {
        public const string InvalidNameMessage = "Use 3–16 letters, numbers or _ only.";

        private readonly IGuestView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;
        private readonly GuestNameGenerator _generator;

        public GuestPresenter(IGuestView view, ScreenRouter router, IAuthService auth, SessionState session, GuestNameGenerator generator)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _generator = generator;
            _view.NameChanged += Validate;
            _view.RerollRequested += OnReroll;
            _view.EnterRequested += OnEnter;
            _view.CreateAccountRequested += OnCreateAccount;
            OnReroll();
        }

        public void Dispose()
        {
            _view.NameChanged -= Validate;
            _view.RerollRequested -= OnReroll;
            _view.EnterRequested -= OnEnter;
            _view.CreateAccountRequested -= OnCreateAccount;
        }

        private void OnCreateAccount() => _router.Navigate(ScreenId.Register);

        private void OnReroll()
        {
            _view.SetName(_generator.Generate());
            Validate();
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
            if (!WizardNameRule.IsValid(_view.Name)) return;
            _auth.Guest(_view.Name, result =>
            {
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

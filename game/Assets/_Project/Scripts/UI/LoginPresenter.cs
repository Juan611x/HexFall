using System;

namespace HexFall.UI
{
    public sealed class LoginPresenter : IDisposable
    {
        public const string InvalidEmailMessage = "Enter a valid email.";
        public const string MissingPasswordMessage = "Enter your password.";

        private readonly ILoginView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;

        public LoginPresenter(ILoginView view, ScreenRouter router, IAuthService auth, SessionState session)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _view.LoginRequested += OnLogin;
            _view.CreateAccountRequested += OnCreateAccount;
        }

        public void Dispose()
        {
            _view.LoginRequested -= OnLogin;
            _view.CreateAccountRequested -= OnCreateAccount;
        }

        private void OnCreateAccount() => _router.Navigate(ScreenId.Register);

        private void OnLogin()
        {
            _view.ClearError();
            if (!EmailRule.IsValid(_view.Email))
            {
                _view.ShowError(InvalidEmailMessage);
                return;
            }

            if (string.IsNullOrEmpty(_view.Password))
            {
                _view.ShowError(MissingPasswordMessage);
                return;
            }

            _auth.LogIn(_view.Email, _view.Password, result =>
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

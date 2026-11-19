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
        private bool _busy;

        public LoginPresenter(ILoginView view, ScreenRouter router, IAuthService auth, SessionState session)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _view.LoginRequested += OnLogin;
            _view.CreateAccountRequested += OnCreateAccount;
            _view.ForgotPasswordRequested += OnForgotPassword;

            if (!string.IsNullOrEmpty(_session.Notice))
            {
                _view.ShowNotice(_session.Notice);
                _session.Notice = null;
            }
        }

        public void Dispose()
        {
            _view.LoginRequested -= OnLogin;
            _view.CreateAccountRequested -= OnCreateAccount;
            _view.ForgotPasswordRequested -= OnForgotPassword;
        }

        private void OnCreateAccount()
        {
            if (_busy) return;
            _router.Navigate(ScreenId.Register);
        }

        private void OnForgotPassword()
        {
            _session.ResetEmail = _view.Email;
            _router.Navigate(ScreenId.ForgotPassword);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _view.SetBusy(busy);
        }

        private void OnLogin()
        {
            if (_busy) return;
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

            SetBusy(true);
            _auth.LogIn(_view.Email, _view.Password, result =>
            {
                if (result.NeedsVerification)
                {
                    // The account exists but is unconfirmed: send a fresh code and let the player finish verifying.
                    var email = _view.Email;
                    _session.PendingEmail = email;
                    _auth.ResendCode(email, _ =>
                    {
                        SetBusy(false);
                        _router.Navigate(ScreenId.Verify);
                    });
                    return;
                }

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

using System;

namespace HexFall.UI
{
    public sealed class RegisterPresenter : IDisposable
    {
        public const string NameNeutral = "3–16 characters: letters, numbers or _. Shown to other players.";
        public const string NameInvalid = "Use 3–16 letters, numbers or _ only.";
        public const string NameValid = "Looks good. Availability is checked when you submit.";
        public const string InvalidEmailMessage = "Enter a valid email.";

        private readonly IRegisterView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;

        public RegisterPresenter(IRegisterView view, ScreenRouter router, IAuthService auth, SessionState session)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _view.Changed += Refresh;
            _view.SubmitRequested += OnSubmit;
            _view.LoginRequested += OnLogin;
            Refresh();
        }

        public void Dispose()
        {
            _view.Changed -= Refresh;
            _view.SubmitRequested -= OnSubmit;
            _view.LoginRequested -= OnLogin;
        }

        private bool IsFormValid() =>
            WizardNameRule.IsValid(_view.WizardName)
            && EmailRule.IsValid(_view.Email)
            && PasswordPolicy.Evaluate(_view.Password, _view.Confirmation).AllMet;

        private void Refresh()
        {
            switch (WizardNameRule.Evaluate(_view.WizardName))
            {
                case NameCheck.Invalid: _view.SetNameHelp(NameInvalid, HelpTone.Invalid); break;
                case NameCheck.Valid: _view.SetNameHelp(NameValid, HelpTone.Valid); break;
                default: _view.SetNameHelp(NameNeutral, HelpTone.Neutral); break;
            }

            _view.SetPasswordRules(PasswordPolicy.Evaluate(_view.Password, _view.Confirmation));

            var email = _view.Email;
            if (!string.IsNullOrEmpty(email) && !EmailRule.IsValid(email)) _view.ShowError(InvalidEmailMessage);
            else _view.ClearError();

            _view.SetSubmitEnabled(IsFormValid());
        }

        private void OnLogin() => _router.Navigate(ScreenId.Login);

        private void OnSubmit()
        {
            if (!IsFormValid()) return;
            var email = _view.Email;
            _auth.SignUp(_view.WizardName, email, _view.Password, result =>
            {
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    return;
                }

                _session.PendingEmail = email;
                _router.Navigate(ScreenId.Verify);
            });
        }
    }
}

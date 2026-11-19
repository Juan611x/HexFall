using System;

namespace HexFall.UI
{
    // Two steps on one screen: ask for a reset code by e-mail, then set a new password with that code.
    public sealed class ForgotPasswordPresenter : IDisposable, ITickable
    {
        public const string PasswordUpdatedNotice = "Password updated. Log in with your new password.";

        private readonly IForgotPasswordView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;
        private readonly VerificationCodeInput _input = new VerificationCodeInput();
        private readonly Countdown _countdown = new Countdown(VerifyPresenter.ResendSeconds);
        private ForgotPasswordStep _step = ForgotPasswordStep.Email;
        private string _email;
        private string _resendText;
        private bool _busy;

        public ForgotPasswordPresenter(IForgotPasswordView view, ScreenRouter router, IAuthService auth, SessionState session)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _view.Changed += OnChanged;
            _view.SendCodeRequested += OnSendCode;
            _view.DigitEntered += OnDigitEntered;
            _view.BackspacePressed += OnBackspace;
            _view.ResetRequested += OnReset;
            _view.ResendRequested += OnResend;
            _view.ChangeEmailRequested += OnChangeEmail;

            if (!string.IsNullOrEmpty(_session.ResetEmail)) _view.SetEmail(_session.ResetEmail);
            _view.ShowStep(ForgotPasswordStep.Email, null);
            _view.SetSendEnabled(EmailRule.IsValid(_view.Email));
        }

        public void Dispose()
        {
            _view.Changed -= OnChanged;
            _view.SendCodeRequested -= OnSendCode;
            _view.DigitEntered -= OnDigitEntered;
            _view.BackspacePressed -= OnBackspace;
            _view.ResetRequested -= OnReset;
            _view.ResendRequested -= OnResend;
            _view.ChangeEmailRequested -= OnChangeEmail;
        }

        public void Tick(float deltaSeconds)
        {
            _countdown.Tick(deltaSeconds);
            RefreshResend();
        }

        private bool CanReset() =>
            _input.IsComplete && PasswordPolicy.Evaluate(_view.Password, _view.Confirmation).AllMet;

        private void OnChanged()
        {
            _view.ClearError();
            if (_step == ForgotPasswordStep.Email) _view.SetSendEnabled(EmailRule.IsValid(_view.Email));
            else RefreshCodeStep();
        }

        private void RefreshCodeStep()
        {
            _view.SetPasswordRules(PasswordPolicy.Evaluate(_view.Password, _view.Confirmation));
            _view.SetResetEnabled(CanReset());
        }

        private void RefreshResend()
        {
            var text = _countdown.IsRunning
                ? $"No code? Resend in {TimeLabel.MinSec(_countdown.RemainingSeconds)}"
                : "Resend code";
            if (text == _resendText) return;
            _resendText = text;
            _view.SetResend(text, !_countdown.IsRunning);
        }

        private void RenderDigits(int focus)
        {
            var digits = new char?[VerificationCodeInput.Length];
            for (var i = 0; i < digits.Length; i++) digits[i] = _input.DigitAt(i);
            _view.SetDigits(digits);
            _view.FocusDigit(focus);
            RefreshCodeStep();
        }

        private void SetBusy(ForgotBusy busy)
        {
            _busy = busy != ForgotBusy.None;
            _view.SetBusy(busy);
        }

        private void OnSendCode()
        {
            var email = _view.Email;
            if (_busy || !EmailRule.IsValid(email)) return;
            _view.ClearError();
            SetBusy(ForgotBusy.Sending);
            _auth.RequestPasswordReset(email, result =>
            {
                SetBusy(ForgotBusy.None);
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    return;
                }

                _email = email;
                _step = ForgotPasswordStep.Code;
                _input.Clear();
                _countdown.Start();
                _resendText = null;
                _view.ShowStep(ForgotPasswordStep.Code, email);
                RenderDigits(0);
                RefreshResend();
            });
        }

        private void OnChangeEmail()
        {
            _step = ForgotPasswordStep.Email;
            _view.ClearError();
            _view.ShowStep(ForgotPasswordStep.Email, null);
            _view.SetSendEnabled(EmailRule.IsValid(_view.Email));
        }

        private void OnDigitEntered(int index, string text)
        {
            _view.ClearError();
            RenderDigits(_input.Type(index, text));
        }

        private void OnBackspace(int index)
        {
            _view.ClearError();
            RenderDigits(_input.Backspace(index));
        }

        private void OnResend()
        {
            if (_busy || _countdown.IsRunning) return;
            SetBusy(ForgotBusy.Resending);
            _auth.RequestPasswordReset(_email, result =>
            {
                SetBusy(ForgotBusy.None);
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    return;
                }

                _countdown.Start();
                RefreshResend();
            });
        }

        private void OnReset()
        {
            if (_busy || !CanReset()) return;
            var password = _view.Password;
            SetBusy(ForgotBusy.Resetting);
            _auth.ConfirmPasswordReset(_email, _input.Code, password, result =>
            {
                if (!result.Success)
                {
                    SetBusy(ForgotBusy.None);
                    _view.ShowError(result.Error);
                    return;
                }

                // The password is changed; sign in with it so the player lands in the lobby.
                _auth.LogIn(_email, password, login =>
                {
                    SetBusy(ForgotBusy.None);
                    _session.ResetEmail = null;
                    if (login.Success && login.Session != null)
                    {
                        _session.Current = login.Session;
                        _router.ResetTo(ScreenId.Lobby);
                        return;
                    }

                    _session.Notice = PasswordUpdatedNotice;
                    _router.ResetTo(ScreenId.Login);
                });
            });
        }
    }
}

using System;

namespace HexFall.UI
{
    public sealed class VerifyPresenter : IDisposable, ITickable
    {
        public const int ResendSeconds = 60;

        private readonly IVerifyView _view;
        private readonly ScreenRouter _router;
        private readonly IAuthService _auth;
        private readonly SessionState _session;
        private readonly VerificationCodeInput _input = new VerificationCodeInput();
        private readonly Countdown _countdown = new Countdown(ResendSeconds);
        private string _resendText;
        private bool _busy;

        public VerifyPresenter(IVerifyView view, ScreenRouter router, IAuthService auth, SessionState session)
        {
            _view = view;
            _router = router;
            _auth = auth;
            _session = session;
            _view.DigitEntered += OnDigitEntered;
            _view.BackspacePressed += OnBackspace;
            _view.SubmitRequested += OnSubmit;
            _view.ResendRequested += OnResend;

            _view.ShowEmail(_session.PendingEmail);
            _view.SetSubmitEnabled(false);
            _countdown.Start();
            RefreshResend();
        }

        public void Dispose()
        {
            _view.DigitEntered -= OnDigitEntered;
            _view.BackspacePressed -= OnBackspace;
            _view.SubmitRequested -= OnSubmit;
            _view.ResendRequested -= OnResend;
        }

        public void Tick(float deltaSeconds)
        {
            _countdown.Tick(deltaSeconds);
            RefreshResend();
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

        private void Render(int focus)
        {
            var digits = new char?[VerificationCodeInput.Length];
            for (var i = 0; i < digits.Length; i++) digits[i] = _input.DigitAt(i);
            _view.SetDigits(digits);
            _view.FocusDigit(focus);
            _view.SetSubmitEnabled(_input.IsComplete);
        }

        private void OnDigitEntered(int index, string text)
        {
            _view.ClearError();
            Render(_input.Type(index, text));
        }

        private void OnBackspace(int index)
        {
            _view.ClearError();
            Render(_input.Backspace(index));
        }

        private void SetBusy(VerifyBusy busy)
        {
            _busy = busy != VerifyBusy.None;
            _view.SetBusy(busy);
        }

        private void OnSubmit()
        {
            if (_busy || !_input.IsComplete) return;
            SetBusy(VerifyBusy.Verifying);
            _auth.Verify(_session.PendingEmail, _input.Code, result =>
            {
                SetBusy(VerifyBusy.None);
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    _input.Clear();
                    Render(0);
                    return;
                }

                _session.Current = result.Session;
                _session.PendingEmail = null;
                // Verified but not signed in (e.g. the page was reloaded): the player logs in next.
                _router.ResetTo(result.Session == null ? ScreenId.Login : ScreenId.Lobby);
            });
        }

        private void OnResend()
        {
            if (_busy || _countdown.IsRunning) return;
            SetBusy(VerifyBusy.Resending);
            _auth.ResendCode(_session.PendingEmail, result =>
            {
                SetBusy(VerifyBusy.None);
                if (!result.Success)
                {
                    _view.ShowError(result.Error);
                    return;
                }

                _countdown.Start();
                RefreshResend();
            });
        }
    }
}

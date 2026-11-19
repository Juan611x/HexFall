using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ForgotPasswordPresenterTests
    {
        private sealed class FakeView : IForgotPasswordView
        {
            public event Action Changed;
            public event Action SendCodeRequested;
            public event Action<int, string> DigitEntered;
            public event Action<int> BackspacePressed;
            public event Action ResetRequested;
            public event Action ResendRequested;
            public event Action ChangeEmailRequested;

            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Confirmation { get; set; } = "";

            public ForgotPasswordStep Step = ForgotPasswordStep.Code;
            public string StepEmail;
            public bool SendEnabled;
            public bool ResetEnabled;
            public char?[] Digits = new char?[6];
            public PasswordCheck Rules;
            public string ResendText;
            public bool ResendEnabled;
            public string Error;
            public ForgotBusy Busy;

            public void SetEmail(string email) => Email = email;
            public void ShowStep(ForgotPasswordStep step, string email) { Step = step; StepEmail = email; }
            public void SetSendEnabled(bool enabled) => SendEnabled = enabled;
            public void SetDigits(char?[] digits) => Digits = digits;
            public void FocusDigit(int index) { }
            public void SetPasswordRules(PasswordCheck check) => Rules = check;
            public void SetResetEnabled(bool enabled) => ResetEnabled = enabled;
            public void SetResend(string text, bool enabled) { ResendText = text; ResendEnabled = enabled; }
            public void ShowError(string message) => Error = message;
            public void ClearError() => Error = null;
            public void SetBusy(ForgotBusy busy) => Busy = busy;

            public string DigitString()
            {
                var s = "";
                foreach (var d in Digits) s += d.HasValue ? d.Value.ToString() : "_";
                return s;
            }

            public void Edit(string email = null, string password = null, string confirmation = null)
            {
                if (email != null) Email = email;
                if (password != null) Password = password;
                if (confirmation != null) Confirmation = confirmation;
                Changed?.Invoke();
            }

            public void RaiseSend() => SendCodeRequested?.Invoke();
            public void EnterDigits(string code) => DigitEntered?.Invoke(0, code);
            public void Backspace(int index) => BackspacePressed?.Invoke(index);
            public void RaiseReset() => ResetRequested?.Invoke();
            public void RaiseResend() => ResendRequested?.Invoke();
            public void RaiseChangeEmail() => ChangeEmailRequested?.Invoke();
        }

        // Records calls and answers with scripted results.
        private sealed class ScriptedAuth : IAuthService
        {
            public AuthResult RequestResult = AuthResult.Ok();
            public AuthResult ConfirmResult = AuthResult.Ok();
            public AuthResult LogInResult = AuthResult.Ok(new PlayerSession("Ash_Mage", false));
            public readonly List<string> Requested = new List<string>();
            public readonly List<string> Confirmed = new List<string>();
            public readonly List<string> LogIns = new List<string>();

            public void RequestPasswordReset(string email, Action<AuthResult> done) { Requested.Add(email); done(RequestResult); }

            public void ConfirmPasswordReset(string email, string code, string newPassword, Action<AuthResult> done)
            {
                Confirmed.Add($"{email}|{code}|{newPassword}");
                done(ConfirmResult);
            }

            public void LogIn(string email, string password, Action<AuthResult> done) { LogIns.Add($"{email}|{password}"); done(LogInResult); }
            public void SignUp(string name, string email, string password, Action<AuthResult> done) { }
            public void Verify(string email, string code, Action<AuthResult> done) { }
            public void ResendCode(string email, Action<AuthResult> done) { }
            public void Guest(string name, Action<AuthResult> done) { }
            public void Restore(Action<AuthResult> done) { }
            public void LogOut(Action done) { }
        }

        private const string Email = "ash@example.com";
        private const string Password = "New#Pass1";

        private FakeView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private ScriptedAuth _auth;
        private ForgotPasswordPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _auth = new ScriptedAuth();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Login);
            _router.Navigate(ScreenId.ForgotPassword);
            _state = new SessionState { ResetEmail = Email };
            _view = new FakeView();
            _presenter = new ForgotPasswordPresenter(_view, _router, _auth, _state);
        }

        private void GoToCodeStep()
        {
            _view.Edit(email: Email);
            _view.RaiseSend();
        }

        private void FillValidForm()
        {
            _view.EnterDigits("123456");
            _view.Edit(password: Password, confirmation: Password);
        }

        [Test]
        public void StartsOnTheEmailStepPrefilledWithTheLoginEmail()
        {
            Assert.AreEqual(ForgotPasswordStep.Email, _view.Step);
            Assert.AreEqual(Email, _view.Email);
            Assert.IsTrue(_view.SendEnabled);
        }

        [Test]
        public void StartsWithSendDisabledWhenThereIsNoPrefill()
        {
            _presenter.Dispose();
            _view = new FakeView();
            _presenter = new ForgotPasswordPresenter(_view, _router, _auth, new SessionState());
            Assert.IsFalse(_view.SendEnabled);
        }

        [Test]
        public void SendIsEnabledOnlyForAValidEmail()
        {
            _view.Edit(email: "nope");
            Assert.IsFalse(_view.SendEnabled);
            _view.Edit(email: "ok@example.com");
            Assert.IsTrue(_view.SendEnabled);
        }

        [Test]
        public void SendWithAnInvalidEmailDoesNothing()
        {
            _view.Edit(email: "nope");
            _view.RaiseSend();
            Assert.IsEmpty(_auth.Requested);
            Assert.AreEqual(ForgotPasswordStep.Email, _view.Step);
        }

        [Test]
        public void SendRequestsACodeAndMovesToTheCodeStep()
        {
            GoToCodeStep();
            CollectionAssert.AreEqual(new[] { Email }, _auth.Requested);
            Assert.AreEqual(ForgotPasswordStep.Code, _view.Step);
            Assert.AreEqual(Email, _view.StepEmail);
            Assert.IsFalse(_view.ResetEnabled);
            Assert.AreEqual("______", _view.DigitString());
        }

        [Test]
        public void SendFailureShowsTheErrorAndStaysOnTheEmailStep()
        {
            _auth.RequestResult = AuthResult.Fail(CognitoErrorMapper.ResetNeedsVerification);
            GoToCodeStep();
            Assert.AreEqual(CognitoErrorMapper.ResetNeedsVerification, _view.Error);
            Assert.AreEqual(ForgotPasswordStep.Email, _view.Step);
        }

        [Test]
        public void ResendIsLockedForSixtySecondsThenCountsDown()
        {
            GoToCodeStep();
            Assert.IsFalse(_view.ResendEnabled);
            Assert.AreEqual("No code? Resend in 1:00", _view.ResendText);
            _presenter.Tick(15f);
            Assert.AreEqual("No code? Resend in 0:45", _view.ResendText);
            _presenter.Tick(45f);
            Assert.IsTrue(_view.ResendEnabled);
            Assert.AreEqual("Resend code", _view.ResendText);
        }

        [Test]
        public void ResendRequestsANewCodeAndRestartsTheCountdown()
        {
            GoToCodeStep();
            _presenter.Tick(60f);
            _view.RaiseResend();
            Assert.AreEqual(2, _auth.Requested.Count);
            Assert.IsFalse(_view.ResendEnabled);
            Assert.AreEqual("No code? Resend in 1:00", _view.ResendText);
        }

        [Test]
        public void ResendBeforeTheCountdownEndsIsIgnored()
        {
            GoToCodeStep();
            _view.RaiseResend();
            Assert.AreEqual(1, _auth.Requested.Count);
        }

        [Test]
        public void ResendFailureShowsTheErrorAndKeepsItEnabled()
        {
            GoToCodeStep();
            _presenter.Tick(60f);
            _auth.RequestResult = AuthResult.Fail("Too many attempts. Try again later.");
            _view.RaiseResend();
            Assert.AreEqual("Too many attempts. Try again later.", _view.Error);
            Assert.IsTrue(_view.ResendEnabled);
        }

        [Test]
        public void ChangeEmailGoesBackToTheEmailStep()
        {
            GoToCodeStep();
            _view.RaiseChangeEmail();
            Assert.AreEqual(ForgotPasswordStep.Email, _view.Step);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void DigitsFillTheBoxes()
        {
            GoToCodeStep();
            _view.EnterDigits("123");
            Assert.AreEqual("123___", _view.DigitString());
        }

        [Test]
        public void BackspaceClearsADigit()
        {
            GoToCodeStep();
            _view.EnterDigits("12");
            _view.Backspace(1);
            Assert.AreEqual("1_____", _view.DigitString());
        }

        [Test]
        public void PasswordRulesFollowTheFields()
        {
            GoToCodeStep();
            _view.Edit(password: "abc", confirmation: "abd");
            Assert.IsFalse(_view.Rules.MinLength);
            Assert.IsTrue(_view.Rules.HasLower);
            Assert.IsFalse(_view.Rules.Matches);
        }

        [Test]
        public void ResetNeedsACompleteCodeAndAValidMatchingPassword()
        {
            GoToCodeStep();
            _view.Edit(password: Password, confirmation: Password);
            Assert.IsFalse(_view.ResetEnabled);
            _view.EnterDigits("12345");
            Assert.IsFalse(_view.ResetEnabled);
            _view.EnterDigits("123456");
            Assert.IsTrue(_view.ResetEnabled);
            _view.Edit(confirmation: "other");
            Assert.IsFalse(_view.ResetEnabled);
        }

        [Test]
        public void ResetWithAnIncompleteFormDoesNothing()
        {
            GoToCodeStep();
            _view.EnterDigits("123456");
            _view.RaiseReset();
            Assert.IsEmpty(_auth.Confirmed);
        }

        [Test]
        public void ResetConfirmsThenSignsInWithTheNewPasswordAndGoesToTheLobby()
        {
            GoToCodeStep();
            FillValidForm();

            _view.RaiseReset();

            CollectionAssert.AreEqual(new[] { $"{Email}|123456|{Password}" }, _auth.Confirmed);
            CollectionAssert.AreEqual(new[] { $"{Email}|{Password}" }, _auth.LogIns);
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.AreEqual("Ash_Mage", _state.Current.Nickname);
            Assert.IsNull(_state.ResetEmail);
        }

        [Test]
        public void ResetFailureShowsTheErrorKeepsTheFormAndDoesNotSignIn()
        {
            GoToCodeStep();
            FillValidForm();
            _auth.ConfirmResult = AuthResult.Fail("That code is not valid. Try again.");

            _view.RaiseReset();

            Assert.AreEqual("That code is not valid. Try again.", _view.Error);
            Assert.IsEmpty(_auth.LogIns);
            Assert.AreEqual(ScreenId.ForgotPassword, _router.Current);
            Assert.AreEqual("123456", _view.DigitString());
        }

        [Test]
        public void FailedSignInAfterAResetFallsBackToLoginWithANotice()
        {
            GoToCodeStep();
            FillValidForm();
            _auth.LogInResult = AuthResult.Fail("Can't reach the server. Check your connection.");

            _view.RaiseReset();

            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.IsNull(_state.Current);
            Assert.AreEqual(ForgotPasswordPresenter.PasswordUpdatedNotice, _state.Notice);
        }

        [Test]
        public void EditingClearsThePreviousError()
        {
            _auth.RequestResult = AuthResult.Fail("boom");
            GoToCodeStep();
            _view.Edit(email: "other@example.com");
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void ClearingTheSessionForgetsTheResetEmailAndNotice()
        {
            _state.Notice = "x";
            _state.Clear();
            Assert.IsNull(_state.ResetEmail);
            Assert.IsNull(_state.Notice);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.Edit(email: Email);
            _view.RaiseSend();
            Assert.IsEmpty(_auth.Requested);
        }

        private DeferredAuthService UseDeferredAuth()
        {
            var deferred = new DeferredAuthService();
            _presenter.Dispose();
            _presenter = new ForgotPasswordPresenter(_view, _router, deferred, _state);
            return deferred;
        }

        private DeferredAuthService DeferredAtCodeStep()
        {
            var auth = UseDeferredAuth();
            GoToCodeStep();
            auth.CompleteNext(AuthResult.Ok());
            return auth;
        }

        [Test]
        public void SendIsBusyWhileTheRequestRunsAndClearedOnSuccess()
        {
            var auth = UseDeferredAuth();
            GoToCodeStep();
            Assert.AreEqual(ForgotBusy.Sending, _view.Busy);
            auth.CompleteNext(AuthResult.Ok());
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            Assert.AreEqual(ForgotPasswordStep.Code, _view.Step);
        }

        [Test]
        public void SendBusyClearedOnFailure()
        {
            var auth = UseDeferredAuth();
            GoToCodeStep();
            auth.CompleteNext(AuthResult.Fail("boom"));
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            Assert.AreEqual("boom", _view.Error);
        }

        [Test]
        public void SecondSendWhileBusyIsIgnored()
        {
            var auth = UseDeferredAuth();
            GoToCodeStep();
            _view.RaiseSend();
            Assert.AreEqual(1, auth.Calls);
        }

        [Test]
        public void ResetIsBusyThroughTheResetAndTheFollowUpLogin()
        {
            var auth = DeferredAtCodeStep();
            FillValidForm();
            _view.RaiseReset();
            Assert.AreEqual(ForgotBusy.Resetting, _view.Busy);
            auth.CompleteNext(AuthResult.Ok());
            Assert.AreEqual(ForgotBusy.Resetting, _view.Busy);
            auth.CompleteNext(AuthResult.Ok(new PlayerSession("Ash_Mage", false)));
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
        }

        [Test]
        public void ResetBusyClearedWhenTheCodeIsRejected()
        {
            var auth = DeferredAtCodeStep();
            FillValidForm();
            _view.RaiseReset();
            auth.CompleteNext(AuthResult.Fail("bad code"));
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            Assert.AreEqual("bad code", _view.Error);
        }

        [Test]
        public void ResetBusyClearedWhenTheFollowUpLoginFails()
        {
            var auth = DeferredAtCodeStep();
            FillValidForm();
            _view.RaiseReset();
            auth.CompleteNext(AuthResult.Ok());
            auth.CompleteNext(AuthResult.Fail("nope"));
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void SecondResetWhileBusyIsIgnored()
        {
            var auth = DeferredAtCodeStep();
            FillValidForm();
            _view.RaiseReset();
            _view.RaiseReset();
            Assert.AreEqual(1, auth.Calls - 1);
        }

        [Test]
        public void ResendIsBusyWhileTheRequestRunsAndClearedOnEveryOutcome()
        {
            var auth = DeferredAtCodeStep();
            _presenter.Tick(60f);
            _view.RaiseResend();
            Assert.AreEqual(ForgotBusy.Resending, _view.Busy);
            auth.CompleteNext(AuthResult.Fail("slow down"));
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
            _view.RaiseResend();
            auth.CompleteNext(AuthResult.Ok());
            Assert.AreEqual(ForgotBusy.None, _view.Busy);
        }

        [Test]
        public void SecondResendWhileBusyIsIgnored()
        {
            var auth = DeferredAtCodeStep();
            _presenter.Tick(60f);
            _view.RaiseResend();
            _view.RaiseResend();
            Assert.AreEqual(2, auth.Calls);
        }

        [Test]
        public void ResetWhileResendingIsIgnored()
        {
            var auth = DeferredAtCodeStep();
            FillValidForm();
            _presenter.Tick(60f);
            _view.RaiseResend();
            _view.RaiseReset();
            Assert.AreEqual(2, auth.Calls);
        }
    }
}

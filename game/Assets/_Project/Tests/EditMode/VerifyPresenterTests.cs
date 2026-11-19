using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class VerifyPresenterTests
    {
        private sealed class FakeVerifyView : IVerifyView
        {
            public event Action<int, string> DigitEntered;
            public event Action<int> BackspacePressed;
            public event Action SubmitRequested;
            public event Action ResendRequested;

            public string Email;
            public char?[] Digits = new char?[6];
            public int Focus = -1;
            public bool SubmitEnabled = true;
            public string ResendText;
            public bool ResendEnabled;
            public string Error;
            public VerifyBusy Busy;

            public void ShowEmail(string email) => Email = email;
            public void SetDigits(char?[] digits) => Digits = digits;
            public void FocusDigit(int index) => Focus = index;
            public void SetSubmitEnabled(bool enabled) => SubmitEnabled = enabled;
            public void SetResend(string text, bool enabled) { ResendText = text; ResendEnabled = enabled; }
            public void ShowError(string message) => Error = message;
            public void ClearError() => Error = null;
            public void SetBusy(VerifyBusy busy) => Busy = busy;

            public string DigitString()
            {
                var s = "";
                foreach (var d in Digits) s += d.HasValue ? d.Value.ToString() : "_";
                return s;
            }

            public void Enter(int index, string text) => DigitEntered?.Invoke(index, text);
            public void Backspace(int index) => BackspacePressed?.Invoke(index);
            public void RaiseSubmit() => SubmitRequested?.Invoke();
            public void RaiseResend() => ResendRequested?.Invoke();
        }

        private FakeVerifyView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private FakeAuthService _auth;
        private VerifyPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _auth = new FakeAuthService();
            _auth.SignUp("Ash_Mage", "ash@example.com", "Arena#2026", _ => { });
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Register);
            _router.Navigate(ScreenId.Verify);
            _state = new SessionState { PendingEmail = "ash@example.com" };
            _view = new FakeVerifyView();
            _presenter = new VerifyPresenter(_view, _router, _auth, _state);
        }

        [Test]
        public void ShowsPendingEmailAndStartsCountdown()
        {
            Assert.AreEqual("ash@example.com", _view.Email);
            Assert.AreEqual("No code? Resend in 1:00", _view.ResendText);
            Assert.IsFalse(_view.ResendEnabled);
            Assert.IsFalse(_view.SubmitEnabled);
        }

        [Test]
        public void TypingDigitsUpdatesViewAndFocus()
        {
            _view.Enter(0, "1");
            Assert.AreEqual("1_____", _view.DigitString());
            Assert.AreEqual(1, _view.Focus);
        }

        [Test]
        public void PasteFillsAllAndEnablesSubmit()
        {
            _view.Enter(0, "123456");
            Assert.AreEqual("123456", _view.DigitString());
            Assert.AreEqual(5, _view.Focus);
            Assert.IsTrue(_view.SubmitEnabled);
        }

        [Test]
        public void NonDigitIsRejectedAndBoxBlanked()
        {
            _view.Enter(2, "z");
            Assert.AreEqual("______", _view.DigitString());
            Assert.AreEqual(2, _view.Focus);
        }

        [Test]
        public void BackspaceMovesFocusBackAndDisablesSubmit()
        {
            _view.Enter(0, "123456");
            _view.Backspace(5);
            Assert.AreEqual("12345_", _view.DigitString());
            Assert.AreEqual(5, _view.Focus);
            Assert.IsFalse(_view.SubmitEnabled);
            _view.Backspace(5);
            Assert.AreEqual("1234__", _view.DigitString());
            Assert.AreEqual(4, _view.Focus);
        }

        [Test]
        public void WrongCodeShowsErrorClearsDigitsAndFocusesFirst()
        {
            _view.Enter(0, "000000");
            _view.RaiseSubmit();
            Assert.AreEqual("That code is not valid. Try again.", _view.Error);
            Assert.AreEqual("______", _view.DigitString());
            Assert.AreEqual(0, _view.Focus);
            Assert.IsFalse(_view.SubmitEnabled);
            Assert.AreEqual(ScreenId.Verify, _router.Current);
        }

        [Test]
        public void TypingAfterErrorClearsIt()
        {
            _view.Enter(0, "000000");
            _view.RaiseSubmit();
            _view.Enter(0, "1");
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void IncompleteSubmitDoesNothing()
        {
            _view.Enter(0, "123");
            _view.RaiseSubmit();
            Assert.AreEqual(ScreenId.Verify, _router.Current);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void CorrectCodeSignsInAndResetsToLobby()
        {
            _view.Enter(0, "123456");
            _view.RaiseSubmit();
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.AreEqual("Ash_Mage", _state.Current.Nickname);
            Assert.IsNull(_state.PendingEmail);
        }

        private sealed class NoSessionAuth : IAuthService
        {
            public void SignUp(string name, string email, string password, Action<AuthResult> done) => done(AuthResult.Ok());
            public void Verify(string email, string code, Action<AuthResult> done) => done(AuthResult.Ok());
            public void ResendCode(string email, Action<AuthResult> done) => done(AuthResult.Ok());
            public void LogIn(string email, string password, Action<AuthResult> done) => done(AuthResult.Ok());
            public void Guest(string name, Action<AuthResult> done) => done(AuthResult.Ok());
            public void RequestPasswordReset(string email, Action<AuthResult> done) { }
            public void ConfirmPasswordReset(string email, string code, string newPassword, Action<AuthResult> done) { }
            public void Restore(Action<AuthResult> done) => done(AuthResult.Fail("none"));
            public void LogOut(Action done) => done();
        }

        [Test]
        public void VerifiedWithoutSessionResetsToLoginAndClearsPendingEmail()
        {
            _presenter.Dispose();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Register);
            _router.Navigate(ScreenId.Verify);
            _view = new FakeVerifyView();
            _presenter = new VerifyPresenter(_view, _router, new NoSessionAuth(), _state);

            _view.Enter(0, "123456");
            _view.RaiseSubmit();

            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.IsNull(_state.Current);
            Assert.IsNull(_state.PendingEmail);
        }

        [Test]
        public void TickUpdatesCountdownLabel()
        {
            _presenter.Tick(19.2f);
            Assert.AreEqual("No code? Resend in 0:41", _view.ResendText);
            Assert.IsFalse(_view.ResendEnabled);
        }

        [Test]
        public void CountdownEndEnablesResend()
        {
            _presenter.Tick(60f);
            Assert.AreEqual("Resend code", _view.ResendText);
            Assert.IsTrue(_view.ResendEnabled);
        }

        [Test]
        public void ResendWhileCountingIsIgnored()
        {
            _presenter.Tick(10f);
            _view.RaiseResend();
            Assert.AreEqual("No code? Resend in 0:50", _view.ResendText);
        }

        [Test]
        public void ResendAfterCountdownRestartsIt()
        {
            _presenter.Tick(60f);
            _view.RaiseResend();
            Assert.AreEqual("No code? Resend in 1:00", _view.ResendText);
            Assert.IsFalse(_view.ResendEnabled);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.Enter(0, "123456");
            Assert.AreEqual("______", _view.DigitString());
        }

        private DeferredAuthService UseDeferredAuth()
        {
            var deferred = new DeferredAuthService();
            _presenter.Dispose();
            _presenter = new VerifyPresenter(_view, _router, deferred, _state);
            return deferred;
        }

        [Test]
        public void VerifyIsBusyWhileTheRequestRunsAndClearedOnSuccess()
        {
            var auth = UseDeferredAuth();
            _view.Enter(0, "123456");
            _view.RaiseSubmit();
            Assert.AreEqual(VerifyBusy.Verifying, _view.Busy);
            auth.CompleteNext(AuthResult.Ok(new PlayerSession("Ash_Mage", false)));
            Assert.AreEqual(VerifyBusy.None, _view.Busy);
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
        }

        [Test]
        public void VerifyBusyClearedOnFailure()
        {
            var auth = UseDeferredAuth();
            _view.Enter(0, "123456");
            _view.RaiseSubmit();
            auth.CompleteNext(AuthResult.Fail("bad code"));
            Assert.AreEqual(VerifyBusy.None, _view.Busy);
            Assert.AreEqual("bad code", _view.Error);
        }

        [Test]
        public void SecondVerifyWhileBusyIsIgnored()
        {
            var auth = UseDeferredAuth();
            _view.Enter(0, "123456");
            _view.RaiseSubmit();
            _view.RaiseSubmit();
            Assert.AreEqual(1, auth.Calls);
        }

        [Test]
        public void ResendIsBusyWhileTheRequestRunsAndClearedOnSuccess()
        {
            var auth = UseDeferredAuth();
            _presenter.Tick(60f);
            _view.RaiseResend();
            Assert.AreEqual(VerifyBusy.Resending, _view.Busy);
            auth.CompleteNext(AuthResult.Ok());
            Assert.AreEqual(VerifyBusy.None, _view.Busy);
        }

        [Test]
        public void ResendBusyClearedOnFailure()
        {
            var auth = UseDeferredAuth();
            _presenter.Tick(60f);
            _view.RaiseResend();
            auth.CompleteNext(AuthResult.Fail("slow down"));
            Assert.AreEqual(VerifyBusy.None, _view.Busy);
            Assert.AreEqual("slow down", _view.Error);
        }

        [Test]
        public void SecondResendWhileBusyIsIgnored()
        {
            var auth = UseDeferredAuth();
            _presenter.Tick(60f);
            _view.RaiseResend();
            _view.RaiseResend();
            Assert.AreEqual(1, auth.Calls);
        }

        [Test]
        public void VerifyWhileResendingIsIgnored()
        {
            var auth = UseDeferredAuth();
            _view.Enter(0, "123456");
            _presenter.Tick(60f);
            _view.RaiseResend();
            _view.RaiseSubmit();
            Assert.AreEqual(1, auth.Calls);
        }

        [Test]
        public void ResendWhileVerifyingIsIgnored()
        {
            var auth = UseDeferredAuth();
            _view.Enter(0, "123456");
            _view.RaiseSubmit();
            _presenter.Tick(60f);
            _view.RaiseResend();
            Assert.AreEqual(1, auth.Calls);
        }
    }
}

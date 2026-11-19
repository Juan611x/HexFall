using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class LoginPresenterTests
    {
        private sealed class FakeLoginView : ILoginView
        {
            public event Action LoginRequested;
            public event Action CreateAccountRequested;
            public event Action ForgotPasswordRequested;

            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Error;
            public int ClearCount;

            public void ShowError(string message) => Error = message;
            public void ClearError() { Error = null; ClearCount++; }

            public void RaiseLogin() => LoginRequested?.Invoke();
            public void RaiseCreateAccount() => CreateAccountRequested?.Invoke();
            public void RaiseForgot() => ForgotPasswordRequested?.Invoke();
        }

        private sealed class SpyAuth : IAuthService
        {
            private readonly FakeAuthService _inner = new FakeAuthService();
            public bool ResendFails;
            public readonly System.Collections.Generic.List<string> Resent = new System.Collections.Generic.List<string>();

            public void SignUp(string name, string email, string password, Action<AuthResult> done) => _inner.SignUp(name, email, password, done);
            public void Verify(string email, string code, Action<AuthResult> done) => _inner.Verify(email, code, done);
            public void LogIn(string email, string password, Action<AuthResult> done) => _inner.LogIn(email, password, done);
            public void Guest(string name, Action<AuthResult> done) => _inner.Guest(name, done);
            public void ResendCode(string email, Action<AuthResult> done)
            {
                Resent.Add(email);
                done(ResendFails ? AuthResult.Fail("nope") : AuthResult.Ok());
            }
        }

        private FakeLoginView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private LoginPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeLoginView();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Login);
            _state = new SessionState();
            _presenter = new LoginPresenter(_view, _router, new FakeAuthService(), _state);
        }

        [Test]
        public void InvalidEmailShowsErrorAndStays()
        {
            _view.Email = "nope";
            _view.Password = "x";
            _view.RaiseLogin();
            Assert.AreEqual(LoginPresenter.InvalidEmailMessage, _view.Error);
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void MissingPasswordShowsError()
        {
            _view.Email = "wizard@example.com";
            _view.RaiseLogin();
            Assert.AreEqual(LoginPresenter.MissingPasswordMessage, _view.Error);
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void WrongCredentialsShowAuthMessage()
        {
            _view.Email = "wizard@example.com";
            _view.Password = "wrong";
            _view.RaiseLogin();
            Assert.AreEqual("Incorrect email or password.", _view.Error);
            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsNull(_state.Current);
        }

        [Test]
        public void SuccessStoresSessionAndResetsToLobby()
        {
            _view.Email = FakeAuthService.DemoEmail;
            _view.Password = FakeAuthService.DemoPassword;
            _view.RaiseLogin();
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.IsNotNull(_state.Current);
            Assert.IsFalse(_state.Current.IsGuest);
        }

        private SpyAuth UseSpyWithUnverifiedAccount()
        {
            var spy = new SpyAuth();
            spy.SignUp("Ash", "ash@example.com", "Arena#2026", _ => { });
            _presenter.Dispose();
            _presenter = new LoginPresenter(_view, _router, spy, _state);
            _view.Email = "ash@example.com";
            _view.Password = "Arena#2026";
            return spy;
        }

        [Test]
        public void UnverifiedLoginResendsCodeAndNavigatesToVerify()
        {
            var spy = UseSpyWithUnverifiedAccount();
            _view.RaiseLogin();
            Assert.AreEqual("ash@example.com", _state.PendingEmail);
            CollectionAssert.AreEqual(new[] { "ash@example.com" }, spy.Resent);
            Assert.AreEqual(ScreenId.Verify, _router.Current);
            Assert.IsNull(_state.Current);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void UnverifiedLoginNavigatesToVerifyEvenWhenResendFails()
        {
            var spy = UseSpyWithUnverifiedAccount();
            spy.ResendFails = true;
            _view.RaiseLogin();
            Assert.AreEqual(ScreenId.Verify, _router.Current);
            Assert.AreEqual("ash@example.com", _state.PendingEmail);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void OrdinaryFailureDoesNotResendOrSetPendingEmail()
        {
            var spy = UseSpyWithUnverifiedAccount();
            _view.Password = "wrong";
            _view.RaiseLogin();
            Assert.AreEqual("Incorrect email or password.", _view.Error);
            Assert.AreEqual(ScreenId.Login, _router.Current);
            Assert.IsEmpty(spy.Resent);
            Assert.IsNull(_state.PendingEmail);
        }

        [Test]
        public void SubmitClearsPreviousError()
        {
            _view.Email = "nope";
            _view.RaiseLogin();
            _view.Email = FakeAuthService.DemoEmail;
            _view.Password = FakeAuthService.DemoPassword;
            _view.RaiseLogin();
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void CreateAccountNavigatesToRegister()
        {
            _view.RaiseCreateAccount();
            Assert.AreEqual(ScreenId.Register, _router.Current);
        }

        [Test]
        public void ForgotPasswordDoesNotNavigate()
        {
            _view.RaiseForgot();
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.RaiseCreateAccount();
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }
    }
}

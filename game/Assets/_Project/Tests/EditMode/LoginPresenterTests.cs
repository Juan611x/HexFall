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

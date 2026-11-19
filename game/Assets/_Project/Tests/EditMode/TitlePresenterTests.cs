using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class TitlePresenterTests
    {
        private sealed class FakeTitleView : ITitleView
        {
            public event Action LoginRequested;
            public event Action CreateAccountRequested;
            public event Action GuestRequested;
            public event Action SettingsRequested;
            public event Action CreditsRequested;

            public void RaiseLogin() => LoginRequested?.Invoke();
            public void RaiseCreateAccount() => CreateAccountRequested?.Invoke();
            public void RaiseGuest() => GuestRequested?.Invoke();
            public void RaiseSettings() => SettingsRequested?.Invoke();
            public void RaiseCredits() => CreditsRequested?.Invoke();
        }

        private FakeTitleView _view;
        private ScreenRouter _router;
        private TitlePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeTitleView();
            _router = new ScreenRouter(ScreenId.Title);
            _presenter = new TitlePresenter(_view, _router);
        }

        [Test]
        public void LoginRequestedNavigatesToLogin()
        {
            _view.RaiseLogin();
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void CreateAccountRequestedNavigatesToRegister()
        {
            _view.RaiseCreateAccount();
            Assert.AreEqual(ScreenId.Register, _router.Current);
        }

        [Test]
        public void GuestRequestedNavigatesToGuest()
        {
            _view.RaiseGuest();
            Assert.AreEqual(ScreenId.Guest, _router.Current);
        }

        [Test]
        public void SettingsRequestedDoesNotNavigate()
        {
            _view.RaiseSettings();
            Assert.AreEqual(ScreenId.Title, _router.Current);
        }

        [Test]
        public void CreditsRequestedDoesNotNavigate()
        {
            _view.RaiseCredits();
            Assert.AreEqual(ScreenId.Title, _router.Current);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();

            _view.RaiseLogin();
            _view.RaiseCreateAccount();
            _view.RaiseGuest();

            Assert.AreEqual(ScreenId.Title, _router.Current);
        }
    }
}

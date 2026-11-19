using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class GuestPresenterTests
    {
        private sealed class FakeGuestView : IGuestView
        {
            public event Action NameChanged;
            public event Action RerollRequested;
            public event Action EnterRequested;
            public event Action CreateAccountRequested;

            public string Name { get; set; } = "";
            public bool EnterEnabled;
            public string Error;

            public void SetBusy(bool busy) { }
            public void SetName(string name) => Name = name;
            public void SetEnterEnabled(bool enabled) => EnterEnabled = enabled;
            public void ShowError(string message) => Error = message;
            public void ClearError() => Error = null;

            public void Type(string name) { Name = name; NameChanged?.Invoke(); }
            public void RaiseReroll() => RerollRequested?.Invoke();
            public void RaiseEnter() => EnterRequested?.Invoke();
            public void RaiseCreateAccount() => CreateAccountRequested?.Invoke();
        }

        private FakeGuestView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private GuestPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeGuestView();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Guest);
            _state = new SessionState();
            _presenter = new GuestPresenter(_view, _router, new FakeAuthService(), _state, new GuestNameGenerator(new Random(5)));
        }

        [Test]
        public void PrefillsAGeneratedValidNameAndEnablesEnter()
        {
            Assert.IsTrue(WizardNameRule.IsValid(_view.Name), _view.Name);
            Assert.IsTrue(_view.EnterEnabled);
        }

        [Test]
        public void RerollReplacesTheName()
        {
            var first = _view.Name;
            _view.RaiseReroll();
            Assert.AreNotEqual(first, _view.Name);
            Assert.IsTrue(WizardNameRule.IsValid(_view.Name));
        }

        [Test]
        public void RerollRecoversFromInvalidName()
        {
            _view.Type("x");
            Assert.IsFalse(_view.EnterEnabled);
            _view.RaiseReroll();
            Assert.IsTrue(_view.EnterEnabled);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void InvalidNameDisablesEnterAndShowsError()
        {
            _view.Type("bad name");
            Assert.IsFalse(_view.EnterEnabled);
            Assert.AreEqual(GuestPresenter.InvalidNameMessage, _view.Error);
        }

        [Test]
        public void EmptyNameDisablesEnterWithoutError()
        {
            _view.Type("");
            Assert.IsFalse(_view.EnterEnabled);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void ValidTypedNameEnablesEnterAndClearsError()
        {
            _view.Type("bad name");
            _view.Type("Good_Name");
            Assert.IsTrue(_view.EnterEnabled);
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void EnterCreatesGuestSessionAndResetsToLobby()
        {
            _view.Type("Good_Name");
            _view.RaiseEnter();
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.IsTrue(_state.Current.IsGuest);
            Assert.AreEqual("Good_Name", _state.Current.Nickname);
        }

        [Test]
        public void EnterWithInvalidNameDoesNothing()
        {
            _view.Type("x");
            _view.RaiseEnter();
            Assert.AreEqual(ScreenId.Guest, _router.Current);
            Assert.IsNull(_state.Current);
        }

        [Test]
        public void CreateAccountNavigatesToRegister()
        {
            _view.RaiseCreateAccount();
            Assert.AreEqual(ScreenId.Register, _router.Current);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.RaiseCreateAccount();
            Assert.AreEqual(ScreenId.Guest, _router.Current);
        }
    }
}

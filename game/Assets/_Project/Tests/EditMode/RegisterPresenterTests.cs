using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class RegisterPresenterTests
    {
        private sealed class FakeRegisterView : IRegisterView
        {
            public event Action Changed;
            public event Action SubmitRequested;
            public event Action LoginRequested;

            public string WizardName { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Confirmation { get; set; } = "";

            public string HelpText;
            public HelpTone Tone;
            public PasswordCheck Rules;
            public bool SubmitEnabled = true;
            public string Error;
            public bool Busy;

            public void SetNameHelp(string text, HelpTone tone) { HelpText = text; Tone = tone; }
            public void SetPasswordRules(PasswordCheck check) => Rules = check;
            public void SetSubmitEnabled(bool enabled) => SubmitEnabled = enabled;
            public void ShowError(string message) => Error = message;
            public void ClearError() => Error = null;
            public void SetBusy(bool busy) => Busy = busy;

            public void Type(string name = null, string email = null, string password = null, string confirm = null)
            {
                if (name != null) WizardName = name;
                if (email != null) Email = email;
                if (password != null) Password = password;
                if (confirm != null) Confirmation = confirm;
                Changed?.Invoke();
            }

            public void RaiseSubmit() => SubmitRequested?.Invoke();
            public void RaiseLogin() => LoginRequested?.Invoke();
        }

        private FakeRegisterView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private FakeAuthService _auth;
        private RegisterPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeRegisterView();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Register);
            _state = new SessionState();
            _auth = new FakeAuthService();
            _presenter = new RegisterPresenter(_view, _router, _auth, _state);
        }

        private void FillValid(string email = "ash@example.com")
        {
            _view.Type("Ash_Mage", email, "Arena#2026", "Arena#2026");
        }

        [Test]
        public void InitialStateIsNeutralWithSubmitDisabled()
        {
            Assert.AreEqual(RegisterPresenter.NameNeutral, _view.HelpText);
            Assert.AreEqual(HelpTone.Neutral, _view.Tone);
            Assert.IsFalse(_view.SubmitEnabled);
            Assert.IsFalse(_view.Rules.AllMet);
        }

        [Test]
        public void NameHelpFollowsTheRule()
        {
            _view.Type(name: "ab");
            Assert.AreEqual((RegisterPresenter.NameInvalid, HelpTone.Invalid), (_view.HelpText, _view.Tone));
            _view.Type(name: "Ash_Mage");
            Assert.AreEqual((RegisterPresenter.NameValid, HelpTone.Valid), (_view.HelpText, _view.Tone));
            _view.Type(name: "");
            Assert.AreEqual((RegisterPresenter.NameNeutral, HelpTone.Neutral), (_view.HelpText, _view.Tone));
        }

        [Test]
        public void PasswordRulesAreReportedLive()
        {
            _view.Type(password: "Arena#2026", confirm: "Arena#2026");
            Assert.IsTrue(_view.Rules.AllMet);
            _view.Type(confirm: "other");
            Assert.IsFalse(_view.Rules.Matches);
            Assert.IsTrue(_view.Rules.HasSymbol);
        }

        [Test]
        public void SubmitEnabledOnlyWhenEverythingIsValid()
        {
            FillValid();
            Assert.IsTrue(_view.SubmitEnabled);
            _view.Type(email: "bad");
            Assert.IsFalse(_view.SubmitEnabled);
            _view.Type(email: "ash@example.com", name: "x");
            Assert.IsFalse(_view.SubmitEnabled);
            _view.Type(name: "Ash_Mage", confirm: "nope");
            Assert.IsFalse(_view.SubmitEnabled);
        }

        [Test]
        public void InvalidEmailShowsLiveErrorOnlyWhenNotEmpty()
        {
            _view.Type(email: "bad");
            Assert.AreEqual(RegisterPresenter.InvalidEmailMessage, _view.Error);
            _view.Type(email: "");
            Assert.IsNull(_view.Error);
            _view.Type(email: "ash@example.com");
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void SubmitWhileInvalidDoesNothing()
        {
            _view.Type("Ash_Mage", "ash@example.com", "weak", "weak");
            _view.RaiseSubmit();
            Assert.AreEqual(ScreenId.Register, _router.Current);
            Assert.IsNull(_state.PendingEmail);
        }

        [Test]
        public void SuccessfulSubmitStoresPendingEmailAndGoesToVerify()
        {
            FillValid();
            _view.RaiseSubmit();
            Assert.AreEqual(ScreenId.Verify, _router.Current);
            Assert.AreEqual("ash@example.com", _state.PendingEmail);
            Assert.IsTrue(_router.CanGoBack);
        }

        [Test]
        public void AuthFailureShowsErrorAndStays()
        {
            FillValid(FakeAuthService.DemoEmail);
            _view.RaiseSubmit();
            Assert.AreEqual("An account with this email already exists.", _view.Error);
            Assert.AreEqual(ScreenId.Register, _router.Current);
        }

        [Test]
        public void EditingClearsAuthError()
        {
            FillValid(FakeAuthService.DemoEmail);
            _view.RaiseSubmit();
            _view.Type(email: "other@example.com");
            Assert.IsNull(_view.Error);
        }

        [Test]
        public void LoginLinkNavigatesToLogin()
        {
            _view.RaiseLogin();
            Assert.AreEqual(ScreenId.Login, _router.Current);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.RaiseLogin();
            Assert.AreEqual(ScreenId.Register, _router.Current);
        }

        private DeferredAuthService UseDeferredAuth()
        {
            var deferred = new DeferredAuthService();
            _presenter.Dispose();
            _presenter = new RegisterPresenter(_view, _router, deferred, _state);
            FillValid();
            return deferred;
        }

        [Test]
        public void BusyWhileSigningUpAndClearedOnSuccess()
        {
            var auth = UseDeferredAuth();
            _view.RaiseSubmit();
            Assert.IsTrue(_view.Busy);
            auth.CompleteNext(AuthResult.Ok());
            Assert.IsFalse(_view.Busy);
            Assert.AreEqual(ScreenId.Verify, _router.Current);
        }

        [Test]
        public void BusyClearedOnFailure()
        {
            var auth = UseDeferredAuth();
            _view.RaiseSubmit();
            auth.CompleteNext(AuthResult.Fail("taken"));
            Assert.IsFalse(_view.Busy);
            Assert.AreEqual("taken", _view.Error);
        }

        [Test]
        public void SecondSubmitWhileBusyIsIgnored()
        {
            var auth = UseDeferredAuth();
            _view.RaiseSubmit();
            _view.RaiseSubmit();
            Assert.AreEqual(1, auth.Calls);
        }

        [Test]
        public void InvalidFormNeverGoesBusy()
        {
            _view.RaiseSubmit();
            Assert.IsFalse(_view.Busy);
        }
    }
}

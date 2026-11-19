using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class GuestNameCheckTests
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
            public bool Busy;

            public void SetBusy(bool busy) => Busy = busy;
            public void SetName(string name) => Name = name;
            public void SetEnterEnabled(bool enabled) => EnterEnabled = enabled;
            public void ShowError(string message) => Error = message;
            public void ClearError() => Error = null;

            public void Type(string name) { Name = name; NameChanged?.Invoke(); }
            public void RaiseReroll() => RerollRequested?.Invoke();
            public void RaiseEnter() => EnterRequested?.Invoke();
        }

        // Holds every check open until the test answers it.
        private sealed class DeferredNames : INameAvailability
        {
            private readonly Queue<Action<NameAvailability>> _pending = new Queue<Action<NameAvailability>>();

            public readonly List<string> Names = new List<string>();
            public int Pending => _pending.Count;

            public void Check(string name, Action<NameAvailability> done) { Names.Add(name); _pending.Enqueue(done); }
            public void CompleteNext(NameAvailability result) => _pending.Dequeue()(result);
        }

        // Answers each check immediately from a fixed script, then Available.
        private sealed class ScriptedNames : INameAvailability
        {
            private readonly Queue<NameAvailability> _script;

            public readonly List<string> Names = new List<string>();

            public ScriptedNames(params NameAvailability[] script) => _script = new Queue<NameAvailability>(script);

            public void Check(string name, Action<NameAvailability> done)
            {
                Names.Add(name);
                done(_script.Count > 0 ? _script.Dequeue() : NameAvailability.Available);
            }
        }

        private FakeGuestView _view;
        private ScreenRouter _router;
        private SessionState _state;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeGuestView();
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Guest);
            _state = new SessionState();
        }

        private GuestPresenter Create(INameAvailability names) =>
            new GuestPresenter(_view, _router, new FakeAuthService(), _state, new GuestNameGenerator(new Random(5)), names);

        // Builds the presenter and answers its initial name check, so tests start from an idle screen.
        private GuestPresenter CreateIdle(DeferredNames names)
        {
            var presenter = Create(names);
            names.CompleteNext(NameAvailability.Available);
            return presenter;
        }

        [Test]
        public void EnterChecksTheNameAndEntersOnlyWhenAvailable()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();

            Assert.AreEqual("Good_Name", names.Names[names.Names.Count - 1]);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
            Assert.IsNull(_state.Current);

            names.CompleteNext(NameAvailability.Available);
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsTrue(_state.Current.IsGuest);
            Assert.AreEqual("Good_Name", _state.Current.Nickname);
        }

        [Test]
        public void TakenNameShowsAnErrorAndStaysOnTheScreen()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();
            names.CompleteNext(NameAvailability.Taken);

            Assert.AreEqual(GuestPresenter.TakenNameMessage, _view.Error);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
            Assert.IsNull(_state.Current);
        }

        [Test]
        public void InvalidAnswerShowsTheFormatMessage()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();
            names.CompleteNext(NameAvailability.Invalid);

            Assert.AreEqual(GuestPresenter.InvalidNameMessage, _view.Error);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
        }

        [Test]
        public void NetworkErrorShowsAConnectionMessageAndDoesNotEnter()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();
            names.CompleteNext(NameAvailability.NetworkError);

            Assert.AreEqual(GuestPresenter.NetworkErrorMessage, _view.Error);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
            Assert.IsNull(_state.Current);
        }

        [Test]
        public void BusyIsSetWhileCheckingAndClearedOnEveryOutcome()
        {
            foreach (var outcome in new[] { NameAvailability.Available, NameAvailability.Taken, NameAvailability.Invalid, NameAvailability.NetworkError })
            {
                SetUp();
                var names = new DeferredNames();
                CreateIdle(names);
                _view.Type("Good_Name");
                _view.RaiseEnter();
                Assert.IsTrue(_view.Busy, outcome.ToString());
                names.CompleteNext(outcome);
                Assert.IsFalse(_view.Busy, outcome.ToString());
            }
        }

        [Test]
        public void SecondEnterWhileCheckingIsIgnored()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();
            _view.RaiseEnter();
            Assert.AreEqual(1, names.Pending);
        }

        [Test]
        public void EnterWithInvalidNameDoesNotCheck()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.Type("x");
            _view.RaiseEnter();
            Assert.AreEqual(0, names.Pending);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void RerollRetriesUntilACandidateIsFree()
        {
            var names = new ScriptedNames(NameAvailability.Available, NameAvailability.Taken, NameAvailability.Taken, NameAvailability.Available);
            Create(names); // initial check: Available
            _view.RaiseReroll();

            Assert.AreEqual(4, names.Names.Count);
            Assert.AreEqual(names.Names[3], _view.Name);
            Assert.IsNull(_view.Error);
            Assert.IsTrue(_view.EnterEnabled);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void RerollGivesUpAfterFiveTakenCandidates()
        {
            var names = new ScriptedNames(
                NameAvailability.Available,
                NameAvailability.Taken, NameAvailability.Taken, NameAvailability.Taken, NameAvailability.Taken, NameAvailability.Taken);
            Create(names);
            var before = _view.Name;
            _view.RaiseReroll();

            Assert.AreEqual(6, names.Names.Count);
            Assert.AreEqual(GuestPresenter.NoFreeNameMessage, _view.Error);
            Assert.AreEqual(before, _view.Name);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void RerollOnNetworkErrorStopsAndReportsIt()
        {
            var names = new ScriptedNames(NameAvailability.Available, NameAvailability.NetworkError);
            Create(names);
            _view.RaiseReroll();

            Assert.AreEqual(2, names.Names.Count);
            Assert.AreEqual(GuestPresenter.NetworkErrorMessage, _view.Error);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void RerollIsBusyWhileCheckingAndIgnoresAnotherPress()
        {
            var names = new DeferredNames();
            CreateIdle(names);
            _view.RaiseReroll();
            Assert.IsTrue(_view.Busy);
            _view.RaiseReroll();
            Assert.AreEqual(1, names.Pending);
            names.CompleteNext(NameAvailability.Available);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void InitialNameIsCheckedToo()
        {
            var names = new ScriptedNames(NameAvailability.Taken, NameAvailability.Available);
            Create(names);
            Assert.AreEqual(2, names.Names.Count);
            Assert.AreEqual(names.Names[1], _view.Name);
        }

        [Test]
        public void WithoutAnAvailabilityServiceGuestsEnterWithoutACheck()
        {
            Create(null);
            _view.Type("Demo_Wizard");
            _view.RaiseEnter();
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
            Assert.IsFalse(_view.Busy);
        }

        [Test]
        public void FakeAvailabilityRejectsARegisteredName()
        {
            Create(new FakeNameAvailability(new FakeAuthService()));
            _view.Type("demo_wizard");
            _view.RaiseEnter();
            Assert.AreEqual(GuestPresenter.TakenNameMessage, _view.Error);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
        }

        [Test]
        public void AnswerAfterDisposeIsIgnored()
        {
            var names = new DeferredNames();
            var presenter = CreateIdle(names);
            _view.Type("Good_Name");
            _view.RaiseEnter();
            presenter.Dispose();
            names.CompleteNext(NameAvailability.Available);
            Assert.AreEqual(ScreenId.Guest, _router.Current);
        }
    }
}

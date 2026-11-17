using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class LobbyPresenterTests
    {
        private sealed class FakeLobbyView : ILobbyView
        {
            public event Action FindMatchRequested;
            public event Action CancelSearchRequested;
            public event Action LeaderboardRequested;
            public event Action HowToPlayRequested;
            public event Action SettingsRequested;
            public event Action LogOutRequested;

            public PlayerSession Player;
            public bool Searching;
            public string SearchTime;

            public void ShowPlayer(PlayerSession player) => Player = player;
            public void ShowSearching(bool searching) => Searching = searching;
            public void SetSearchTime(string label) => SearchTime = label;

            public void RaiseFind() => FindMatchRequested?.Invoke();
            public void RaiseCancel() => CancelSearchRequested?.Invoke();
            public void RaiseLeaderboard() => LeaderboardRequested?.Invoke();
            public void RaiseHowToPlay() => HowToPlayRequested?.Invoke();
            public void RaiseSettings() => SettingsRequested?.Invoke();
            public void RaiseLogOut() => LogOutRequested?.Invoke();
        }

        private FakeLobbyView _view;
        private ScreenRouter _router;
        private SessionState _state;
        private LobbyPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _router = new ScreenRouter(ScreenId.Title);
            _router.Navigate(ScreenId.Login);
            _router.ResetTo(ScreenId.Lobby);
            _state = new SessionState { Current = new PlayerSession("Ash_Mage", false) };
            _view = new FakeLobbyView();
            _presenter = new LobbyPresenter(_view, _router, _state);
        }

        [Test]
        public void ShowsPlayerAndIdleState()
        {
            Assert.AreSame(_state.Current, _view.Player);
            Assert.IsFalse(_view.Searching);
        }

        [Test]
        public void FindMatchStartsSearchingFromZero()
        {
            _view.RaiseFind();
            Assert.IsTrue(_view.Searching);
            Assert.AreEqual("0:00", _view.SearchTime);
        }

        [Test]
        public void TickAdvancesSearchLabel()
        {
            _view.RaiseFind();
            _presenter.Tick(7.4f);
            Assert.AreEqual("0:07", _view.SearchTime);
        }

        [Test]
        public void TickWhileIdleDoesNotTouchTheLabel()
        {
            _presenter.Tick(5f);
            Assert.IsNull(_view.SearchTime);
        }

        [Test]
        public void CancelReturnsToIdleAndNextSearchRestartsAtZero()
        {
            _view.RaiseFind();
            _presenter.Tick(12f);
            _view.RaiseCancel();
            Assert.IsFalse(_view.Searching);
            _view.RaiseFind();
            Assert.AreEqual("0:00", _view.SearchTime);
        }

        [Test]
        public void LogOutClearsSessionAndBackStackAndGoesToTitle()
        {
            _view.RaiseFind();
            _view.RaiseLogOut();
            Assert.AreEqual(ScreenId.Title, _router.Current);
            Assert.IsFalse(_router.CanGoBack);
            Assert.IsNull(_state.Current);
        }

        [Test]
        public void LeaderboardHowToPlayAndSettingsDoNotNavigate()
        {
            _view.RaiseLeaderboard();
            _view.RaiseHowToPlay();
            _view.RaiseSettings();
            Assert.AreEqual(ScreenId.Lobby, _router.Current);
        }

        [Test]
        public void DisposeStopsReactingToViewEvents()
        {
            _presenter.Dispose();
            _view.RaiseFind();
            Assert.IsFalse(_view.Searching);
        }
    }
}

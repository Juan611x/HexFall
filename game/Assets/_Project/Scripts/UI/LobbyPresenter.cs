using System;

namespace HexFall.UI
{
    public sealed class LobbyPresenter : IDisposable, ITickable
    {
        private readonly ILobbyView _view;
        private readonly ScreenRouter _router;
        private readonly SessionState _session;
        private readonly MatchSearchTimer _timer = new MatchSearchTimer();
        private string _shownTime;

        public LobbyPresenter(ILobbyView view, ScreenRouter router, SessionState session)
        {
            _view = view;
            _router = router;
            _session = session;
            _view.FindMatchRequested += OnFindMatch;
            _view.CancelSearchRequested += OnCancel;
            _view.LogOutRequested += OnLogOut;

            _view.ShowPlayer(_session.Current);
            _view.ShowSearching(false);
        }

        public void Dispose()
        {
            _view.FindMatchRequested -= OnFindMatch;
            _view.CancelSearchRequested -= OnCancel;
            _view.LogOutRequested -= OnLogOut;
        }

        public void Tick(float deltaSeconds)
        {
            if (!_timer.IsSearching) return;
            _timer.Tick(deltaSeconds);
            ShowTime();
        }

        private void ShowTime()
        {
            if (_timer.Label == _shownTime) return;
            _shownTime = _timer.Label;
            _view.SetSearchTime(_shownTime);
        }

        private void OnFindMatch()
        {
            _timer.Start();
            _shownTime = null;
            _view.ShowSearching(true);
            ShowTime();
        }

        private void OnCancel()
        {
            _timer.Cancel();
            _view.ShowSearching(false);
        }

        private void OnLogOut()
        {
            _timer.Cancel();
            _session.Clear();
            _router.ResetTo(ScreenId.Title);
        }
    }
}

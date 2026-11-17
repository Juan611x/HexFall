using System;

namespace HexFall.UI
{
    public sealed class TitlePresenter : IDisposable
    {
        private readonly ITitleView _view;
        private readonly ScreenRouter _router;

        public TitlePresenter(ITitleView view, ScreenRouter router)
        {
            _view = view;
            _router = router;
            _view.LoginRequested += OnLogin;
            _view.CreateAccountRequested += OnCreateAccount;
            _view.GuestRequested += OnGuest;
        }

        public void Dispose()
        {
            _view.LoginRequested -= OnLogin;
            _view.CreateAccountRequested -= OnCreateAccount;
            _view.GuestRequested -= OnGuest;
        }

        private void OnLogin() => _router.Navigate(ScreenId.Login);
        private void OnCreateAccount() => _router.Navigate(ScreenId.Register);
        private void OnGuest() => _router.Navigate(ScreenId.Guest);
    }
}

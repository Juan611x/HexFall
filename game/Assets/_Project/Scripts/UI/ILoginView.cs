using System;

namespace HexFall.UI
{
    public interface ILoginView
    {
        event Action LoginRequested;
        event Action CreateAccountRequested;
        event Action ForgotPasswordRequested;

        string Email { get; }
        string Password { get; }

        void ShowNotice(string message);
        void ShowError(string message);
        void ClearError();
    }
}

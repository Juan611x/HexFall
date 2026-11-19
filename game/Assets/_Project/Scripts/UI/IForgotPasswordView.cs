using System;

namespace HexFall.UI
{
    public enum ForgotPasswordStep { Email, Code }

    public enum ForgotBusy { None, Sending, Resetting, Resending }

    public interface IForgotPasswordView
    {
        // Raised whenever the e-mail, password or confirmation field changes.
        event Action Changed;
        event Action SendCodeRequested;
        event Action<int, string> DigitEntered;
        event Action<int> BackspacePressed;
        event Action ResetRequested;
        event Action ResendRequested;
        event Action ChangeEmailRequested;

        string Email { get; }
        string Password { get; }
        string Confirmation { get; }

        void SetEmail(string email);
        void ShowStep(ForgotPasswordStep step, string email);
        void SetSendEnabled(bool enabled);
        void SetDigits(char?[] digits);
        void FocusDigit(int index);
        void SetPasswordRules(PasswordCheck check);
        void SetResetEnabled(bool enabled);
        void SetResend(string text, bool enabled);
        void ShowError(string message);
        void ClearError();
        // Which request is running; everything else on the form is locked until it is None again.
        void SetBusy(ForgotBusy busy);
    }
}

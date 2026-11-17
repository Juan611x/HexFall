using System;

namespace HexFall.UI
{
    public interface IVerifyView
    {
        event Action<int, string> DigitEntered;
        event Action<int> BackspacePressed;
        event Action SubmitRequested;
        event Action ResendRequested;

        void ShowEmail(string email);
        void SetDigits(char?[] digits);
        void FocusDigit(int index);
        void SetSubmitEnabled(bool enabled);
        void SetResend(string text, bool enabled);
        void ShowError(string message);
        void ClearError();
    }
}

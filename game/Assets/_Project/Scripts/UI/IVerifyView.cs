using System;

namespace HexFall.UI
{
    public enum VerifyBusy { None, Verifying, Resending }

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
        // Which request is running; everything else on the form is locked until it is None again.
        void SetBusy(VerifyBusy busy);
    }
}

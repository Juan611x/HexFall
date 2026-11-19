using System;

namespace HexFall.UI
{
    public enum HelpTone { Neutral, Invalid, Valid }

    public interface IRegisterView
    {
        // Raised whenever any field changes.
        event Action Changed;
        event Action SubmitRequested;
        event Action LoginRequested;

        string WizardName { get; }
        string Email { get; }
        string Password { get; }
        string Confirmation { get; }

        void SetNameHelp(string text, HelpTone tone);
        void SetPasswordRules(PasswordCheck check);
        void SetSubmitEnabled(bool enabled);
        void ShowError(string message);
        void ClearError();
        // While busy the form shows a spinner and accepts no further input.
        void SetBusy(bool busy);
    }
}

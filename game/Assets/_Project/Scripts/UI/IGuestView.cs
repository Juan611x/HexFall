using System;

namespace HexFall.UI
{
    public interface IGuestView
    {
        event Action NameChanged;
        event Action RerollRequested;
        event Action EnterRequested;
        event Action CreateAccountRequested;

        string Name { get; }

        void SetName(string name);
        void SetEnterEnabled(bool enabled);
        void ShowError(string message);
        void ClearError();
    }
}

using System;

namespace HexFall.UI
{
    public interface ITitleView
    {
        event Action LoginRequested;
        event Action CreateAccountRequested;
        event Action GuestRequested;
        event Action SettingsRequested;
        event Action CreditsRequested;
    }
}

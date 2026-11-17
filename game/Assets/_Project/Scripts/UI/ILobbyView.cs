using System;

namespace HexFall.UI
{
    public interface ILobbyView
    {
        event Action FindMatchRequested;
        event Action CancelSearchRequested;
        event Action LeaderboardRequested;
        event Action HowToPlayRequested;
        event Action SettingsRequested;
        event Action LogOutRequested;

        void ShowPlayer(PlayerSession player);
        void ShowSearching(bool searching);
        void SetSearchTime(string label);
    }
}

using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class LobbyView : ILobbyView
    {
        private const string BrightColor = "#f5f3ff";

        private readonly Label _name;
        private readonly Label _guestTag;
        private readonly Label _stats;
        private readonly Button _findMatch;
        private readonly VisualElement _searching;
        private readonly VisualElement _blink;
        private readonly Label _searchLabel;
        private readonly IVisualElementScheduledItem _blinker;

        public event Action FindMatchRequested;
        public event Action CancelSearchRequested;
        public event Action LeaderboardRequested;
        public event Action HowToPlayRequested;
        public event Action SettingsRequested;
        public event Action LogOutRequested;

        public LobbyView(VisualElement root)
        {
            _name = root.Q<Label>("player-name");
            _guestTag = root.Q<Label>("guest-tag");
            _stats = root.Q<Label>("player-stats");
            _findMatch = root.Q<Button>("find-match-button");
            _searching = root.Q<VisualElement>("searching-block");
            _blink = root.Q<VisualElement>("blink");
            _searchLabel = root.Q<Label>("search-label");

            _findMatch.clicked += () => FindMatchRequested?.Invoke();
            root.Q<Button>("cancel-search-button").clicked += () => CancelSearchRequested?.Invoke();
            root.Q<Button>("leaderboard-button").clicked += () => LeaderboardRequested?.Invoke();
            root.Q<Button>("how-to-play-button").clicked += () => HowToPlayRequested?.Invoke();
            root.Q<Button>("settings-button").clicked += () => SettingsRequested?.Invoke();
            root.Q<Button>("logout-button").clicked += () => LogOutRequested?.Invoke();

            _blinker = _blink.schedule.Execute(() => _blink.style.opacity = _blink.resolvedStyle.opacity > 0.5f ? 0.2f : 1f).Every(450);
            _blinker.Pause();
        }

        public void ShowPlayer(PlayerSession player)
        {
            _name.text = player.Nickname;
            _guestTag.SetVisible(player.IsGuest);
            _stats.text = $"Matches {Bright(player.Matches)} · Wins {Bright(player.Wins)} · Eliminations {Bright(player.Eliminations)}";
        }

        public void ShowSearching(bool searching)
        {
            _findMatch.SetVisible(!searching);
            _searching.SetVisible(searching);
            if (searching)
            {
                _blink.style.opacity = 1f;
                _blinker.Resume();
            }
            else
            {
                _blinker.Pause();
            }
        }

        public void SetSearchTime(string label) => _searchLabel.text = $"SEARCHING {label}";

        private static string Bright(int value) => $"<color={BrightColor}>{value}</color>";
    }
}

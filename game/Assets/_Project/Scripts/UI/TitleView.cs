using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class TitleView : ITitleView
    {
        public event Action LoginRequested;
        public event Action CreateAccountRequested;
        public event Action GuestRequested;
        public event Action SettingsRequested;
        public event Action CreditsRequested;

        public TitleView(VisualElement root)
        {
            root.Q<Button>("login-button").clicked += () => LoginRequested?.Invoke();
            root.Q<Button>("create-account-button").clicked += () => CreateAccountRequested?.Invoke();
            root.Q<Button>("guest-button").clicked += () => GuestRequested?.Invoke();
            root.Q<Button>("settings-link").clicked += () => SettingsRequested?.Invoke();
            root.Q<Button>("credits-link").clicked += () => CreditsRequested?.Invoke();
        }
    }
}

using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class GuestView : IGuestView
    {
        private readonly TextField _name;
        private readonly Label _error;
        private readonly Button _enter;

        public event Action NameChanged;
        public event Action RerollRequested;
        public event Action EnterRequested;
        public event Action CreateAccountRequested;

        public GuestView(VisualElement root)
        {
            _name = root.Q<TextField>("name-field");
            _error = root.Q<Label>("error-label");
            _enter = root.Q<Button>("enter-button");

            _name.RegisterValueChangedCallback(_ => NameChanged?.Invoke());
            _name.OnEnter(() => EnterRequested?.Invoke());
            root.Q<Button>("reroll-button").clicked += () => RerollRequested?.Invoke();
            _enter.clicked += () => EnterRequested?.Invoke();
            root.Q<Button>("create-account-link").clicked += () => CreateAccountRequested?.Invoke();
        }

        public string Name => _name.value;

        public void SetName(string name) => _name.SetValueWithoutNotify(name);
        public void SetEnterEnabled(bool enabled) => _enter.SetEnabled(enabled);
        public void ShowError(string message) => _error.ShowText(message);
        public void ClearError() => _error.HideText();
    }
}

using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class GuestView : IGuestView
    {
        private readonly TextField _name;
        private readonly Label _error;
        private readonly Button _enter;
        private readonly Button _reroll;
        private readonly Button _create;
        private readonly BusyButton _enterBusy;
        private bool _enterEnabled;
        private bool _busy;

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
            _reroll = root.Q<Button>("reroll-button");
            _create = root.Q<Button>("create-account-link");
            _enterBusy = new BusyButton(_enter, "CHECKING...");
            _reroll.clicked += () => RerollRequested?.Invoke();
            _enter.clicked += () => EnterRequested?.Invoke();
            _create.clicked += () => CreateAccountRequested?.Invoke();
        }

        public string Name => _name.value;

        public void SetName(string name) => _name.SetValueWithoutNotify(name);
        public void SetEnterEnabled(bool enabled)
        {
            _enterEnabled = enabled;
            _enter.SetEnabled(enabled && !_busy);
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            _enterBusy.SetBusy(busy);
            _enter.SetEnabled(_enterEnabled && !busy);
            foreach (var element in new VisualElement[] { _reroll, _create, _name })
                element.SetEnabled(!busy);
        }
        public void ShowError(string message) => _error.ShowText(message);
        public void ClearError() => _error.HideText();
    }
}

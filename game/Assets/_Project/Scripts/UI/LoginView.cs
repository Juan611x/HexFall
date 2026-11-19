using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class LoginView : ILoginView
    {
        private readonly TextField _email;
        private readonly TextField _password;
        private readonly Label _error;
        private readonly Label _notice;
        private readonly Button _toggle;
        private readonly IconElement _icon;

        public event Action LoginRequested;
        public event Action CreateAccountRequested;
        public event Action ForgotPasswordRequested;

        public LoginView(VisualElement root)
        {
            _email = root.Q<TextField>("email-field");
            _password = root.Q<TextField>("password-field");
            _error = root.Q<Label>("error-label");
            _notice = root.Q<Label>("notice-label");
            _toggle = root.Q<Button>("password-toggle");
            _icon = root.Q<IconElement>("password-icon");

            root.Q<Button>("login-submit").clicked += () => LoginRequested?.Invoke();
            root.Q<Button>("create-account-link").clicked += () => CreateAccountRequested?.Invoke();
            root.Q<Button>("forgot-link").clicked += () => ForgotPasswordRequested?.Invoke();
            _toggle.clicked += TogglePasswordVisibility;
            _email.OnEnter(() => LoginRequested?.Invoke());
            _password.OnEnter(() => LoginRequested?.Invoke());
        }

        public string Email => _email.value;
        public string Password => _password.value;

        public void ShowNotice(string message) => _notice.ShowText(message);
        public void ShowError(string message) => _error.ShowText(message);
        public void ClearError()
        {
            _error.HideText();
            _notice.HideText();
        }

        private void TogglePasswordVisibility()
        {
            _password.isPasswordField = !_password.isPasswordField;
            _icon.kind = _password.isPasswordField ? IconKind.Eye : IconKind.EyeOff;
            _toggle.tooltip = _password.isPasswordField ? "Show password" : "Hide password";
        }
    }
}

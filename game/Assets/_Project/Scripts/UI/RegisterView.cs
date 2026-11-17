using System;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class RegisterView : IRegisterView
    {
        private readonly TextField _name;
        private readonly TextField _email;
        private readonly TextField _password;
        private readonly TextField _confirm;
        private readonly Label _nameHelp;
        private readonly Label _error;
        private readonly Button _submit;
        private readonly VisualElement _root;

        public event Action Changed;
        public event Action SubmitRequested;
        public event Action LoginRequested;

        public RegisterView(VisualElement root)
        {
            _root = root;
            _name = root.Q<TextField>("name-field");
            _email = root.Q<TextField>("email-field");
            _password = root.Q<TextField>("password-field");
            _confirm = root.Q<TextField>("confirm-field");
            _nameHelp = root.Q<Label>("name-help");
            _error = root.Q<Label>("error-label");
            _submit = root.Q<Button>("register-submit");

            foreach (var field in new[] { _name, _email, _password, _confirm })
                field.RegisterValueChangedCallback(_ => Changed?.Invoke());
            _submit.clicked += () => SubmitRequested?.Invoke();
            root.Q<Button>("login-link").clicked += () => LoginRequested?.Invoke();
        }

        public string WizardName => _name.value;
        public string Email => _email.value;
        public string Password => _password.value;
        public string Confirmation => _confirm.value;

        public void SetNameHelp(string text, HelpTone tone)
        {
            _nameHelp.text = text;
            _nameHelp.EnableInClassList("hf-help--neutral", tone == HelpTone.Neutral);
            _nameHelp.EnableInClassList("hf-help--invalid", tone == HelpTone.Invalid);
            _nameHelp.EnableInClassList("hf-help--valid", tone == HelpTone.Valid);
        }

        public void SetPasswordRules(PasswordCheck check)
        {
            SetRule("rule-length", check.MinLength);
            SetRule("rule-upper", check.HasUpper);
            SetRule("rule-lower", check.HasLower);
            SetRule("rule-digit", check.HasDigit);
            SetRule("rule-symbol", check.HasSymbol);
            SetRule("rule-match", check.Matches);
        }

        public void SetSubmitEnabled(bool enabled) => _submit.SetEnabled(enabled);
        public void ShowError(string message) => _error.ShowText(message);
        public void ClearError() => _error.HideText();

        private void SetRule(string name, bool met) => _root.Q<VisualElement>(name).EnableInClassList("hf-rule--met", met);
    }
}

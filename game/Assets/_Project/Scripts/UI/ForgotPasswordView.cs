using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class ForgotPasswordView : IForgotPasswordView
    {
        private readonly VisualElement _root;
        private readonly VisualElement _emailStep;
        private readonly VisualElement _codeStep;
        private readonly Label _subtitle;
        private readonly TextField _email;
        private readonly TextField _password;
        private readonly TextField _confirm;
        private readonly TextField[] _boxes = new TextField[VerificationCodeInput.Length];
        private readonly Button _send;
        private readonly Button _reset;
        private readonly Button _resend;
        private readonly Label _emailError;
        private readonly Label _codeError;
        private ForgotPasswordStep _step = ForgotPasswordStep.Email;

        public event Action Changed;
        public event Action SendCodeRequested;
        public event Action<int, string> DigitEntered;
        public event Action<int> BackspacePressed;
        public event Action ResetRequested;
        public event Action ResendRequested;
        public event Action ChangeEmailRequested;

        public ForgotPasswordView(VisualElement root)
        {
            _root = root;
            _emailStep = root.Q<VisualElement>("email-step");
            _codeStep = root.Q<VisualElement>("code-step");
            _subtitle = root.Q<Label>("forgot-subtitle");
            _email = root.Q<TextField>("email-field");
            _password = root.Q<TextField>("password-field");
            _confirm = root.Q<TextField>("confirm-field");
            _send = root.Q<Button>("send-code");
            _reset = root.Q<Button>("reset-submit");
            _resend = root.Q<Button>("resend-button");
            _emailError = root.Q<Label>("email-error");
            _codeError = root.Q<Label>("code-error");

            foreach (var field in new[] { _email, _password, _confirm })
                field.RegisterValueChangedCallback(_ => Changed?.Invoke());
            _email.OnEnter(() => SendCodeRequested?.Invoke());
            _send.clicked += () => SendCodeRequested?.Invoke();
            _reset.clicked += () => ResetRequested?.Invoke();
            _resend.clicked += () => ResendRequested?.Invoke();
            root.Q<Button>("change-email-link").clicked += () => ChangeEmailRequested?.Invoke();

            for (var i = 0; i < _boxes.Length; i++)
            {
                var index = i;
                var box = root.Q<TextField>($"digit-{i}");
                _boxes[i] = box;
                box.RegisterValueChangedCallback(evt => DigitEntered?.Invoke(index, evt.newValue));
                box.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Backspace && string.IsNullOrEmpty(box.value)) BackspacePressed?.Invoke(index);
                });
            }
        }

        public string Email => _email.value;
        public string Password => _password.value;
        public string Confirmation => _confirm.value;

        public void SetEmail(string email) => _email.SetValueWithoutNotify(email ?? string.Empty);

        public void ShowStep(ForgotPasswordStep step, string email)
        {
            _step = step;
            _emailStep.SetVisible(step == ForgotPasswordStep.Email);
            _codeStep.SetVisible(step == ForgotPasswordStep.Code);
            _subtitle.text = step == ForgotPasswordStep.Email
                ? "Enter your email and we will send you a 6-digit code."
                : $"We sent a 6-digit code to {email}. Enter it with your new password.";
        }

        public void SetSendEnabled(bool enabled) => _send.SetEnabled(enabled);

        public void SetDigits(char?[] digits)
        {
            for (var i = 0; i < _boxes.Length; i++)
                _boxes[i].SetValueWithoutNotify(digits[i].HasValue ? digits[i].Value.ToString() : string.Empty);
        }

        // Deferred so the key event that produced the digit is not delivered to the next box.
        public void FocusDigit(int index) => _boxes[index].schedule.Execute(() => _boxes[index].Focus());

        public void SetPasswordRules(PasswordCheck check)
        {
            SetRule("rule-length", check.MinLength);
            SetRule("rule-upper", check.HasUpper);
            SetRule("rule-lower", check.HasLower);
            SetRule("rule-digit", check.HasDigit);
            SetRule("rule-symbol", check.HasSymbol);
            SetRule("rule-match", check.Matches);
        }

        public void SetResetEnabled(bool enabled) => _reset.SetEnabled(enabled);

        public void SetResend(string text, bool enabled)
        {
            _resend.text = text;
            _resend.SetEnabled(enabled);
        }

        public void ShowError(string message) => ActiveError.ShowText(message);

        public void ClearError()
        {
            _emailError.HideText();
            _codeError.HideText();
        }

        private Label ActiveError => _step == ForgotPasswordStep.Email ? _emailError : _codeError;

        private void SetRule(string name, bool met) => _root.Q<VisualElement>(name).EnableInClassList("hf-rule--met", met);
    }
}

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HexFall.UI
{
    public sealed class VerifyView : IVerifyView
    {
        private readonly TextField[] _boxes = new TextField[VerificationCodeInput.Length];
        private readonly Label _subtitle;
        private readonly Label _error;
        private readonly Button _submit;
        private readonly Button _resend;

        public event Action<int, string> DigitEntered;
        public event Action<int> BackspacePressed;
        public event Action SubmitRequested;
        public event Action ResendRequested;

        public VerifyView(VisualElement root)
        {
            _subtitle = root.Q<Label>("verify-subtitle");
            _error = root.Q<Label>("error-label");
            _submit = root.Q<Button>("verify-submit");
            _resend = root.Q<Button>("resend-button");

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

            _submit.clicked += () => SubmitRequested?.Invoke();
            _resend.clicked += () => ResendRequested?.Invoke();
        }

        public void ShowEmail(string email) => _subtitle.text = $"We sent a 6-digit code to {email}.";

        public void SetDigits(char?[] digits)
        {
            for (var i = 0; i < _boxes.Length; i++)
                _boxes[i].SetValueWithoutNotify(digits[i].HasValue ? digits[i].Value.ToString() : string.Empty);
        }

        // Deferred so the key event that produced the digit is not delivered to the next box.
        public void FocusDigit(int index) => _boxes[index].schedule.Execute(() => _boxes[index].Focus());

        public void SetSubmitEnabled(bool enabled) => _submit.SetEnabled(enabled);

        public void SetResend(string text, bool enabled)
        {
            _resend.text = text;
            _resend.SetEnabled(enabled);
        }

        public void ShowError(string message) => _error.ShowText(message);
        public void ClearError() => _error.HideText();
    }
}

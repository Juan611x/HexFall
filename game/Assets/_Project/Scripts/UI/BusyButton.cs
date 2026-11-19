using UnityEngine.UIElements;

namespace HexFall.UI
{
    // Swaps a button's label for a progress label and shows a spinner while a request runs.
    // Enabled/disabled stays with the owning view; this only handles the look of the busy state.
    internal sealed class BusyButton
    {
        private readonly Button _button;
        private readonly string _busyText;
        private readonly SpinnerElement _spinner;
        private string _idleText;
        private bool _busy;

        public BusyButton(Button button, string busyText, bool withSpinner = true)
        {
            _button = button;
            _busyText = busyText;
            if (!withSpinner) return;
            _spinner = new SpinnerElement();
            _spinner.AddToClassList("hf-spinner");
            _spinner.SetSpinning(false);
            button.Add(_spinner);
        }

        public void SetBusy(bool busy)
        {
            if (busy == _busy) return;
            _busy = busy;
            if (busy)
            {
                _idleText = _button.text;
                // Keep the button as wide as it was so the longer progress label does not make the layout jump.
                var width = _button.resolvedStyle.width;
                if (width > 0f) _button.style.minWidth = width;
                _button.text = _busyText;
            }
            else
            {
                _button.text = _idleText;
                _button.style.minWidth = StyleKeyword.Null;
            }

            _button.EnableInClassList("hf-btn--busy", busy);
            _spinner?.SetSpinning(busy);
        }
    }
}

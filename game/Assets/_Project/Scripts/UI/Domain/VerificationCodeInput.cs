using System.Text;

namespace HexFall.UI
{
    public sealed class VerificationCodeInput
    {
        public const int Length = 6;

        private readonly char?[] _digits = new char?[Length];

        public char? DigitAt(int index) => _digits[index];
        public bool IsComplete
        {
            get
            {
                foreach (var d in _digits) if (!d.HasValue) return false;
                return true;
            }
        }

        public string Code
        {
            get
            {
                var sb = new StringBuilder(Length);
                foreach (var d in _digits) if (d.HasValue) sb.Append(d.Value);
                return sb.ToString();
            }
        }

        // Digits in `text` fill boxes starting at `index` (a paste fills all); returns the box to focus next.
        public int Type(int index, string text)
        {
            var count = 0;
            if (text != null)
            {
                foreach (var c in text)
                {
                    if (c < '0' || c > '9' || index + count >= Length) continue;
                    _digits[index + count] = c;
                    count++;
                }
            }

            if (count == 0)
            {
                if (string.IsNullOrEmpty(text)) _digits[index] = null;
                return index;
            }

            return System.Math.Min(index + count, Length - 1);
        }

        public int Backspace(int index)
        {
            if (_digits[index].HasValue)
            {
                _digits[index] = null;
                return index;
            }

            if (index == 0) return 0;
            _digits[index - 1] = null;
            return index - 1;
        }

        public void Clear()
        {
            for (var i = 0; i < Length; i++) _digits[i] = null;
        }
    }
}

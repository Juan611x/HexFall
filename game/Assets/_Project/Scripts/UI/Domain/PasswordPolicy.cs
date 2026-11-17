namespace HexFall.UI
{
    public readonly struct PasswordCheck
    {
        public PasswordCheck(bool minLength, bool hasUpper, bool hasLower, bool hasDigit, bool hasSymbol, bool matches)
        {
            MinLength = minLength; HasUpper = hasUpper; HasLower = hasLower;
            HasDigit = hasDigit; HasSymbol = hasSymbol; Matches = matches;
        }

        public bool MinLength { get; }
        public bool HasUpper { get; }
        public bool HasLower { get; }
        public bool HasDigit { get; }
        public bool HasSymbol { get; }
        public bool Matches { get; }
        public bool AllMet => MinLength && HasUpper && HasLower && HasDigit && HasSymbol && Matches;
    }

    public static class PasswordPolicy
    {
        public const int MinLength = 8;

        public static PasswordCheck Evaluate(string password, string confirmation)
        {
            password = password ?? string.Empty;
            var upper = false; var lower = false; var digit = false; var symbol = false;
            foreach (var c in password)
            {
                if (c >= 'A' && c <= 'Z') upper = true;
                else if (c >= 'a' && c <= 'z') lower = true;
                else if (c >= '0' && c <= '9') digit = true;
                else if (!char.IsWhiteSpace(c)) symbol = true;
            }

            return new PasswordCheck(password.Length >= MinLength, upper, lower, digit, symbol, password.Length > 0 && password == confirmation);
        }
    }
}

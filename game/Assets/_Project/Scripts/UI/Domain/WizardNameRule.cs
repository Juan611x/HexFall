namespace HexFall.UI
{
    public enum NameCheck { Empty, Invalid, Valid }

    public static class WizardNameRule
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        public static NameCheck Evaluate(string name)
        {
            if (string.IsNullOrEmpty(name)) return NameCheck.Empty;
            if (name.Length < MinLength || name.Length > MaxLength) return NameCheck.Invalid;
            foreach (var c in name)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return NameCheck.Invalid;
            }
            return NameCheck.Valid;
        }

        public static bool IsValid(string name) => Evaluate(name) == NameCheck.Valid;
    }
}

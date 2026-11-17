namespace HexFall.UI
{
    public static class EmailRule
    {
        // Shape check only (local@domain.tld); real validation is the auth backend's job.
        public static bool IsValid(string email)
        {
            if (string.IsNullOrEmpty(email) || email.IndexOf(' ') >= 0) return false;
            var at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@')) return false;
            var domain = email.Substring(at + 1);
            var dot = domain.LastIndexOf('.');
            return dot > 0 && dot < domain.Length - 1;
        }
    }
}

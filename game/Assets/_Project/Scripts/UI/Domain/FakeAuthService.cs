using System;
using System.Collections.Generic;

namespace HexFall.UI
{
    public sealed class FakeAuthService : IAuthService
    {
        public const string ValidCode = "123456";
        public const string DemoEmail = "wizard@example.com";
        public const string DemoPassword = "Arena#2026";

        private sealed class Account
        {
            public string Name;
            public string Password;
            public bool Verified;
        }

        private readonly Dictionary<string, Account> _accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase)
        {
            { DemoEmail, new Account { Name = "Demo_Wizard", Password = DemoPassword, Verified = true } },
        };

        public void SignUp(string name, string email, string password, Action<AuthResult> done)
        {
            if (_accounts.ContainsKey(email))
            {
                done(AuthResult.Fail("An account with this email already exists."));
                return;
            }

            _accounts[email] = new Account { Name = name, Password = password };
            done(AuthResult.Ok());
        }

        public void Verify(string email, string code, Action<AuthResult> done)
        {
            if (!_accounts.TryGetValue(email, out var account) || account.Verified)
            {
                done(AuthResult.Fail("No pending verification for this email."));
                return;
            }

            if (code != ValidCode)
            {
                done(AuthResult.Fail("That code is not valid. Try again."));
                return;
            }

            account.Verified = true;
            done(AuthResult.Ok(new PlayerSession(account.Name, false)));
        }

        public void ResendCode(string email, Action<AuthResult> done)
        {
            if (_accounts.TryGetValue(email, out var account) && !account.Verified) done(AuthResult.Ok());
            else done(AuthResult.Fail("No pending verification for this email."));
        }

        public void LogIn(string email, string password, Action<AuthResult> done)
        {
            if (_accounts.TryGetValue(email, out var account) && account.Verified && account.Password == password)
                done(AuthResult.Ok(new PlayerSession(account.Name, false)));
            else
                done(AuthResult.Fail("Incorrect email or password."));
        }

        public void Guest(string name, Action<AuthResult> done)
        {
            if (WizardNameRule.IsValid(name)) done(AuthResult.Ok(new PlayerSession(name, true)));
            else done(AuthResult.Fail("Use 3–16 letters, numbers or _ only."));
        }
    }
}

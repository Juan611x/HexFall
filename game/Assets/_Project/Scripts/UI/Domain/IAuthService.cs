using System;

namespace HexFall.UI
{
    public readonly struct AuthResult
    {
        private AuthResult(bool success, string error, PlayerSession session)
        {
            Success = success; Error = error; Session = session;
        }

        public bool Success { get; }
        public string Error { get; }
        public PlayerSession Session { get; }

        public static AuthResult Ok(PlayerSession session = null) => new AuthResult(true, null, session);
        public static AuthResult Fail(string error) => new AuthResult(false, error, null);
    }

    // Callback-shaped so an asynchronous (Cognito) adapter can replace the in-memory fake.
    public interface IAuthService
    {
        // Success means the account awaits e-mail verification.
        void SignUp(string name, string email, string password, Action<AuthResult> done);
        void Verify(string email, string code, Action<AuthResult> done);
        void ResendCode(string email, Action<AuthResult> done);
        void LogIn(string email, string password, Action<AuthResult> done);
        void Guest(string name, Action<AuthResult> done);
    }
}

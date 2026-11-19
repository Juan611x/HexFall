using System;

namespace HexFall.UI
{
    public readonly struct AuthResult
    {
        private AuthResult(bool success, string error, PlayerSession session, bool needsVerification = false)
        {
            Success = success; Error = error; Session = session; NeedsVerification = needsVerification;
        }

        public bool Success { get; }
        public string Error { get; }
        public PlayerSession Session { get; }
        // A failure kind: the account exists but its email is not verified yet.
        public bool NeedsVerification { get; }

        public static AuthResult Ok(PlayerSession session = null) => new AuthResult(true, null, session);
        public static AuthResult Fail(string error) => new AuthResult(false, error, null);
        public static AuthResult Unverified(string error) => new AuthResult(false, error, null, true);
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
        // Signs in again from a saved session, if there is one. Fails (without a network call) when none is saved.
        void Restore(Action<AuthResult> done);
        // Forgets the session locally and invalidates it on the server; `done` runs either way.
        void LogOut(Action done);
    }
}

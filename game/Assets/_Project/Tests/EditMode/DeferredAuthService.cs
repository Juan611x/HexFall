using System;
using System.Collections.Generic;

namespace HexFall.UI.Tests
{
    // Holds every request open until the test completes it, like the asynchronous Cognito adapter.
    internal sealed class DeferredAuthService : IAuthService
    {
        private readonly Queue<Action<AuthResult>> _pending = new Queue<Action<AuthResult>>();

        public int Calls { get; private set; }
        public int Pending => _pending.Count;

        public void CompleteNext(AuthResult result) => _pending.Dequeue()(result);

        private void Hold(Action<AuthResult> done)
        {
            Calls++;
            _pending.Enqueue(done);
        }

        public void SignUp(string name, string email, string password, Action<AuthResult> done) => Hold(done);
        public void Verify(string email, string code, Action<AuthResult> done) => Hold(done);
        public void ResendCode(string email, Action<AuthResult> done) => Hold(done);
        public void LogIn(string email, string password, Action<AuthResult> done) => Hold(done);
        public void Guest(string name, Action<AuthResult> done) => Hold(done);
        public void RequestPasswordReset(string email, Action<AuthResult> done) => Hold(done);
        public void ConfirmPasswordReset(string email, string code, string newPassword, Action<AuthResult> done) => Hold(done);
        public void Restore(Action<AuthResult> done) => Hold(done);
        public void LogOut(Action done) => done();
    }
}

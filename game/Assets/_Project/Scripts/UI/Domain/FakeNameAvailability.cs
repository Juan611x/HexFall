using System;

namespace HexFall.UI
{
    // Editor and offline stand-in: a name is taken when the in-memory fake auth has an account with it.
    public sealed class FakeNameAvailability : INameAvailability
    {
        private readonly FakeAuthService _auth;

        public FakeNameAvailability(FakeAuthService auth) => _auth = auth;

        public void Check(string name, Action<NameAvailability> done)
        {
            if (!WizardNameRule.IsValid(name)) done(NameAvailability.Invalid);
            else done(_auth.HasName(name) ? NameAvailability.Taken : NameAvailability.Available);
        }
    }
}

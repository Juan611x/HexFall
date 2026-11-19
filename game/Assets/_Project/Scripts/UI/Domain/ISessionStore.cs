namespace HexFall.UI
{
    // Where the signed-in player's refresh token survives a page reload. ID/access tokens are never stored.
    public interface ISessionStore
    {
        void Save(string email, string refreshToken);
        bool TryLoad(out string email, out string refreshToken);
        void Clear();
    }

    // Volatile store: the default for adapters without persistence, and the test double.
    public sealed class InMemorySessionStore : ISessionStore
    {
        private string _email;
        private string _refreshToken;

        public void Save(string email, string refreshToken)
        {
            _email = email;
            _refreshToken = refreshToken;
        }

        public bool TryLoad(out string email, out string refreshToken)
        {
            email = _email;
            refreshToken = _refreshToken;
            return !string.IsNullOrEmpty(_refreshToken);
        }

        public void Clear()
        {
            _email = null;
            _refreshToken = null;
        }
    }
}

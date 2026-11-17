namespace HexFall.UI
{
    public sealed class PlayerSession
    {
        public PlayerSession(string nickname, bool isGuest)
        {
            Nickname = nickname;
            IsGuest = isGuest;
        }

        public string Nickname { get; }
        public bool IsGuest { get; }
        public int Matches { get; }
        public int Wins { get; }
        public int Eliminations { get; }
    }

    // Shared mutable app state: who is signed in and which e-mail awaits verification.
    public sealed class SessionState
    {
        public PlayerSession Current { get; set; }
        public string PendingEmail { get; set; }
        public void Clear() { Current = null; PendingEmail = null; }
    }
}

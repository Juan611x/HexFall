namespace HexFall.UI
{
    // Which screen the menu opens on, given the outcome of restoring a saved session.
    public static class StartupDecision
    {
        public static ScreenId InitialScreen(AuthResult restore) =>
            restore.Success && restore.Session != null && !restore.Session.IsGuest ? ScreenId.Lobby : ScreenId.Title;
    }
}

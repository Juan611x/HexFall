using UnityEngine;

namespace HexFall.UI
{
    // PlayerPrefs is backed by IndexedDB on WebGL, so the session survives page reloads. Unity glue, covered via ISessionStore fakes.
    public sealed class PlayerPrefsSessionStore : ISessionStore
    {
        private const string EmailKey = "hexfall.session.email";
        private const string RefreshKey = "hexfall.session.refresh";

        public void Save(string email, string refreshToken)
        {
            PlayerPrefs.SetString(EmailKey, email ?? string.Empty);
            PlayerPrefs.SetString(RefreshKey, refreshToken ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool TryLoad(out string email, out string refreshToken)
        {
            email = PlayerPrefs.GetString(EmailKey, string.Empty);
            refreshToken = PlayerPrefs.GetString(RefreshKey, string.Empty);
            return !string.IsNullOrEmpty(refreshToken);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(EmailKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.Save();
        }
    }
}

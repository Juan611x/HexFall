using System;
using System.Text;
using UnityEngine;

namespace HexFall.UI
{
    // Reads display claims from an ID token. The signature is not validated: the client only
    // uses the claims to show a name, and the server must verify tokens itself.
    public static class JwtPayload
    {
        [Serializable]
        private sealed class Claims
        {
            public string preferred_username;
            public string email;
        }

        public static bool TryGetNickname(string idToken, out string nickname)
        {
            nickname = null;
            if (string.IsNullOrEmpty(idToken)) return false;

            var parts = idToken.Split('.');
            if (parts.Length < 2) return false;

            try
            {
                var json = Encoding.UTF8.GetString(DecodeBase64Url(parts[1]));
                var claims = JsonUtility.FromJson<Claims>(json);
                var name = !string.IsNullOrEmpty(claims?.preferred_username) ? claims.preferred_username : claims?.email;
                if (string.IsNullOrEmpty(name)) return false;
                nickname = name;
                return true;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException)
            {
                return false;
            }
        }

        private static byte[] DecodeBase64Url(string segment)
        {
            var s = segment.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }

            return Convert.FromBase64String(s);
        }
    }
}

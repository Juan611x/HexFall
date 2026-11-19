using System;
using UnityEngine;

namespace HexFall.UI
{
    // URL and response mapping of the name check endpoint (GET <base>/names/<name>), kept free of
    // UnityWebRequest so it can be tested. See DD-021.
    public static class NameCheckWire
    {
        [Serializable]
        private sealed class Body
        {
            public bool available;
        }

        // The Function URL ends with a slash, a hand-written base may not.
        public static string UrlFor(string baseUrl, string name) =>
            baseUrl.TrimEnd('/') + "/names/" + Uri.EscapeDataString(name);

        public static NameAvailability Parse(long statusCode, string body, bool networkError)
        {
            if (networkError) return NameAvailability.NetworkError;
            if (statusCode == 400) return NameAvailability.Invalid;
            if (statusCode != 200 || string.IsNullOrEmpty(body) || !body.Contains("\"available\""))
                return NameAvailability.NetworkError;

            try
            {
                return JsonUtility.FromJson<Body>(body).available ? NameAvailability.Available : NameAvailability.Taken;
            }
            catch (ArgumentException)
            {
                return NameAvailability.NetworkError;
            }
        }
    }
}

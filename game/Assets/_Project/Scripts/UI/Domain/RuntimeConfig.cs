using System;
using UnityEngine;

namespace HexFall.UI
{
    // Deployment settings the page serves as config.json next to index.html (see tools/deploy_webgl.py).
    public sealed class RuntimeConfig
    {
        public const string FileName = "config.json";

        [Serializable]
        private sealed class CognitoBody
        {
            public string userPoolId;
            public string clientId;
        }

        [Serializable]
        private sealed class Body
        {
            public string environment;
            public string region;
            public string apiBaseUrl;
            public string realtimeUrl;
            public CognitoBody cognito;
        }

        public static readonly RuntimeConfig Empty = new RuntimeConfig(null);

        private RuntimeConfig(Body body)
        {
            Environment = Clean(body?.environment);
            Region = Clean(body?.region);
            ApiBaseUrl = Clean(body?.apiBaseUrl);
            RealtimeUrl = Clean(body?.realtimeUrl);
            CognitoUserPoolId = Clean(body?.cognito?.userPoolId);
            CognitoClientId = Clean(body?.cognito?.clientId);
        }

        public string Environment { get; }
        public string Region { get; }
        public string ApiBaseUrl { get; }
        public string RealtimeUrl { get; }
        public string CognitoUserPoolId { get; }
        public string CognitoClientId { get; }

        public bool HasCognito =>
            Region.Length > 0 && CognitoUserPoolId.Length > 0 && CognitoClientId.Length > 0;

        // Missing or malformed JSON yields the empty config, so the game falls back to the fake services.
        public static RuntimeConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Empty;
            try
            {
                return new RuntimeConfig(JsonUtility.FromJson<Body>(json));
            }
            catch (ArgumentException)
            {
                return Empty;
            }
        }

        // config.json lives next to the page: drop the page file, query and fragment from its URL.
        public static string UrlFor(string pageUrl)
        {
            if (string.IsNullOrEmpty(pageUrl)) return FileName;

            var end = pageUrl.IndexOfAny(new[] { '?', '#' });
            var path = end >= 0 ? pageUrl.Substring(0, end) : pageUrl;
            var schemeEnd = path.IndexOf("://", StringComparison.Ordinal);
            var lastSlash = path.LastIndexOf('/');
            if (lastSlash < 0 || (schemeEnd >= 0 && lastSlash <= schemeEnd + 2)) return path + "/" + FileName;
            return path.Substring(0, lastSlash + 1) + FileName;
        }

        private static string Clean(string value) => value?.Trim() ?? string.Empty;
    }
}

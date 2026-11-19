using System;
using UnityEngine.Networking;

namespace HexFall.UI
{
    // GETs the public name check through UnityWebRequest, which works in WebGL. The callback runs on the main thread.
    public sealed class UnityWebRequestNameAvailability : INameAvailability
    {
        private const int TimeoutSeconds = 10;

        private readonly string _baseUrl;

        public UnityWebRequestNameAvailability(string baseUrl) => _baseUrl = baseUrl;

        public void Check(string name, Action<NameAvailability> done)
        {
            var request = UnityWebRequest.Get(NameCheckWire.UrlFor(_baseUrl, name));
            request.timeout = TimeoutSeconds;
            request.SendWebRequest().completed += _ =>
            {
                // ConnectionError covers unreachable hosts, CORS failures and the timeout.
                var networkError = request.result == UnityWebRequest.Result.ConnectionError;
                var result = NameCheckWire.Parse(request.responseCode, request.downloadHandler?.text, networkError);
                request.Dispose();
                done(result);
            };
        }
    }
}

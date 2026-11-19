using System;
using System.Text;
using UnityEngine.Networking;

namespace HexFall.UI
{
    // Posts Cognito Identity Provider JSON calls through UnityWebRequest, which works in WebGL
    // (no threads, no System.Net.Http). The completed callback runs on the main thread.
    public sealed class UnityWebRequestCognitoTransport : ICognitoTransport
    {
        private const int TimeoutSeconds = 15;

        private readonly string _endpoint;

        public UnityWebRequestCognitoTransport(string region)
        {
            _endpoint = "https://cognito-idp." + region + ".amazonaws.com/";
        }

        public void Send(string operation, string jsonBody, Action<CognitoResponse> done)
        {
            var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody))
                {
                    contentType = "application/x-amz-json-1.1",
                },
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            request.SetRequestHeader("X-Amz-Target", "AWSCognitoIdentityProviderService." + operation);

            request.SendWebRequest().completed += _ =>
            {
                // ConnectionError covers unreachable hosts, CORS failures and the timeout.
                var networkError = request.result == UnityWebRequest.Result.ConnectionError;
                var response = new CognitoResponse(request.responseCode, request.downloadHandler?.text, networkError);
                request.Dispose();
                done(response);
            };
        }
    }
}

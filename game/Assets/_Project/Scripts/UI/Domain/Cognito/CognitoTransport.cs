using System;

namespace HexFall.UI
{
    // What came back from one Cognito call. StatusCode is 0 when the request never completed.
    public readonly struct CognitoResponse
    {
        public CognitoResponse(long statusCode, string body, bool networkError)
        {
            StatusCode = statusCode;
            Body = body;
            NetworkError = networkError;
        }

        public long StatusCode { get; }
        public string Body { get; }
        public bool NetworkError { get; }
        public bool IsSuccess => !NetworkError && StatusCode >= 200 && StatusCode < 300;
    }

    // Port over the HTTP layer so the Cognito adapter stays pure C# and testable.
    public interface ICognitoTransport
    {
        // operation is the Cognito API name, e.g. "SignUp"; done is raised once, on the main thread.
        void Send(string operation, string jsonBody, Action<CognitoResponse> done);
    }
}

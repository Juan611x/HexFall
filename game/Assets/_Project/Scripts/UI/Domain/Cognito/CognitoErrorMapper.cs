using System;
using UnityEngine;

namespace HexFall.UI
{
    // Turns a failed Cognito response into player-facing copy. Raw server text is never surfaced.
    public static class CognitoErrorMapper
    {
        public const string Generic = "Something went wrong. Try again.";
        public const string Network = "Can't reach the server. Check your connection.";
        private const string TooMany = "Too many attempts. Try again later.";

        [Serializable]
        private sealed class ErrorBody
        {
            public string __type;
        }

        public static string Map(CognitoResponse response)
        {
            if (response.NetworkError) return Network;

            switch (ErrorType(response.Body))
            {
                case "UsernameExistsException": return "An account with this email already exists.";
                case "CodeMismatchException": return "That code is not valid. Try again.";
                case "ExpiredCodeException": return "That code has expired. Request a new one.";
                case "NotAuthorizedException": return "Incorrect email or password.";
                case "UserNotConfirmedException": return "Verify your email before logging in.";
                case "InvalidPasswordException": return "That password does not meet the requirements.";
                case "LimitExceededException":
                case "TooManyRequestsException":
                case "TooManyFailedAttemptsException":
                    return TooMany;
                default: return Generic;
            }
        }

        // __type may be prefixed ("com.amazonaws...#NotAuthorizedException"); only the suffix matters.
        private static string ErrorType(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var type = JsonUtility.FromJson<ErrorBody>(body)?.__type;
                if (string.IsNullOrEmpty(type)) return null;
                var hash = type.LastIndexOf('#');
                return hash >= 0 ? type.Substring(hash + 1) : type;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}

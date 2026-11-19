using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexFall.UI
{
    // Tokens of the signed-in user, kept in memory only (never persisted) for later server calls.
    public sealed class AuthTokens
    {
        public AuthTokens(string idToken, string accessToken, string refreshToken, int expiresInSeconds)
        {
            IdToken = idToken;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresInSeconds = expiresInSeconds;
        }

        public string IdToken { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public int ExpiresInSeconds { get; }
    }

    // IAuthService over the Cognito Identity Provider JSON API (USER_PASSWORD_AUTH, no SDK).
    public sealed class CognitoAuthService : IAuthService
    {
        [Serializable]
        private sealed class AuthenticationResultBody
        {
            public string IdToken;
            public string AccessToken;
            public string RefreshToken;
            public int ExpiresIn;
        }

        [Serializable]
        private sealed class InitiateAuthBody
        {
            public string ChallengeName;
            public AuthenticationResultBody AuthenticationResult;
        }

        private readonly ICognitoTransport _transport;
        private readonly string _clientId;
        // Passwords of accounts awaiting verification, so Verify can sign the user in afterwards.
        private readonly Dictionary<string, string> _pendingPasswords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public CognitoAuthService(ICognitoTransport transport, string clientId)
        {
            _transport = transport;
            _clientId = clientId;
        }

        public AuthTokens Tokens { get; private set; }

        public void SignUp(string name, string email, string password, Action<AuthResult> done)
        {
            var body = new CognitoJsonObject()
                .Str("ClientId", _clientId)
                .Str("Username", email)
                .Str("Password", password)
                .Raw("UserAttributes", CognitoJsonObject.Array(
                    Attribute("email", email),
                    Attribute("preferred_username", name)))
                .Build();
            _transport.Send("SignUp", body, response =>
            {
                if (!response.IsSuccess)
                {
                    done(AuthResult.Fail(CognitoErrorMapper.Map(response)));
                    return;
                }

                _pendingPasswords[email] = password;
                done(AuthResult.Ok());
            });
        }

        public void Verify(string email, string code, Action<AuthResult> done)
        {
            var body = new CognitoJsonObject()
                .Str("ClientId", _clientId)
                .Str("Username", email)
                .Str("ConfirmationCode", code)
                .Build();
            _transport.Send("ConfirmSignUp", body, response =>
            {
                if (!response.IsSuccess)
                {
                    done(AuthResult.Fail(CognitoErrorMapper.Map(response)));
                    return;
                }

                // Confirmed: sign in with the password given at sign-up, if this page still holds it.
                if (!_pendingPasswords.TryGetValue(email, out var password))
                {
                    done(AuthResult.Ok());
                    return;
                }

                _pendingPasswords.Remove(email);
                Authenticate(email, password, result =>
                {
                    // The account is confirmed even if the automatic sign-in failed; the player can log in.
                    done(result.Success ? result : AuthResult.Ok());
                });
            });
        }

        public void ResendCode(string email, Action<AuthResult> done)
        {
            var body = new CognitoJsonObject().Str("ClientId", _clientId).Str("Username", email).Build();
            _transport.Send("ResendConfirmationCode", body, response =>
                done(response.IsSuccess ? AuthResult.Ok() : AuthResult.Fail(CognitoErrorMapper.Map(response))));
        }

        public void LogIn(string email, string password, Action<AuthResult> done) =>
            Authenticate(email, password, done, cachePasswordIfUnverified: true);

        public void Guest(string name, Action<AuthResult> done)
        {
            if (WizardNameRule.IsValid(name)) done(AuthResult.Ok(new PlayerSession(name, true)));
            else done(AuthResult.Fail("Use 3–16 letters, numbers or _ only."));
        }

        private void Authenticate(string email, string password, Action<AuthResult> done, bool cachePasswordIfUnverified = false)
        {
            var body = new CognitoJsonObject()
                .Str("AuthFlow", "USER_PASSWORD_AUTH")
                .Str("ClientId", _clientId)
                .Raw("AuthParameters", new CognitoJsonObject().Str("USERNAME", email).Str("PASSWORD", password).Build())
                .Build();
            _transport.Send("InitiateAuth", body, response =>
            {
                if (!response.IsSuccess)
                {
                    if (cachePasswordIfUnverified && CognitoErrorMapper.IsUnverified(response))
                    {
                        // Kept so a later Verify can sign the player straight in.
                        _pendingPasswords[email] = password;
                        done(AuthResult.Unverified(CognitoErrorMapper.Map(response)));
                        return;
                    }

                    done(AuthResult.Fail(CognitoErrorMapper.Map(response)));
                    return;
                }

                done(ReadSession(response.Body));
            });
        }

        private AuthResult ReadSession(string body)
        {
            AuthenticationResultBody tokens = null;
            try
            {
                if (!string.IsNullOrEmpty(body)) tokens = JsonUtility.FromJson<InitiateAuthBody>(body)?.AuthenticationResult;
            }
            catch (ArgumentException)
            {
            }

            // A challenge (e.g. NEW_PASSWORD_REQUIRED) has no tokens; no challenge flow is supported.
            if (tokens == null || string.IsNullOrEmpty(tokens.IdToken)
                || !JwtPayload.TryGetNickname(tokens.IdToken, out var nickname))
            {
                return AuthResult.Fail(CognitoErrorMapper.Generic);
            }

            Tokens = new AuthTokens(tokens.IdToken, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
            return AuthResult.Ok(new PlayerSession(nickname, false));
        }

        private static string Attribute(string name, string value) =>
            new CognitoJsonObject().Str("Name", name).Str("Value", value).Build();
    }
}

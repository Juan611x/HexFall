using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class CognitoAuthServiceTests
    {
        private sealed class FakeTransport : ICognitoTransport
        {
            public readonly List<KeyValuePair<string, string>> Calls = new List<KeyValuePair<string, string>>();
            private readonly Queue<CognitoResponse> _responses = new Queue<CognitoResponse>();

            public void Enqueue(CognitoResponse response) => _responses.Enqueue(response);
            public void EnqueueOk(string body = "{}") => Enqueue(new CognitoResponse(200, body, false));
            public void EnqueueError(string type) =>
                Enqueue(new CognitoResponse(400, "{\"__type\":\"" + type + "\",\"message\":\"raw\"}", false));

            public void Send(string operation, string jsonBody, Action<CognitoResponse> done)
            {
                Calls.Add(new KeyValuePair<string, string>(operation, jsonBody));
                done(_responses.Dequeue());
            }
        }

        private const string ClientId = "client123";

        private FakeTransport _transport;
        private InMemorySessionStore _store;
        private CognitoAuthService _auth;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
            _store = new InMemorySessionStore();
            _auth = new CognitoAuthService(_transport, ClientId, _store);
        }

        private static string Segment(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string AuthBody(string name, string idSuffix = "")
        {
            var id = Segment("{\"alg\":\"none\"}") + "." + Segment("{\"preferred_username\":\"" + name + "\"}") + ".sig" + idSuffix;
            return "{\"AuthenticationResult\":{\"IdToken\":\"" + id + "\",\"AccessToken\":\"access-1\",\"RefreshToken\":\"refresh-1\",\"ExpiresIn\":3600,\"TokenType\":\"Bearer\"}}";
        }

        private AuthResult SignUp(string name = "Ash_Mage", string email = "ash@example.com", string password = "Arena#2026")
        {
            AuthResult result = default;
            _auth.SignUp(name, email, password, r => result = r);
            return result;
        }

        private AuthResult Verify(string email = "ash@example.com", string code = "123456")
        {
            AuthResult result = default;
            _auth.Verify(email, code, r => result = r);
            return result;
        }

        private AuthResult LogIn(string email = "ash@example.com", string password = "Arena#2026")
        {
            AuthResult result = default;
            _auth.LogIn(email, password, r => result = r);
            return result;
        }

        [Test]
        public void SignUpSendsSignUpWithEmailAndPreferredUsername()
        {
            _transport.EnqueueOk();
            var result = SignUp();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Session);
            Assert.AreEqual(1, _transport.Calls.Count);
            Assert.AreEqual("SignUp", _transport.Calls[0].Key);
            Assert.AreEqual(
                "{\"ClientId\":\"client123\",\"Username\":\"ash@example.com\",\"Password\":\"Arena#2026\","
                + "\"UserAttributes\":[{\"Name\":\"email\",\"Value\":\"ash@example.com\"},{\"Name\":\"preferred_username\",\"Value\":\"Ash_Mage\"}]}",
                _transport.Calls[0].Value);
        }

        [Test]
        public void SignUpEscapesPasswordInBody()
        {
            _transport.EnqueueOk();
            SignUp(password: "pa\"ss\\w");
            StringAssert.Contains("\"Password\":\"pa\\\"ss\\\\w\"", _transport.Calls[0].Value);
        }

        [Test]
        public void SignUpErrorIsMapped()
        {
            _transport.EnqueueError("UsernameExistsException");
            var result = SignUp();
            Assert.IsFalse(result.Success);
            Assert.AreEqual("An account with this email already exists.", result.Error);
        }

        [Test]
        public void SignUpNetworkFailureIsMapped()
        {
            _transport.Enqueue(new CognitoResponse(0, "", true));
            Assert.AreEqual(CognitoErrorMapper.Network, SignUp().Error);
        }

        [Test]
        public void VerifyConfirmsThenSignsInWithCachedPassword()
        {
            _transport.EnqueueOk();
            SignUp();
            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));

            var result = Verify();

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Ash_Mage", result.Session.Nickname);
            Assert.IsFalse(result.Session.IsGuest);
            Assert.AreEqual(3, _transport.Calls.Count);
            Assert.AreEqual("ConfirmSignUp", _transport.Calls[1].Key);
            Assert.AreEqual("{\"ClientId\":\"client123\",\"Username\":\"ash@example.com\",\"ConfirmationCode\":\"123456\"}", _transport.Calls[1].Value);
            Assert.AreEqual("InitiateAuth", _transport.Calls[2].Key);
            Assert.AreEqual(
                "{\"AuthFlow\":\"USER_PASSWORD_AUTH\",\"ClientId\":\"client123\",\"AuthParameters\":{\"USERNAME\":\"ash@example.com\",\"PASSWORD\":\"Arena#2026\"}}",
                _transport.Calls[2].Value);
            Assert.AreEqual("access-1", _auth.Tokens.AccessToken);
        }

        [Test]
        public void VerifyEmailMatchIsCaseInsensitive()
        {
            _transport.EnqueueOk();
            SignUp(email: "Ash@Example.com");
            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            Assert.IsNotNull(Verify(email: "ash@example.com").Session);
        }

        [Test]
        public void VerifyWithoutCachedPasswordSucceedsWithoutSession()
        {
            _transport.EnqueueOk();
            var result = Verify();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Session);
            Assert.AreEqual(1, _transport.Calls.Count);
            Assert.AreEqual("ConfirmSignUp", _transport.Calls[0].Key);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void VerifyWrongCodeFailsAndKeepsPasswordForRetry()
        {
            _transport.EnqueueOk();
            SignUp();
            _transport.EnqueueError("CodeMismatchException");
            var wrong = Verify(code: "000000");
            Assert.IsFalse(wrong.Success);
            Assert.AreEqual("That code is not valid. Try again.", wrong.Error);
            Assert.AreEqual(2, _transport.Calls.Count);

            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            Assert.IsNotNull(Verify().Session);
        }

        [Test]
        public void VerifyClearsPasswordCacheAfterConfirmation()
        {
            _transport.EnqueueOk();
            SignUp();
            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            Verify();

            _transport.EnqueueOk();
            var again = Verify();
            Assert.IsNull(again.Session);
            Assert.AreEqual(4, _transport.Calls.Count);
        }

        [Test]
        public void VerifyStillSucceedsWithoutSessionWhenSignInAfterConfirmFails()
        {
            _transport.EnqueueOk();
            SignUp();
            _transport.EnqueueOk();
            _transport.Enqueue(new CognitoResponse(0, "", true));
            var result = Verify();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Session);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void ResendSendsResendConfirmationCode()
        {
            _transport.EnqueueOk();
            AuthResult result = default;
            _auth.ResendCode("ash@example.com", r => result = r);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("ResendConfirmationCode", _transport.Calls[0].Key);
            Assert.AreEqual("{\"ClientId\":\"client123\",\"Username\":\"ash@example.com\"}", _transport.Calls[0].Value);
        }

        [Test]
        public void ResendLimitIsMapped()
        {
            _transport.EnqueueError("LimitExceededException");
            AuthResult result = default;
            _auth.ResendCode("ash@example.com", r => result = r);
            Assert.AreEqual("Too many attempts. Try again later.", result.Error);
        }

        [Test]
        public void LogInSendsInitiateAuthAndReturnsSessionAndTokens()
        {
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            var result = LogIn();
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Ash_Mage", result.Session.Nickname);
            Assert.AreEqual("InitiateAuth", _transport.Calls[0].Key);
            Assert.AreEqual(
                "{\"AuthFlow\":\"USER_PASSWORD_AUTH\",\"ClientId\":\"client123\",\"AuthParameters\":{\"USERNAME\":\"ash@example.com\",\"PASSWORD\":\"Arena#2026\"}}",
                _transport.Calls[0].Value);
            Assert.AreEqual("access-1", _auth.Tokens.AccessToken);
            Assert.AreEqual("refresh-1", _auth.Tokens.RefreshToken);
            Assert.AreEqual(3600, _auth.Tokens.ExpiresInSeconds);
            Assert.IsNotEmpty(_auth.Tokens.IdToken);
        }

        [TestCase("NotAuthorizedException", "Incorrect email or password.")]
        public void LogInErrorsAreMapped(string type, string expected)
        {
            _transport.EnqueueError(type);
            var result = LogIn();
            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.Error);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void LogInOfUnconfirmedAccountReturnsUnverifiedResult()
        {
            _transport.EnqueueError("com.amazonaws.cognito#UserNotConfirmedException");
            var result = LogIn();
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.NeedsVerification);
            Assert.AreEqual("Verify your email before logging in.", result.Error);
            Assert.IsNull(result.Session);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void OrdinaryLogInFailureDoesNotNeedVerification()
        {
            _transport.EnqueueError("NotAuthorizedException");
            Assert.IsFalse(LogIn().NeedsVerification);
        }

        [Test]
        public void VerifyAfterUnverifiedLogInSignsInWithCachedPassword()
        {
            _transport.EnqueueError("UserNotConfirmedException");
            LogIn(password: "Arena#2026");
            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));

            var result = Verify();

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Ash_Mage", result.Session.Nickname);
            Assert.AreEqual(3, _transport.Calls.Count);
            Assert.AreEqual("ConfirmSignUp", _transport.Calls[1].Key);
            Assert.AreEqual("InitiateAuth", _transport.Calls[2].Key);
            Assert.AreEqual(
                "{\"AuthFlow\":\"USER_PASSWORD_AUTH\",\"ClientId\":\"client123\",\"AuthParameters\":{\"USERNAME\":\"ash@example.com\",\"PASSWORD\":\"Arena#2026\"}}",
                _transport.Calls[2].Value);
        }

        [Test]
        public void LogInChallengeResponseFailsWithGenericMessage()
        {
            _transport.EnqueueOk("{\"ChallengeName\":\"NEW_PASSWORD_REQUIRED\",\"Session\":\"x\"}");
            var result = LogIn();
            Assert.IsFalse(result.Success);
            Assert.AreEqual(CognitoErrorMapper.Generic, result.Error);
        }

        [Test]
        public void LogInWithUnreadableTokenFailsWithGenericMessage()
        {
            _transport.EnqueueOk("{\"AuthenticationResult\":{\"IdToken\":\"garbage\",\"AccessToken\":\"a\",\"RefreshToken\":\"r\",\"ExpiresIn\":10}}");
            var result = LogIn();
            Assert.IsFalse(result.Success);
            Assert.AreEqual(CognitoErrorMapper.Generic, result.Error);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void LogInWithEmptyBodyFailsWithGenericMessage()
        {
            _transport.EnqueueOk("");
            Assert.AreEqual(CognitoErrorMapper.Generic, LogIn().Error);
        }

        [Test]
        public void LogInSavesRefreshTokenAndEmail()
        {
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            LogIn();
            Assert.IsTrue(_store.TryLoad(out var email, out var refresh));
            Assert.AreEqual("ash@example.com", email);
            Assert.AreEqual("refresh-1", refresh);
        }

        [Test]
        public void FailedLogInSavesNothing()
        {
            _transport.EnqueueError("NotAuthorizedException");
            LogIn();
            Assert.IsFalse(_store.TryLoad(out _, out _));
        }

        [Test]
        public void VerifyAutoSignInSavesRefreshToken()
        {
            _transport.EnqueueOk();
            SignUp();
            _transport.EnqueueOk();
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            Verify();
            Assert.IsTrue(_store.TryLoad(out var email, out var refresh));
            Assert.AreEqual("ash@example.com", email);
            Assert.AreEqual("refresh-1", refresh);
        }

        [Test]
        public void GuestIsNeverPersisted()
        {
            _auth.Guest("Wizard_1", _ => { });
            Assert.IsFalse(_store.TryLoad(out _, out _));
        }

        private static string RefreshOnlyBody(string name) =>
            AuthBody(name).Replace(",\"RefreshToken\":\"refresh-1\"", "");

        private AuthResult Restore()
        {
            AuthResult result = default;
            _auth.Restore(r => result = r);
            return result;
        }

        [Test]
        public void RestoreRefreshesAndKeepsStoredRefreshToken()
        {
            _store.Save("ash@example.com", "stored-refresh");
            _transport.EnqueueOk(RefreshOnlyBody("Ash_Mage"));

            var result = Restore();

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Ash_Mage", result.Session.Nickname);
            Assert.IsFalse(result.Session.IsGuest);
            Assert.AreEqual(1, _transport.Calls.Count);
            Assert.AreEqual("InitiateAuth", _transport.Calls[0].Key);
            Assert.AreEqual(
                "{\"AuthFlow\":\"REFRESH_TOKEN_AUTH\",\"ClientId\":\"client123\",\"AuthParameters\":{\"REFRESH_TOKEN\":\"stored-refresh\"}}",
                _transport.Calls[0].Value);
            Assert.AreEqual("stored-refresh", _auth.Tokens.RefreshToken);
            Assert.AreEqual("access-1", _auth.Tokens.AccessToken);
            Assert.IsTrue(_store.TryLoad(out _, out var kept));
            Assert.AreEqual("stored-refresh", kept);
        }

        [Test]
        public void RestoreWithEmptyStoreFailsWithoutNetworkCall()
        {
            var result = Restore();
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _transport.Calls.Count);
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void RestoreRejectedByServerClearsTheStore()
        {
            _store.Save("ash@example.com", "stale");
            _transport.EnqueueError("NotAuthorizedException");
            var result = Restore();
            Assert.IsFalse(result.Success);
            Assert.IsFalse(_store.TryLoad(out _, out _));
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void RestoreWithUnreadableResponseClearsTheStore()
        {
            _store.Save("ash@example.com", "stored-refresh");
            _transport.EnqueueOk("{}");
            Assert.IsFalse(Restore().Success);
            Assert.IsFalse(_store.TryLoad(out _, out _));
        }

        [Test]
        public void RestoreNetworkFailureKeepsTheStoreForTheNextVisit()
        {
            _store.Save("ash@example.com", "stored-refresh");
            _transport.Enqueue(new CognitoResponse(0, "", true));
            var result = Restore();
            Assert.IsFalse(result.Success);
            Assert.AreEqual(CognitoErrorMapper.Network, result.Error);
            Assert.IsTrue(_store.TryLoad(out _, out _));
        }

        [Test]
        public void LogOutRevokesStoredTokenAndClearsEverything()
        {
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            LogIn();
            _transport.EnqueueOk();
            var done = false;

            _auth.LogOut(() => done = true);

            Assert.IsTrue(done);
            Assert.AreEqual("RevokeToken", _transport.Calls[1].Key);
            Assert.AreEqual("{\"Token\":\"refresh-1\",\"ClientId\":\"client123\"}", _transport.Calls[1].Value);
            Assert.IsFalse(_store.TryLoad(out _, out _));
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void LogOutClearsEverythingEvenWhenRevokeFails()
        {
            _transport.EnqueueOk(AuthBody("Ash_Mage"));
            LogIn();
            _transport.Enqueue(new CognitoResponse(0, "", true));
            var done = false;

            _auth.LogOut(() => done = true);

            Assert.IsTrue(done);
            Assert.IsFalse(_store.TryLoad(out _, out _));
            Assert.IsNull(_auth.Tokens);
        }

        [Test]
        public void LogOutWithoutASessionSendsNothing()
        {
            var done = false;
            _auth.LogOut(() => done = true);
            Assert.IsTrue(done);
            Assert.AreEqual(0, _transport.Calls.Count);
        }

        [Test]
        public void RequestPasswordResetSendsForgotPassword()
        {
            _transport.EnqueueOk("{\"CodeDeliveryDetails\":{}}");
            AuthResult result = default;
            _auth.RequestPasswordReset("ash@example.com", r => result = r);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("ForgotPassword", _transport.Calls[0].Key);
            Assert.AreEqual("{\"ClientId\":\"client123\",\"Username\":\"ash@example.com\"}", _transport.Calls[0].Value);
        }

        [TestCase("InvalidParameterException", CognitoErrorMapper.ResetNeedsVerification)]
        [TestCase("LimitExceededException", "Too many attempts. Try again later.")]
        [TestCase("UserNotFoundException", CognitoErrorMapper.Generic)]
        public void RequestPasswordResetErrorsAreMapped(string type, string expected)
        {
            _transport.EnqueueError(type);
            AuthResult result = default;
            _auth.RequestPasswordReset("ash@example.com", r => result = r);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.Error);
        }

        [Test]
        public void RequestPasswordResetNetworkFailureIsMapped()
        {
            _transport.Enqueue(new CognitoResponse(0, "", true));
            AuthResult result = default;
            _auth.RequestPasswordReset("ash@example.com", r => result = r);
            Assert.AreEqual(CognitoErrorMapper.Network, result.Error);
        }

        [Test]
        public void ConfirmPasswordResetSendsConfirmForgotPassword()
        {
            _transport.EnqueueOk();
            AuthResult result = default;
            _auth.ConfirmPasswordReset("ash@example.com", "123456", "New#Pass1", r => result = r);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("ConfirmForgotPassword", _transport.Calls[0].Key);
            Assert.AreEqual(
                "{\"ClientId\":\"client123\",\"Username\":\"ash@example.com\",\"ConfirmationCode\":\"123456\",\"Password\":\"New#Pass1\"}",
                _transport.Calls[0].Value);
            Assert.IsNull(_auth.Tokens);
        }

        [TestCase("CodeMismatchException", "That code is not valid. Try again.")]
        [TestCase("ExpiredCodeException", "That code has expired. Request a new one.")]
        [TestCase("InvalidPasswordException", "That password does not meet the requirements.")]
        [TestCase("LimitExceededException", "Too many attempts. Try again later.")]
        [TestCase("InvalidParameterException", CognitoErrorMapper.Generic)]
        public void ConfirmPasswordResetErrorsAreMapped(string type, string expected)
        {
            _transport.EnqueueError(type);
            AuthResult result = default;
            _auth.ConfirmPasswordReset("ash@example.com", "123456", "New#Pass1", r => result = r);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.Error);
        }

        [Test]
        public void InvalidParameterOutsidePasswordResetStaysGeneric()
        {
            _transport.EnqueueError("InvalidParameterException");
            Assert.AreEqual(CognitoErrorMapper.Generic, LogIn().Error);
        }

        [Test]
        public void GuestFollowsWizardNameRuleWithoutCallingTransport()
        {
            AuthResult ok = default, bad = default;
            _auth.Guest("Wizard_1", r => ok = r);
            _auth.Guest("x", r => bad = r);
            Assert.IsTrue(ok.Success);
            Assert.IsTrue(ok.Session.IsGuest);
            Assert.AreEqual("Wizard_1", ok.Session.Nickname);
            Assert.IsFalse(bad.Success);
            Assert.AreEqual(0, _transport.Calls.Count);
        }
    }
}

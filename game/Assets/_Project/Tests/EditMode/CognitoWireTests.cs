using System;
using System.Text;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class CognitoJsonTests
    {
        [Test]
        public void BuildsFlatObject()
        {
            var json = new CognitoJsonObject().Str("ClientId", "abc").Str("Username", "a@b.c").Build();
            Assert.AreEqual("{\"ClientId\":\"abc\",\"Username\":\"a@b.c\"}", json);
        }

        [Test]
        public void EmptyObject()
        {
            Assert.AreEqual("{}", new CognitoJsonObject().Build());
        }

        [Test]
        public void EscapesQuotesBackslashesAndControlCharacters()
        {
            var json = new CognitoJsonObject().Str("p", "a\"b\\c\nd\te\r\u0001").Build();
            Assert.AreEqual("{\"p\":\"a\\\"b\\\\c\\nd\\te\\r\\u0001\"}", json);
        }

        [Test]
        public void EscapesKeysToo()
        {
            var json = new CognitoJsonObject().Str("k\"", "v").Build();
            Assert.AreEqual("{\"k\\\"\":\"v\"}", json);
        }

        [Test]
        public void UnicodePassesThroughUnescaped()
        {
            var json = new CognitoJsonObject().Str("n", "Mágico_ñ").Build();
            Assert.AreEqual("{\"n\":\"Mágico_ñ\"}", json);
        }

        [Test]
        public void NestsObjectsAndArrays()
        {
            var json = new CognitoJsonObject()
                .Str("ClientId", "c")
                .Raw("AuthParameters", new CognitoJsonObject().Str("USERNAME", "u").Str("PASSWORD", "p").Build())
                .Raw("UserAttributes", CognitoJsonObject.Array(
                    new CognitoJsonObject().Str("Name", "email").Str("Value", "e").Build(),
                    new CognitoJsonObject().Str("Name", "preferred_username").Str("Value", "w").Build()))
                .Build();
            Assert.AreEqual(
                "{\"ClientId\":\"c\",\"AuthParameters\":{\"USERNAME\":\"u\",\"PASSWORD\":\"p\"},"
                + "\"UserAttributes\":[{\"Name\":\"email\",\"Value\":\"e\"},{\"Name\":\"preferred_username\",\"Value\":\"w\"}]}",
                json);
        }

        [Test]
        public void NullStringBecomesEmpty()
        {
            Assert.AreEqual("{\"a\":\"\"}", new CognitoJsonObject().Str("a", null).Build());
        }
    }

    public class CognitoErrorMapperTests
    {
        private static CognitoResponse Error(string type) =>
            new CognitoResponse(400, "{\"__type\":\"" + type + "\",\"message\":\"raw server text\"}", false);

        [TestCase("UsernameExistsException", "An account with this email already exists.")]
        [TestCase("CodeMismatchException", "That code is not valid. Try again.")]
        [TestCase("ExpiredCodeException", "That code has expired. Request a new one.")]
        [TestCase("NotAuthorizedException", "Incorrect email or password.")]
        [TestCase("UserNotConfirmedException", "Verify your email before logging in.")]
        [TestCase("InvalidPasswordException", "That password does not meet the requirements.")]
        [TestCase("LimitExceededException", "Too many attempts. Try again later.")]
        [TestCase("TooManyRequestsException", "Too many attempts. Try again later.")]
        [TestCase("TooManyFailedAttemptsException", "Too many attempts. Try again later.")]
        [TestCase("SomethingNewException", "Something went wrong. Try again.")]
        public void MapsKnownTypes(string type, string expected)
        {
            Assert.AreEqual(expected, CognitoErrorMapper.Map(Error(type)));
        }

        [Test]
        public void MatchesSuffixAfterHash()
        {
            var response = Error("com.amazonaws.cognito.identity.idp.model#NotAuthorizedException");
            Assert.AreEqual("Incorrect email or password.", CognitoErrorMapper.Map(response));
        }

        [Test]
        public void NetworkErrorMapsToConnectionCopy()
        {
            var response = new CognitoResponse(0, "", true);
            Assert.AreEqual("Can't reach the server. Check your connection.", CognitoErrorMapper.Map(response));
        }

        [Test]
        public void NonJsonBodyFallsBack()
        {
            var response = new CognitoResponse(502, "<html>bad gateway</html>", false);
            Assert.AreEqual("Something went wrong. Try again.", CognitoErrorMapper.Map(response));
        }

        [Test]
        public void EmptyBodyFallsBack()
        {
            Assert.AreEqual("Something went wrong. Try again.", CognitoErrorMapper.Map(new CognitoResponse(400, null, false)));
        }

        [Test]
        public void NeverSurfacesRawServerText()
        {
            StringAssert.DoesNotContain("raw server text", CognitoErrorMapper.Map(Error("SomethingNewException")));
        }

        [Test]
        public void ResponseIsSuccessOnlyFor2xxWithoutNetworkError()
        {
            Assert.IsTrue(new CognitoResponse(200, "{}", false).IsSuccess);
            Assert.IsFalse(new CognitoResponse(400, "{}", false).IsSuccess);
            Assert.IsFalse(new CognitoResponse(200, "{}", true).IsSuccess);
        }
    }

    public class JwtPayloadTests
    {
        private static string Segment(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string Token(string payloadJson) => Segment("{\"alg\":\"none\"}") + "." + Segment(payloadJson) + ".sig";

        [Test]
        public void ReadsPreferredUsername()
        {
            Assert.IsTrue(JwtPayload.TryGetNickname(Token("{\"preferred_username\":\"Ash_Mage\",\"email\":\"a@b.c\"}"), out var name));
            Assert.AreEqual("Ash_Mage", name);
        }

        [Test]
        public void FallsBackToEmailWhenNoPreferredUsername()
        {
            Assert.IsTrue(JwtPayload.TryGetNickname(Token("{\"email\":\"a@b.c\"}"), out var name));
            Assert.AreEqual("a@b.c", name);
        }

        [Test]
        public void HandlesUrlSafeAlphabetAndMissingPadding()
        {
            // These bytes produce '+' and '/' in standard base64, which base64url maps to '-' and '_'.
            var json = "{\"preferred_username\":\"Wiz_???>>>~~\"}";
            var token = Token(json);
            Assert.IsTrue(token.Contains("-") || token.Contains("_"));
            Assert.IsTrue(JwtPayload.TryGetNickname(token, out var name));
            Assert.AreEqual("Wiz_???>>>~~", name);
        }

        [Test]
        public void HandlesNonAsciiPayload()
        {
            Assert.IsTrue(JwtPayload.TryGetNickname(Token("{\"preferred_username\":\"Mágico\"}"), out var name));
            Assert.AreEqual("Mágico", name);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("onlyonesegment")]
        [TestCase("a.!!!notbase64!!!.c")]
        public void MalformedTokensFail(string token)
        {
            Assert.IsFalse(JwtPayload.TryGetNickname(token, out var name));
            Assert.IsNull(name);
        }

        [Test]
        public void PayloadWithoutNameOrEmailFails()
        {
            Assert.IsFalse(JwtPayload.TryGetNickname(Token("{\"sub\":\"1\"}"), out _));
        }
    }
}

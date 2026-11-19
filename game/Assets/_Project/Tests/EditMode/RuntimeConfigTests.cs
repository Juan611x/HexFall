using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class RuntimeConfigTests
    {
        private const string Full =
            "{\"environment\":\"dev\",\"region\":\"us-east-1\",\"apiBaseUrl\":\"https://api.example.com\","
            + "\"realtimeUrl\":\"wss://rt.example.com\",\"cognito\":{\"userPoolId\":\"us-east-1_AbC123\",\"clientId\":\"client123\"}}";

        [Test]
        public void ParsesEveryDeployedKey()
        {
            var config = RuntimeConfig.Parse(Full);
            Assert.AreEqual("dev", config.Environment);
            Assert.AreEqual("us-east-1", config.Region);
            Assert.AreEqual("https://api.example.com", config.ApiBaseUrl);
            Assert.AreEqual("wss://rt.example.com", config.RealtimeUrl);
            Assert.AreEqual("us-east-1_AbC123", config.CognitoUserPoolId);
            Assert.AreEqual("client123", config.CognitoClientId);
            Assert.IsTrue(config.HasCognito);
        }

        [TestCase("{\"region\":\"\",\"cognito\":{\"userPoolId\":\"p\",\"clientId\":\"c\"}}")]
        [TestCase("{\"region\":\"us-east-1\",\"cognito\":{\"userPoolId\":\"\",\"clientId\":\"c\"}}")]
        [TestCase("{\"region\":\"us-east-1\",\"cognito\":{\"userPoolId\":\"p\",\"clientId\":\"\"}}")]
        [TestCase("{\"region\":\"us-east-1\"}")]
        [TestCase("{\"region\":\"us-east-1\",\"cognito\":{\"userPoolId\":\"  \",\"clientId\":\"c\"}}")]
        public void HasCognitoNeedsRegionPoolAndClient(string json)
        {
            Assert.IsFalse(RuntimeConfig.Parse(json).HasCognito);
        }

        [Test]
        public void DeployedDefaultsWithEmptyStringsHaveNoCognito()
        {
            var config = RuntimeConfig.Parse(
                "{\"environment\":\"\",\"region\":\"\",\"apiBaseUrl\":\"\",\"realtimeUrl\":\"\",\"cognito\":{\"userPoolId\":\"\",\"clientId\":\"\"}}");
            Assert.IsFalse(config.HasCognito);
            Assert.AreEqual("", config.Region);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("<html>404 not found</html>")]
        [TestCase("{not json")]
        public void MissingOrInvalidJsonGivesEmptyConfig(string json)
        {
            var config = RuntimeConfig.Parse(json);
            Assert.IsFalse(config.HasCognito);
            Assert.AreEqual("", config.Region);
            Assert.AreEqual("", config.CognitoClientId);
            Assert.AreEqual("", config.CognitoUserPoolId);
        }

        [Test]
        public void EmptyConfigHasNoCognito()
        {
            Assert.IsFalse(RuntimeConfig.Empty.HasCognito);
        }

        [TestCase("https://d1.cloudfront.net/index.html", "https://d1.cloudfront.net/config.json")]
        [TestCase("https://d1.cloudfront.net/", "https://d1.cloudfront.net/config.json")]
        [TestCase("https://d1.cloudfront.net", "https://d1.cloudfront.net/config.json")]
        [TestCase("https://d1.cloudfront.net/game/index.html?x=1#top", "https://d1.cloudfront.net/game/config.json")]
        [TestCase("https://d1.cloudfront.net/?a=b/c", "https://d1.cloudfront.net/config.json")]
        [TestCase("http://localhost:8080/index.html", "http://localhost:8080/config.json")]
        [TestCase("", "config.json")]
        [TestCase(null, "config.json")]
        public void ConfigUrlSitsNextToThePage(string pageUrl, string expected)
        {
            Assert.AreEqual(expected, RuntimeConfig.UrlFor(pageUrl));
        }

        [Test]
        public void ValuesAreTrimmed()
        {
            var config = RuntimeConfig.Parse(
                "{\"region\":\" us-east-1 \",\"cognito\":{\"userPoolId\":\" p \",\"clientId\":\" c \"}}");
            Assert.AreEqual("us-east-1", config.Region);
            Assert.AreEqual("c", config.CognitoClientId);
            Assert.IsTrue(config.HasCognito);
        }
    }
}

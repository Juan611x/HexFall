using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class NameCheckTests
    {
        [Test]
        public void UrlAppendsTheEscapedNameToTheBase()
        {
            Assert.AreEqual("https://x.on.aws/names/Merlin", NameCheckWire.UrlFor("https://x.on.aws", "Merlin"));
        }

        [Test]
        public void UrlToleratesATrailingSlashOnTheBase()
        {
            Assert.AreEqual("https://x.on.aws/names/Merlin", NameCheckWire.UrlFor("https://x.on.aws/", "Merlin"));
        }

        [Test]
        public void UrlEscapesUnsafeCharacters()
        {
            Assert.AreEqual("https://x.on.aws/names/a%20b%2Fc", NameCheckWire.UrlFor("https://x.on.aws/", "a b/c"));
        }

        [Test]
        public void ParsesAvailableAndTaken()
        {
            Assert.AreEqual(NameAvailability.Available, NameCheckWire.Parse(200, "{\"name\":\"M\",\"available\":true}", false));
            Assert.AreEqual(NameAvailability.Taken, NameCheckWire.Parse(200, "{\"name\":\"M\",\"available\":false}", false));
        }

        [Test]
        public void BadRequestIsInvalid()
        {
            Assert.AreEqual(NameAvailability.Invalid, NameCheckWire.Parse(400, "{\"error\":\"InvalidWizardName\"}", false));
        }

        [Test]
        public void NetworkFailuresAndUnexpectedResponsesAreNetworkErrors()
        {
            Assert.AreEqual(NameAvailability.NetworkError, NameCheckWire.Parse(0, null, true));
            Assert.AreEqual(NameAvailability.NetworkError, NameCheckWire.Parse(500, "oops", false));
            Assert.AreEqual(NameAvailability.NetworkError, NameCheckWire.Parse(403, "{}", false));
            Assert.AreEqual(NameAvailability.NetworkError, NameCheckWire.Parse(200, "<html>", false));
            Assert.AreEqual(NameAvailability.NetworkError, NameCheckWire.Parse(200, "", false));
        }

        [Test]
        public void FakeTreatsRegisteredNamesAsTakenIgnoringCase()
        {
            var fake = new FakeNameAvailability(new FakeAuthService());
            NameAvailability result = default;
            fake.Check("demo_WIZARD", r => result = r);
            Assert.AreEqual(NameAvailability.Taken, result);
        }

        [Test]
        public void FakeReportsUnusedNamesAsAvailable()
        {
            var fake = new FakeNameAvailability(new FakeAuthService());
            NameAvailability result = default;
            fake.Check("Free_Name", r => result = r);
            Assert.AreEqual(NameAvailability.Available, result);
        }

        [Test]
        public void FakeRejectsMalformedNames()
        {
            var fake = new FakeNameAvailability(new FakeAuthService());
            NameAvailability result = default;
            fake.Check("no", r => result = r);
            Assert.AreEqual(NameAvailability.Invalid, result);
        }

        [Test]
        public void FakeSeesNamesRegisteredAfterwards()
        {
            var auth = new FakeAuthService();
            var fake = new FakeNameAvailability(auth);
            auth.SignUp("Newcomer", "n@example.com", "Arena#2026", _ => { });
            NameAvailability result = default;
            fake.Check("newcomer", r => result = r);
            Assert.AreEqual(NameAvailability.Taken, result);
        }
    }
}

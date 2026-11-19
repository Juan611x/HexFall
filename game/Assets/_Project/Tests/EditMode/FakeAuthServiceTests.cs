using System;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class FakeAuthServiceTests
    {
        private FakeAuthService _auth;

        [SetUp]
        public void SetUp() => _auth = new FakeAuthService();

        private static AuthResult Run(Action<Action<AuthResult>> call)
        {
            AuthResult? got = null;
            call(r => got = r);
            Assert.IsTrue(got.HasValue, "callback was not invoked");
            return got.Value;
        }

        [Test]
        public void SignUpSucceedsAsPendingWithoutSession()
        {
            var r = Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            Assert.IsTrue(r.Success);
            Assert.IsNull(r.Session);
        }

        [Test]
        public void SignUpRejectsDuplicateEmailCaseInsensitive()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            var r = Run(d => _auth.SignUp("Other", "A@B.CO", "Arena#2026", d));
            Assert.IsFalse(r.Success);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void SignUpRejectsDemoEmail()
        {
            Assert.IsFalse(Run(d => _auth.SignUp("Ash", FakeAuthService.DemoEmail, "Arena#2026", d)).Success);
        }

        [Test]
        public void VerifyAcceptsTheFixedCodeAndReturnsRegisteredSession()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            var r = Run(d => _auth.Verify("a@b.co", "123456", d));
            Assert.IsTrue(r.Success);
            Assert.AreEqual("Ash", r.Session.Nickname);
            Assert.IsFalse(r.Session.IsGuest);
        }

        [Test]
        public void VerifyRejectsWrongCode()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            var r = Run(d => _auth.Verify("a@b.co", "000000", d));
            Assert.IsFalse(r.Success);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void VerifyRejectsUnknownEmail()
        {
            Assert.IsFalse(Run(d => _auth.Verify("nobody@b.co", "123456", d)).Success);
        }

        [Test]
        public void LogInFailsBeforeVerificationAndSucceedsAfter()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            var pending = Run(d => _auth.LogIn("a@b.co", "Arena#2026", d));
            Assert.IsFalse(pending.Success);
            Assert.IsTrue(pending.NeedsVerification);
            Assert.AreEqual("Verify your email before logging in.", pending.Error);
            Run(d => _auth.Verify("a@b.co", "123456", d));
            var r = Run(d => _auth.LogIn("A@b.co", "Arena#2026", d));
            Assert.IsTrue(r.Success);
            Assert.AreEqual("Ash", r.Session.Nickname);
        }

        [Test]
        public void LogInOfUnverifiedAccountWithWrongPasswordIsOrdinaryFailure()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            var r = Run(d => _auth.LogIn("a@b.co", "nope", d));
            Assert.IsFalse(r.Success);
            Assert.IsFalse(r.NeedsVerification);
            Assert.AreEqual("Incorrect email or password.", r.Error);
        }

        [Test]
        public void LogInAcceptsSeededDemoAccount()
        {
            var r = Run(d => _auth.LogIn(FakeAuthService.DemoEmail, FakeAuthService.DemoPassword, d));
            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.Session.IsGuest);
        }

        [Test]
        public void LogInRejectsWrongPasswordAndUnknownEmail()
        {
            Assert.IsFalse(Run(d => _auth.LogIn(FakeAuthService.DemoEmail, "nope", d)).Success);
            Assert.IsFalse(Run(d => _auth.LogIn("x@y.zz", "Arena#2026", d)).Success);
        }

        [Test]
        public void ResendCodeSucceedsOnlyForPendingAccount()
        {
            Run(d => _auth.SignUp("Ash", "a@b.co", "Arena#2026", d));
            Assert.IsTrue(Run(d => _auth.ResendCode("a@b.co", d)).Success);
            Assert.IsFalse(Run(d => _auth.ResendCode("zz@b.co", d)).Success);
        }

        [Test]
        public void GuestReturnsGuestSession()
        {
            var r = Run(d => _auth.Guest("Ashen_Mage_042", d));
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.Session.IsGuest);
            Assert.AreEqual("Ashen_Mage_042", r.Session.Nickname);
            Assert.AreEqual(0, r.Session.Matches + r.Session.Wins + r.Session.Eliminations);
        }

        [Test]
        public void GuestRejectsInvalidName()
        {
            Assert.IsFalse(Run(d => _auth.Guest("x", d)).Success);
        }
    }
}

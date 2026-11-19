using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class StartupDecisionTests
    {
        [Test]
        public void RestoredPlayerStartsAtLobby()
        {
            var restored = AuthResult.Ok(new PlayerSession("Ash_Mage", false));
            Assert.AreEqual(ScreenId.Lobby, StartupDecision.InitialScreen(restored));
        }

        [Test]
        public void FailedRestoreStartsAtTitle()
        {
            Assert.AreEqual(ScreenId.Title, StartupDecision.InitialScreen(AuthResult.Fail("none")));
        }

        [Test]
        public void SuccessWithoutSessionStartsAtTitle()
        {
            Assert.AreEqual(ScreenId.Title, StartupDecision.InitialScreen(AuthResult.Ok()));
        }

        [Test]
        public void GuestSessionIsNeverRestoredIntoLobby()
        {
            var guest = AuthResult.Ok(new PlayerSession("Wizard_1", true));
            Assert.AreEqual(ScreenId.Title, StartupDecision.InitialScreen(guest));
        }
    }
}

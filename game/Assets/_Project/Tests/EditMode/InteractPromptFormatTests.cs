using NUnit.Framework;

namespace HexFall.UI.Tests
{
    // Text of the Interact prompt: a key badge and a short label naming the spell that would be taken.
    public class InteractPromptFormatTests
    {
        [Test]
        public void TheKeyBadgeIsE()
        {
            Assert.AreEqual("E", InteractPromptFormat.Key);
        }

        [Test]
        public void TheLabelNamesTheSpellItSwapsTo()
        {
            Assert.AreEqual("Swap to Fire", InteractPromptFormat.Label("Fire"));
        }

        [Test]
        public void TheNameIsTrimmed()
        {
            Assert.AreEqual("Swap to Earth", InteractPromptFormat.Label("  Earth \n"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ANamelessSpellGetsTheGenericLabel(string name)
        {
            Assert.AreEqual("Swap spell", InteractPromptFormat.Label(name));
        }
    }
}

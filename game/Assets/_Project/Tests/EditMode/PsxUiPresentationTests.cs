using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class PsxUiPresentationTests
    {
        [Test]
        public void EnabledRendersToTextureWithPsxClassAndCompositeOn()
        {
            var p = PsxUiPresentation.Resolve(true);

            Assert.IsTrue(p.RenderToTexture);
            Assert.AreEqual("hf-psx-ui", p.Class);
            Assert.AreEqual(1f, p.CompositeFlag);
        }

        [Test]
        public void DisabledIsPlainOverlayWithoutClassAndCompositeOff()
        {
            var p = PsxUiPresentation.Resolve(false);

            Assert.IsFalse(p.RenderToTexture);
            Assert.IsNull(p.Class);
            Assert.AreEqual(0f, p.CompositeFlag);
        }
    }
}

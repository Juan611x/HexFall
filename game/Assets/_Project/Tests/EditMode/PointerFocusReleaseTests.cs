using NUnit.Framework;
using UnityEngine.UIElements;

namespace HexFall.UI.Tests
{
    public class PointerFocusReleaseTests
    {
        [Test]
        public void ButtonReleasesFocusAfterPointerPress() =>
            Assert.That(PointerFocusRelease.ShouldRelease(new Button()), Is.True);

        [Test]
        public void SliderReleasesFocusAfterPointerPress() =>
            Assert.That(PointerFocusRelease.ShouldRelease(new Slider()), Is.True);

        [Test]
        public void SliderIntReleasesFocusAfterPointerPress() =>
            Assert.That(PointerFocusRelease.ShouldRelease(new SliderInt()), Is.True);

        [Test]
        public void ToggleReleasesFocusAfterPointerPress() =>
            Assert.That(PointerFocusRelease.ShouldRelease(new Toggle()), Is.True);

        [Test]
        public void TextFieldKeepsFocusSoThePlayerCanType() =>
            Assert.That(PointerFocusRelease.ShouldRelease(new TextField()), Is.False);

        [Test]
        public void NothingFocusedReleasesNothing() =>
            Assert.That(PointerFocusRelease.ShouldRelease(null), Is.False);
    }
}

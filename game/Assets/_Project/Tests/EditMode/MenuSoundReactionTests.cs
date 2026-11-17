using NUnit.Framework;
using UnityEngine.UIElements;

namespace HexFall.UI.Tests
{
    public class MenuSoundReactionTests
    {
        private sealed class FakeSfx : IMenuSfx
        {
            public int Hovers, Clicks, Backs;
            public void PlayHover() => Hovers++;
            public void PlayClick() => Clicks++;
            public void PlayBack() => Backs++;
        }

        private FakeSfx _sfx;
        private MenuSoundReaction _reaction;

        [SetUp]
        public void SetUp()
        {
            _sfx = new FakeSfx();
            _reaction = new MenuSoundReaction(_sfx);
        }

        [Test]
        public void EnteringAButtonPlaysHoverOnce()
        {
            _reaction.OnPointerEnter(new Button());
            Assert.AreEqual(1, _sfx.Hovers);
            Assert.AreEqual(0, _sfx.Clicks + _sfx.Backs);
        }

        [Test]
        public void EnteringAChildOfAButtonDoesNotPlayHover()
        {
            var button = new Button();
            var label = new Label("x");
            button.Add(label);
            _reaction.OnPointerEnter(label);
            Assert.AreEqual(0, _sfx.Hovers);
        }

        [Test]
        public void EnteringNonButtonOrNullDoesNotPlayHover()
        {
            _reaction.OnPointerEnter(new VisualElement());
            _reaction.OnPointerEnter(new Label("x"));
            _reaction.OnPointerEnter(null);
            Assert.AreEqual(0, _sfx.Hovers);
        }

        [Test]
        public void DisabledButtonDoesNotPlayHover()
        {
            var button = new Button();
            button.SetEnabled(false);
            _reaction.OnPointerEnter(button);
            Assert.AreEqual(0, _sfx.Hovers);
        }

        [Test]
        public void ButtonInsideDisabledContainerDoesNotPlayHover()
        {
            var container = new VisualElement();
            var button = new Button();
            container.Add(button);
            container.SetEnabled(false);
            _reaction.OnPointerEnter(button);
            Assert.AreEqual(0, _sfx.Hovers);
        }

        [Test]
        public void PressingAButtonPlaysClick()
        {
            _reaction.OnPress(new Button { name = "login-button" });
            Assert.AreEqual(1, _sfx.Clicks);
            Assert.AreEqual(0, _sfx.Backs);
        }

        [Test]
        public void PressingInsideAButtonPlaysClick()
        {
            var button = new Button();
            var label = new Label("x");
            button.Add(label);
            _reaction.OnPress(label);
            Assert.AreEqual(1, _sfx.Clicks);
        }

        [Test]
        public void PressingBackPlaysBackInsteadOfClick()
        {
            _reaction.OnPress(new Button { name = MenuSoundReaction.BackButtonName });
            Assert.AreEqual(1, _sfx.Backs);
            Assert.AreEqual(0, _sfx.Clicks);
        }

        [Test]
        public void PressingInsideBackPlaysBack()
        {
            var back = new Button { name = "back-button" };
            var label = new Label("BACK");
            back.Add(label);
            _reaction.OnPress(label);
            Assert.AreEqual(1, _sfx.Backs);
            Assert.AreEqual(0, _sfx.Clicks);
        }

        [Test]
        public void PressingDisabledButtonPlaysNothing()
        {
            var button = new Button();
            button.SetEnabled(false);
            _reaction.OnPress(button);
            Assert.AreEqual(0, _sfx.Clicks + _sfx.Backs);
        }

        [Test]
        public void PressingNonButtonOrNullPlaysNothing()
        {
            _reaction.OnPress(new VisualElement());
            _reaction.OnPress(null);
            Assert.AreEqual(0, _sfx.Clicks + _sfx.Backs);
        }
    }
}

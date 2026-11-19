using NUnit.Framework;
using UnityEngine.UIElements;

namespace HexFall.UI.Tests
{
    public class MenuClickCastReactionTests
    {
        private sealed class FakeWizardAnimator : IWizardAnimator
        {
            public int Casts;
            public void Cast() => Casts++;
        }

        private FakeWizardAnimator _wizard;
        private MenuClickCastReaction _reaction;

        [SetUp]
        public void SetUp()
        {
            _wizard = new FakeWizardAnimator();
            _reaction = new MenuClickCastReaction(_wizard);
        }

        [Test]
        public void ButtonClickCastsOnce()
        {
            _reaction.OnClick(new Button());
            Assert.AreEqual(1, _wizard.Casts);
        }

        [Test]
        public void EachClickCastsAgain()
        {
            var button = new Button();
            _reaction.OnClick(button);
            _reaction.OnClick(button);
            _reaction.OnClick(button);
            Assert.AreEqual(3, _wizard.Casts);
        }

        [Test]
        public void ClickOnElementInsideButtonCasts()
        {
            var button = new Button();
            var inner = new Label("x");
            button.Add(inner);
            _reaction.OnClick(inner);
            Assert.AreEqual(1, _wizard.Casts);
        }

        [Test]
        public void ClickOnNonButtonDoesNotCast()
        {
            var panel = new VisualElement();
            var label = new Label("x");
            panel.Add(label);
            _reaction.OnClick(label);
            _reaction.OnClick(panel);
            Assert.AreEqual(0, _wizard.Casts);
        }

        [Test]
        public void ButtonInsideANoCastAreaDoesNotCast()
        {
            var area = new VisualElement();
            area.AddToClassList(MenuClickCastReaction.NoCastClass);
            var button = new Button();
            area.Add(button);
            _reaction.OnClick(button);
            Assert.AreEqual(0, _wizard.Casts);
        }

        [Test]
        public void NoCastButtonItselfDoesNotCast()
        {
            var button = new Button();
            button.AddToClassList(MenuClickCastReaction.NoCastClass);
            _reaction.OnClick(button);
            Assert.AreEqual(0, _wizard.Casts);
        }

        [Test]
        public void NullTargetDoesNotCast()
        {
            _reaction.OnClick(null);
            Assert.AreEqual(0, _wizard.Casts);
        }
    }
}

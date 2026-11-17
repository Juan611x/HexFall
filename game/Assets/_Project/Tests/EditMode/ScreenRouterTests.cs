using System.Collections.Generic;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class ScreenRouterTests
    {
        [Test]
        public void StartsAtInitialScreen()
        {
            var router = new ScreenRouter(ScreenId.Title);
            Assert.AreEqual(ScreenId.Title, router.Current);
        }

        [Test]
        public void CannotGoBackAtRoot()
        {
            var router = new ScreenRouter(ScreenId.Title);
            Assert.IsFalse(router.CanGoBack);
        }

        [Test]
        public void NavigateChangesCurrentScreen()
        {
            var router = new ScreenRouter(ScreenId.Title);
            router.Navigate(ScreenId.Login);
            Assert.AreEqual(ScreenId.Login, router.Current);
            Assert.IsTrue(router.CanGoBack);
        }

        [Test]
        public void NavigateToCurrentScreenDoesNothing()
        {
            var router = new ScreenRouter(ScreenId.Title);
            var raised = 0;
            router.ScreenChanged += (_, __) => raised++;

            router.Navigate(ScreenId.Title);

            Assert.AreEqual(0, raised);
            Assert.IsFalse(router.CanGoBack);
        }

        [Test]
        public void BackReturnsToPreviousScreensInReverseOrder()
        {
            var router = new ScreenRouter(ScreenId.Title);
            router.Navigate(ScreenId.Login);
            router.Navigate(ScreenId.Verify);

            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.Login, router.Current);
            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.Title, router.Current);
            Assert.IsFalse(router.CanGoBack);
        }

        [Test]
        public void BackAtRootReturnsFalseAndKeepsScreen()
        {
            var router = new ScreenRouter(ScreenId.Title);
            var raised = 0;
            router.ScreenChanged += (_, __) => raised++;

            Assert.IsFalse(router.Back());
            Assert.AreEqual(ScreenId.Title, router.Current);
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ScreenChangedReportsPreviousAndCurrentOnNavigate()
        {
            var router = new ScreenRouter(ScreenId.Title);
            var events = new List<(ScreenId, ScreenId)>();
            router.ScreenChanged += (p, c) => events.Add((p, c));

            router.Navigate(ScreenId.Register);

            CollectionAssert.AreEqual(new[] { (ScreenId.Title, ScreenId.Register) }, events);
        }

        [Test]
        public void ScreenChangedReportsPreviousAndCurrentOnBack()
        {
            var router = new ScreenRouter(ScreenId.Title);
            router.Navigate(ScreenId.Guest);
            var events = new List<(ScreenId, ScreenId)>();
            router.ScreenChanged += (p, c) => events.Add((p, c));

            router.Back();

            CollectionAssert.AreEqual(new[] { (ScreenId.Guest, ScreenId.Title) }, events);
        }
    }
}

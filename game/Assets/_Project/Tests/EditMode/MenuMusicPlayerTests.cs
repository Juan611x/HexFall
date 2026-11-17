using System.Collections.Generic;
using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class MenuMusicPlayerTests
    {
        private sealed class FakeLayers : IMusicLayers
        {
            public int Starts;
            public readonly List<float> Volumes = new List<float> { 0f, 0f };
            public int LayerCount => Volumes.Count;
            public void StartAll() => Starts++;
            public void SetVolume(int layer, float volume) => Volumes[layer] = volume;
        }

        private FakeLayers _layers;

        [SetUp]
        public void SetUp() => _layers = new FakeLayers();

        private MenuMusicPlayer Create(bool requireGesture, float fade = 2f) =>
            new MenuMusicPlayer(_layers, new[] { 0.5f, 0.4f }, fade, requireGesture);

        [Test]
        public void BeginStartsImmediatelyWhenNoGestureIsRequired()
        {
            var player = Create(false);
            player.Begin();
            Assert.AreEqual(1, _layers.Starts);
            Assert.IsTrue(player.IsStarted);
        }

        [Test]
        public void BeginWaitsForAGestureWhenRequired()
        {
            var player = Create(true);
            player.Begin();
            Assert.AreEqual(0, _layers.Starts);
            Assert.IsFalse(player.IsStarted);
        }

        [Test]
        public void GestureStartsTheMusicOnlyOnce()
        {
            var player = Create(true);
            player.Begin();
            player.OnUserGesture();
            player.OnUserGesture();
            Assert.AreEqual(1, _layers.Starts);
            Assert.IsTrue(player.IsStarted);
        }

        [Test]
        public void GestureBeforeBeginDoesNotStart()
        {
            var player = Create(true);
            player.OnUserGesture();
            Assert.AreEqual(0, _layers.Starts);
        }

        [Test]
        public void GestureAfterImmediateStartDoesNotRestart()
        {
            var player = Create(false);
            player.Begin();
            player.OnUserGesture();
            Assert.AreEqual(1, _layers.Starts);
        }

        [Test]
        public void TickDoesNothingBeforeStart()
        {
            var player = Create(true);
            player.Begin();
            player.Tick(1f);
            Assert.AreEqual(0f, _layers.Volumes[0]);
        }

        [Test]
        public void LayersStartSilent()
        {
            var player = Create(false);
            player.Begin();
            Assert.AreEqual(0f, _layers.Volumes[0], 1e-5f);
            Assert.AreEqual(0f, _layers.Volumes[1], 1e-5f);
        }

        [Test]
        public void VolumesRampLinearlyToTheirOwnTargets()
        {
            var player = Create(false, 2f);
            player.Begin();
            player.Tick(1f);
            Assert.AreEqual(0.25f, _layers.Volumes[0], 1e-5f);
            Assert.AreEqual(0.2f, _layers.Volumes[1], 1e-5f);
        }

        [Test]
        public void VolumesReachTargetsAndStayThere()
        {
            var player = Create(false, 2f);
            player.Begin();
            player.Tick(1.5f);
            player.Tick(5f);
            Assert.AreEqual(0.5f, _layers.Volumes[0], 1e-5f);
            Assert.AreEqual(0.4f, _layers.Volumes[1], 1e-5f);
        }

        [Test]
        public void ZeroFadeJumpsStraightToTargets()
        {
            var player = Create(false, 0f);
            player.Begin();
            player.Tick(0f);
            Assert.AreEqual(0.5f, _layers.Volumes[0], 1e-5f);
        }

        [Test]
        public void FadeRunsFromTheMomentOfTheGesture()
        {
            var player = Create(true, 2f);
            player.Begin();
            player.Tick(10f);
            player.OnUserGesture();
            player.Tick(1f);
            Assert.AreEqual(0.25f, _layers.Volumes[0], 1e-5f);
        }
    }
}

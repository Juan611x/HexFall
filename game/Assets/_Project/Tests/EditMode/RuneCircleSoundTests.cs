using System.Collections.Generic;
using HexFall.World;
using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexFall.UI.Tests
{
    // Layer 2 of the rune circle sounds: each effect asks for its own one-shot when it fires; the circle
    // can also hold an ambient loop (layer 1, chosen later). The one-shot player raises SpatialSfx.Requested
    // before it plays, so the effects are tested without any audio device.
    public class RuneCircleSoundTests
    {
        private const string Folder = "Assets/_Project/Prefabs/RuneCircles";
        private const string Clips = "Assets/ThirdParty/CC0Combat150/MagicSpells";

        private sealed class Request
        {
            public AudioClip Clip;
            public Vector3 Position;
            public float Volume;
        }

        private sealed class FakeTarget : IRuneCircleTarget
        {
            public bool Accepts = true;
            public Vector3 FeetPosition => Vector3.zero;
            public float FacingYaw => 0f;
            public bool IsGrounded => true;
            public float GravityMagnitude => 15f;
            public int Launches;
            public void Launch(float verticalSpeed) { Launches++; }
            public bool Teleport(Vector3 position, float yaw) => Accepts;
        }

        private readonly List<Request> _requests = new List<Request>();
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _requests.Clear();
            SpatialSfx.Requested += OnRequested;
        }

        [TearDown]
        public void TearDown()
        {
            SpatialSfx.Requested -= OnRequested;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private void OnRequested(AudioClip clip, Vector3 position, float volume) =>
            _requests.Add(new Request { Clip = clip, Position = position, Volume = volume });

        private AudioClip NewClip(string name)
        {
            var clip = AudioClip.Create(name, 480, 1, 48000, false);
            _spawned.Add(clip);
            return clip;
        }

        private GameObject NewObject(Vector3 position)
        {
            var go = new GameObject("circle");
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void ALaunchRequestsItsSoundAtTheCircle()
        {
            var clip = NewClip("launch");
            var go = NewObject(new Vector3(3f, 0f, 4f));
            var effect = go.AddComponent<LaunchEffect>();
            effect.ConfigureSound(clip, 0.7f);

            effect.Apply(new RuneCircleActivation(go.transform, new FakeTarget()));

            Assert.AreEqual(1, _requests.Count);
            Assert.AreSame(clip, _requests[0].Clip);
            Assert.AreEqual(new Vector3(3f, 0f, 4f), _requests[0].Position);
            Assert.AreEqual(0.7f, _requests[0].Volume, 0.0001f);
        }

        [Test]
        public void ALaunchWithoutAClipIsSilent()
        {
            var go = NewObject(Vector3.zero);
            var effect = go.AddComponent<LaunchEffect>();

            effect.Apply(new RuneCircleActivation(go.transform, new FakeTarget()));

            Assert.IsEmpty(_requests);
        }

        private TeleportEffect NewTeleport(Vector3 from, Vector3 to, AudioClip clip, out GameObject departure)
        {
            departure = NewObject(from);
            var arrival = NewObject(to).AddComponent<RuneCircle>();
            var effect = departure.AddComponent<TeleportEffect>();
            effect.Configure(arrival, true);
            effect.ConfigureSound(clip, 0.9f);
            return effect;
        }

        [Test]
        public void ATeleportRequestsItsSoundAtTheDepartureAndAtTheArrival()
        {
            var clip = NewClip("teleport");
            var effect = NewTeleport(new Vector3(1f, 0f, 0f), new Vector3(30f, 0f, 5f), clip, out var departure);

            effect.Apply(new RuneCircleActivation(departure.transform, new FakeTarget()));

            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), _requests[0].Position);
            Assert.AreEqual(new Vector3(30f, 0f, 5f), _requests[1].Position);
            Assert.AreSame(clip, _requests[0].Clip);
            Assert.AreSame(clip, _requests[1].Clip);
            Assert.AreEqual(0.9f, _requests[1].Volume, 0.0001f);
        }

        [Test]
        public void ARefusedTeleportIsSilent()
        {
            var effect = NewTeleport(Vector3.zero, new Vector3(30f, 0f, 0f), NewClip("teleport"), out var departure);

            effect.Apply(new RuneCircleActivation(departure.transform, new FakeTarget { Accepts = false }));

            Assert.IsEmpty(_requests);
        }

        [Test]
        public void ATeleportWithoutADestinationIsSilent()
        {
            var go = NewObject(Vector3.zero);
            var effect = go.AddComponent<TeleportEffect>();
            effect.ConfigureSound(NewClip("teleport"), 1f);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no destination"));

            effect.Apply(new RuneCircleActivation(go.transform, new FakeTarget()));

            Assert.IsEmpty(_requests);
        }

        [Test]
        public void TheEffectsVolumeScalesTheRequestedVolumeAndStaysInRange()
        {
            Assert.AreEqual(0.35f, SpatialSfx.Scaled(0.7f, 0.5f), 0.0001f);
            Assert.AreEqual(1f, SpatialSfx.Scaled(2f, 1f), 0.0001f);
            Assert.AreEqual(0f, SpatialSfx.Scaled(0.7f, -1f), 0.0001f);
        }

        [Test]
        public void WithoutAnAmbientClipTheCircleStaysSilent()
        {
            var audio = NewObject(Vector3.zero).AddComponent<RuneCircleAudio>();

            Assert.IsNull(audio.AmbientClip);
            Assert.IsNull(audio.Build());
            Assert.IsNull(audio.GetComponent<AudioSource>());
        }

        [Test]
        public void AnAmbientClipBecomesALoopingSpatialSource()
        {
            var audio = NewObject(Vector3.zero).AddComponent<RuneCircleAudio>();
            var clip = NewClip("ambient");
            audio.ConfigureAmbient(clip, 0.5f);

            var source = audio.Build();

            Assert.IsNotNull(source);
            Assert.AreSame(clip, source.clip);
            Assert.IsTrue(source.loop);
            Assert.IsFalse(source.playOnAwake);
            Assert.AreEqual(1f, source.spatialBlend, 0.0001f);
            Assert.Greater(source.maxDistance, source.minDistance);
            Assert.AreEqual(0.5f * SpatialSfx.EffectsLevel(), source.volume, 0.0001f);
        }

        private static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            return prefab;
        }

        [Test]
        public void TheBasePrefabHasTheAmbientHookButNoAmbientClipYet()
        {
            var audio = Load("RuneCircle").GetComponent<RuneCircleAudio>();
            Assert.IsNotNull(audio);
            Assert.IsNull(audio.AmbientClip);
        }

        [Test]
        public void TheLaunchPrefabPlaysTheLaunchClip()
        {
            var effect = Load("RuneCircle_Launch").GetComponent<LaunchEffect>();
            Assert.IsNotNull(effect.Sound);
            Assert.AreEqual("RuneLaunch", effect.Sound.name);
            Assert.Greater(effect.SoundVolume, 0f);
        }

        [Test]
        public void TheTeleportPrefabPlaysTheTeleportClip()
        {
            var effect = Load("RuneCircle_Teleport").GetComponent<TeleportEffect>();
            Assert.IsNotNull(effect.Sound);
            Assert.AreEqual("RuneTeleport", effect.Sound.name);
            Assert.Greater(effect.SoundVolume, 0f);
        }

        [TestCase("RuneTeleport", 0.5f, 1f)]
        [TestCase("RuneLaunch", 1.5f, 2.5f)]
        [TestCase("PickupHeal", 1f, 2f)]
        [TestCase("PickupShield", 1f, 2f)]
        public void TheDerivedClipsAreImportedAsShortMonoSounds(string name, float min, float max)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Clips}/{name}.wav");
            Assert.IsNotNull(clip, name);
            Assert.AreEqual(1, clip.channels);
            Assert.That(clip.length, Is.InRange(min, max));
            var importer = (AudioImporter)AssetImporter.GetAtPath($"{Clips}/{name}.wav");
            Assert.IsTrue(importer.defaultSampleSettings.loadType == AudioClipLoadType.CompressedInMemory);
            Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData);
        }
    }
}

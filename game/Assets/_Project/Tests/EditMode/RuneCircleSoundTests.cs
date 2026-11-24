using System.Collections.Generic;
using System.IO;
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
        private const string Loops = "Assets/ThirdParty/OpenGameArt/Rubberduck30Loops";
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
        public void TheBasePrefabHasTheAmbientHookButNoAmbientClip()
        {
            var audio = Load("RuneCircle").GetComponent<RuneCircleAudio>();
            Assert.IsNotNull(audio);
            Assert.IsNull(audio.AmbientClip);
        }

        [TestCase("RuneCircle_Launch", "RuneAmbienceLaunch", 0.3f)]
        [TestCase("RuneCircle_Teleport", "RuneAmbienceTeleport", 0.3f)]
        public void EachVariantHasItsQuietAmbientLoop(string prefab, string clipName, float volume)
        {
            var audio = Load(prefab).GetComponent<RuneCircleAudio>();
            Assert.IsNotNull(audio.AmbientClip);
            Assert.AreEqual(clipName, audio.AmbientClip.name);
            Assert.AreEqual(volume, audio.AmbientVolume, 0.0001f);
            Assert.AreEqual(1.5f, audio.MinDistance, 0.0001f);
            Assert.AreEqual(12f, audio.MaxDistance, 0.0001f);
        }

        [TestCase("RuneCircle_Launch")]
        [TestCase("RuneCircle_Teleport")]
        public void TheAmbientIsQuieterThanTheEffectSound(string prefab)
        {
            var go = Load(prefab);
            var effectVolume = prefab == "RuneCircle_Launch" ? go.GetComponent<LaunchEffect>().SoundVolume : go.GetComponent<TeleportEffect>().SoundVolume;
            Assert.Less(go.GetComponent<RuneCircleAudio>().AmbientVolume, effectVolume * 0.5f);
        }

        [TestCase("RuneAmbienceLaunch", 8f, 8.5f)]
        [TestCase("RuneAmbienceTeleport", 3f, 4f)]
        public void TheAmbientLoopsAreImportedForLooping3DUse(string name, float min, float max)
        {
            var path = $"{Loops}/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.IsNotNull(clip, name);
            Assert.That(clip.length, Is.InRange(min, max));
            Assert.AreEqual(1, clip.channels, "mono, spatialised by the AudioSource");
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, "PCM in memory keeps the loop point exact");
            Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData);
            Assert.IsTrue(File.Exists($"{Loops}/LICENSE.txt"));
        }

        // The loops are levelled by art/audio/derive_rune_circle_ambience.py to -20 dBFS RMS; the effect clips sit near
        // -15 dBFS, so at ambient volume 0.3 the bed is about 15 dB under them: audible, still background.
        [TestCase("RuneAmbienceLaunch")]
        [TestCase("RuneAmbienceTeleport")]
        public void TheAmbientLoopsAreLevelledToMinusTwentyDb(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Loops}/{name}.wav");
            Assert.IsNotNull(clip, name);
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(data, 0));
            double sum = 0; float peak = 0;
            foreach (var v in data) { sum += v * v; peak = Mathf.Max(peak, Mathf.Abs(v)); }
            var rmsDb = 20.0 * System.Math.Log10(System.Math.Sqrt(sum / data.Length));
            Assert.AreEqual(-20.0, rmsDb, 1.0, "RMS dBFS");
            Assert.LessOrEqual(20.0 * System.Math.Log10(peak), -0.9, "peak dBFS");
        }

        [Test]
        public void TheSourceUsesTheConfiguredRolloff()
        {
            var audio = NewObject(Vector3.zero).AddComponent<RuneCircleAudio>();
            audio.ConfigureAmbient(NewClip("ambient"), 0.25f);

            var source = audio.Build();

            Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
            Assert.AreEqual(audio.MinDistance, source.minDistance, 0.0001f);
            Assert.AreEqual(audio.MaxDistance, source.maxDistance, 0.0001f);
        }

        [TestCase(10f, 0f, 0f)]
        [TestCase(10f, 0.5f, 5f)]
        [TestCase(10f, 1f, 0f)]
        [TestCase(0f, 0.7f, 0f)]
        public void TheLoopStartsAtARandomPointInsideTheClip(float length, float random, float expected)
        {
            Assert.AreEqual(expected, RuneCircleAudio.StartOffset(length, random), 0.0001f);
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

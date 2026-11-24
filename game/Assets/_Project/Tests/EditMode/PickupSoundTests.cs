using System.Collections.Generic;
using HexFall.Player;
using HexFall.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The generic Pickup plays its collect sound once, only when the collect actually gave something.
    public class PickupSoundTests
    {
        private sealed class TestPickup : Pickup
        {
            public bool Gives = true;
            public void Wake() => Awake();
            protected override bool TryApply(GameObject collector) => Gives;
        }

        private readonly List<(AudioClip clip, Vector3 position, float volume)> _requests = new List<(AudioClip, Vector3, float)>();
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

        private void OnRequested(AudioClip clip, Vector3 position, float volume) => _requests.Add((clip, position, volume));

        private TestPickup NewPickup(Vector3 position, out AudioClip clip)
        {
            var go = new GameObject("pickup");
            go.transform.position = position;
            _spawned.Add(go);
            go.AddComponent<BoxCollider>(); // Pickup needs a collider, which RequireComponent cannot add (abstract type)
            var pickup = go.AddComponent<TestPickup>();
            clip = AudioClip.Create("collect", 480, 1, 48000, false);
            _spawned.Add(clip);
            pickup.ConfigureCollectSound(clip, 0.6f);
            pickup.Wake();
            return pickup;
        }

        [Test]
        public void ACollectThatGivesSomethingPlaysTheSoundAtThePickup()
        {
            var pickup = NewPickup(new Vector3(2f, 1f, 3f), out var clip);

            Assert.IsTrue(pickup.TryCollect(pickup.gameObject));

            Assert.AreEqual(1, _requests.Count);
            Assert.AreSame(clip, _requests[0].clip);
            Assert.AreEqual(new Vector3(2f, 1f, 3f), _requests[0].position);
            Assert.AreEqual(0.6f, _requests[0].volume, 0.0001f);
        }

        [Test]
        public void ACollectThatGivesNothingIsSilent()
        {
            var pickup = NewPickup(Vector3.zero, out _);
            pickup.Gives = false;

            Assert.IsFalse(pickup.TryCollect(pickup.gameObject));

            Assert.IsEmpty(_requests);
        }

        [Test]
        public void AHiddenPickupPlaysNothingAgain()
        {
            var pickup = NewPickup(Vector3.zero, out _);
            pickup.TryCollect(pickup.gameObject);

            Assert.IsFalse(pickup.TryCollect(pickup.gameObject));

            Assert.AreEqual(1, _requests.Count);
        }

        [Test]
        public void APickupWithoutAClipCollectsInSilence()
        {
            var pickup = NewPickup(Vector3.zero, out _);
            pickup.ConfigureCollectSound(null, 1f);

            Assert.IsTrue(pickup.TryCollect(pickup.gameObject));

            Assert.IsEmpty(_requests);
        }

        [TestCase("Pickup_HealthPotion", "PickupHeal")]
        [TestCase("Pickup_ShieldRefill", "PickupShield")]
        public void ThePickupPrefabsCarryTheirCollectClip(string prefabName, string clipName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Pickups/{prefabName}.prefab");
            var pickup = prefab.GetComponent<Pickup>();
            Assert.IsNotNull(pickup.CollectSound);
            Assert.AreEqual(clipName, pickup.CollectSound.name);
            Assert.Greater(pickup.CollectVolume, 0f);
        }
    }
}

using System.Collections.Generic;
using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The RuneCircle component driven by a fake target: discovery, footprint and grounded wiring to the
    // gate, effect order and resilience to a destroyed target. The circle is ticked by hand (EditMode runs no Update).
    public class RuneCircleAdapterTests
    {
        private const float Dt = 0.1f;

        private sealed class FakeTarget : IRuneCircleTarget
        {
            public Vector3 FeetPosition { get; set; }
            public float FacingYaw => 0f;
            public bool IsGrounded { get; set; } = true;
            public float GravityMagnitude => 15f;
            public int Launches;
            public void Launch(float verticalSpeed) { Launches++; }
            public void Teleport(Vector3 position, float yaw) { }
        }

        // A destroyable target that, like the player's motor, is a component.
        private sealed class BehaviourTarget : MonoBehaviour, IRuneCircleTarget
        {
            public bool IsGrounded => true;
            public float GravityMagnitude => 15f;
            public Vector3 FeetPosition => transform.position;
            public float FacingYaw => 0f;
            public void Launch(float verticalSpeed) { }
            public void Teleport(Vector3 position, float yaw) { }
        }

        private static readonly List<string> Log = new List<string>();

        private sealed class EffectA : MonoBehaviour, IRuneCircleEffect { public void Apply(RuneCircleActivation a) { Log.Add("A"); } }
        private sealed class EffectB : MonoBehaviour, IRuneCircleEffect { public void Apply(RuneCircleActivation a) { Log.Add("B"); } }
        private sealed class EffectC : MonoBehaviour, IRuneCircleEffect { public void Apply(RuneCircleActivation a) { Log.Add("C"); } }

        private sealed class CountingEffect : MonoBehaviour, IRuneCircleEffect
        {
            public int Applied;
            [System.NonSerialized] public RuneCircleActivation Last;
            public void Apply(RuneCircleActivation a) { Applied++; Last = a; }
        }

        // Asks the target to launch, like LaunchEffect does.
        private sealed class LaunchingEffect : MonoBehaviour, IRuneCircleEffect
        {
            public void Apply(RuneCircleActivation a) { a.Target.Launch(10f); }
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => Log.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private RuneCircle NewCircle(Vector3? position = null)
        {
            var go = new GameObject("circle");
            _spawned.Add(go);
            go.transform.position = position ?? Vector3.zero;
            return go.AddComponent<RuneCircle>();
        }

        private static void Run(RuneCircle circle, int ticks)
        {
            for (var i = 0; i < ticks; i++) circle.Tick(Dt);
        }

        [Test]
        public void LockBeforeAwakeDoesNotThrow()
        {
            var circle = NewCircle();
            Assert.DoesNotThrow(() => circle.Lock());
        }

        [Test]
        public void ALockedCircleStaysQuietUntilTheTargetLeavesAndReturns()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var target = new FakeTarget { FeetPosition = Vector3.zero };
            circle.Lock(target);

            Run(circle, 5);
            Assert.AreEqual(0, counter.Applied, "Arriving inside a locked circle must not fire it.");

            target.FeetPosition = new Vector3(5f, 0f, 0f);
            Run(circle, 2);
            target.FeetPosition = Vector3.zero;
            Run(circle, 3);
            Assert.AreEqual(1, counter.Applied, "Stepping off and back on fires it again.");
        }

        [Test]
        public void NothingFiresBeforeAPlayerIsDiscovered()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            Assert.DoesNotThrow(() => Run(circle, 5));
            Assert.AreEqual(0, counter.Applied);
        }

        [Test]
        public void ATriggerEntryWithoutATargetIsIgnored()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var rock = new GameObject("rock");
            _spawned.Add(rock);

            circle.OnTriggerEnter(rock.AddComponent<BoxCollider>());
            Run(circle, 5);

            Assert.AreEqual(0, counter.Applied);
        }

        [Test]
        public void ATriggerEntryDiscoversTheTargetOnTheColliderOrItsParent()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var player = new GameObject("player").AddComponent<BehaviourTarget>();
            _spawned.Add(player.gameObject);
            var body = new GameObject("body");
            body.transform.SetParent(player.transform, false);

            circle.OnTriggerEnter(body.AddComponent<CapsuleCollider>());
            Run(circle, 3);

            Assert.AreEqual(1, counter.Applied);
            Assert.AreSame(player, counter.Last.Target);
            Assert.AreSame(circle.transform, counter.Last.Circle);
        }

        [Test]
        public void StandingInsideTheDiscFiresOnceAndOutsideItNever()
        {
            var circle = NewCircle(new Vector3(3f, 0f, 3f));
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var target = new FakeTarget { FeetPosition = new Vector3(4f, 0f, 3f) };
            circle.Track(target);

            Run(circle, 3);
            Assert.AreEqual(0, counter.Applied, "1 m away is outside the 0.9 m disc.");

            target.FeetPosition = new Vector3(3.5f, 0f, 3f);
            Run(circle, 10);
            Assert.AreEqual(1, counter.Applied, "Fires once per stay.");
        }

        [Test]
        public void AboveTheHeightBandIsOutside()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var target = new FakeTarget { FeetPosition = new Vector3(0f, 2f, 0f), IsGrounded = false };
            circle.Track(target);

            Run(circle, 5);
            Assert.AreEqual(0, counter.Applied);
        }

        [Test]
        public void RequireGroundedKeepsAnAirbornePlayerInsideFromFiring()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var target = new FakeTarget { FeetPosition = new Vector3(0f, 0.5f, 0f), IsGrounded = false };
            circle.Track(target);

            Run(circle, 5);
            Assert.AreEqual(0, counter.Applied);

            target.IsGrounded = true;
            Run(circle, 1);
            Assert.AreEqual(1, counter.Applied);
        }

        [Test]
        public void EffectsApplyInComponentOrder()
        {
            var circle = NewCircle();
            circle.gameObject.AddComponent<EffectB>();
            circle.gameObject.AddComponent<EffectC>();
            circle.gameObject.AddComponent<EffectA>();
            circle.Track(new FakeTarget());

            Run(circle, 1);

            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, Log);
        }

        [Test]
        public void ADestroyedTargetIsHandledSafely()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var player = new GameObject("player").AddComponent<BehaviourTarget>();
            circle.Track(player);
            Run(circle, 1);
            Assert.AreEqual(1, counter.Applied);

            Object.DestroyImmediate(player.gameObject);

            Assert.DoesNotThrow(() => Run(circle, 20));
            Assert.AreEqual(1, counter.Applied);
        }

        [Test]
        public void AnInactiveTargetCountsAsOutside()
        {
            var circle = NewCircle();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var player = new GameObject("player").AddComponent<BehaviourTarget>();
            _spawned.Add(player.gameObject);
            player.gameObject.SetActive(false);
            circle.Track(player);

            Run(circle, 5);
            Assert.AreEqual(0, counter.Applied);
        }

        [Test]
        public void ARefusedLaunchStillConsumesTheFiring()
        {
            // The effect's request may be ignored (the motor refuses a launch while climbing) but the
            // gate has already fired: the player must leave and return for another try.
            var circle = NewCircle();
            circle.gameObject.AddComponent<LaunchingEffect>();
            var counter = circle.gameObject.AddComponent<CountingEffect>();
            var target = new FakeTarget();
            circle.Track(target);

            Run(circle, 20);

            Assert.AreEqual(1, target.Launches);
            Assert.AreEqual(1, counter.Applied, "Staying put does not retry a refused launch.");
        }
    }
}

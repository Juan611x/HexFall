using System.Collections.Generic;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The ledge sensor against real colliders. Feet at the origin looking +Z; the floor top is y = 0.
    public class LedgeProbeTests
    {
        private const float Radius = 0.28f;
        private const float SlopeLimit = 45f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private ClimbSettings _climb;
        private float _crouchHeight;

        [SetUp]
        public void SetUp()
        {
            _climb = new ClimbSettings();
            _crouchHeight = new LocomotionSettings().crouchHeight;
            Box("Floor", new Vector3(0f, -0.5f, 5f), new Vector3(20f, 1f, 20f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject Box(string name, Vector3 center, Vector3 size, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            if (parent == null) _created.Add(go);
            return go;
        }

        private LedgeMeasurement Measure(Vector3 direction, Transform ignoreRoot = null)
        {
            Physics.SyncTransforms();
            var probe = new LedgeProbe(~0, Radius, SlopeLimit, _climb, _crouchHeight, ignoreRoot);
            return probe.Measure(Vector3.zero, direction);
        }

        // A block whose near face is at z = 0.5.
        private void Block(float height, float depth) =>
            Box("Block", new Vector3(0f, height * 0.5f, 0.5f + depth * 0.5f), new Vector3(4f, height, depth));

        [Test]
        public void DeepBlockMeasuresAClimbUp()
        {
            Block(1f, 1f);

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(1f, ledge.TopHeight, 0.03f);
            Assert.AreEqual(0.5f, ledge.TopPoint.z, 0.05f);
            Assert.AreEqual(1f, ledge.TopPoint.y, 0.03f);
            Assert.AreEqual(1f, Vector3.Dot(ledge.ApproachDirection, Vector3.forward), 0.01f);
            Assert.GreaterOrEqual(ledge.TopDepth, _climb.standDepth);
            Assert.GreaterOrEqual(ledge.Headroom, _climb.minHeadroom);
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void ThinWallWithFloorBehindMeasuresAClimbOver()
        {
            Block(1f, 0.2f);

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(1f, ledge.TopHeight, 0.03f);
            Assert.AreEqual(0.2f, ledge.TopDepth, 0.04f);
            Assert.IsTrue(ledge.LandingClear);
            Assert.AreEqual(0f, ledge.LandingHeight, 0.03f);
            Assert.AreEqual(ClimbKind.ClimbOver, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void StepBelowTheMinimumMeasuresItsHeightAndClassifiesNone()
        {
            Block(0.3f, 1f);

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(0.3f, ledge.TopHeight, 0.03f);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void WallAboveTheMaximumHasNoLedge()
        {
            Block(2f, 1f);

            var ledge = Measure(Vector3.forward);

            Assert.IsFalse(ledge.HasLedge);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void LowCeilingOverTheTopLeavesNoHeadroom()
        {
            Block(1f, 1f);
            Box("Ceiling", new Vector3(0f, 2f + 0.1f, 1f), new Vector3(4f, 0.2f, 2f));

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(1f, ledge.Headroom, 0.05f);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void BeamLowOverAThinWallLeavesTooLittleHeadroomToClimbOver()
        {
            Block(1f, 0.2f);
            // Beam from just before the wall to past it, bottom 0.6 m above the top; the landing stays free.
            Box("Beam", new Vector3(0f, 1.6f + 0.1f, 0.675f), new Vector3(4f, 0.2f, 0.45f));

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.IsTrue(ledge.LandingClear, "the landing itself is free");
            Assert.AreEqual(0.6f, ledge.Headroom, 0.05f);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void ThinWallWithClearSpaceOverItStillClimbsOver()
        {
            Block(1f, 0.2f);

            var ledge = Measure(Vector3.forward);

            Assert.GreaterOrEqual(ledge.Headroom, _climb.overMinHeadroom);
            Assert.AreEqual(ClimbKind.ClimbOver, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void ThinWallWithAWallRightBehindBlocksTheLanding()
        {
            Block(1f, 0.2f);
            Box("BackWall", new Vector3(0f, 1f, 1.1f), new Vector3(4f, 2f, 0.2f));

            var ledge = Measure(Vector3.forward);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(0.2f, ledge.TopDepth, 0.04f);
            Assert.IsFalse(ledge.LandingClear);
            Assert.AreEqual(ClimbKind.None, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void AngledApproachStillMeasuresAlongTheWallNormal()
        {
            Block(1f, 1f);
            var direction = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;

            var ledge = Measure(direction);

            Assert.IsTrue(ledge.HasLedge);
            Assert.AreEqual(1f, Vector3.Dot(ledge.ApproachDirection, Vector3.forward), 0.01f);
            Assert.AreEqual(1f, ledge.TopHeight, 0.03f);
            Assert.AreEqual(0.5f, ledge.TopPoint.z, 0.05f);
            Assert.AreEqual(ClimbKind.ClimbUp, LedgeClassifier.Classify(ledge, _climb));
        }

        [Test]
        public void ObstacleBeyondTheDetectionDistanceIsNotSensed()
        {
            Box("FarBlock", new Vector3(0f, 0.5f, 3f), new Vector3(4f, 1f, 1f));

            Assert.IsFalse(Measure(Vector3.forward).HasLedge);
        }

        [Test]
        public void OwnCollidersAreIgnored()
        {
            var root = new GameObject("Player");
            _created.Add(root);
            Box("Hand", new Vector3(0f, 0.5f, 0.4f), new Vector3(0.3f, 1f, 0.1f), root.transform);

            Assert.IsFalse(Measure(Vector3.forward, root.transform).HasLedge, "ignored");
            Assert.IsTrue(Measure(Vector3.forward).HasLedge, "control: counted when not ignored");
        }

        [Test]
        public void TriggersAreIgnored()
        {
            var block = Box("Zone", new Vector3(0f, 0.5f, 1f), new Vector3(4f, 1f, 1f));
            block.GetComponent<BoxCollider>().isTrigger = true;

            Assert.IsFalse(Measure(Vector3.forward).HasLedge);
        }
    }
}

using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The Sandbox climb course (TestArena/ClimbCourse): one obstacle per climb branch, sized against
    // the climb thresholds serialized on the player prefab (not the C# defaults).
    public class ClimbCourseSceneTests
    {
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox.unity";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private static float Climb(string field)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, "Player prefab not found.");
            var motor = prefab.GetComponentInChildren<PlayerMotor>(true);
            Assert.IsNotNull(motor, "PlayerMotor not found on the prefab.");
            var prop = new SerializedObject(motor).FindProperty("settings.climb." + field);
            Assert.IsNotNull(prop, $"settings.climb.{field} not serialized.");
            return prop.floatValue;
        }

        private static void WithCourse(System.Action<Transform> check)
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try
            {
                var course = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == "ClimbCourse");
                Assert.IsNotNull(course, "ClimbCourse not found in Sandbox.");
                Assert.IsNotNull(course.parent);
                Assert.AreEqual("TestArena", course.parent.name);
                Physics.SyncTransforms();
                check(course);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Bounds Part(Transform course, string name)
        {
            var part = course.Find(name);
            Assert.IsNotNull(part, $"{name} not found.");
            var box = part.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, $"{name} has no BoxCollider.");
            Assert.IsFalse(box.isTrigger);
            Assert.AreEqual(0, box.gameObject.layer);
            Assert.AreEqual("M_TestWall", part.GetComponent<MeshRenderer>().sharedMaterial.name);
            return box.bounds;
        }

        // Approach is along +z: the depth of a top is its z size; the front face is the lowest z.
        [Test]
        public void LowStepIsBelowTheClimbMinimum()
        {
            WithCourse(c => Assert.Less(Part(c, "Step_Low").max.y, Climb("climbMinHeight")));
        }

        [Test]
        public void ClimbBoxIsInRangeAndDeepEnoughToStandOn()
        {
            WithCourse(c =>
            {
                var b = Part(c, "Box_ClimbUp");
                Assert.GreaterOrEqual(b.max.y, Climb("climbMinHeight"));
                Assert.LessOrEqual(b.max.y, Climb("climbMaxHeight"));
                Assert.GreaterOrEqual(b.size.z, Climb("standDepth"));
            });
        }

        [Test]
        public void OverWallIsInRangeThinAndHasLandingRoomBehind()
        {
            WithCourse(c =>
            {
                var b = Part(c, "Wall_Over");
                Assert.GreaterOrEqual(b.max.y, Climb("climbMinHeight"));
                Assert.LessOrEqual(b.max.y, Climb("climbMaxHeight"));
                Assert.LessOrEqual(b.size.z, Climb("overMaxThickness"));
                Assert.Less(b.size.z, Climb("standDepth"), "Too deep to be classified over.");
                var depth = Climb("landingClearDepth") + 0.6f;
                var half = new Vector3(0.5f, 0.4f, depth / 2f);
                var center = new Vector3(b.center.x, 0.5f, b.max.z + 0.02f + depth / 2f);
                Assert.IsFalse(Physics.CheckBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore),
                    "Landing area behind the wall is blocked.");
            });
        }

        [Test]
        public void TooHighWallIsAboveTheClimbMaximum()
        {
            WithCourse(c => Assert.Greater(Part(c, "Wall_TooHigh").max.y, Climb("climbMaxHeight")));
        }

        [Test]
        public void BeamWallLeavesLessHeadroomThanOverNeeds()
        {
            WithCourse(c =>
            {
                var wall = Part(c, "Wall_Beam");
                var beam = Part(c, "Beam_Low");
                Assert.GreaterOrEqual(wall.max.y, Climb("climbMinHeight"));
                Assert.LessOrEqual(wall.max.y, Climb("climbMaxHeight"));
                Assert.LessOrEqual(wall.size.z, Climb("overMaxThickness"));
                Assert.Less(beam.min.y - wall.max.y, Climb("overMinHeadroom"), "Headroom over the wall.");
                Assert.Greater(beam.min.y - wall.max.y, 0.3f, "Beam too low to be a fair test.");
                Assert.IsTrue(beam.min.x < wall.center.x && beam.max.x > wall.center.x);
                Assert.IsTrue(beam.min.z < wall.center.z && beam.max.z > wall.center.z, "Beam must be over the wall top.");
                Assert.GreaterOrEqual(beam.min.z, wall.min.z - 0.001f, "Beam must not stick out in front of the wall face.");
            });
        }

        [Test]
        public void ObstaclesDoNotOverlapEachOtherOrOtherTestArenaParts()
        {
            WithCourse(c =>
            {
                var own = c.GetComponentsInChildren<BoxCollider>().ToList();
                Assert.GreaterOrEqual(own.Count, 6);
                var others = c.parent.GetComponentsInChildren<Collider>()
                    .Where(o => !o.transform.IsChildOf(c) && o.name != "Ground").ToList();
                foreach (var a in own)
                {
                    foreach (var o in others)
                        Assert.IsFalse(a.bounds.Intersects(o.bounds), $"{a.name} overlaps {o.name}.");
                    foreach (var b in own.Where(b => b != a))
                        Assert.IsFalse(a.bounds.Intersects(b.bounds), $"{a.name} overlaps {b.name}.");
                }
            });
        }

        [Test]
        public void EachObstacleHasRunUpRoomInFront()
        {
            WithCourse(c =>
            {
                foreach (var name in new[] { "Step_Low", "Box_ClimbUp", "Wall_Over", "Wall_TooHigh", "Wall_Beam" })
                {
                    var b = Part(c, name);
                    var half = new Vector3(0.5f, 0.4f, 3f);
                    var center = new Vector3(b.center.x, 0.5f, b.min.z - 3.2f);
                    Assert.IsFalse(Physics.CheckBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore),
                        $"No 6 m run-up in front of {name}.");
                }
            });
        }
    }
}

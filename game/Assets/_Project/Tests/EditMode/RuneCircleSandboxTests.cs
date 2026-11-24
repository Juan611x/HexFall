using System.Linq;
using HexFall.World.RuneCircles;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The Sandbox rune circles: one launch circle where the old test object stood and a distant teleport pair.
    public class RuneCircleSandboxTests
    {
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox.unity";
        private const string Folder = "Assets/_Project/Prefabs/RuneCircles";

        private static void WithSandbox(System.Action<Scene> check)
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try { check(scene); }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Transform Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name);

        private static void AssertInstanceOf(Transform t, string variant)
        {
            Assert.IsNotNull(t, $"{variant} instance missing.");
            Assert.IsTrue(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject), $"{t.name} is not a prefab instance root.");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            Assert.AreEqual($"{Folder}/{variant}.prefab", AssetDatabase.GetAssetPath(source));
        }

        [Test]
        public void TheOldJumpCircleTestObjectIsGone()
        {
            WithSandbox(scene => Assert.IsNull(Find(scene, "Jump Circle")));
        }

        [Test]
        public void GroupHoldsTheLaunchCircleAndTheTeleportPair()
        {
            WithSandbox(scene =>
            {
                var group = Find(scene, "RuneCircles");
                Assert.IsNotNull(group);
                Assert.IsNull(group.parent);
                CollectionAssert.AreEquivalent(
                    new[] { "RuneCircle_Launch", "RuneCircle_Teleport_A", "RuneCircle_Teleport_B" },
                    group.Cast<Transform>().Select(t => t.name).ToArray());
                AssertInstanceOf(group.Find("RuneCircle_Launch"), "RuneCircle_Launch");
                AssertInstanceOf(group.Find("RuneCircle_Teleport_A"), "RuneCircle_Teleport");
                AssertInstanceOf(group.Find("RuneCircle_Teleport_B"), "RuneCircle_Teleport");
            });
        }

        [Test]
        public void LaunchCircleKeepsTheOldSpot()
        {
            WithSandbox(scene =>
            {
                var p = Find(scene, "RuneCircle_Launch").position;
                Assert.AreEqual(7.5f, p.x, 0.001f);
                Assert.AreEqual(0.01f, p.y, 0.001f);
                Assert.AreEqual(-8f, p.z, 0.001f);
            });
        }

        [Test]
        public void CirclesSitOnTheFloorAndTheTeleportPairIsFarApart()
        {
            WithSandbox(scene =>
            {
                foreach (var n in new[] { "RuneCircle_Launch", "RuneCircle_Teleport_A", "RuneCircle_Teleport_B" })
                {
                    var p = Find(scene, n).position;
                    Assert.AreEqual(0.01f, p.y, 0.001f, n);
                    Assert.LessOrEqual(Mathf.Abs(p.x), 28f, n);
                    Assert.LessOrEqual(Mathf.Abs(p.z), 28f, n);
                }
                var a = Find(scene, "RuneCircle_Teleport_A").position;
                var b = Find(scene, "RuneCircle_Teleport_B").position;
                Assert.GreaterOrEqual(Vector3.Distance(a, b), 15f);
            });
        }

        [Test]
        public void TheTeleportPairPointsAtEachOther()
        {
            WithSandbox(scene =>
            {
                var a = Find(scene, "RuneCircle_Teleport_A");
                var b = Find(scene, "RuneCircle_Teleport_B");
                Assert.AreSame(b.GetComponent<RuneCircle>(), a.GetComponent<TeleportEffect>().Destination);
                Assert.AreSame(a.GetComponent<RuneCircle>(), b.GetComponent<TeleportEffect>().Destination);
            });
        }

        [Test]
        public void TheLaunchCircleHasNoTeleportEffect()
        {
            WithSandbox(scene => Assert.IsNull(Find(scene, "RuneCircle_Launch").GetComponent<TeleportEffect>()));
        }
    }
}

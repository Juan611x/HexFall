using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // The Sandbox crouch tunnel: a 1.4 m roof over a ~2.5 m wide, ~5 m long lane.
    public class CrouchTunnelSceneTests
    {
        private const string SandboxPath = "Assets/_Project/Scenes/Sandbox.unity";
        private const float Radius = 0.28f;

        private static void WithTunnel(System.Action<Transform> check)
        {
            var scene = SceneManager.GetSceneByPath(SandboxPath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Additive);
            try
            {
                var tunnel = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == "CrouchTunnel");
                Assert.IsNotNull(tunnel, "CrouchTunnel not found in Sandbox.");
                Assert.IsNotNull(tunnel.parent);
                Assert.AreEqual("TestArena", tunnel.parent.name);
                Physics.SyncTransforms();
                check(tunnel);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static BoxCollider Part(Transform tunnel, string name)
        {
            var part = tunnel.Find(name);
            Assert.IsNotNull(part, $"{name} not found.");
            var box = part.GetComponent<BoxCollider>();
            Assert.IsNotNull(box, $"{name} has no BoxCollider.");
            return box;
        }

        [TestCase("Tunnel_Roof")]
        [TestCase("Tunnel_WallLeft")]
        [TestCase("Tunnel_WallRight")]
        public void PartsAreSolidDefaultLayerBrickBoxes(string name)
        {
            WithTunnel(tunnel =>
            {
                var box = Part(tunnel, name);
                Assert.IsFalse(box.isTrigger);
                Assert.AreEqual(0, box.gameObject.layer);
                Assert.AreEqual("M_TestWall", box.GetComponent<MeshRenderer>().sharedMaterial.name);
            });
        }

        [Test]
        public void RoofUndersideIsAtOnePointFourMetres()
        {
            WithTunnel(tunnel => Assert.AreEqual(1.4f, Part(tunnel, "Tunnel_Roof").bounds.min.y, 0.01f));
        }

        [Test]
        public void InteriorIsWideAndLongEnough()
        {
            WithTunnel(tunnel =>
            {
                var left = Part(tunnel, "Tunnel_WallLeft").bounds;
                var right = Part(tunnel, "Tunnel_WallRight").bounds;
                var lo = left.center.x < right.center.x ? left : right;
                var hi = left.center.x < right.center.x ? right : left;
                Assert.GreaterOrEqual(hi.min.x - lo.max.x, 2f, "Interior width");
                Assert.GreaterOrEqual(Part(tunnel, "Tunnel_Roof").bounds.size.z, 4f, "Interior length");
            });
        }

        [Test]
        public void InteriorFitsACrouchedCapsuleButNotAStandingOne()
        {
            WithTunnel(tunnel =>
            {
                var c = Part(tunnel, "Tunnel_Roof").bounds.center;
                bool Overlaps(float height)
                {
                    var bottom = new Vector3(c.x, 0.02f + Radius, c.z);
                    var top = new Vector3(c.x, 0.02f + height - Radius, c.z);
                    return Physics.CheckCapsule(bottom, top, Radius, ~0, QueryTriggerInteraction.Ignore);
                }
                Assert.IsFalse(Overlaps(1.1f), "1.1 m capsule should fit.");
                Assert.IsTrue(Overlaps(1.8f), "1.8 m capsule should be blocked.");
            });
        }
    }
}

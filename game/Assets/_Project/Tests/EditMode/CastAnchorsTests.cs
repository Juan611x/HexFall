using System.Collections.Generic;
using System.Reflection;
using HexFall.Player;
using HexFall.Player.Spells;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HexFall.UI.Tests
{
    // Where a spell starts from: a per-spell anchor id resolved to a transform on the wizard.
    public class CastAnchorsTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private Transform Point(string name, Vector3 at)
        {
            var go = new GameObject(name) { transform = { position = at } };
            _made.Add(go);
            return go.transform;
        }

        private CastAnchors Anchors(params (string Id, Transform Anchor)[] entries)
        {
            var host = new GameObject("Anchors");
            _made.Add(host);
            var anchors = host.AddComponent<CastAnchors>();
            anchors.Configure(entries);
            return anchors;
        }

        [Test]
        public void AnIdResolvesToItsTransform()
        {
            var muzzle = Point("m", Vector3.zero);
            var orb = Point("o", Vector3.up);
            var anchors = Anchors((CastAnchors.Muzzle, muzzle), (CastAnchors.StaffOrb, orb));

            Assert.AreSame(orb, anchors.Resolve(CastAnchors.StaffOrb));
            Assert.AreSame(muzzle, anchors.Resolve(CastAnchors.Muzzle));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("NoSuchAnchor")]
        public void AnEmptyOrUnknownIdFallsBackToTheMuzzle(string id)
        {
            var muzzle = Point("m", Vector3.zero);
            var anchors = Anchors((CastAnchors.Muzzle, muzzle), (CastAnchors.StaffOrb, Point("o", Vector3.up)));

            Assert.AreSame(muzzle, anchors.Resolve(id));
        }

        [Test]
        public void IdsMatchIgnoringCase()
        {
            var orb = Point("o", Vector3.up);
            var anchors = Anchors((CastAnchors.Muzzle, Point("m", Vector3.zero)), (CastAnchors.StaffOrb, orb));
            Assert.AreSame(orb, anchors.Resolve("stafforb"));
        }

        [Test]
        public void AnEntryWithoutATransformFallsBackToTheMuzzle()
        {
            var muzzle = Point("m", Vector3.zero);
            var anchors = Anchors((CastAnchors.Muzzle, muzzle), (CastAnchors.StaffOrb, null));
            Assert.AreSame(muzzle, anchors.Resolve(CastAnchors.StaffOrb));
        }

        [Test]
        public void ADestroyedAnchorFallsBackToTheMuzzle()
        {
            var muzzle = Point("m", Vector3.zero);
            var orb = Point("o", Vector3.up);
            var anchors = Anchors((CastAnchors.Muzzle, muzzle), (CastAnchors.StaffOrb, orb));
            Object.DestroyImmediate(orb.gameObject);
            Assert.AreSame(muzzle, anchors.Resolve(CastAnchors.StaffOrb));
        }

        [Test]
        public void WithNoMuzzleEntryAnUnknownIdResolvesToNull()
        {
            var anchors = Anchors((CastAnchors.StaffOrb, Point("o", Vector3.up)));
            Assert.IsNull(anchors.Resolve("NoSuchAnchor"));
        }

        [Test]
        public void PositionForUsesTheAnchorOrTheGivenFallback()
        {
            var anchors = Anchors((CastAnchors.StaffOrb, Point("o", new Vector3(1f, 2f, 3f))));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), anchors.PositionFor(CastAnchors.StaffOrb, Vector3.zero));
            Assert.AreEqual(new Vector3(9f, 9f, 9f), anchors.PositionFor("NoSuchAnchor", new Vector3(9f, 9f, 9f)));
        }

        // ---- the wizard prefab ----

        private GameObject Wizard(out Scene scene)
        {
            scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        }

        [Test]
        public void TheWizardMapsMuzzleToItsBoltMuzzleAndStaffOrbToATransformOnTheStaff()
        {
            var wizard = Wizard(out var scene);
            try
            {
                var anchors = wizard.GetComponent<CastAnchors>();
                Assert.IsNotNull(anchors, "PlayerWizard needs CastAnchors");
                Assert.AreEqual("BoltMuzzle", anchors.Resolve(CastAnchors.Muzzle).name);

                var orb = anchors.Resolve(CastAnchors.StaffOrb);
                Assert.IsNotNull(orb);
                Assert.AreNotEqual("BoltMuzzle", orb.name, "the staff orb is its own anchor");
                var staff = FindByName(wizard.transform, "Wizard_Staff");
                Assert.IsNotNull(staff);
                Assert.IsTrue(orb.IsChildOf(staff), "the orb anchor rides on the staff");
            }
            finally
            {
                Object.DestroyImmediate(wizard);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void TheStaffOrbAnchorSitsAtTheBallEndOfTheStaffMesh()
        {
            var wizard = Wizard(out var scene);
            try
            {
                var staff = FindByName(wizard.transform, "Wizard_Staff");
                var orb = wizard.GetComponent<CastAnchors>().Resolve(CastAnchors.StaffOrb);
                var mesh = staff.GetComponent<MeshFilter>().sharedMesh;
                var local = staff.InverseTransformPoint(orb.position);
                // The ball is the thick end: the mesh's lowest Y (about -0.47 .. -0.31 in staff space).
                Assert.That(local.y, Is.InRange(mesh.bounds.min.y, mesh.bounds.min.y + mesh.bounds.size.y * 0.25f));
                Assert.That(Mathf.Abs(local.x), Is.LessThan(0.03f), "on the staff axis");
                Assert.That(Mathf.Abs(local.z), Is.LessThan(0.03f), "on the staff axis");
            }
            finally
            {
                Object.DestroyImmediate(wizard);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}

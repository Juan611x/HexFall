using System.Collections.Generic;
using HexFall.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The point under the crosshair skips the wizard's own colliders (children of the PlayerCombat host)
    // and stops at anything else.
    public class PlayerCombatAimTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private readonly List<Object> _made = new List<Object>();
        private PlayerCombat _combat;
        private Camera _camera;

        private T Track<T>(T o) where T : Object
        {
            _made.Add(o);
            return o;
        }

        [SetUp]
        public void SetUp()
        {
            var wizard = Track(new GameObject("Wizard"));
            _combat = wizard.AddComponent<PlayerCombat>();
            var camGo = Track(new GameObject("Cam"));
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            _camera = camGo.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private GameObject Box(string name, Vector3 at, Transform parent = null)
        {
            var go = Track(new GameObject(name));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<BoxCollider>();
            return go;
        }

        [Test]
        public void TheAimSkipsTheWizardsOwnChildColliderInTheWay()
        {
            Box("Hand", new Vector3(0f, 0f, -5f), _combat.transform);
            Box("Wall", new Vector3(0f, 0f, 5f));
            Physics.SyncTransforms();

            Assert.AreEqual(4.5f, _combat.AimPointFrom(_camera).z, 1e-3f);
        }

        [Test]
        public void TheAimStopsAtAnObjectThatIsNotPartOfTheWizard()
        {
            Box("Pillar", new Vector3(0f, 0f, -5f));
            Box("Wall", new Vector3(0f, 0f, 5f));
            Physics.SyncTransforms();

            Assert.AreEqual(-5.5f, _combat.AimPointFrom(_camera).z, 1e-3f);
        }

        [Test]
        public void TheAimFarPointIsUsedWhenNothingIsHit()
        {
            Box("Hand", new Vector3(0f, 0f, -5f), _combat.transform);
            Physics.SyncTransforms();

            Assert.AreEqual(90f, _combat.AimPointFrom(_camera).z, 0.5f); // 100 m along the ray, which starts at the near plane
        }

        [Test]
        public void PlayerCombatSitsOnThePlayerWizardRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var combat = prefab.GetComponent<PlayerCombat>();
            Assert.IsNotNull(combat, "the aim filter is relative to the PlayerCombat host, so it must be the wizard root");
            Assert.AreSame(prefab.transform, combat.transform);
        }
    }
}

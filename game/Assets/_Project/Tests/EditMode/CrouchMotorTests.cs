using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexFall.Player.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class CrouchBindingsTests
    {
        private const string ControlsPath = "_Project/Input/HexFallControls.inputactions";

        private static string[] Lines() => File.ReadAllLines(Path.Combine(Application.dataPath, ControlsPath));

        [Test]
        public void CrouchActionExistsAsAButton()
        {
            Assert.IsTrue(Lines().Any(l => l.Contains("\"name\": \"Crouch\"") && l.Contains("\"type\": \"Button\"")));
        }

        [Test]
        public void CrouchIsBoundToCOnKeyboardAndEastButtonOnGamepad()
        {
            var bindings = Lines().Where(l => l.Contains("\"action\": \"Crouch\"")).ToList();

            Assert.IsTrue(bindings.Any(l => l.Contains("<Keyboard>/c\"") && l.Contains("\"groups\": \"KeyboardMouse\"")));
            Assert.IsTrue(bindings.Any(l => l.Contains("<Gamepad>/buttonEast") && l.Contains("\"groups\": \"Gamepad\"")));
        }

        [Test]
        public void EveryIdInTheAssetIsUnique()
        {
            var ids = Lines()
                .Select(l => System.Text.RegularExpressions.Regex.Match(l, "\"id\": \"([0-9a-f-]{36})\""))
                .Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();

            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }
    }

    public class CapsuleShapeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        [Test]
        public void PrefabStandHeightSettingMatchesTheControllerHeight()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller = prefab.GetComponent<CharacterController>();
            var settings = new UnityEditor.SerializedObject(prefab.GetComponent<PlayerMotor>()).FindProperty("settings");

            var stand = settings.FindPropertyRelative("standHeight").floatValue;
            var crouch = settings.FindPropertyRelative("crouchHeight").floatValue;

            Assert.AreEqual(controller.height, stand, 1e-5f);
            Assert.Less(crouch, stand);
        }

        [Test]
        public void ShorterShapeKeepsTheFeetWhereTheyWere()
        {
            var (_, standCenter) = CapsuleShape.For(1.8f, 0.03f);
            var (crouchHeight, crouchCenter) = CapsuleShape.For(1.1f, 0.03f);

            Assert.AreEqual(standCenter - 1.8f * 0.5f, crouchCenter - crouchHeight * 0.5f, 1e-5f);
        }

        [Test]
        public void StepMovesTowardTheTargetAtTheGivenSpeed()
        {
            Assert.AreEqual(1.7f, CapsuleShape.Step(1.8f, 1.1f, 1f, 0.1f), 1e-5f);
            Assert.AreEqual(1.9f, CapsuleShape.Step(1.8f, 2.4f, 1f, 0.1f), 1e-5f);
        }

        [Test]
        public void StepConvergesWithoutOvershoot()
        {
            var h = 1.8f;
            for (var i = 0; i < 100; i++)
            {
                h = CapsuleShape.Step(h, 1.1f, 8f, 1f / 60f);
                Assert.GreaterOrEqual(h, 1.1f);
            }
            Assert.AreEqual(1.1f, h, 1e-5f);
        }
    }

    public class StandClearanceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _owner;
        private StandClearance _clearance;

        [SetUp]
        public void SetUp()
        {
            // Far from the origin so other tests' objects do not interfere. Feet at y = 0.
            _owner = Track(new GameObject("Owner"));
            _owner.transform.position = new Vector3(2000f, 0f, 2000f);
            Physics.SyncTransforms();
            _clearance = new StandClearance(1, 0.25f, 1.8f, 0.03f, _owner.transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private GameObject Slab(float bottomY, GameObject parent = null)
        {
            var slab = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            slab.transform.position = _owner.transform.position + Vector3.up * (bottomY + 0.25f);
            slab.transform.localScale = new Vector3(4f, 0.5f, 4f);
            if (parent != null) slab.transform.SetParent(parent.transform, true);
            Physics.SyncTransforms();
            return slab;
        }

        [Test]
        public void OpenSpace_CanStand()
        {
            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.1f));
        }

        [Test]
        public void PartlyResized_CeilingJustAboveStandingTop_CanStand()
        {
            // Standing top is 0.03 + 1.8 = 1.83; the slab starts above it, so it must not count.
            Slab(1.85f);
            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.7f));
        }

        [Test]
        public void PartlyResized_CeilingInsideTheGap_CannotStand()
        {
            // The current top is 1.73; a slab at 1.75 sits inside the 1.73 to 1.83 band.
            Slab(1.75f);
            Assert.IsFalse(_clearance.CanStand(_owner.transform.position, 1.7f));
        }

        [Test]
        public void LowCeiling_CannotStand()
        {
            Slab(1.4f);
            Assert.IsFalse(_clearance.CanStand(_owner.transform.position, 1.1f));
        }

        [Test]
        public void CeilingAboveStandingHeight_CanStand()
        {
            Slab(1.9f);
            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.1f));
        }

        [Test]
        public void OwnColliderAbove_IsIgnored()
        {
            Slab(1.4f, _owner);
            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.1f));
        }

        [Test]
        public void FloorUnderTheFeet_IsNotACeiling()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = _owner.transform.position + Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();

            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.1f));
        }

        [Test]
        public void AlreadyStanding_CanStandWithoutProbing()
        {
            Slab(1.4f);
            Assert.IsTrue(_clearance.CanStand(_owner.transform.position, 1.8f));
        }

        [Test]
        public void LayerNotInMask_IsIgnored()
        {
            Slab(1.4f);
            var other = new StandClearance(1 << 5, 0.25f, 1.8f, 0.03f, _owner.transform);
            Assert.IsTrue(other.CanStand(_owner.transform.position, 1.1f));
        }
    }
}

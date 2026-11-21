using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // Animation Rigging rejects a Multi-Aim constraint whose source list holds an empty slot, and then
    // silently skips it. Read through serialization so the test needs no Animation Rigging reference.
    public class WizardRigTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        [Test]
        public void AimConstraintsHaveNoEmptySourceSlots()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "PlayerWizard prefab not found.");

            var aims = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c != null && c.GetType().Name == "MultiAimConstraint")
                .ToList();
            Assert.IsNotEmpty(aims, "No Multi-Aim constraints on the wizard.");

            foreach (var aim in aims)
            {
                var sources = new SerializedObject(aim).FindProperty("m_Data.m_SourceObjects");
                var length = sources.FindPropertyRelative("m_Length").intValue;
                Assert.Greater(length, 0, $"{aim.name} has no aim source.");
                for (var i = 0; i < length; i++)
                {
                    var source = sources.FindPropertyRelative($"m_Item{i}.transform").objectReferenceValue;
                    Assert.IsNotNull(source, $"{aim.name} source slot {i} is empty.");
                }
            }
        }

        private static MonoBehaviour FindLean(GameObject prefab, string name) =>
            prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name == "OverrideTransform" && c.name == name);

        // The slide lean is split across the spine for a natural curve: about -120 degrees in total, enough to cancel the forward hunch of the crouch pose.
        [TestCase("SlideLean", "Abdomen", -65f)]
        [TestCase("SlideLeanChest", "Torso", -55f)]
        public void SlideLeanConstrainsASpineBoneWithAFixedBackwardTilt(string constraint, string boneName, float pitch)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var lean = FindLean(prefab, constraint);
            Assert.IsNotNull(lean, $"No {constraint} override constraint on the wizard.");

            var data = new SerializedObject(lean).FindProperty("m_Data");
            var bone = (Transform)data.FindPropertyRelative("m_ConstrainedObject").objectReferenceValue;
            Assert.IsNotNull(bone, $"{constraint} has no constrained bone.");
            Assert.IsTrue(bone.IsChildOf(prefab.transform), "The constrained bone is not part of the wizard.");
            Assert.AreEqual(boneName, bone.name);

            // Pivot space = the override is an offset on top of the animated pose.
            Assert.AreEqual(2, data.FindPropertyRelative("m_Space").intValue);
            Assert.AreEqual(0f, data.FindPropertyRelative("m_PositionWeight").floatValue, 1e-5f);
            Assert.AreEqual(1f, data.FindPropertyRelative("m_RotationWeight").floatValue, 1e-5f);
            var euler = data.FindPropertyRelative("m_OverrideRotation").vector3Value;
            Assert.AreEqual(pitch, euler.x, 1e-3f);
            Assert.AreEqual(0f, euler.y, 1e-3f);
            Assert.AreEqual(0f, euler.z, 1e-3f);
        }

        [TestCase("SlideLean", "slideLean")]
        [TestCase("SlideLeanChest", "slideLeanChest")]
        public void SlideLeanIsEvaluatedBeforeTheChestAimAndWiredToTheDriver(string constraint, string field)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var lean = FindLean(prefab, constraint);
            Assert.IsNotNull(lean);
            var chestAim = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .First(c => c != null && c.GetType().Name == "MultiAimConstraint" && c.name == "ChestAim");
            Assert.Less(lean.transform.GetSiblingIndex(), chestAim.transform.GetSiblingIndex());

            var driver = prefab.GetComponent<HexFall.Player.Locomotion.PlayerIKDriver>();
            var prop = new SerializedObject(driver).FindProperty(field);
            Assert.IsNotNull(prop, $"PlayerIKDriver has no {field} field.");
            Assert.AreSame(lean, prop.objectReferenceValue);
        }

        [Test]
        public void TheAbdomenLeanIsEvaluatedBeforeTheChestLean()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.Less(FindLean(prefab, "SlideLean").transform.GetSiblingIndex(),
                FindLean(prefab, "SlideLeanChest").transform.GetSiblingIndex());
        }
    }
}

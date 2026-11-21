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

        private static MonoBehaviour FindLean(GameObject prefab) =>
            prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name == "OverrideTransform" && c.name == "SlideLean");

        [Test]
        public void SlideLeanConstrainsTheAbdomenBoneWithAFixedBackwardTilt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var lean = FindLean(prefab);
            Assert.IsNotNull(lean, "No SlideLean override constraint on the wizard.");

            var data = new SerializedObject(lean).FindProperty("m_Data");
            var bone = (Transform)data.FindPropertyRelative("m_ConstrainedObject").objectReferenceValue;
            Assert.IsNotNull(bone, "SlideLean has no constrained bone.");
            Assert.IsTrue(bone.IsChildOf(prefab.transform), "The constrained bone is not part of the wizard.");
            Assert.AreEqual("Abdomen", bone.name);

            // Pivot space = the override is an offset on top of the animated pose.
            Assert.AreEqual(2, data.FindPropertyRelative("m_Space").intValue);
            Assert.AreEqual(0f, data.FindPropertyRelative("m_PositionWeight").floatValue, 1e-5f);
            var tilt = Quaternion.Euler(data.FindPropertyRelative("m_OverrideRotation").vector3Value);
            Assert.That(Quaternion.Angle(Quaternion.identity, tilt), Is.InRange(15f, 40f));
        }

        [Test]
        public void SlideLeanIsEvaluatedBeforeTheChestAimAndWiredToTheDriver()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var lean = FindLean(prefab);
            Assert.IsNotNull(lean);
            var chestAim = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .First(c => c != null && c.GetType().Name == "MultiAimConstraint" && c.name == "ChestAim");
            Assert.Less(lean.transform.GetSiblingIndex(), chestAim.transform.GetSiblingIndex());

            var driver = prefab.GetComponent<HexFall.Player.Locomotion.PlayerIKDriver>();
            var wired = new SerializedObject(driver).FindProperty("slideLean").objectReferenceValue;
            Assert.AreSame(lean, wired);
        }
    }
}

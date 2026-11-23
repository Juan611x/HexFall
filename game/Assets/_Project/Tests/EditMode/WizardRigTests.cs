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

        // The real UAL2 slide clip carries the lean, the low seat and the stretched legs, so the IK
        // stand-ins built before it are gone from the prefab and from the driver.
        [TestCase("SlideLean")]
        [TestCase("SlideLeanChest")]
        public void TheSlideLeanStandInsAreGone(string constraint)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsFalse(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == constraint),
                $"{constraint} is still on the wizard.");
        }

        [TestCase("slideLean")]
        [TestCase("slideLeanChest")]
        public void TheDriverHasNoSlideLeanReferences(string field)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var driver = prefab.GetComponent<HexFall.Player.Locomotion.PlayerIKDriver>();
            Assert.IsNull(new SerializedObject(driver).FindProperty(field), $"PlayerIKDriver still has {field}.");
        }

        [Test]
        public void TheHandmadeSlidePoseIsGone()
        {
            var assembly = typeof(HexFall.Player.Locomotion.PlayerIKDriver).Assembly;
            Assert.IsNull(assembly.GetType("HexFall.Player.Locomotion.SlidePose"));
        }
    }
}

using HexFall.Player;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The shipped registry maps the walkable textures to their surfaces and the wizard uses it.
    public class SurfaceAssetsTests
    {
        private const string RegistryPath = "Assets/_Project/Surfaces/SurfaceRegistry.asset";
        private const string StonePath = "Assets/_Project/Surfaces/Surface_Stone.asset";
        private const string BrickPath = "Assets/_Project/Surfaces/Surface_Brick.asset";
        private const string DefaultPath = "Assets/_Project/Surfaces/Surface_Default.asset";
        private const string StoneTexturePath = "Assets/_Project/Art/Environment/Textures/T_StoneTiles.png";
        private const string BrickTexturePath = "Assets/_Project/Art/Environment/Textures/T_CastleBrick.png";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/PlayerWizard.prefab";

        private static SurfaceRegistry Registry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<SurfaceRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "SurfaceRegistry asset not found.");
            return registry;
        }

        [Test]
        public void RegistryMapsStoneTilesToStone()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(StoneTexturePath);
            Assert.IsNotNull(texture, "T_StoneTiles not found.");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(StonePath), Registry().Resolve(texture));
        }

        [Test]
        public void RegistryMapsCastleBrickToBrick()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(BrickTexturePath);
            Assert.IsNotNull(texture, "T_CastleBrick not found.");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(BrickPath), Registry().Resolve(texture));
        }

        [Test]
        public void RegistryFallsBackToTheDefaultSurface()
        {
            var registry = Registry();
            Assert.IsNotNull(registry, "SurfaceRegistry asset not found.");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(DefaultPath), registry.DefaultSurface);
            Assert.IsNotNull(registry.DefaultSurface);
        }

        [Test]
        public void WizardSensorUsesTheShippedRegistry()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var sensor = prefab.GetComponent<PlayerSurfaceSensor>();
            Assert.IsNotNull(sensor, "PlayerWizard has no PlayerSurfaceSensor.");
            var registry = new SerializedObject(sensor).FindProperty("registry").objectReferenceValue;
            Assert.AreEqual(Registry(), registry);
        }
    }
}

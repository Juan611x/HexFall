using HexFall.Surfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HexFall.UI.Tests
{
    public class SurfaceRegistryTests
    {
        SurfaceRegistry registry;
        SurfaceDefinition stone;
        SurfaceDefinition brick;
        SurfaceDefinition fallback;
        Texture2D stoneTex;
        Texture2D brickTex;

        [SetUp]
        public void SetUp()
        {
            registry = ScriptableObject.CreateInstance<SurfaceRegistry>();
            stone = ScriptableObject.CreateInstance<SurfaceDefinition>();
            brick = ScriptableObject.CreateInstance<SurfaceDefinition>();
            fallback = ScriptableObject.CreateInstance<SurfaceDefinition>();
            stoneTex = new Texture2D(1, 1);
            brickTex = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(brick);
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(stoneTex);
            Object.DestroyImmediate(brickTex);
        }

        [Test]
        public void Resolve_MappedTexture_ReturnsItsSurface()
        {
            registry.SetEntries(new[]
            {
                new SurfaceTextureEntry { texture = stoneTex, surface = stone },
                new SurfaceTextureEntry { texture = brickTex, surface = brick },
            }, fallback);

            Assert.AreSame(stone, registry.Resolve(stoneTex));
            Assert.AreSame(brick, registry.Resolve(brickTex));
        }

        [Test]
        public void Resolve_UnmappedOrNullTexture_ReturnsDefault()
        {
            registry.SetEntries(new[] { new SurfaceTextureEntry { texture = stoneTex, surface = stone } }, fallback);

            Assert.AreSame(fallback, registry.Resolve(brickTex));
            Assert.AreSame(fallback, registry.Resolve(null));
        }

        [Test]
        public void Resolve_NullDefault_ReturnsNull()
        {
            registry.SetEntries(new SurfaceTextureEntry[0], null);

            Assert.IsNull(registry.Resolve(stoneTex));
            Assert.IsNull(registry.Resolve(null));
        }

        [Test]
        public void Resolve_DuplicateTexture_FirstWinsAndWarnsOnce()
        {
            registry.SetEntries(new[]
            {
                new SurfaceTextureEntry { texture = stoneTex, surface = stone },
                new SurfaceTextureEntry { texture = stoneTex, surface = brick },
            }, fallback);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Duplicate"));
            Assert.AreSame(stone, registry.Resolve(stoneTex));
            Assert.AreSame(stone, registry.Resolve(stoneTex));
        }

        [Test]
        public void Resolve_IgnoresEntriesWithNullTextureOrSurface()
        {
            registry.SetEntries(new[]
            {
                new SurfaceTextureEntry { texture = null, surface = stone },
                new SurfaceTextureEntry { texture = brickTex, surface = null },
            }, fallback);

            Assert.AreSame(fallback, registry.Resolve(brickTex));
        }

        [Test]
        public void SetEntries_RebuildsCache()
        {
            registry.SetEntries(new[] { new SurfaceTextureEntry { texture = stoneTex, surface = stone } }, fallback);
            Assert.AreSame(stone, registry.Resolve(stoneTex));

            registry.SetEntries(new[] { new SurfaceTextureEntry { texture = stoneTex, surface = brick } }, fallback);
            Assert.AreSame(brick, registry.Resolve(stoneTex));
        }
    }

    public class SurfaceDefinitionTests
    {
        SurfaceDefinition definition;

        [SetUp]
        public void SetUp() => definition = ScriptableObject.CreateInstance<SurfaceDefinition>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(definition);

        [Test]
        public void TryGetEffect_ReturnsEntryForImpact()
        {
            definition.SetEffects(new[]
            {
                new SurfaceEffect { impact = SurfaceImpact.Footstep },
                new SurfaceEffect { impact = SurfaceImpact.Landing, volumeRange = new Vector2(0.5f, 0.6f) },
            });

            Assert.IsTrue(definition.TryGetEffect(SurfaceImpact.Landing, out var effect));
            Assert.AreEqual(SurfaceImpact.Landing, effect.impact);
            Assert.AreEqual(0.5f, effect.volumeRange.x, 1e-5f);
        }

        [Test]
        public void TryGetEffect_MissingImpact_ReturnsFalse()
        {
            definition.SetEffects(new[] { new SurfaceEffect { impact = SurfaceImpact.Footstep } });

            Assert.IsFalse(definition.TryGetEffect(SurfaceImpact.Landing, out var effect));
            Assert.IsNull(effect);
        }

        [Test]
        public void Effect_Defaults()
        {
            var effect = new SurfaceEffect();

            Assert.AreEqual(new Vector2(0.8f, 1f), effect.volumeRange);
            Assert.AreEqual(new Vector2(0.95f, 1.05f), effect.pitchRange);
        }
    }
}

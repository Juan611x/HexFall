using System.Linq;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HexFall.UI.Tests
{
    // The shipped surfaces carry the imported Kenney footsteps: concrete on brick, grass on stone.
    public class FootstepAssetsTests
    {
        private const string PackPath = "Assets/ThirdParty/Kenney/ImpactSounds";

        private static SurfaceEffect FootstepOf(string surfacePath)
        {
            var surface = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(surfacePath);
            Assert.IsNotNull(surface, $"{surfacePath} not found.");
            Assert.IsTrue(surface.TryGetEffect(SurfaceImpact.Footstep, out var effect), $"{surface.name} has no footstep effect.");
            return effect;
        }

        private static void AssertClips(SurfaceEffect effect, string prefix)
        {
            var expected = Enumerable.Range(0, 5)
                .Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>($"{PackPath}/{prefix}_{i:000}.ogg"))
                .ToArray();
            Assert.That(expected, Has.All.Not.Null, $"Missing {prefix} clips in the pack.");
            CollectionAssert.AreEquivalent(expected, effect.clips);
        }

        [Test]
        public void BrickStepsUseTheConcreteFootsteps()
        {
            AssertClips(FootstepOf("Assets/_Project/Surfaces/Surface_Brick.asset"), "footstep_concrete");
        }

        [Test]
        public void StoneStepsUseTheGrassFootsteps()
        {
            AssertClips(FootstepOf("Assets/_Project/Surfaces/Surface_Stone.asset"), "footstep_grass");
        }

        [Test]
        public void TheWholePackIsImported()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { PackPath });
            Assert.AreEqual(130, clips.Length);
        }
    }
}

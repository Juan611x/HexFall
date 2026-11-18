using System;
using NUnit.Framework;
using UnityEditor;

namespace HexFall.Editor.Build.Tests
{
    public sealed class WebGLBuildConfigTests
    {
        private static readonly string[] Scenes = { "Assets/_Project/Scenes/MainMenu.unity" };

        [Test]
        public void ResolveOutputPath_WithoutArgument_ReturnsDefault()
        {
            var result = WebGLBuildConfig.ResolveOutputPath(new[] { "Unity.exe", "-batchmode" });

            Assert.AreEqual(WebGLBuildConfig.DefaultOutputPath, result);
        }

        [Test]
        public void ResolveOutputPath_NullArgs_ReturnsDefault()
        {
            Assert.AreEqual(WebGLBuildConfig.DefaultOutputPath, WebGLBuildConfig.ResolveOutputPath(null));
        }

        [Test]
        public void ResolveOutputPath_WithArgument_ReturnsValue()
        {
            var result = WebGLBuildConfig.ResolveOutputPath(new[] { "-batchmode", "-buildOutput", "Builds/Custom" });

            Assert.AreEqual("Builds/Custom", result);
        }

        [Test]
        public void ResolveOutputPath_ArgumentWithoutValue_ReturnsDefault()
        {
            var result = WebGLBuildConfig.ResolveOutputPath(new[] { "-batchmode", "-buildOutput" });

            Assert.AreEqual(WebGLBuildConfig.DefaultOutputPath, result);
        }

        [Test]
        public void ResolveOutputPath_EmptyValue_ReturnsDefault()
        {
            var result = WebGLBuildConfig.ResolveOutputPath(new[] { "-buildOutput", "  " });

            Assert.AreEqual(WebGLBuildConfig.DefaultOutputPath, result);
        }

        [Test]
        public void CreateOptions_TargetsWebGL()
        {
            var options = WebGLBuildConfig.CreateOptions(Scenes, "Builds/WebGL");

            Assert.AreEqual(BuildTarget.WebGL, options.target);
            Assert.AreEqual(BuildTargetGroup.WebGL, options.targetGroup);
            Assert.AreEqual(BuildOptions.None, options.options);
            Assert.AreEqual("Builds/WebGL", options.locationPathName);
            CollectionAssert.AreEqual(Scenes, options.scenes);
        }

        [Test]
        public void CreateOptions_NoScenes_Throws()
        {
            Assert.Throws<ArgumentException>(() => WebGLBuildConfig.CreateOptions(Array.Empty<string>(), "out"));
            Assert.Throws<ArgumentException>(() => WebGLBuildConfig.CreateOptions(null, "out"));
        }

        [Test]
        public void EnabledScenePaths_FiltersDisabledScenes()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/A.unity", true),
                new EditorBuildSettingsScene("Assets/B.unity", false),
                new EditorBuildSettingsScene("Assets/C.unity", true),
            };

            var result = WebGLBuildConfig.EnabledScenePaths(scenes);

            CollectionAssert.AreEqual(new[] { "Assets/A.unity", "Assets/C.unity" }, result);
        }
    }
}

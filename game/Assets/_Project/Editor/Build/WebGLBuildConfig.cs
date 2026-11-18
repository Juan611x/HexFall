using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace HexFall.Editor.Build
{
    /// <summary>
    /// Pure WebGL build configuration: command line parsing and option assembly.
    /// Has no side effects, so it is unit-testable without running a build.
    /// </summary>
    public static class WebGLBuildConfig
    {
        public const string DefaultOutputPath = "Builds/WebGL";
        public const string OutputArgument = "-buildOutput";

        public static string ResolveOutputPath(string[] commandLineArgs)
        {
            if (commandLineArgs == null)
            {
                return DefaultOutputPath;
            }

            var index = Array.IndexOf(commandLineArgs, OutputArgument);
            if (index < 0 || index + 1 >= commandLineArgs.Length)
            {
                return DefaultOutputPath;
            }

            var value = commandLineArgs[index + 1];
            return string.IsNullOrWhiteSpace(value) ? DefaultOutputPath : value;
        }

        public static BuildPlayerOptions CreateOptions(string[] scenes, string outputPath)
        {
            if (scenes == null || scenes.Length == 0)
            {
                throw new ArgumentException("At least one scene is required to build.", nameof(scenes));
            }

            return new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
        }

        public static string[] EnabledScenePaths(IEnumerable<EditorBuildSettingsScene> scenes)
        {
            return scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }
    }
}

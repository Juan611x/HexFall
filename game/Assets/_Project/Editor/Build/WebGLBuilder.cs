using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HexFall.Editor.Build
{
    /// <summary>
    /// Editor glue for WebGL builds. Verified by running a real build, not unit tests.
    /// </summary>
    public static class WebGLBuilder
    {
        /// <summary>Batch-mode entry point. Exits the editor with 0 on success, 1 otherwise.</summary>
        public static void BuildFromCommandLine()
        {
            var exitCode = 1;
            try
            {
                var outputPath = WebGLBuildConfig.ResolveOutputPath(Environment.GetCommandLineArgs());
                exitCode = Build(outputPath) == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError($"HEXFALL_BUILD_RESULT Exception {exception}");
            }

            EditorApplication.Exit(exitCode);
        }

        [MenuItem("HexFall/Build/WebGL")]
        public static void BuildFromMenu()
        {
            Build(WebGLBuildConfig.DefaultOutputPath);
        }

        private static BuildResult Build(string outputPath)
        {
            var scenes = WebGLBuildConfig.EnabledScenePaths(EditorBuildSettings.scenes);
            var options = WebGLBuildConfig.CreateOptions(scenes, outputPath);

            var summary = BuildPipeline.BuildPlayer(options).summary;

            Debug.Log(
                $"HEXFALL_BUILD_RESULT {summary.result} errors={summary.totalErrors} " +
                $"size={summary.totalSize} output={summary.outputPath}");
            return summary.result;
        }
    }
}

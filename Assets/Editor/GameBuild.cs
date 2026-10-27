using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Witch.Editor
{
    public static class GameBuild
    {
        public const string DefaultOutputPath = "/private/tmp/WitchGamePrototype.app";
        public const string ReleaseOutputPath = "/private/tmp/WitchGamePrototype-Release.app";

        public static void BuildMacArm64()
        {
            BuildMacArm64(DefaultOutputPath, BuildOptions.CleanBuildCache | BuildOptions.Development);
        }

        public static void BuildMacArm64Release()
        {
            BuildMacArm64(ReleaseOutputPath, BuildOptions.CleanBuildCache);
        }

        private static void BuildMacArm64(string outputPath, BuildOptions buildOptions)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length != 4 || scenes[0] != "Assets/Scenes/Bootstrap.unity")
            {
                throw new InvalidOperationException("Game build requires exactly four production scenes starting with Bootstrap.");
            }

            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = buildOptions
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("macOS game build failed: " + report.summary.result);
            }
        }
    }
}

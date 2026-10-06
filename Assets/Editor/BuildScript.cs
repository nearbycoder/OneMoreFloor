using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneMoreFloor.EditorTools
{
    public static class BuildScript
    {
        [MenuItem("One More Floor/Build Linux")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/OneMoreFloor.x86_64");

        /// <summary>
        /// A universal (Intel + Apple silicon) app bundle. Built on Linux it is unsigned and un-notarized: Gatekeeper
        /// blocks it until the player right-clicks it and picks Open (or clears the quarantine flag).
        /// </summary>
        [MenuItem("One More Floor/Build macOS")]
        public static void BuildMac()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Build(BuildTarget.StandaloneOSX, "Builds/Mac/OneMoreFloor.app");
        }

        /// <summary>Needs Windows Build Support (Mono) installed for this editor version; not installed on the dev machine.</summary>
        [MenuItem("One More Floor/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/OneMoreFloor.exe");

        static void Build(BuildTarget target, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Debug.LogError($"[BuildScript] {target} build support isn't installed for this editor");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[BuildScript] {target} {s.result} {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

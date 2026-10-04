using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneMoreFloor.EditorTools
{
    public static class BuildScript
    {
        [MenuItem("One More Floor/Build Linux")]
        public static void BuildLinux()
        {
            const string outDir = "Builds/Linux";
            Directory.CreateDirectory(outDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = outDir + "/OneMoreFloor.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[BuildScript] {s.result} {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

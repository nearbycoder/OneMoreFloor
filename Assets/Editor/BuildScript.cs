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

        /// <summary>
        /// The browser build → Builds/Pages, the site GitHub Pages serves (Tools/build-pages.sh adds .nojekyll). Brotli
        /// with the JavaScript decompression fallback, so it runs from any static host (no Content-Encoding headers
        /// needed); no threads, so no SharedArrayBuffer or COOP/COEP headers. The page is Assets/WebGLTemplates/OneMoreFloor.
        /// </summary>
        [MenuItem("One More Floor/Build Web (GitHub Pages)")]
        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;   // a new build never meets an old file in the browser's cache
            PlayerSettings.WebGL.dataCaching = true;
            // the heap peaks near 220 MB: a ceiling well above that, so a runaway allocation ends in the page's
            // out-of-memory message rather than a phone closing the tab
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.template = "PROJECT:OneMoreFloor";
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            ExcludeMobileQuality(BuildTarget.WebGL);
            Build(BuildTarget.WebGL, "Builds/Pages", BuildTargetGroup.WebGL);
        }

        /// <summary>
        /// The browser plays at the PC quality level, like the desktop (ProjectSetup.ConfigureUrp); leaving the Mobile
        /// level in a WebGL build only adds its pipeline's shader variants (and the build rewrites Mobile_RPAsset).
        /// </summary>
        static void ExcludeMobileQuality(BuildTarget target)
        {
            string platform = BuildPipeline.GetBuildTargetGroup(target).ToString();
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue != "Mobile") continue;
                var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                bool has = false;
                for (int e = 0; e < excluded.arraySize; e++) has |= excluded.GetArrayElementAtIndex(e).stringValue == platform;
                if (has) continue;
                excluded.InsertArrayElementAtIndex(excluded.arraySize);
                excluded.GetArrayElementAtIndex(excluded.arraySize - 1).stringValue = platform;
            }
            if (so.ApplyModifiedProperties()) AssetDatabase.SaveAssets();
        }

        static void Build(BuildTarget target, string path, BuildTargetGroup group = BuildTargetGroup.Standalone)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[BuildScript] {target} build support isn't installed for this editor");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(target == BuildTarget.WebGL ? path : Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            // the Input System adds its actions asset to the preloaded assets for the build and doesn't always take it
            // out again in batch mode: keep the project's list as it was
            var preloaded = PlayerSettings.GetPreloadedAssets();
            var report = BuildPipeline.BuildPlayer(opts);
            PlayerSettings.SetPreloadedAssets(preloaded);
            AssetDatabase.SaveAssets();
            var s = report.summary;
            Debug.Log($"[BuildScript] {target} {s.result} {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

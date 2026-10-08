using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace OneMoreFloor.EditorTools
{
    /// <summary>
    /// Idempotent project wiring: player settings, URP quality, TextMeshPro essentials, SDF font assets,
    /// and the single bootstrap scene. Menu: One More Floor/Apply Project Setup, or batch:
    /// Tools/unity.sh batch OneMoreFloor.EditorTools.ProjectSetup.Apply
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string FontDir = "Assets/Resources/Fonts";
        const string SdfDir = "Assets/Resources/Fonts/SDF";

        static readonly string[] Fonts = { "Limelight-Regular", "Bungee-Regular", "VarelaRound-Regular", "PatrickHand-Regular" };

        /// <summary>Batch entry point (-executeMethod): runs everything, then exits with a status code.</summary>
        public static void Apply()
        {
            bool ok = Run(true);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>For a resident editor (menu or `unity command eval`): never exits.</summary>
        [MenuItem("One More Floor/Apply Project Setup")]
        public static void ApplyInEditor() => Run(false);

        static bool Run(bool rebuildScene)
        {
            bool ok = true;
            try
            {
                EnsureMaterials();
                ConfigurePlayer();
                ConfigureUrp();
                ConfigureFidelity();
                ok &= ImportTmpEssentials();
                ok &= BuildFontAssets();
                if (rebuildScene) BuildScene();
                AssetDatabase.SaveAssets();
                Debug.Log("[ProjectSetup] done");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                ok = false;
            }
            return ok;
        }

        /// <summary>Template materials in Resources so the URP shaders they use always ship in builds.</summary>
        static void EnsureMaterials()
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            Ensure("Lit", "Universal Render Pipeline/Lit", null);
            Ensure("Unlit", "Universal Render Pipeline/Unlit", null);
            Ensure("LitTransparent", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            });
            Ensure("Sky", "OMF/SkyGradient", null);
            Ensure("Particles", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            });
        }

        static void Ensure(string name, string shaderName, System.Action<Material> configure)
        {
            string path = $"Assets/Resources/Materials/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("[ProjectSetup] missing shader " + shaderName); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                configure?.Invoke(m);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
                configure?.Invoke(m);
                EditorUtility.SetDirty(m);
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Nearby";
            PlayerSettings.productName = "One More Floor";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.nearbycoder.onemorefloor");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Icons/kind_bellhop.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static void ConfigureUrp()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp == null) { Debug.LogWarning("[ProjectSetup] PC_RPAsset missing"); return; }
            rp.supportsHDR = true;
            rp.msaaSampleCount = 4;
            rp.shadowDistance = 140f;
            rp.shadowCascadeCount = 2;
            rp.mainLightShadowmapResolution = 4096;
            rp.renderScale = 1f;
            EditorUtility.SetDirty(rp);

            // Use the PC quality level everywhere so the editor and the Linux player look the same.
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != "PC") continue;
                QualitySettings.SetQualityLevel(i, true);
                var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
                var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
                for (int p = 0; p < perPlatform.arraySize; p++)
                    perPlatform.GetArrayElementAtIndex(p).FindPropertyRelative("second").intValue = i;
                so.ApplyModifiedProperties();
            }
            GraphicsSettings.defaultRenderPipeline = rp;
        }

        /// <summary>Batch entry point for the GRAPHICS FIDELITY assets alone.</summary>
        public static void ApplyFidelity()
        {
            bool ok = true;
            try { ConfigureFidelity(); AssetDatabase.SaveAssets(); }
            catch (System.Exception ex) { Debug.LogException(ex); ok = false; }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// GRAPHICS FIDELITY (see GraphicsQuality) picks one of these renderers per step: 0 PC_Renderer (today's
        /// ambient occlusion), 1 none (LOW), 2 half-resolution 4-sample (MEDIUM), 3 full-resolution 12-sample (ULTRA).
        /// They're real assets so the build keeps the shader variants each one needs; URP strips the ones no renderer
        /// in the build uses. FidelityVariants.asset does the same for ULTRA's post effects (high-quality bloom and
        /// depth of field): the game builds its own profile at runtime, and URP only keeps the post-processing variants
        /// that a VolumeProfile asset in the project uses.
        /// </summary>
        static void ConfigureFidelity()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var baseData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rp == null || baseData == null) { Debug.LogWarning("[ProjectSetup] PC_RPAsset or PC_Renderer missing"); return; }
            var list = new ScriptableRendererData[]
            {
                baseData,
                FidelityRenderer(baseData, "PC_Renderer_NoAO", null),
                FidelityRenderer(baseData, "PC_Renderer_AOLow", so =>
                {
                    so.FindProperty("m_Settings.Downsample").boolValue = true;
                    so.FindProperty("m_Settings.Samples").enumValueIndex = 2;        // Low: 4 samples
                }),
                FidelityRenderer(baseData, "PC_Renderer_AOHigh", so =>
                {
                    so.FindProperty("m_Settings.Samples").enumValueIndex = 0;        // High: 12 samples
                    so.FindProperty("m_Settings.Intensity").floatValue = 0.75f;
                    so.FindProperty("m_Settings.Radius").floatValue = 0.45f;
                }),
            };
            var rso = new SerializedObject(rp);
            var arr = rso.FindProperty("m_RendererDataList");
            arr.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            rso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);

            const string profilePath = "Assets/Settings/FidelityVariants.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) != null) AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.highQualityFiltering.Override(true);
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.highQualitySampling.Override(true);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] fidelity renderers and variants profile written");
        }

        static UniversalRendererData FidelityRenderer(UniversalRendererData src, string name, System.Action<SerializedObject> ssao)
        {
            string path = $"Assets/Settings/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path) != null) AssetDatabase.DeleteAsset(path);
            var data = Object.Instantiate(src);
            data.name = name;
            data.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(data, path);
            var map = new System.Collections.Generic.List<long>();
            if (ssao != null)
                foreach (var f in src.rendererFeatures)
                {
                    if (!(f is ScreenSpaceAmbientOcclusion)) continue;
                    var copy = Object.Instantiate(f);
                    copy.name = f.name;
                    AssetDatabase.AddObjectToAsset(copy, data);
                    data.rendererFeatures.Add(copy);
                    var so = new SerializedObject(copy);
                    ssao(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(copy, out string _, out long localId);
                    map.Add(localId);
                }
            var dso = new SerializedObject(data);
            var mp = dso.FindProperty("m_RendererFeatureMap");
            mp.arraySize = map.Count;
            for (int i = 0; i < map.Count; i++) mp.GetArrayElementAtIndex(i).longValue = map[i];
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        static bool ImportTmpEssentials()
        {
            // AssetDatabase.ImportPackage is asynchronous in batch mode, so the essentials are extracted
            // with Tools/extract_unitypackage.py (and committed). This only verifies they're there.
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return true;
            Debug.LogError("[ProjectSetup] TMP essentials missing. Run: python3 Tools/extract_unitypackage.py " +
                           "\"Library/PackageCache/com.unity.ugui@*/Package Resources/TMP Essential Resources.unitypackage\" .");
            return false;
        }

        static bool BuildFontAssets()
        {
            Directory.CreateDirectory(SdfDir);
            bool ok = true;
            foreach (var name in Fonts)
            {
                string assetPath = $"{SdfDir}/{name} SDF.asset";
                if (File.Exists(assetPath)) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf");
                if (font == null) { Debug.LogError("[ProjectSetup] missing font " + name); ok = false; continue; }
                var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fa == null) { Debug.LogError("[ProjectSetup] could not create font asset " + name); ok = false; continue; }
                fa.name = name + " SDF";
                fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×—–…’“”·•★☆");
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.material.name = name + " SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                foreach (var tex in fa.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = name + " SDF Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
                EditorUtility.SetDirty(fa);
                Debug.Log("[ProjectSetup] font asset " + assetPath);
            }
            AssetDatabase.SaveAssets();
            return ok;
        }

        static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("Boot");
            boot.AddComponent<GameRoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            Debug.Log("[ProjectSetup] scene " + ScenePath);
        }
    }
}

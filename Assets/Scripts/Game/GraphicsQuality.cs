using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OneMoreFloor
{
    /// <summary>
    /// The GRAPHICS FIDELITY setting, LOW to ULTRA. HIGH is the look the game was made with and the default; MEDIUM and
    /// LOW trade antialiasing, shadow detail, ambient occlusion, bloom, particles and resolution for frame rate on weaker
    /// GPUs and big screens; ULTRA adds what HIGH leaves out. It's applied to a runtime copy of the pipeline asset, so
    /// the project's asset is never changed (the editor would save it). Ambient occlusion is picked per step by renderer
    /// (see ProjectSetup.ConfigureFidelity), so the build keeps every variant it needs.
    /// </summary>
    public static class GraphicsQuality
    {
        public const int Low = 0, Medium = 1, High = 2, Ultra = 3;
#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>In a browser MEDIUM: WebGL costs more per pixel than the desktop renderer, and the page renders at the
        /// screen's pixel density. HIGH and ULTRA stay one click away in Settings.</summary>
        public const int Default = Medium;
#else
        public const int Default = High;
#endif
        public static readonly string[] Names = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        /// <summary>The old GRAPHICS setting (0 HIGH, 1 BALANCED, 2 LOW) as a step of this one.</summary>
        public static int FromLegacy(int graphics) => graphics == 1 ? Medium : graphics == 2 ? Low : High;

        /// <summary>A step as the old GRAPHICS setting, for older builds reading a newer save (ULTRA is HIGH there).</summary>
        public static int ToLegacy(int level) => level == Medium ? 1 : level == Low ? 2 : 0;

        /// <summary>The pipeline's renderers, in PC_RPAsset's list.</summary>
        public const int RendererAo = 0, RendererNoAo = 1, RendererAoLow = 2, RendererAoHigh = 3;

        public struct Preset
        {
            public int Msaa, ShadowRes, Cascades, Renderer, LutSize;
            public float RenderScale, Particles;
            public bool SoftShadows, Bloom, BloomQuarter, LampShadows, SkylineFocus, PreciseColor;
            public AnisotropicFiltering Aniso;
        }

        public static Preset For(int level)
        {
            switch (Mathf.Clamp(level, Low, Ultra))
            {
                case Low: return new Preset { Msaa = 1, ShadowRes = 1024, Cascades = 1, Renderer = RendererNoAo, LutSize = 32, RenderScale = 0.8f, Particles = 0.5f,
                                              Aniso = AnisotropicFiltering.Disable };
                case Medium: return new Preset { Msaa = 2, ShadowRes = 2048, Cascades = 2, Renderer = RendererAoLow, LutSize = 32, RenderScale = 1f, Particles = 0.8f,
                                                 SoftShadows = true, Bloom = true, BloomQuarter = true, Aniso = AnisotropicFiltering.Enable };
                case Ultra: return new Preset { Msaa = 8, ShadowRes = 8192, Cascades = 4, Renderer = RendererAoHigh, LutSize = 64, RenderScale = 1f, Particles = 1.6f,
                                                SoftShadows = true, Bloom = true, LampShadows = true, SkylineFocus = true, PreciseColor = true,
                                                Aniso = AnisotropicFiltering.ForceEnable };
                // HIGH: exactly the settings the game shipped with (PC_RPAsset, PC_Renderer and the PC quality level)
                default: return new Preset { Msaa = 4, ShadowRes = 4096, Cascades = 2, Renderer = RendererAo, LutSize = 32, RenderScale = 1f, Particles = 1f,
                                             SoftShadows = true, Bloom = true, Aniso = AnisotropicFiltering.ForceEnable };
            }
        }

        static UniversalRenderPipelineAsset copy;

        /// <summary>The level last applied (-1 before the first).</summary>
        public static int Applied { get; private set; } = -1;

        /// <summary>The renderer the world camera uses (for self-tests).</summary>
        public static int AppliedRenderer { get; private set; } = -1;

        public static void Apply(int level, Light sun, Volume post, Camera worldCam)
        {
            level = Mathf.Clamp(level, Low, Ultra);
            var p = For(level);
            if (copy == null)
            {
                var src = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (src == null) return;
                copy = Object.Instantiate(src);
                copy.name = src.name + " (runtime)";
                QualitySettings.renderPipeline = copy;
            }
            copy.msaaSampleCount = p.Msaa;
            copy.mainLightShadowmapResolution = p.ShadowRes;
            copy.shadowCascadeCount = p.Cascades;
            copy.renderScale = p.RenderScale;
            copy.colorGradingLutSize = p.LutSize;
            copy.hdrColorBufferPrecision = p.PreciseColor ? HDRColorBufferPrecision._64Bits : HDRColorBufferPrecision._32Bits;
            // the floor lamps' shadows share the additional-light atlas: 10 lamps x 6 faces at 512 px
            copy.additionalLightsShadowmapResolution = p.LampShadows ? 4096 : 2048;
            QualitySettings.anisotropicFiltering = p.Aniso;
            if (worldCam)
            {
                var data = worldCam.GetUniversalAdditionalCameraData();
                int renderers = copy.rendererDataList.Length;
                int r = p.Renderer < renderers ? p.Renderer : 0;
                data.SetRenderer(r);
                AppliedRenderer = r;
            }
            if (sun) sun.shadows = p.SoftShadows ? LightShadows.Soft : LightShadows.Hard;
            if (post && post.profile)
            {
                var profile = post.profile;
                if (profile.TryGet<Bloom>(out var bloom))
                {
                    bloom.active = p.Bloom;
                    // high-quality filtering is the bloom variant the build keeps (URP strips the variants no
                    // VolumeProfile asset uses, and SampleSceneProfile and FidelityVariants use this one); without it
                    // the bloom passes ran but never reached the screen
                    bloom.highQualityFiltering.Override(true);
                    bloom.downscale.Override(p.BloomQuarter ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
                }
                if (!profile.TryGet<DepthOfField>(out var dof))
                {
                    dof = profile.Add<DepthOfField>(true);
                    dof.mode.Override(DepthOfFieldMode.Gaussian);
                    dof.highQualitySampling.Override(true);
                    dof.gaussianMaxRadius.Override(1.5f);
                }
                dof.active = p.SkylineFocus;
            }
            FloorView.LampShadows = p.LampShadows;
            Fx.Density = p.Particles;
            Applied = level;
        }

        /// <summary>
        /// ULTRA's depth of field keeps the tower sharp and softens only what's well behind it (the far city and the
        /// skyline), so it follows the camera as it frames the tower or zooms in.
        /// </summary>
        public static void Focus(Volume post, Camera cam, float towerZ)
        {
            if (Applied != Ultra || !post || !post.profile || !cam) return;
            if (!post.profile.TryGet<DepthOfField>(out var dof)) return;
            float d = Mathf.Abs(towerZ - cam.transform.position.z);
            dof.gaussianStart.Override(d + 20f);
            dof.gaussianEnd.Override(d + 110f);
        }
    }
}

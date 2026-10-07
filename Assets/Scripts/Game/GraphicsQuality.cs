using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OneMoreFloor
{
    /// <summary>
    /// The GRAPHICS setting. HIGH is the look the game was made with; BALANCED and LOW trade antialiasing, shadow
    /// detail, bloom and resolution for frame rate on weaker GPUs and big screens. It's applied to a runtime copy of
    /// the pipeline asset, so the project's asset is never changed (the editor would save it).
    /// </summary>
    public static class GraphicsQuality
    {
        public const int High = 0, Balanced = 1, Low = 2;
        public static readonly string[] Names = { "HIGH", "BALANCED", "LOW" };

        public struct Preset
        {
            public int Msaa, ShadowRes;
            public float RenderScale;
            public bool SoftShadows, Bloom;
        }

        public static Preset For(int level)
        {
            switch (Mathf.Clamp(level, High, Low))
            {
                case Balanced: return new Preset { Msaa = 2, ShadowRes = 2048, RenderScale = 1f, SoftShadows = true, Bloom = true };
                case Low: return new Preset { Msaa = 1, ShadowRes = 1024, RenderScale = 0.8f, SoftShadows = false, Bloom = false };
                default: return new Preset { Msaa = 4, ShadowRes = 4096, RenderScale = 1f, SoftShadows = true, Bloom = true };
            }
        }

        static UniversalRenderPipelineAsset copy;

        /// <summary>The level last applied (-1 before the first).</summary>
        public static int Applied { get; private set; } = -1;

        public static void Apply(int level, Light sun, Volume post)
        {
            level = Mathf.Clamp(level, High, Low);
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
            copy.renderScale = p.RenderScale;
            if (sun) sun.shadows = p.SoftShadows ? LightShadows.Soft : LightShadows.Hard;
            if (post && post.profile && post.profile.TryGet<Bloom>(out var bloom)) bloom.active = p.Bloom;
            Applied = level;
        }
    }
}

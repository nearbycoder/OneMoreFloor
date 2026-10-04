using UnityEditor;

namespace OneMoreFloor.EditorTools
{
    /// <summary>Blender-rendered icons: transparent, mipmapped, uncompressed (they're small).</summary>
    public class IconImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Icons/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.filterMode = UnityEngine.FilterMode.Trilinear;
            t.sRGBTexture = true;
        }
    }
}

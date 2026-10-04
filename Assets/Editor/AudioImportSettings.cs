using UnityEditor;
using UnityEngine;

namespace OneMoreFloor.EditorTools
{
    /// <summary>Music streams compressed; short effects decompress on load for zero-latency playback.</summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/") || assetPath.Contains("/amb_");
            var s = importer.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = music ? 0.75f : 1f;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
            importer.forceToMono = false;
            importer.loadInBackground = false;
        }
    }
}

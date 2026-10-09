using UnityEditor;

namespace OneMoreFloor.EditorTools
{
    /// <summary>
    /// Import settings for the Blender-generated FBX files under Resources/Models. Characters (Char_*) are
    /// skinned on an armature and carry their animation clips as takes; everything else is static.
    /// </summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        /// <summary>Raised when the settings change, so every model imports again.</summary>
        public override uint GetVersion() => 2;

        static bool IsCharacter(string path) => System.IO.Path.GetFileName(path).StartsWith("Char_");

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            bool character = IsCharacter(assetPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importAnimation = character;
            importer.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.addCollider = false;
            // the particle meshes (bats, coins) are read by their particle systems on the CPU; WebGL refuses them otherwise
            importer.isReadable = System.IO.Path.GetFileName(assetPath) == "FxMeshes.fbx";
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (character)
            {
                importer.optimizeGameObjects = false;
                importer.skinWeights = ModelImporterSkinWeights.Standard;
                importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                importer.resampleCurves = true;
            }
        }

        /// <summary>Takes come in as "Rig|Walk": name the clips after the action and make them all loop.</summary>
        void OnPreprocessAnimation()
        {
            if (!assetPath.Contains("/Resources/Models/") || !IsCharacter(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                var n = clips[i].takeName;
                int bar = n.LastIndexOf('|');
                clips[i].name = bar >= 0 ? n.Substring(bar + 1) : n;
                clips[i].loopTime = true;
                clips[i].loopPose = false;
                clips[i].lockRootRotation = false;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
            }
            importer.clipAnimations = clips;
        }
    }
}

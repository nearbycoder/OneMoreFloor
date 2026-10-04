using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OneMoreFloor.EditorTools
{
    /// <summary>
    /// Checks the imported character rigs: samples every clip of Char_&lt;kind&gt; at four moments and renders
    /// the poses into one sheet (rows = clips). Run from the resident editor:
    ///   unity command eval 'return OneMoreFloor.EditorTools.RigCheck.Sheet("Commuter", "/tmp/rig.png");'
    /// </summary>
    public static class RigCheck
    {
        public static string Sheet(string kind, string path)
        {
            var go = ModelLibrary.Instantiate("Char_" + kind);
            if (go == null) return "no model";
            var clips = AssetDatabase.LoadAllAssetsAtPath($"Assets/Resources/Models/Char_{kind}.fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).OrderBy(c => c.name).ToArray();
            var report = new StringBuilder();
            report.Append($"{kind}: clips {string.Join(",", clips.Select(c => $"{c.name}({c.length:0.00}s,loop={c.isLooping})"))}\n");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.forceMatrixRecalculationPerRender = true;
                smr.updateWhenOffscreen = true;
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                report.Append($"skinned {smr.name}: {smr.bones.Length} bones, root {smr.rootBone?.name}\n");
            var bone = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "LegL");
            if (bone) report.Append($"LegL up axis (model space) {go.transform.InverseTransformDirection(bone.up)}\n");

            var camGo = new GameObject("RigCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.33f, 0.38f);
            cam.fieldOfView = 30f;
            cam.transform.position = new Vector3(1.2f, 1.5f, 5.2f);
            cam.transform.LookAt(new Vector3(0, 1.0f, 0));
            var lightGo = new GameObject("RigSun");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.2f;
            lightGo.transform.rotation = Quaternion.Euler(40, 200, 0);
            go.transform.rotation = Quaternion.Euler(0, 15, 0);

            const int W = 220, H = 300;
            int cols = 4, rows = Mathf.Max(1, clips.Length);
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGB24, false);
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            var tmp = new Texture2D(W, H, TextureFormat.RGB24, false);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (clips.Length > 0) clips[r].SampleAnimation(go, clips[r].length * c / cols);
                    cam.Render();
                    RenderTexture.active = rt;
                    tmp.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tmp.Apply();
                    sheet.SetPixels(c * W, (rows - 1 - r) * H, W, H, tmp.GetPixels());
                }
            }
            RenderTexture.active = null;
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tmp);
            Object.DestroyImmediate(sheet);
            return report.ToString();
        }
    }
}

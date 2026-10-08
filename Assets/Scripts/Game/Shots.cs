using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OneMoreFloor
{
    /// <summary>
    /// Renders the game cameras (world + UI) into a PNG without needing a visible window. Used by the
    /// autopilot, by headless editor captures (eval), and for README screenshots. The UI camera is
    /// rendered separately onto a transparent target and composited on the CPU (premultiplied alpha).
    /// </summary>
    public static class Shots
    {
        public static string Capture(string path, int width = 1920, int height = 1080, bool withUi = true, int msaa = 4)
        {
            if (GameRoot.Instance == null) return "no GameRoot";
            var world = CaptureTexture(width, height, withUi, msaa);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, world.EncodeToPNG());
            Object.Destroy(world);
            return path;
        }

        /// <summary>The composited frame (bottom row first). Caller destroys it. <paramref name="msaa"/> is the world
        /// render's antialiasing (the trailer passes the GRAPHICS FIDELITY step's, so ULTRA records with 8x).</summary>
        public static Texture2D CaptureTexture(int width, int height, bool withUi = true, int msaa = 4)
        {
            var root = GameRoot.Instance;
            var world = Render(root.WorldCam, width, height, null, msaa);
            if (withUi && root.UiCam != null)
            {
                var ui = Render(root.UiCam, width, height, new Color(0, 0, 0, 0), 1);
                var w = world.GetPixels32();
                var u = ui.GetPixels32();
                for (int i = 0; i < w.Length; i++)
                {
                    int a = u[i].a;
                    if (a == 0) continue;
                    int ia = 255 - a;
                    w[i].r = (byte)Mathf.Min(255, u[i].r + w[i].r * ia / 255);
                    w[i].g = (byte)Mathf.Min(255, u[i].g + w[i].g * ia / 255);
                    w[i].b = (byte)Mathf.Min(255, u[i].b + w[i].b * ia / 255);
                }
                world.SetPixels32(w);
                world.Apply();
                Object.Destroy(ui);
            }
            return world;
        }

        /// <summary>Capture <paramref name="count"/> frames every <paramref name="interval"/> seconds (runs as a coroutine).</summary>
        public static System.Collections.IEnumerator Sequence(string dir, int count, float interval, int width = 960, int height = 540)
        {
            for (int i = 0; i < count; i++)
            {
                Capture(System.IO.Path.Combine(dir, $"f{i:000}.png"), width, height);
                yield return new WaitForSeconds(interval);
            }
        }

        public static string StartSequence(string dir, int count, float interval)
        {
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "f*.png")) File.Delete(f);
            GameRoot.Instance.StartCoroutine(Sequence(dir, count, interval));
            return dir;
        }

        static Texture2D Render(Camera cam, int width, int height, Color? clear, int msaa)
        {
            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = Mathf.Max(1, msaa), sRGB = true };
            var rt = RenderTexture.GetTemporary(desc);
            var data = cam.GetUniversalAdditionalCameraData();
            var prevType = data.renderType;
            var prevFlags = cam.clearFlags;
            var prevBg = cam.backgroundColor;
            var prevTarget = cam.targetTexture;
            UniversalAdditionalCameraData parent = null;
            if (prevType == CameraRenderType.Overlay)
            {
                parent = GameRoot.Instance.WorldCam.GetUniversalAdditionalCameraData();
                parent.cameraStack.Remove(cam);
                data.renderType = CameraRenderType.Base;
            }
            if (clear.HasValue)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = clear.Value;
            }
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBg;
            if (parent != null)
            {
                data.renderType = prevType;
                parent.cameraStack.Add(cam);
            }

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}

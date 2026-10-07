using System.Collections;
using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// -omfPerf [shift] [-omfNoVsync] [-omfGraphics 0|1|2] [-omfPerfShot file.png]: plays a shift with the bot at
    /// normal speed for 30 seconds and logs frame-time statistics, then quits. Vsync can be disabled so a throttled
    /// (occluded) window doesn't hide the real cost of a frame.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        int shift = 8;

        public static void TryStart(GameRoot root)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-omfPerf");
            if (i < 0) return;
            var p = root.gameObject.AddComponent<PerfProbe>();
            if (i + 1 < args.Length) int.TryParse(args[i + 1], out p.shift);
            if (System.Array.IndexOf(args, "-omfNoVsync") >= 0) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 1000; }
        }

        IEnumerator Start()
        {
            SaveData.Ephemeral = true;
            yield return new WaitForSecondsRealtime(1f);
            var root = GameRoot.Instance;
            root.BeginShift(shift);
            root.Runner.AutoBot = Bot.Decent(3);
            yield return new WaitForSecondsRealtime(3f);
            var times = new List<float>();
            float t = 0f;
            while (t < 30f)
            {
                yield return null;
                times.Add(Time.unscaledDeltaTime);
                t += Time.unscaledDeltaTime;
            }
            times.Sort();
            float avg = 0f;
            foreach (var x in times) avg += x;
            avg /= times.Count;
            Debug.Log($"[Perf] shift {shift} graphics {(GraphicsQuality.Applied >= 0 ? GraphicsQuality.Names[GraphicsQuality.Applied] : "?")} vsync {QualitySettings.vSyncCount} {Screen.width}x{Screen.height} frames {times.Count} " +
                      $"avg {1f / avg:0.0} fps | p50 {times[times.Count / 2] * 1000f:0.0} ms p95 {times[(int)(times.Count * 0.95f)] * 1000f:0.0} ms max {times[times.Count - 1] * 1000f:0.0} ms | " +
                      $"renderers {FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length} lights {FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            // -omfPerfShot file.png: what the player really sees (the back buffer, after the pipeline's own MSAA,
            // render scale and post), unlike Shots.Capture, which renders the cameras again into its own target
            var args = System.Environment.GetCommandLineArgs();
            int si = System.Array.IndexOf(args, "-omfPerfShot");
            if (si >= 0 && si + 1 < args.Length)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(args[si + 1], tex.EncodeToPNG());
                Destroy(tex);
                Debug.Log("[Perf] frame saved to " + args[si + 1]);
            }
            Application.Quit(0);
        }
    }
}

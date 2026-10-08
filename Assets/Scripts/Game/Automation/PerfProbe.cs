using System.Collections;
using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// -omfPerf [shift] [-omfNoVsync] [-omfFidelity 0-3] [-omfPerfShot file.png]: plays a shift with the bot at
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
            var args0 = System.Environment.GetCommandLineArgs();
            int li = System.Array.IndexOf(args0, "-omfPerfSecs");
            float secs = 30f;
            if (li >= 0 && li + 1 < args0.Length) float.TryParse(args0[li + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            // hitches: every frame over 40 ms, with what the shift did on it and the frame before (events, a GC)
            var frameEvents = new List<string>();
            string lastEvents = "";
            root.Runner.OnEvent += e => frameEvents.Add(e.Type.ToString());
            int gc0 = System.GC.CollectionCount(0), hitches = 0;
            var times = new List<float>();
            float t = 0f;
            // the first 3 s (the shift's opening) count for hitches but not for the averages
            while (t < secs + 3f)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                if (t >= 3f) times.Add(dt);
                t += dt;
                int gc = System.GC.CollectionCount(0);
                string now = string.Join(",", frameEvents);
                if (dt > 0.04f)
                {
                    hitches++;
                    Debug.Log($"[Perf] hitch {dt * 1000f:0} ms at {t:0.00} s (shift clock {root.Runner.Sim?.Time:0.00}){(gc != gc0 ? " gc" : "")} | this frame: {now} | before: {lastEvents}");
                }
                gc0 = gc;
                lastEvents = now;
                frameEvents.Clear();
            }
            Debug.Log($"[Perf] {hitches} frames over 40 ms");
            times.Sort();
            float avg = 0f;
            foreach (var x in times) avg += x;
            avg /= times.Count;
            Debug.Log($"[Perf] shift {shift} fidelity {(GraphicsQuality.Applied >= 0 ? GraphicsQuality.Names[GraphicsQuality.Applied] : "?")} vsync {QualitySettings.vSyncCount} {Screen.width}x{Screen.height} frames {times.Count} " +
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

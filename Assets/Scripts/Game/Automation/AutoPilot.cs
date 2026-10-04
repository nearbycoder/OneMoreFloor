using System.Collections;
using System.IO;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Self-test for the built game (launch with -omfAutopilot &lt;outdir&gt;). It walks the real menus,
    /// plays every shift with the bot through the full presentation, captures screenshots, and
    /// prints PASS/FAIL lines. Any error or exception in the log fails the run. Uses an ephemeral
    /// save so a player's progress is never touched.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        string outDir;
        int errors;
        int shots;
        float speed = 3f;
        int onlyShift = -1;

        public static void TryStart(GameRoot root)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-omfAutopilot");
            if (i < 0) return;
            var ap = root.gameObject.AddComponent<AutoPilot>();
            ap.outDir = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autopilot");
            int s = System.Array.IndexOf(args, "-omfAutopilotShift");
            if (s >= 0 && s + 1 < args.Length) int.TryParse(args[s + 1], out ap.onlyShift);
        }

        void Awake()
        {
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (msg.Contains("GPUResidentDrawer")) return;
                errors++;
                Debug.Log($"[AutoPilot] FAIL logged {type}: {msg}");
            }
        }

        void Shot(string name)
        {
            Directory.CreateDirectory(outDir);
            Shots.Capture(Path.Combine(outDir, $"{shots++:00}_{name}.png"), 1920, 1080);
        }

        IEnumerator Start()
        {
            var root = GameRoot.Instance;
            Debug.Log("[AutoPilot] start -> " + outDir);
            // unlock everything in the ephemeral save so every shift can be visited
            var save = SaveData.Current;
            for (int k = 0; k < ShiftCatalog.All.Count; k++) save.Stars[k] = 0;
            yield return Wait(2.5f);
            Shot("title");

            root.ShowRoster();
            yield return Wait(1.2f);
            Shot("roster_locked");

            int first = onlyShift >= 0 ? onlyShift : 0;
            int last = onlyShift >= 0 ? onlyShift : ShiftCatalog.All.Count - 1;
            for (int k = first; k <= last; k++)
            {
                var def = ShiftCatalog.Get(k);
                for (int u = 0; u < k; u++) if (save.Stars[u] == 0) save.Stars[u] = 1;
                root.ShowIntro(k);
                yield return Wait(1.0f);
                if (k <= 1 || k == 5 || k == 8) Shot($"intro_{def.Id}");
                root.BeginShift(k);
                var runner = root.Runner;
                runner.AutoBot = Bot.Decent((ulong)(k + 11));
                Time.timeScale = speed;
                float played = 0f;
                float limit = k == 0 ? 200f : 60f;  // Monday plays to the bell; others get a minute of game time
                float nextShot = 18f;
                while (!runner.Sim.Ended && played < limit)
                {
                    yield return null;
                    played += Time.unscaledDeltaTime * speed;
                    if (played >= nextShot)
                    {
                        Time.timeScale = 0f;
                        yield return null;
                        Shot($"play_{def.Id}_{(int)nextShot}s");
                        Time.timeScale = speed;
                        nextShot += k == 0 ? 40f : 30f;
                    }
                }
                Time.timeScale = 1f;
                var sim = runner.Sim;
                if (!sim.Ended)
                {
                    // end the shift early through the real flow: pause, then fast-forward the clock
                    root.Pause();
                    yield return Wait(0.6f);
                    if (k == 2) Shot("pause");
                    root.Resume();
                    runner.FastForward(sim.Def.Endless ? 0f : sim.TimeLeft + 0.1f);
                    if (sim.Def.Endless) { runner.AutoBot = null; while (!sim.Ended) runner.FastForward(5f); }
                }
                yield return Wait(6.5f);
                Shot($"results_{def.Id}");
                bool ok = sim.Ended && sim.Score > 0 && sim.DeliveredCount > 0;
                Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {def.Id}: score {sim.Score} stars {sim.StarCount} delivered {sim.DeliveredCount} complaints {sim.Complaints} stops {sim.Stops} fired {sim.Fired}");
                if (!ok) errors++;
                if (save.Stars[k] == 0) save.Stars[k] = 1;
            }

            if (onlyShift < 0)
            {
                root.ShowRoster();
                yield return Wait(1.2f);
                Shot("roster_unlocked");
                var settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
                root.ShowSettings(null);
                yield return Wait(1.0f);
                Shot("settings");
                settings.Hide();
                var ending = FindAnyObjectByType<EndingScreen>(FindObjectsInactive.Include);
                ending.Show();
                yield return Wait(6f);
                Shot("ending");
                root.ShowTitle();
                yield return Wait(1.5f);
            }

            Debug.Log(errors == 0 ? "[AutoPilot] done: PASS" : $"[AutoPilot] done: FAIL ({errors} problems)");
            yield return Wait(0.5f);
            Application.Quit(errors == 0 ? 0 : 1);
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}

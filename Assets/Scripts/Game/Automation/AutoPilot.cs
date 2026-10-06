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
        bool padOnly, uiOnly;
        int frames;
        float frameTime, worst;

        public static void TryStart(GameRoot root)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-omfAutopilot");
            if (i < 0) return;
            var ap = root.gameObject.AddComponent<AutoPilot>();
            ap.outDir = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autopilot");
            int s = System.Array.IndexOf(args, "-omfAutopilotShift");
            if (s >= 0 && s + 1 < args.Length)
            {
                if (args[s + 1] == "pad") ap.padOnly = true;
                else if (args[s + 1] == "ui") ap.uiOnly = true;
                else int.TryParse(args[s + 1], out ap.onlyShift);
            }
            int sp = System.Array.IndexOf(args, "-omfAutopilotSpeed");
            if (sp >= 0 && sp + 1 < args.Length) float.TryParse(args[sp + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ap.speed);
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

            if (uiOnly)
            {
                yield return UiChecks(root);
                yield return Finish();
                yield break;
            }

            if (padOnly)
            {
                yield return PadCheck(root);
                yield return Finish();
                yield break;
            }

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
                    if (Time.unscaledDeltaTime < 0.25f) { frames++; frameTime += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
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
                yield return PadCheck(root);
                yield return UiChecks(root);
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

            yield return Finish();
        }

        IEnumerator Finish()
        {
            if (frames > 0) Debug.Log($"[AutoPilot] perf: {frames / frameTime:0} fps average during play, worst frame {worst * 1000f:0} ms (captures excluded)");
            Debug.Log(errors == 0 ? "[AutoPilot] done: PASS" : $"[AutoPilot] done: FAIL ({errors} problems)");
            yield return Wait(0.5f);
            Application.Quit(errors == 0 ? 0 : 1);
        }

        void Check(string what, bool ok, string area = "pad")
        {
            Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {area}: {what}");
            if (!ok) errors++;
        }

        /// <summary>
        /// Menu and flow checks that need a particular save or situation, set up in the ephemeral save: the late pass,
        /// the time card's complaint breakdown, pausing when focus or the controller goes away, and the settings.
        /// </summary>
        IEnumerator UiChecks(GameRoot root)
        {
            var save = SaveData.Current;
            var results = FindAnyObjectByType<ResultsScreen>(FindObjectsInactive.Include);
            System.Array.Clear(save.Stars, 0, save.Stars.Length);
            System.Array.Clear(save.Plays, 0, save.Plays.Length);
            System.Array.Clear(save.Best, 0, save.Best.Length);

            // late pass: two tries on Tuesday without a star, then a third
            save.Stars[0] = 2; save.Plays[0] = 1; save.Best[0] = 20500;
            save.Plays[1] = Progress.LatePassAttempts - 1;
            root.ShowRoster();
            yield return Wait(1.2f);
            Shot("ui_roster_one_more_try");
            Check("Wednesday is locked with one try to go", !save.Unlocked(2) && Progress.TriesToLatePass(2, save.Stars, save.Plays) == 1, "ui");
            root.StartShift(1, 4242);
            var runner = root.Runner;
            runner.AutoBot = Bot.Human(3, 0f);
            yield return Wait(1f);
            runner.FastForward(40f);
            runner.AutoBot = null; // walk away: complaints pile up
            runner.FastForward(runner.Sim.TimeLeft + 0.1f);
            yield return Wait(6.5f);
            Shot("ui_results_late_pass");
            var sim = runner.Sim;
            Check($"third try without a star opens Wednesday (stars {sim.StarCount}, complaints {sim.Complaints})",
                sim.StarCount == 0 && save.Unlocked(2) && save.LatePassed(2) && results.Visible, "ui");
            root.ShowRoster();
            yield return Wait(1.2f);
            Shot("ui_roster_late_pass");
            Check("the title offers Wednesday next", save.NextShift() == 2, "ui");

            Check("the time card lists what the complaints were about", results.CausesText.Contains("TOOK THE STAIRS") && results.AdviceText.Length > 0, "ui");

            // a weak player's Graveyard Shift: several kinds of complaint, one tip for the biggest
            for (int k = 0; k < 8; k++) save.Stars[k] = Mathf.Max(save.Stars[k], 1);
            root.StartShift(8, 1301);
            runner = root.Runner;
            runner.AutoBot = Bot.Human(7, 0f);
            yield return Wait(1f);
            runner.FastForward(runner.Sim.TimeLeft + 0.1f);
            yield return Wait(6.5f);
            sim = runner.Sim;
            var biggest = Advice.Biggest(sim.ComplaintsBy);
            Check($"weak Graveyard time card: '{results.CausesText}' / '{results.AdviceText}'",
                results.Visible && sim.Complaints > 0 && biggest.HasValue && results.AdviceText == Advice.Tip(biggest.Value), "ui");
            Shot("ui_results_causes");

            // auto-pause: losing focus, then the controller going away
            var pause = FindAnyObjectByType<PauseScreen>(FindObjectsInactive.Include);
            root.AutoPause = true;
            root.StartShift(0, 77);
            runner = root.Runner;
            runner.AutoBot = Bot.Decent(5);
            yield return Wait(3f);
            root.SendMessage("OnApplicationFocus", false);
            yield return null;
            float t0 = runner.Sim.Time;
            yield return Wait(1.5f);
            Check("losing focus pauses the shift and stops the clock", pause.Visible && runner.Paused && runner.Sim.Time == t0, "ui");
            Shot("ui_pause_focus");
            root.Resume();
            yield return Wait(0.6f);
            Check("resume carries on from the same moment", !runner.Paused && runner.Sim.Time > t0, "ui");
            yield return Pad("rb");
            PadSim.Unplug();
            yield return Wait(0.6f);
            Check("unplugging the controller in use pauses", pause.Visible && runner.Paused && pause.Reason.Contains("CONTROLLER"), "ui");
            Shot("ui_pause_pad");
            root.Resume();
            root.AutoPause = false;
            root.QuitShift();
            yield return Wait(1f);
        }

        IEnumerator Pad(string script, float gap = 0.35f)
        {
            PadSim.Play(script, gap);
            yield return null;
            while (PadSim.Busy) yield return null;
            yield return Wait(0.4f);
        }

        /// <summary>Drives the menus and a shift with a virtual gamepad through the Input System.</summary>
        IEnumerator PadCheck(GameRoot root)
        {
            var title = FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            var roster = FindAnyObjectByType<RosterScreen>(FindObjectsInactive.Include);
            var pause = FindAnyObjectByType<PauseScreen>(FindObjectsInactive.Include);
            root.ShowTitle();
            yield return Wait(1.5f);
            // first press shows the focus ring on START SHIFT, the second moves to DUTY ROSTER, A opens it
            yield return Pad("down down a");
            Check("title -> roster with d-pad + A", roster.Visible && Controls.Pad && UiNav.Driving);
            Shot("pad_roster");
            yield return Pad("b");
            Check("B goes back to the title", title.Visible && !roster.Visible);

            root.BeginShift(1);
            var runner = root.Runner;
            runner.AutoBot = null;
            yield return Wait(2.5f);
            float zoomBefore = runner.Rig.Zoom;
            yield return Pad("up hold:rt");
            Check("in-shift floor cursor follows the d-pad", runner.CursorMode && runner.CursorSlot >= 1);
            Check("RT zooms in", runner.Rig.Zoom > zoomBefore + 0.2f);
            yield return Pad("a");
            Check("A sends the car to the cursor floor", runner.Sim.Car.Target == runner.Sim.B.At(runner.CursorSlot) || runner.Sim.Car.DockedSlot == runner.CursorSlot);
            yield return Wait(4f);
            yield return Pad("y rb");
            Shot("pad_play");
            yield return Pad("start");
            Check("Start pauses", pause.Visible && runner.Paused);
            yield return Pad("start");
            Check("Start resumes", !pause.Visible && !runner.Paused);
            yield return Pad("hold:lt hold:lt");
            Check("LT zooms back out", runner.Rig.Zoom < 0.05f);
            root.QuitShift();
            yield return Wait(1f);
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}

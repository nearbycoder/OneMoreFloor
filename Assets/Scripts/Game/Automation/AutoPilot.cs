using System.Collections;
using System.Collections.Generic;
using System.IO;
using OneMoreFloor.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Switch;

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
        bool popupShot, jamShot;

        public static void TryStart(GameRoot root)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-omfAutopilot");
            if (i < 0) return;
            // The virtual pads must keep working when another window takes focus (a shared desktop does that):
            // by default the Input System disables devices while unfocused. Players keep the default (and a
            // focus loss pauses the shift); the focus pause itself is checked through OnApplicationFocus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
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
            // at the window's own size, so other aspect ratios are captured as players see them
            Shots.Capture(Path.Combine(outDir, $"{shots++:00}_{name}.png"), Screen.width, Screen.height);
        }

        IEnumerator Start()
        {
            var root = GameRoot.Instance;
            Debug.Log($"[AutoPilot] start -> {outDir} ({Screen.width}x{Screen.height})");
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
                // one-time tips would all be used up by Wednesday: forget them so every shift's tips get measured
                save.SeenHints = new string[0];
                root.ShowIntro(k);
                yield return Wait(1.0f);
                if (k <= 1 || k == 5 || k == 8) Shot($"intro_{def.Id}");
                root.BeginShift(k);
                var runner = root.Runner;
                runner.AutoBot = Bot.Decent((ulong)(k + 11));
                runner.Hud.WorstPopupOverlap = 0f;
                runner.Hud.CoinCrossFrames = runner.Hud.CoinOverTextFrames = 0;
                int starFrames = 0, starBad = 0, starLag = 0, starPeak = 0;
                string starWhy = "";
                int chipFrames = 0, chipBad = 0, chipSeen = 0, chipStops = 0, chipStopBad = 0, chipJams = 0, chipStopMarks = 0, prevStops = runner.Sim.Stops;
                string chipWhy = "";
                string[] prevChips = null;
                FloorId[] prevSlots = null;
                FloorId? prevDock = null;
                int tipFrames = 0, tipBad = 0, tipSpill = 0;
                float tipWorst = 0f, tipNarrowest = float.MaxValue;
                string tipWhy = "";
                Time.timeScale = speed;
                float played = 0f;
                // timed shifts play to the bell (Monday's clock waits for the first drop-off, hence the slack);
                // Overtime only ends on complaints, so it gets a minute before a fast-forward
                float limit = def.Endless ? 60f : def.Duration * 2f;
                float nextShot = 18f;
                bool pauseShot = false;
                while (!runner.Sim.Ended && played < limit)
                {
                    yield return null;
                    played += Time.unscaledDeltaTime * speed;
                    // the HUD's star track agrees with the score: lit stars, and the next target in the caption
                    {
                        var ss = runner.Sim;
                        int expect = ss.Def.StarsFor(ss.Score);
                        bool right = runner.Hud.StarsDrawnLit == expect
                                     && (expect >= 3 ? runner.Hud.StarCaption.Contains("ALL THREE")
                                                     : runner.Hud.StarCaption.Contains("$" + (ss.Def.Stars[expect] - ss.Score).ToString("N0")));
                        starFrames++;
                        starPeak = Mathf.Max(starPeak, runner.Hud.StarsDrawnLit);
                        if (right) starLag = 0;
                        else if (++starLag > 1) { starBad++; starWhy = $"score {ss.Score}, drawn {runner.Hud.StarsDrawnLit}, '{runner.Hud.StarCaption}'"; }
                    }
                    // the forecast chips on the floor labels: the sim's preview every frame, and where floors really land
                    {
                        var ss = runner.Sim;
                        var lab = runner.Hud.FloorLabels;
                        var hover = runner.HoverFloor;
                        FloorId? dock = hover.HasValue && ss.B.Has(hover.Value) && (ss.Car.Target.HasValue || hover.Value != ss.DockedFloor) ? hover : ss.Car.Target;
                        bool whatIf = dock.HasValue && dock == hover && dock != ss.Car.Target;
                        bool jam = false;
                        var after = SaveData.Current.ShowForecast && !ss.Ended ? ss.PreviewStop(dock, out jam) : null;
                        bool visible = !ss.Ended && runner.Rig.ZoomShown < 0.35f;
                        if (ss.Stops != prevStops && prevChips != null && prevDock.HasValue && prevDock.Value == ss.DockedFloor)
                        {
                            chipStops++;
                            bool missed = false;
                            for (int sl = 0; sl < prevSlots.Length; sl++)
                            {
                                var f = prevSlots[sl];
                                if (!ss.B.Has(f)) continue; // left the building at the stop
                                string c = sl < prevChips.Length ? prevChips[sl] : "";
                                int want = c.Length > 0 && (c[0] == '+' || c[0] == '-') ? int.Parse(c.Substring(1)) - 1 : sl;
                                if (c == "JAM") chipJams++;
                                if (ss.B.SlotOf(f) != want) { missed = true; chipWhy = $"stop {ss.Stops}: {f} chip '{c}' but landed in {ss.B.SlotOf(f) + 1}"; }
                            }
                            if (missed) chipStopBad++;
                        }
                        prevStops = ss.Stops;
                        if (visible)
                        {
                            chipFrames++;
                            bool any = false, right = true;
                            for (int sl = 0; sl < ss.B.Count; sl++)
                            {
                                var f = ss.B.At(sl);
                                int to = after != null ? after.SlotOf(f) : -1;
                                string want = after != null && jam && dock == f ? "JAM" : to >= 0 && to != sl ? (to > sl ? "+" : "-") + (to + 1)
                                            : after != null && dock == f && to == sl ? (whatIf ? "STOP?" : "STOP") : "";
                                string got = lab.ChipAt(sl);
                                if (got == "STOP") chipStopMarks++;
                                if (got.Length > 0) any = true;
                                if (got != want) { right = false; chipWhy = $"slot {sl + 1} ({f}): chip '{got}', preview '{want}'"; }
                            }
                            if (!right) chipBad++;
                            if (any) chipSeen++;
                            bool jamUp = false;
                            for (int sl = 0; sl < ss.B.Count; sl++) jamUp |= lab.ChipAt(sl) == "JAM";
                            if (!jamShot && jamUp && k >= 3)
                            {
                                // the anchor rule on the labels: the car's next stop is a floor the next card names
                                jamShot = true;
                                Time.timeScale = 0f;
                                yield return null;
                                Shot($"forecast_jam_{def.Id}");
                                Time.timeScale = speed;
                            }
                            prevChips = new string[ss.B.Count];
                            for (int sl = 0; sl < ss.B.Count; sl++) prevChips[sl] = lab.ChipAt(sl);
                            prevSlots = ss.B.Slots.ToArray();
                            prevDock = lab.PreviewedDock;
                        }
                        else prevChips = null;
                    }
                    // the coach tip never covers a floor label, and its text fits its box
                    {
                        var coach = root.Coach;
                        var lab = runner.Hud.FloorLabels;
                        if (coach != null && coach.Showing && lab.Alpha > 0.5f && !runner.Sim.Ended)
                        {
                            tipFrames++;
                            var tb = UiKit.ScreenRect(coach.Box, coach.GetComponentInParent<Canvas>().worldCamera);
                            tipNarrowest = Mathf.Min(tipNarrowest, coach.Box.rect.width);
                            float o = 0f;
                            for (int sl = 0; sl < runner.Sim.B.Count; sl++)
                            {
                                var r = lab.ScreenRectAt(sl, resting: true);
                                if (!r.HasValue) continue;
                                float ox = Mathf.Min(tb.xMax, r.Value.xMax) - Mathf.Max(tb.xMin, r.Value.xMin);
                                float oy = Mathf.Min(tb.yMax, r.Value.yMax) - Mathf.Max(tb.yMin, r.Value.yMin);
                                if (ox > 0f && oy > 0f && Mathf.Min(ox, oy) > o) { o = Mathf.Min(ox, oy); tipWhy = $"slot {sl + 1} by {ox:0.0} x {oy:0.0} px"; }
                            }
                            if (o > 0f) tipBad++;
                            tipWorst = Mathf.Max(tipWorst, o);
                            var tt = coach.Text;
                            if (tt.GetPreferredValues(tt.text, tt.rectTransform.rect.width, 0f).y > tt.rectTransform.rect.height + 1f) tipSpill++;
                        }
                    }
                    if (Time.unscaledDeltaTime < 0.25f) { frames++; frameTime += Time.unscaledDeltaTime; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
                    if (!popupShot && k >= 3 && runner.Hud.PopupCount >= 4)
                    {
                        // a busy reward moment, to see the popups laid out
                        popupShot = true;
                        Time.timeScale = 0f;
                        yield return null;
                        Shot($"popups_{def.Id}");
                        Time.timeScale = speed;
                    }
                    if (played >= nextShot)
                    {
                        Time.timeScale = 0f;
                        yield return null;
                        Shot($"play_{def.Id}_{(int)nextShot}s");
                        Time.timeScale = speed;
                        nextShot += 45f;
                    }
                    if ((k == 2 || onlyShift >= 0) && !pauseShot && played >= 33f)
                    {
                        pauseShot = true;
                        root.Pause();
                        yield return Wait(0.6f);
                        Shot("pause");
                        string lay = ControlsLayout(FindAnyObjectByType<PauseScreen>());
                        Check($"{def.Id}: the pause card and its controls panel fit side by side at {Screen.width}x{Screen.height}{(lay.Length > 0 ? ":" + lay : "")}", lay.Length == 0, "ui");
                        root.Resume();
                    }
                }
                Time.timeScale = 1f;
                var sim = runner.Sim;
                bool rang = sim.Ended;
                if (!def.Endless)
                    Check($"{def.Id}: played to the bell in {played:0} s of game time (rush hour {(sim.RushHour ? "ran" : "never came")})", rang && sim.RushHour, "bell");
                if (!sim.Ended)
                {
                    // end the shift early through the real flow: pause, then fast-forward the clock
                    root.Pause();
                    yield return Wait(0.6f);
                    root.Resume();
                    runner.FastForward(sim.Def.Endless ? 0f : sim.TimeLeft + 0.1f);
                    if (sim.Def.Endless) { runner.AutoBot = null; while (!sim.Ended) runner.FastForward(5f); }
                }
                yield return Wait(6.5f);
                Shot($"results_{def.Id}");
                bool ok = sim.Ended && sim.Score > 0 && sim.DeliveredCount > 0;
                Debug.Log($"[AutoPilot] {(ok ? "PASS" : "FAIL")} {def.Id}: score {sim.Score} stars {sim.StarCount} delivered {sim.DeliveredCount} complaints {sim.Complaints} stops {sim.Stops} fired {sim.Fired}");
                if (!ok) errors++;
                // popups never draw over each other (a few px of glyph-box contact is allowed)
                float overlap = runner.Hud.WorstPopupOverlap;
                Check($"{def.Id}: worst popup overlap {overlap:0.0} px" + (overlap > 0f ? $" ({runner.Hud.WorstPopupPair})" : ""), overlap <= 4f, "popups");
                // flying coins burst from where the tip pops up: they may pass behind its text, never over it
                Check($"{def.Id}: flying coins crossed popup text on {runner.Hud.CoinCrossFrames} frames and drew over it on {runner.Hud.CoinOverTextFrames}",
                      runner.Hud.CoinOverTextFrames == 0, "popups");
                Check($"{def.Id}: HUD star track matched the score on {starFrames - starBad}/{starFrames} frames (peak {starPeak} lit)" + (starBad > 0 ? $" last miss: {starWhy}" : ""),
                      starBad == 0 && starFrames > 0, "stars");
                Check($"{def.Id}: forecast chips matched the preview on {chipFrames - chipBad}/{chipFrames} frames (chips up on {chipSeen}, STOP marks {chipStopMarks})" + (chipBad > 0 ? $" last miss: {chipWhy}" : ""),
                      chipBad == 0 && chipSeen > 0 && chipStopMarks > 0, "forecast");
                Check($"{def.Id}: floors landed where the chips said at {chipStops - chipStopBad}/{chipStops} stops ({chipJams} JAM chips)" + (chipStopBad > 0 ? $" miss: {chipWhy}" : ""),
                      chipStopBad == 0 && chipStops > 0, "forecast");
                Check($"{def.Id}: the coach tip cleared the resting floor labels on {tipFrames - tipBad}/{tipFrames} frames (worst {tipWorst:0.0} px, narrowest box {(tipFrames > 0 ? tipNarrowest : 0f):0})"
                      + (tipBad > 0 ? $" last: {tipWhy}" : "") + (tipSpill > 0 ? $"; text spilled on {tipSpill} frames" : ""), tipBad == 0 && tipSpill == 0 && tipFrames > 0, "tips");
                Check($"{def.Id}: after the bell the track shows {runner.Hud.StarsDrawnLit} stars for {sim.StarCount}", runner.Hud.StarsDrawnLit == sim.StarCount, "stars");
                if (save.Stars[k] == 0) save.Stars[k] = 1;
            }

            if (onlyShift < 0)
            {
                yield return PadCheck(root);
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
                yield return UiChecks(root);
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
            save.EndingSeen = true; // a star here would otherwise queue the first-time ending
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

            // relaxed shifts: a weak player's Wednesday runs to the bell and saves no stars or best
            System.Array.Clear(save.Stars, 0, save.Stars.Length);
            System.Array.Clear(save.Best, 0, save.Best.Length);
            System.Array.Clear(save.RelaxedClear, 0, save.RelaxedClear.Length);
            System.Array.Clear(save.FiredCount, 0, save.FiredCount.Length);
            save.Stars[0] = save.Stars[1] = 1;
            save.Relaxed = true;
            root.ShowIntro(2);
            yield return Wait(1.2f);
            Shot("ui_intro_relaxed");
            root.StartShift(2, 2024);
            runner = root.Runner;
            runner.AutoBot = Bot.Human(5, 0f);
            yield return Wait(1f);
            runner.FastForward(runner.Sim.TimeLeft + 0.1f);
            yield return Wait(6.5f);
            sim = runner.Sim;
            Shot("ui_results_relaxed");
            Check($"a relaxed Wednesday isn't fired and saves no stars (score {sim.Score}, complaints {sim.Complaints}, clear {save.RelaxedClear[2]})",
                sim.Relaxed && !sim.Fired && save.Stars[2] == 0 && save.Best[2] == 0 && results.BestLineText.Contains("RELAXED")
                && save.RelaxedClear[2] == (sim.Score >= sim.Def.Stars[0]) && save.Unlocked(3) == save.RelaxedClear[2], "ui");
            root.ShowRoster();
            yield return Wait(1.2f);
            Shot("ui_roster_relaxed");
            save.Relaxed = false;

            // fired a second time on a shift: the time card suggests Relaxed
            save.FiredCount[2] = 1;
            root.StartShift(2, 31);
            runner = root.Runner;
            runner.AutoBot = null;
            yield return Wait(1f);
            runner.FastForward(runner.Sim.TimeLeft + 0.1f);
            yield return Wait(6.5f);
            Check("fired twice on a shift: the time card suggests Relaxed", runner.Sim.Fired && results.HintText.Contains("Relaxed"), "ui");
            Shot("ui_results_suggest_relaxed");

            // guest size: a crowded nine-floor shift at full zoom-out, then a full car in close-up
            save.Zoom = 0f;
            root.StartShift(6, 777);
            runner = root.Runner;
            runner.Rig.Zoom = 0f;
            runner.AutoBot = Bot.Strong(9);
            runner.FastForward(70f);
            runner.Paused = true;
            yield return Wait(1.5f);
            Shot("ui_guests_busy");
            runner.Paused = false;
            // fill the car: let everyone in at each stop, then head for the busiest other floor
            runner.AutoBot = null;
            var fs = runner.Sim;
            for (int i = 0; i < 160 && !(fs.Car.Load == fs.Car.Capacity && fs.Car.IsOpen); i++)
            {
                if (fs.Car.IsOpen)
                {
                    fs.BoardAll();
                    if (fs.Car.Load < fs.Car.Capacity)
                    {
                        int bestSlot = -1, most = 0;
                        for (int sl = 0; sl < fs.B.Count; sl++)
                            if (sl != fs.Car.DockedSlot && fs.Waiting[(int)fs.B.At(sl)].Count > most) { most = fs.Waiting[(int)fs.B.At(sl)].Count; bestSlot = sl; }
                        if (bestSlot >= 0) runner.RequestSend(bestSlot, false);
                    }
                }
                runner.FastForward(0.5f);
            }
            runner.Paused = true;
            runner.Rig.Zoom = 1f;
            yield return Wait(2.5f);
            Shot("ui_guests_full_car");
            Check($"captured a full car for the guest-size check (load {runner.Sim.Car.Load})", runner.Sim.Car.Load == runner.Sim.Car.Capacity, "ui");
            runner.Paused = false;
            runner.Rig.Zoom = 0f;
            root.QuitShift();
            yield return Wait(1f);

            // patience at a glance: a floor's waiting badges (label column and panel) follow its most impatient guest
            root.StartShift(8, 91);
            runner = root.Runner;
            runner.Rig.Zoom = 0f;
            runner.AutoBot = null;
            runner.FastForward(9f);
            var ps = runner.Sim;
            int lowSlot = -1, warnSlot = -1, calmSlot = -1;
            for (int sl = 0; sl < ps.B.Count; sl++)
            {
                if (sl == ps.Car.DockedSlot || ps.Waiting[(int)ps.B.At(sl)].Count == 0) continue;
                if (lowSlot < 0) lowSlot = sl; else if (calmSlot < 0) calmSlot = sl; else if (warnSlot < 0) warnSlot = sl;
            }
            void SetPatience(int sl, float frac)
            {
                if (sl < 0) return;
                var w = ps.Waiting[(int)ps.B.At(sl)];
                foreach (var g in w) g.Patience = g.PatienceMax;
                w[0].Patience = w[0].PatienceMax * frac;
            }
            SetPatience(lowSlot, 0.15f);
            SetPatience(calmSlot, 1f);
            SetPatience(warnSlot, 0.4f);
            runner.Paused = true;
            yield return Wait(1.2f);
            var labelsUi = runner.Hud.FloorLabels;
            var panelUi = runner.Hud.Panel;
            bool BadgeIs(int sl, Color c) => sl >= 0 && labelsUi.BadgeAt(sl) != null && labelsUi.BadgeAt(sl).Visible && labelsUi.BadgeAt(sl).Shown == c
                                             && panelUi.BadgeAt(sl) != null && panelUi.BadgeAt(sl).Visible && panelUi.BadgeAt(sl).Shown == c;
            Check($"a floor with a guest at 15% patience shows red badges on its label and panel button (slot {lowSlot + 1})", BadgeIs(lowSlot, Palette.Bad), "ui");
            Check($"a floor where everyone's patient shows calm badges (slot {calmSlot + 1})", BadgeIs(calmSlot, WaitBadge.Calm), "ui");
            if (warnSlot >= 0) Check($"a floor with a guest at 40% shows amber badges (slot {warnSlot + 1})", BadgeIs(warnSlot, Palette.Warn), "ui");
            Shot("ui_patience_badges");
            // the amber case on the red floor too, so it's checked even when no third floor has guests waiting
            SetPatience(lowSlot, 0.4f);
            yield return Wait(0.6f);
            Check($"raising that guest to 40% turns its floor's badges amber (slot {lowSlot + 1})", BadgeIs(lowSlot, Palette.Warn), "ui");
            Shot("ui_patience_badges_amber");
            runner.Paused = false;
            root.QuitShift();
            yield return Wait(1f);

            // the forecast tags mark the stop they describe: STOP on the car's target, STOP? on a floor being pointed at
            {
                root.StartShift(8, 77);
                runner = root.Runner;
                runner.Rig.Zoom = 0f;
                runner.InputEnabled = false;
                runner.ShowHoverFloor(null);
                runner.AutoBot = Bot.Decent(5);
                var lab = runner.Hud.FloorLabels;
                float waited = 0f;
                while (waited < 30f && !(runner.Sim.Car.Target.HasValue && !runner.Sim.Car.IsOpen && runner.Sim.Car.Target != runner.Sim.DockedFloor && lab.Alpha > 0.95f))
                { yield return null; waited += Time.unscaledDeltaTime; }
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                var ss = runner.Sim;
                FloorId target = ss.Car.Target ?? FloorId.Lobby;
                int tSlot = ss.B.SlotOf(target);
                ss.PreviewStop(target, out bool tJam);
                string atTarget = lab.ChipAt(tSlot);
                Check($"heading for {target}: its label says {(tJam ? "JAM" : "STOP")} (got '{atTarget}')",
                      ss.Car.Target.HasValue && atTarget == (tJam ? "JAM" : "STOP") && !lab.WhatIf && lab.PreviewedDock == target, "ui");
                Shot("ui_forecast_stop");
                // point at another floor the card leaves alone: STOP? there, and every tag follows that stop
                int hSlot = -1;
                for (int sl = ss.B.Count - 1; sl >= 0 && hSlot < 0; sl--)
                {
                    var f = ss.B.At(sl);
                    if (f == target || f == ss.DockedFloor) continue;
                    var a = ss.PreviewStop(f, out bool j);
                    if (!j && a.SlotOf(f) == sl) hSlot = sl;
                }
                if (hSlot >= 0)
                {
                    var hover = ss.B.At(hSlot);
                    runner.ShowHoverFloor(hover);
                    yield return null;
                    yield return null;
                    var after = ss.PreviewStop(hover, out _);
                    string why = "";
                    for (int sl = 0; sl < ss.B.Count; sl++)
                    {
                        var f = ss.B.At(sl);
                        int to = after.SlotOf(f);
                        string want = to >= 0 && to != sl ? (to > sl ? "+" : "-") + (to + 1) : f == hover ? "STOP?" : "";
                        if (lab.ChipAt(sl) != want) why += $" slot {sl + 1} '{lab.ChipAt(sl)}' want '{want}'";
                    }
                    Check($"pointing at {hover} while the car heads for {target}: STOP? on {hover} and the tags follow that stop{why}",
                          why.Length == 0 && lab.WhatIf && lab.PreviewedDock == hover, "ui");
                    Shot("ui_forecast_stop_if");
                    // pointing at the floor the car is already heading for is no what-if
                    runner.ShowHoverFloor(target);
                    yield return null;
                    yield return null;
                    Check($"pointing at the car's own target shows STOP, not STOP? (got '{lab.ChipAt(tSlot)}')", lab.ChipAt(tSlot) == (tJam ? "JAM" : "STOP") && !lab.WhatIf, "ui");
                }
                else Check("found a floor to point at for the STOP? check", false, "ui");
                runner.ShowHoverFloor(null);
                runner.InputEnabled = true;
                Time.timeScale = 1f;
                root.QuitShift();
                yield return Wait(1f);
            }

            // daily Overtime: today's shift opens the same way every time, and today's best is kept
            int ot = GameRoot.OvertimeIndex;
            for (int k = 0; k < ot; k++) save.Stars[k] = Mathf.Max(save.Stars[k], 1);
            save.EndingSeen = true;
            save.DailyDate = ""; save.DailyBest = save.DailyRecord = save.DailyPlays = 0;
            root.DailyDateOverride = new System.DateTime(2026, 10, 6);
            root.ShowIntro(ot);
            yield return Wait(1.2f);
            Shot("ui_intro_daily");
            root.BeginDaily();
            runner = root.Runner;
            runner.AutoBot = null;
            runner.FastForward(12f);
            string open1 = Opening(runner.Sim);
            runner.AutoBot = Bot.Decent(4);
            runner.FastForward(90f);
            runner.AutoBot = null;
            while (!runner.Sim.Ended) runner.FastForward(5f);
            yield return Wait(6.5f);
            int daily1 = runner.Sim.Score;
            Shot("ui_results_daily");
            Check($"today's shift is recorded (${daily1}, best today ${save.DailyBestOn("2026-10-06")})",
                results.Visible && root.DailyRun && save.DailyBestOn("2026-10-06") == daily1 && save.DailyPlays == 1 && results.HintText.Contains("Today's shift"), "ui");
            root.RestartShift(); // ONE MORE SHIFT plays today's shift again
            runner = root.Runner;
            runner.AutoBot = null;
            runner.FastForward(12f);
            string open2 = Opening(runner.Sim);
            runner.AutoBot = null;
            while (!runner.Sim.Ended) runner.FastForward(5f);
            yield return Wait(6.5f);
            Shot("ui_results_daily_again");
            Check($"a lower second run keeps today's best (${runner.Sim.Score} vs ${daily1})",
                save.DailyPlays == 2 && save.DailyBestOn("2026-10-06") == System.Math.Max(daily1, runner.Sim.Score)
                && (runner.Sim.Score > daily1 || results.BestLineText.Contains("TODAY'S BEST")), "ui");
            Check("ONE MORE SHIFT replays today's shift with the same opening", root.DailyRun && open1 == open2 && open1.Length > 0, "ui");
            root.QuitShift();
            root.DailyDateOverride = null;
            yield return Wait(1f);

            // auto-pause: losing focus, then the controller going away
            var pause = FindAnyObjectByType<PauseScreen>(FindObjectsInactive.Include);
            root.AutoPause = true;
            root.StartShift(8, 77); // nine floors: the tightest fit for the prompt strip
            runner = root.Runner;
            runner.Rig.Zoom = 0f; // the whole tower, as the prompt-strip framing is meant to be checked
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
            // a nudged mouse becomes the active input, but the player is still holding the controller
            var nudge = InputSystem.AddDevice<Mouse>("OMF Virtual Mouse");
            nudge.MakeCurrent();
            InputSystem.QueueStateEvent(nudge, new MouseState { delta = new Vector2(40f, 0f) });
            yield return null;
            yield return null;
            bool nudged = !Controls.Pad;
            // a second controller nobody touched this shift goes away: play carries on
            var spare = InputSystem.AddDevice<Gamepad>("OMF Spare Pad");
            yield return null;
            InputSystem.RemoveDevice(spare);
            yield return Wait(0.5f);
            Check("an untouched second controller going away doesn't pause", !runner.Paused && !pause.Visible, "ui");
            PadSim.Unplug();
            yield return Wait(0.6f);
            InputSystem.RemoveDevice(nudge);
            Check($"unplugging the controller in use pauses, even after a mouse nudge (nudged {nudged})", nudged && pause.Visible && runner.Paused && pause.Reason.Contains("CONTROLLER"), "ui");
            Shot("ui_pause_pad");
            root.Resume();
            root.AutoPause = false;

            // controller families: the prompt strip and hints follow the pad in use
            runner.AutoBot = null;
            runner.InputEnabled = false;
            Controls.ForcePad = true;
            runner.ScriptCursor(1, -1);
            var ds = InputSystem.AddDevice<DualShock4GamepadHID>();
            ds.MakeCurrent();
            yield return Wait(0.8f);
            Check("a DualShock 4 shows PlayStation prompts", PadGlyphs.Family == PadFamily.PlayStation && PadGlyphs.Face(GamepadButton.South).Symbol != null
                && PadGlyphs.Words("Press <b>A</b> (<b>LB/RB</b> picks one, hold <b>RT</b> to zoom)") == "Press <b>Cross</b> (<b>L1/R1</b> picks one, hold <b>R2</b> to zoom)", "ui");
            Shot("ui_prompts_playstation");
            root.Pause();
            yield return Wait(0.6f);
            {
                string ct = pause.ControlsPanel.Text, lay = ControlsLayout(pause);
                Check($"with a DualShock 4 the controls panel uses PlayStation names{(lay.Length > 0 ? ":" + lay : "")}",
                      ct.StartsWith("PLAYSTATION CONTROLLER\n") && ct.Contains("\nCROSS: Send") && ct.Contains("\nTRIANGLE: Let everyone in") && ct.Contains("\nSQUARE: Let the picked rider")
                      && ct.Contains("\nCIRCLE: Unpick") && ct.Contains("L1 / R1") && ct.Contains("\nR2 / L2: Zoom") && ct.Contains("\nOPTIONS: Pause") && lay.Length == 0, "ui");
            }
            Shot("ui_pause_controls_playstation");
            root.Resume();
            yield return Wait(0.4f);
            var nx = InputSystem.AddDevice<SwitchProControllerHID>();
            nx.MakeCurrent();
            yield return Wait(0.8f);
            Check("a Switch Pro Controller shows Nintendo prompts (B on the bottom)", PadGlyphs.Family == PadFamily.Nintendo
                && PadGlyphs.Name(GamepadButton.South) == "B" && PadGlyphs.Name(GamepadButton.RightTrigger) == "ZR", "ui");
            Shot("ui_prompts_nintendo");
            root.Pause();
            yield return Wait(0.6f);
            {
                string ct = pause.ControlsPanel.Text;
                Check("with a Switch Pro Controller the controls panel uses Nintendo names (B sends the car)",
                      ct.StartsWith("NINTENDO CONTROLLER\n") && ct.Contains("\nB: Send") && ct.Contains("\nA: Unpick") && ct.Contains("\nX: Let everyone in")
                      && ct.Contains("\nZR / ZL: Zoom") && ct.Contains("\n+: Pause") && pause.ControlsPanel.Overflow.Length == 0, "ui");
            }
            root.Resume();
            yield return Wait(0.4f);
            InputSystem.RemoveDevice(ds);
            InputSystem.RemoveDevice(nx);
            Controls.ReleaseForcedPad();
            runner.InputEnabled = true;
            root.Pause();
            yield return Wait(0.6f);
            {
                string ct = pause.ControlsPanel.Text, lay = ControlsLayout(pause);
                Check($"with the mouse the controls panel lists mouse and keyboard{(lay.Length > 0 ? ":" + lay : "")}",
                      ct.StartsWith("MOUSE AND KEYBOARD\n") && ct.Contains("\nRIGHT-CLICK A RIDER: Let them off") && ct.Contains("\nSPACE: Let everyone in")
                      && ct.Contains("CLICK A FLOOR, 1-9") && lay.Length == 0, "ui");
            }
            Shot("ui_pause_controls_mouse");
            root.Resume();
            yield return Wait(0.4f);
            Check("a plain gamepad shows Xbox prompts", PadGlyphs.Family == PadFamily.Xbox && PadGlyphs.Name(GamepadButton.South) == "A", "ui");
            root.QuitShift();
            yield return Wait(1f);

            // reduced motion, switched on from the settings screen
            var settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            root.ShowSettings(null);
            yield return Wait(0.6f);
            UiToggle motion = null;
            foreach (var tg in settings.GetComponentsInChildren<UiToggle>(true)) if (tg.name.Contains("REDUCED")) motion = tg;
            if (motion != null) motion.Set(true);
            yield return Wait(0.6f);
            Shot("ui_settings_reduced_motion");
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Check("reduced motion toggles on, stills the camera and survives a save round trip",
                motion != null && save.ReducedMotion && back.ReducedMotion && root.Rig.Still && root.Rig.ShakeScale == 0f, "ui");
            settings.Hide();
            save.ReducedMotion = false;
            root.ApplySettings();

            // the guest guide from the roster, early in the week: only what the open shifts introduce, the rest locked
            for (int k = 0; k < save.Stars.Length; k++) { save.Stars[k] = 0; save.Plays[k] = 0; save.RelaxedClear[k] = false; }
            save.Stars[0] = 1; save.Stars[1] = 2;
            var rosterScreen = FindAnyObjectByType<RosterScreen>(FindObjectsInactive.Include);
            var guideScreen = FindAnyObjectByType<GuideScreen>(FindObjectsInactive.Include);
            root.ShowRoster();
            yield return Wait(0.8f);
            UiButton guideBtn = null;
            foreach (var b in rosterScreen.GetComponentsInChildren<UiButton>(true)) if (b.name == "Btn_GUEST GUIDE") guideBtn = b;
            Check($"every control on the roster takes a click at its centre{HitMisses()}", hitMisses == 0, "ui");
            guideBtn?.Click();
            yield return Wait(0.8f);
            int gExpect = Guide.OpenCount(Guide.Guests, save.Unlocked), bExpect = Guide.OpenCount(Guide.Building, save.Unlocked);
            Check($"the roster's GUEST GUIDE lists what Monday to Wednesday introduce: {guideScreen.GuestsOpen} guests (want {gExpect}), {guideScreen.BuildingOpen} cards (want {bExpect})",
                  guideBtn != null && guideScreen.Visible && guideScreen.GuestsOpen == gExpect && guideScreen.BuildingOpen == bExpect
                  && gExpect == 3 && bExpect == 5, "ui");
            Shot("ui_guide_wednesday");
            guideScreen.Back();
            yield return Wait(0.6f);
            Check("closing the guide returns to the roster", rosterScreen.Visible && !guideScreen.Visible, "ui");
            root.ShowTitle();
            yield return Wait(1f);
        }

        /// <summary>The pause card and its controls panel are on screen, apart, and every row fits ("" when all's well).</summary>
        static string ControlsLayout(PauseScreen pause)
        {
            var cam = GameRoot.Instance.UiCam;
            var panel = UiKit.ScreenRect(pause.ControlsPanel.Rt, cam);
            var card = UiKit.ScreenRect(pause.CardRect, cam);
            string bad = "";
            foreach (var (name, r) in new[] { ("panel", panel), ("card", card) })
                if (r.xMin < 0f || r.yMin < 0f || r.xMax > Screen.width || r.yMax > Screen.height) bad += $" {name} off screen ({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";
            if (panel.Overlaps(card)) bad += $" panel overlaps the card by {card.xMax - panel.xMin:0} px";
            return bad + pause.ControlsPanel.Overflow;
        }

        int hitMisses;

        /// <summary>
        /// Hit-tests every visible button, slider and toggle: a pointer at its centre must land on it (as Tools/uicheck.sh
        /// does in the editor). Sets <see cref="hitMisses"/> and returns "" or a list of the misses for the check line.
        /// </summary>
        string HitMisses()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var cam = GameRoot.Instance.UiCam;
            var targets = new List<MonoBehaviour>();
            targets.AddRange(FindObjectsByType<UiButton>(FindObjectsSortMode.None));
            targets.AddRange(FindObjectsByType<UiSlider>(FindObjectsSortMode.None));
            targets.AddRange(FindObjectsByType<UiToggle>(FindObjectsSortMode.None));
            var sb = new System.Text.StringBuilder();
            int ok = 0;
            hitMisses = 0;
            foreach (var b in targets)
            {
                if (!b.isActiveAndEnabled) continue;
                var cg = b.GetComponentInParent<CanvasGroup>();
                if (cg != null && (!cg.blocksRaycasts || cg.alpha < 0.5f)) continue;
                var rt = (RectTransform)b.transform;
                var sp = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
                var pe = new UnityEngine.EventSystems.PointerEventData(es) { position = sp };
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pe, hits);
                var top = hits.Count > 0 ? hits[0].gameObject : null;
                if (top != null && top.transform.IsChildOf(b.transform)) ok++;
                else { hitMisses++; sb.Append($" MISS {b.name} (hit {(top ? top.name : "none")})"); }
            }
            return $" ({ok} hit{(hitMisses > 0 ? "," + sb : "")})";
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
            Check($"every control on the pause card takes a click at its centre{HitMisses()}", hitMisses == 0);
            {
                string ct = pause.ControlsPanel.Text, lay = ControlsLayout(pause);
                Check($"the controls panel beside the pause card names the controller's buttons{(lay.Length > 0 ? ":" + lay : "")}",
                      ct.StartsWith("CONTROLLER\n") && ct.Contains("\nA: Send the car") && ct.Contains("LB / RB") && ct.Contains("\nRT / LT: Zoom")
                      && ct.Contains("\nSTART: Pause") && lay.Length == 0);
            }
            // the guest guide from the pause card: Start already put the ring on RESUME, one step down is GUEST GUIDE
            var guide = FindAnyObjectByType<GuideScreen>(FindObjectsInactive.Include);
            float tPaused = runner.Sim.Time;
            yield return Pad("down a");
            yield return Wait(0.6f);
            var sv = SaveData.Current;
            int gOpen = Guide.OpenCount(Guide.Guests, sv.Unlocked), bOpen = Guide.OpenCount(Guide.Building, sv.Unlocked);
            Check($"the pause card opens the guest guide with d-pad + A: {guide.GuestsOpen}/{Guide.Guests.Count} guests, {guide.BuildingOpen}/{Guide.Building.Count} cards, shift still paused",
                  guide.Visible && !pause.Visible && runner.Paused && runner.Sim.Time == tPaused && guide.GuestsOpen == gOpen && guide.BuildingOpen == bOpen);
            Check($"every control on the guide takes a click at its centre{HitMisses()}", hitMisses == 0);
            Shot("pad_guide");
            yield return Pad("b");
            Check("B closes the guide back to the pause card", pause.Visible && !guide.Visible && runner.Paused);
            yield return Pad("start");
            Check("Start resumes", !pause.Visible && !runner.Paused);
            yield return Pad("hold:lt hold:lt");
            Check("LT zooms back out", runner.Rig.Zoom < 0.05f);

            // restart and quit ask for a second press: "down down A" from RESUME only arms RESTART SHIFT
            var before = runner.Sim;
            yield return Pad("start");
            float tArm = runner.Sim.Time;
            yield return Pad("down down a");
            Check($"down, down, A on the pause card arms RESTART SHIFT ('{pause.RestartButton.Label.text}', '{pause.Hint}') and the shift stays paused",
                  pause.Visible && runner.Paused && runner.Sim == before && runner.Sim.Time == tArm && pause.Armed == pause.RestartButton
                  && pause.RestartButton.Label.text == "REALLY RESTART?" && pause.Hint.Contains("AGAIN"));
            Shot("pad_pause_restart_armed");
            yield return Pad("up");
            Check("moving the focus ring off it calls the restart off", pause.Armed == null && pause.RestartButton.Label.text == "RESTART SHIFT" && runner.Sim == before);
            yield return Pad("down a a");
            yield return Wait(0.6f);
            Check($"a second A restarts the shift (fresh run at {runner.Sim.Time:0.0} s)", runner.Sim != before && !pause.Visible && !runner.Paused && runner.Sim.Time < 2f);
            // the mouse: one click arms QUIT TO ROSTER, hovering another button disarms it, two clicks quit
            root.Pause();
            yield return Wait(0.6f);
            var roster2 = FindAnyObjectByType<RosterScreen>(FindObjectsInactive.Include);
            pause.QuitButton.Click();
            yield return null;
            bool armedQuit = pause.Armed == pause.QuitButton && pause.QuitButton.Label.text == "REALLY QUIT?" && pause.Visible;
            Shot("pad_pause_quit_armed");
            UiButton resumeBtn = null;
            foreach (var b in pause.GetComponentsInChildren<UiButton>()) if (b.name == "Btn_RESUME") resumeBtn = b;
            resumeBtn.OnPointerEnter(null);
            yield return null;
            yield return null;
            bool disarmed = pause.Armed == null && pause.QuitButton.Label.text == "QUIT TO ROSTER";
            resumeBtn.OnPointerExit(null);
            pause.QuitButton.Click();
            yield return null;
            Check($"one click arms QUIT TO ROSTER ({armedQuit}), pointing at RESUME disarms it ({disarmed}), and the shift is still on", armedQuit && disarmed && pause.Visible && !roster2.Visible);
            pause.QuitButton.Click();
            yield return Wait(0.8f);
            Check("two clicks on QUIT TO ROSTER quit to the roster", roster2.Visible && !pause.Visible);
            yield return Wait(0.4f);
        }

        /// <summary>The building's order and the first guests: what two runs of the same daily share.</summary>
        static string Opening(ShiftSim sim)
        {
            var sb = new System.Text.StringBuilder(string.Join(",", sim.B.Slots));
            for (int i = 0; i < Mathf.Min(6, sim.All.Count); i++) sb.Append($" {sim.All[i].Kind}:{sim.All[i].Origin}>{sim.All[i].Dest}");
            return sb.ToString();
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}

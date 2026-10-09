using OneMoreFloor.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>Bootstrap: the Main scene only contains this. Everything else is built at runtime.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public const int UiLayer = 5;

        public Camera WorldCam { get; private set; }
        public Camera UiCam { get; private set; }
        public Canvas Canvas { get; private set; }
        public ShiftRunner Runner { get; private set; }
        public CameraRig Rig { get; private set; }
        public BuildingView Building { get; private set; }
        public Light Sun { get; private set; }
        public Sky Sky { get; private set; }
        public Volume Post { get; private set; }
        public AudioDirector Audio { get; private set; }

        void Awake()
        {
            Instance = this;
            var args = System.Environment.GetCommandLineArgs();
            SaveData.Ephemeral = System.Array.IndexOf(args, "-omfAutopilot") >= 0 || System.Array.IndexOf(args, "-omfEphemeral") >= 0
                                 || System.Array.IndexOf(args, "-omfDemo") >= 0 || System.Array.IndexOf(args, "-omfTrailer") >= 0
                                 || System.Array.IndexOf(args, "-omfFidelityShots") >= 0 || System.Array.IndexOf(args, "-omfSkyShots") >= 0
                                 || (Application.isEditor && Application.isBatchMode);   // headless editor captures
            // Pace frames ourselves: on Wayland a vsync'd swap blocks on compositor frame callbacks,
            // which some compositors throttle hard for unfocused windows. Compositors don't tear.
            QualitySettings.vSyncCount = 0;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = Mathf.Clamp((int)System.Math.Round(hz > 1 ? hz : 60), 60, 240);
            // in a browser the page's animation frames pace the game at the display's rate (a set rate uses timers instead)
            if (Web.IsWeb) Application.targetFrameRate = -1;
            // a page can only go fullscreen when the player asks, so every visit starts in the window
            if (Web.IsWeb) SaveData.Current.Fullscreen = false;
            // the last visit in this tab stopped without the page closing (a phone that ran out of memory): start light
            if (Web.IsWeb && Web.Lite) SaveData.Current.Fidelity = GraphicsQuality.Low;
            // recordings and self-tests run unattended, often without focus
            AutoPause = System.Array.IndexOf(args, "-omfAutopilot") < 0 && System.Array.IndexOf(args, "-omfDemo") < 0
                        && System.Array.IndexOf(args, "-omfTrailer") < 0 && !(Application.isEditor && Application.isBatchMode);
            int gq = System.Array.IndexOf(args, "-omfFidelity");
            if (gq >= 0 && gq + 1 < args.Length && int.TryParse(args[gq + 1], out int level)) GraphicsOverride = Mathf.Clamp(level, GraphicsQuality.Low, GraphicsQuality.Ultra);
            InputSystem.onDeviceChange += OnDeviceChange;
            Controls.Ensure(gameObject);
            BuildWorld();
            BuildUi();
            gameObject.AddComponent<UiNav>();
            WebBridge.Create(this);
        }

        TitleScreen title;
        RosterScreen roster;
        IntroScreen intro;
        PauseScreen pause;
        SettingsScreen settings;
        GuideScreen guide;
        ResultsScreen results;
        EndingScreen ending;
        int currentShift;
        ulong runSeed = 1;
        bool endingPending;
        public bool InShift { get; private set; }
        /// <summary>When set, every shift uses this seed instead of the clock (reproducible recordings).</summary>
        public ulong FixedSeed { get; set; }

        void Start()
        {
            ApplySettings();
            AutoPilot.TryStart(this);
            PerfProbe.TryStart(this);
            FidelityShots.TryStart(this);
            DemoReel.TryStart(this);
            TrailerReel.TryStart(this);
            int direct = StartIndexFromArgs();
            if (direct >= 0) BeginShift(direct);
            else ShowTitle();
        }

        static int StartIndexFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-omfShift" && int.TryParse(args[i + 1], out int s)) return Mathf.Clamp(s, 0, ShiftCatalog.All.Count - 1);
            return -1;
        }

        void HideAll()
        {
            foreach (var sc in new UiScreen[] { title, roster, intro, pause, settings, guide, results, ending })
                if (sc != null && sc.Visible) sc.Hide();
        }

        /// <summary>Title: the tower runs itself at dusk behind the logo.</summary>
        public void ShowTitle()
        {
            HideAll();
            StartAttract();
            title.Show();
            Audio.PlayTitle();
            Audio.MuffleTitle(false);
        }

        void StartAttract()
        {
            InShift = false;
            var def = ShiftCatalog.Get(3);
            Sky.Apply("dusk", Sun, WorldCam);
            Rig.ScreenX = 0.68f;
            Rig.AllowZoom = false;
            Runner.Attract = true;
            Runner.Begin(def, (ulong)Random.Range(1, 100000));
            Runner.AutoBot = Bot.Strong((ulong)Random.Range(1, 1000));
            Runner.Hud.SetVisible(false);
            Audio.StopGameplayMusic();
        }

        /// <summary>The first Graveyard clear plays the ending on the way out of the results, whichever
        /// button the player picks next (its Continue leads to the roster).</summary>
        bool PlayPendingEnding()
        {
            if (!endingPending) return false;
            endingPending = false;
            if (InShift || !Runner.Attract) { StartAttract(); Audio.PlayTitle(); }
            HideAll();
            ending.Show();
            Audio.Sting("sting_finale", 0.2f, 0.9f);
            return true;
        }

        public void ShowRoster()
        {
            if (PlayPendingEnding()) return;
            if (InShift || !Runner.Attract) { StartAttract(); Audio.PlayTitle(); }
            HideAll();
            roster.Show();
            Audio.MuffleTitle(false);
        }

        public void ShowIntro(int index)
        {
            if (index < 0 || index >= ShiftCatalog.All.Count) { ShowRoster(); return; }
            if (!SaveData.Current.Unlocked(index)) { ShowRoster(); return; }
            if (PlayPendingEnding()) return;
            if (InShift || !Runner.Attract) { StartAttract(); Audio.PlayTitle(); }
            HideAll();
            intro.Setup(index);
            intro.Show();
            Audio.MuffleTitle(true);
        }

        public void ShowSettings(UiScreen from) => settings.Open(from);

        /// <summary>Into or out of a shift: the screen doors close, <paramref name="change"/> runs behind them, and they open.
        /// The shut doors name where they're going (the hotel, unless a shift is given).</summary>
        public void Doors(System.Action change, int shift = -1, bool daily = false)
        {
            if (daily) ScreenDoors.Run(change, "Today's Shift · " + DateLabel(Today), "Daily Overtime");
            else if (shift >= 0 && shift < ShiftCatalog.All.Count) ScreenDoors.Run(change, ShiftCatalog.Get(shift).Day, ShiftCatalog.Get(shift).Title);
            else ScreenDoors.Run(change, "Back to the lobby");
        }

        /// <summary>ONE MORE SHIFT / RESTART: the doors name the shift being played again.</summary>
        public void DoorsRestart() => Doors(RestartShift, DailyRun ? -1 : currentShift, DailyRun);

        public void ShowGuide(UiScreen from) => guide.Open(from);

        public void BeginShift(int index)
        {
            HideAll();
            currentShift = index;
            DailyRun = false;
            runSeed = FixedSeed != 0 ? FixedSeed : (ulong)System.DateTime.Now.Ticks;
            InShift = true;
            shiftStartedAt = Time.realtimeSinceStartup;
            var def = ShiftCatalog.Get(index);
            Sky.Apply(def.Lighting, Sun, WorldCam);
            Rig.ScreenX = 0.505f;
            Rig.AllowZoom = true;
            Rig.Zoom = SaveData.Current.Zoom;
            Runner.Attract = false;
            Runner.AutoBot = null;
            Runner.Begin(def, runSeed, SaveData.Current.Relaxed && !def.Endless);
            // once the stars are lit, the track chases the best (relaxed runs don't save one)
            Runner.Hud.BestToBeat = Runner.Sim.Relaxed ? 0 : SaveData.Current.Best[index];
            Runner.Hud.SetVisible(true);
            Runner.Ended = OnShiftEnded;
            Audio.PlayGameplay(def);
            Audio.Sting("sting_start", 0.5f, 0.8f);
            Coach?.BeginShift(def);
        }

        public void RestartShift()
        {
            if (InShift && Runner.Sim != null && !Runner.Sim.Ended) Runner.Log.Finish("restarted");
            if (DailyRun) BeginDaily();
            else BeginShift(currentShift);
        }

        /// <summary>This run is today's daily Overtime.</summary>
        public bool DailyRun { get; private set; }
        /// <summary>Automation: pretend today is this date (null = the real local date).</summary>
        public System.DateTime? DailyDateOverride;
        public System.DateTime Today => (DailyDateOverride ?? System.DateTime.Now).Date;
        public static string DateKey(System.DateTime d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        public static string DateLabel(System.DateTime d) => d.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);

        public static int OvertimeIndex
        {
            get { for (int i = 0; i < ShiftCatalog.All.Count; i++) if (ShiftCatalog.Get(i).Endless) return i; return -1; }
        }

        /// <summary>Today's Overtime: everyone playing on this date gets the same seed.</summary>
        public void BeginDaily()
        {
            int ot = OvertimeIndex;
            if (ot < 0 || !SaveData.Current.Unlocked(ot)) { ShowRoster(); return; }
            var d = Today;
            StartShift(ot, Progress.DailySeed(d.Year, d.Month, d.Day));
            DailyRun = true;
            Runner.Hud.SetShiftName("Today's Shift · " + DateLabel(d), "Daily Overtime");
            Runner.Hud.BestToBeat = SaveData.Current.DailyBestOn(DateKey(d));
        }

        /// <summary>Remember the player's zoom level for the next shift.</summary>
        void SaveZoom()
        {
            var save = SaveData.Current;
            if (Mathf.Abs(save.Zoom - Rig.Zoom) < 0.01f) return;
            save.Zoom = Rig.Zoom;
            save.Save();
        }

        public void Pause() => Pause("");

        void Pause(string reason)
        {
            if (!InShift || Runner.Sim == null || Runner.Sim.Ended || pause.Visible || settings.Visible || guide.Visible) return;   // already paused underneath
            Runner.Paused = true;
            Runner.Holding = 0f;
            pause.Reason = reason;
            pause.Show();
            SaveZoom();
        }

        /// <summary>
        /// Pause a running shift when the window loses focus or the controller being played with goes away, so
        /// patience never drains while the player can't act. Recordings and the autopilot turn it off.
        /// </summary>
        public bool AutoPause { get; set; }

        /// <summary>Frames per second at most while the window is in the background (the shift has paused itself then).</summary>
        public const int BackgroundRate = 30;
        int foregroundRate = int.MinValue;

        /// <summary>The window doesn't have focus (as last reported by <see cref="OnApplicationFocus"/>).</summary>
        public bool InBackground { get; private set; }

        void OnApplicationFocus(bool focus)
        {
            InBackground = !focus;
            Debug.Log(focus ? "[Focus] the window has focus" : "[Focus] the window lost focus");
            if (!focus) AutoPauseFor("Paused while you were away");
            UpdateBackgroundSound();
            // nothing that matters moves while the window is in the background, so don't keep the GPU (and a laptop's
            // fans) busy at the display's full rate. Unattended runs (recordings, the autopilot) keep their rate.
            if (!focus && AutoPause && foregroundRate == int.MinValue)
            {
                foregroundRate = Application.targetFrameRate;
                Application.targetFrameRate = foregroundRate > 0 ? Mathf.Min(foregroundRate, BackgroundRate) : BackgroundRate;
            }
            else if (focus && foregroundRate != int.MinValue)
            {
                Application.targetFrameRate = foregroundRate;
                foregroundRate = int.MinValue;
            }
        }

        /// <summary>MUTE IN BACKGROUND: quiet while the window is in the background. Unattended runs (recordings, the
        /// autopilot) keep their sound, as they keep their frame rate.</summary>
        void UpdateBackgroundSound()
        {
            if (Audio != null) Audio.Muted = InBackground && AutoPause && SaveData.Current.MuteInBackground;
        }

        void OnDestroy() => InputSystem.onDeviceChange -= OnDeviceChange;

        float shiftStartedAt;

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad) || (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected)) return;
            // the pad being played with: the last one pressed, if it's still the active input or was used this shift
            // (a nudged mouse clears Controls.Pad, but the player is still holding the controller)
            if (device == Controls.LastPadDevice && (Controls.Pad || Controls.LastPadUse >= shiftStartedAt))
                AutoPauseFor("Controller disconnected");
        }

        public void AutoPauseFor(string reason)
        {
            if (!AutoPause || Runner == null || Runner.Attract || results.Visible) return;
            Pause(reason);
        }

        public void Resume()
        {
            pause.Hide();
            if (settings.Visible) settings.Hide();
            if (guide.Visible) guide.Hide();
            Runner.Paused = false;
            // a beat to find your place before the clock runs again
            if (InShift && Runner.Sim != null && !Runner.Sim.Ended && !Runner.Attract) Runner.Holding = ShiftRunner.ResumeCount;
        }

        public void QuitShift()
        {
            if (InShift && Runner.Sim != null && !Runner.Sim.Ended) Runner.Log.Finish("quit");
            Runner.Paused = false;
            ShowRoster();
        }

        void OnShiftEnded(ShiftSim sim)
        {
            if (!InShift || Runner.Attract) return;
            var save = SaveData.Current;
            save.Zoom = Rig.Zoom;
            bool firstGraveyard = sim.Def.Id == "graveyard" && sim.StarCount >= 1 && !save.EndingSeen;  // a relaxed clear counts
            int nextIdx = sim.Def.Index + 1;
            bool nextWasOpen = save.Unlocked(nextIdx);
            bool best = save.Record(sim.Def.Index, sim.Score, sim.StarCount, sim.Relaxed, sim.Fired);
            bool latePass = !nextWasOpen && nextIdx < ShiftCatalog.All.Count && save.LatePassed(nextIdx);
            bool relaxedOpened = !nextWasOpen && !latePass && sim.Relaxed && nextIdx < ShiftCatalog.All.Count && save.Unlocked(nextIdx);
            bool dailyBest = DailyRun && save.RecordDaily(DateKey(Today), sim.Score);
            Runner.Log.Finish("finished");
            if (firstGraveyard)
            {
                save.EndingSeen = true;
                save.Save();
                endingPending = true;
            }
            results.Setup(sim, best && sim.Score > 0, latePass, relaxedOpened);
            if (DailyRun) results.SetupDaily(DateKey(Today), DateLabel(Today), dailyBest && sim.Score > 0);
            results.Show();
            Audio.Sting("sting_clockout", 0.25f, sim.Fired ? 0.5f : 0.9f);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ApplySettings()
        {
            var save = SaveData.Current;
            if (Audio != null)
            {
                Audio.MasterVolume = save.Master;
                Audio.MusicVolume = save.Music;
                Audio.SfxVolume = save.Sfx;
                UpdateBackgroundSound();
            }
            Rig.ShakeScale = save.ScreenShake && !save.ReducedMotion ? 1f : 0f;
            Rig.Still = save.ReducedMotion;
            TextFloor.Large = save.LargeText;
            if (Runner != null && Runner.Hud != null) Runner.Hud.Panel.ShowForecast(save.ShowForecast);
            GraphicsQuality.Apply(GraphicsOverride >= 0 ? GraphicsOverride : save.Fidelity, Sun, Post, WorldCam);
            if (Web.IsWeb)
            {
                // the page fills the browser window; FULLSCREEN asks the browser for the whole screen
                if (save.Fullscreen != Web.IsFullscreen)
                {
                    Web.RequestFullscreen(save.Fullscreen);
                    fullscreenAskedAt = Time.unscaledTime;
                }
            }
            else if (!Application.isEditor)
            {
                var mode = save.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                int dw = Display.main.systemWidth, dh = Display.main.systemHeight;
                if (Screen.fullScreenMode != mode)
                {
                    if (save.Fullscreen) Screen.SetResolution(dw, dh, mode);
                    else { var w = WindowSize(dw, dh); Screen.SetResolution(w.x, w.y, mode); }
                }
                else if (!save.Fullscreen && dw > 0 && (Screen.width > dw * 0.95f || Screen.height > dh * 0.95f))
                {
                    // a window that doesn't fit the display (a small laptop screen, or a size remembered from a bigger one)
                    var w = WindowSize(dw, dh);
                    Screen.SetResolution(w.x, w.y, mode);
                }
            }
        }

        float fullscreenAskedAt = -10f, reportedAt = -10f;

        /// <summary>In a browser, twice a second: what's on screen, for the page and its checks (Web.Status).</summary>
        void ReportToPage()
        {
            if (!Web.IsWeb || Time.unscaledTime - reportedAt < 0.5f) return;
            reportedAt = Time.unscaledTime;
            string screen = title.Visible ? "title" : roster.Visible ? "roster" : intro.Visible ? "intro" : settings.Visible ? "settings"
                          : guide.Visible ? "guide" : pause.Visible ? "pause" : results.Visible ? "results" : ending.Visible ? "ending"
                          : InShift ? "shift" : "other";
            var sim = InShift ? Runner.Sim : null;
            var save = SaveData.Current;
            Web.Status("{\"screen\":\"" + screen + "\",\"score\":" + (sim != null ? sim.Score : 0) + ",\"delivered\":" + (sim != null ? sim.DeliveredCount : 0)
                       + ",\"time\":" + (sim != null ? sim.Time : 0f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                       + ",\"fidelity\":" + GraphicsQuality.Applied + ",\"fullscreen\":" + (save.Fullscreen ? "true" : "false")
                       + ",\"largeText\":" + (save.LargeText ? "true" : "false")
                       // touch play, for Tools/check-mobile.mjs: riders aboard, the car's floor and doors, what a tap picked
                       + ",\"touch\":" + (Controls.Touch ? "true" : "false") + ",\"riders\":" + (sim != null ? sim.Car.Riders.Count : 0)
                       + ",\"car\":" + (sim != null ? sim.Car.Pos : 0f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                       + ",\"open\":" + (sim != null && sim.Car.IsOpen ? "true" : "false")
                       + ",\"picked\":" + (InShift ? Runner.TouchPid : -1) + ",\"pickedFloor\":" + (InShift && Runner.TouchFloor.HasValue ? (int)Runner.TouchFloor.Value : -1)
                       + ",\"zoom\":" + Rig.Zoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                       + (sim != null && ShiftRunner.TouchTest ? WhereThingsAre(sim) : "") + "}");
        }

        /// <summary>
        /// For the touch checks (-omfTouchTest only, to keep the report small): where each guest and floor is on the screen (canvas pixels from the bottom left), so a test
        /// can tap them. Guests as [id, riding, x, y]; floors as [slot, floor, x, y], a point on the floor clear of its queue.
        /// </summary>
        string WhereThingsAre(Core.ShiftSim sim)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder(",\"guests\":[");
            bool first = true;
            void Add(int a, int b, Vector3 world)
            {
                var p = WorldCam.WorldToScreenPoint(world);
                // behind the camera, or not a number (a view mid-transition): left out, so the JSON stays valid
                if (!(p.z > 0f) || float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) return;
                sb.Append(first ? "" : ",").Append('[').Append(a).Append(',').Append(b).Append(',')
                  .Append(p.x.ToString("0", ci)).Append(',').Append(p.y.ToString("0", ci)).Append(']');
                first = false;
            }
            for (int f = 0; f < sim.Waiting.Length; f++)
                foreach (var g in sim.Waiting[f]) { var at = Runner.GuestPoint(g.Id); if (at.HasValue) Add(g.Id, 0, at.Value); }
            foreach (var g in sim.Car.Riders) { var at = Runner.GuestPoint(g.Id); if (at.HasValue) Add(g.Id, 1, at.Value); }
            sb.Append("],\"floors\":[");
            first = true;
            for (int slot = 0; slot < sim.B.Count; slot++)
            {
                var fv = Building[sim.B.At(slot)];
                if (fv) Add(slot, (int)fv.Id, fv.transform.position + new Vector3(-5f, Layout.SlotHeight * 0.7f, Layout.FrontZ));
            }
            sb.Append("],\"here\":").Append(sim.Car.IsOpen ? sim.Waiting[(int)sim.DockedFloor].Count : 0);
            return sb.ToString();
        }

        /// <summary>In a browser, Esc or the browser's own controls can leave fullscreen: FULLSCREEN follows the page.</summary>
        void FollowPageFullscreen()
        {
            if (!Web.IsWeb || Time.unscaledTime - fullscreenAskedAt < 1.5f) return;   // the browser takes a moment to switch
            bool on = Web.IsFullscreen;
            if (SaveData.Current.Fullscreen == on) return;
            SaveData.Current.Fullscreen = on;
            settings.SyncFromSave();
        }

        /// <summary>F11 / Alt+Enter: flip the FULLSCREEN setting itself, so Settings shows it and the next launch keeps it.</summary>
        public void ToggleFullscreen()
        {
            var save = SaveData.Current;
            save.Fullscreen = !save.Fullscreen;
            ApplySettings();
            save.Save();
            settings.SyncFromSave();
        }

        /// <summary>Automation (-omfFidelity N, 0 LOW to 3 ULTRA): play at this GRAPHICS FIDELITY without touching the save (-1 = the save's).</summary>
        public static int GraphicsOverride = -1;

        /// <summary>The windowed size: 1600x900, shrunk to 90% of the display when that's smaller, always 16:9.</summary>
        public static Vector2Int WindowSize(int displayW, int displayH)
        {
            int w = Mathf.Min(1600, Mathf.FloorToInt(displayW * 0.9f));
            int h = Mathf.RoundToInt(w * 9f / 16f);
            if (h > displayH * 0.9f)
            {
                h = Mathf.FloorToInt(displayH * 0.9f);
                w = Mathf.RoundToInt(h * 16f / 9f);
            }
            return new Vector2Int(w, h);
        }

        public Coach Coach { get; private set; }

        void BuildWorld()
        {
            Audio = AudioDirector.Create(transform);
            var camGo = new GameObject("WorldCamera");
            camGo.tag = "MainCamera";
            WorldCam = camGo.AddComponent<Camera>();
            WorldCam.fieldOfView = 24f;
            WorldCam.nearClipPlane = 1f;
            WorldCam.farClipPlane = 600f;
            WorldCam.cullingMask = ~(1 << UiLayer);
            WorldCam.clearFlags = CameraClearFlags.SolidColor;
            WorldCam.backgroundColor = Palette.Hex(0x23324F);
            var wdata = WorldCam.GetUniversalAdditionalCameraData();
            wdata.renderPostProcessing = true;
            wdata.antialiasing = AntialiasingMode.None;
            Rig = CameraRig.Create(WorldCam);

            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.75f;
            Sun.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            Sky = Sky.Create(transform);
            Fx.Create(transform);

            var volGo = new GameObject("Post");
            Post = volGo.AddComponent<Volume>();
            Post.isGlobal = true;
            Post.priority = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.15f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);
            Post.profile = profile;

            Building = BuildingView.Create(transform);
            var car = CarView.Create(Building.transform);

            var runnerGo = new GameObject("Runner");
            runnerGo.transform.SetParent(transform, false);
            Runner = runnerGo.AddComponent<ShiftRunner>();
            Runner.Building = Building;
            Runner.Car = car;
            Runner.Rig = Rig;
            Runner.Cam = WorldCam;
        }

        void BuildUi()
        {
            var uiGo = new GameObject("UiCamera");
            UiCam = uiGo.AddComponent<Camera>();
            UiCam.orthographic = true;
            UiCam.cullingMask = 1 << UiLayer;
            UiCam.clearFlags = CameraClearFlags.Depth;
            UiCam.nearClipPlane = 0.1f;
            UiCam.farClipPlane = 50f;
            uiGo.transform.position = new Vector3(0, -1000, 0);
            var udata = UiCam.GetUniversalAdditionalCameraData();
            udata.renderType = CameraRenderType.Overlay;
            udata.renderPostProcessing = false;
            WorldCam.GetUniversalAdditionalCameraData().cameraStack.Add(UiCam);

            var canvasGo = new GameObject("Canvas");
            canvasGo.layer = UiLayer;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = UiCam;
            Canvas.planeDistance = 10f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = TextFloor.Reference;
            scaler.matchWidthOrHeight = TextFloor.Match;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TextFloor>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            Runner.Hud = Hud.Create(Canvas.transform, Runner, WorldCam);
            Coach = Coach.Create(Canvas.transform, Runner);
            title = TitleScreen.Create(Canvas.transform, this);
            roster = RosterScreen.Create(Canvas.transform, this);
            intro = IntroScreen.Create(Canvas.transform, this);
            pause = PauseScreen.Create(Canvas.transform, this);
            settings = SettingsScreen.Create(Canvas.transform, this);
            guide = GuideScreen.Create(Canvas.transform, this);
            results = ResultsScreen.Create(Canvas.transform, this);
            ending = EndingScreen.Create(Canvas.transform, this);
            ScreenDoors.Create(Canvas.transform);
            SetLayerRecursive(canvasGo, UiLayer);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        /// <summary>Developer/automation entry: start any shift directly.</summary>
        public void StartShift(int index, ulong seed)
        {
            var prev = FixedSeed;
            FixedSeed = seed;
            BeginShift(index);
            FixedSeed = prev;
        }

        void LateUpdate() => GraphicsQuality.Focus(Post, WorldCam, Building.transform.position.z);

        void Update()
        {
            var kb = Keyboard.current;
            bool pausePressed = Controls.PausePressed || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame));
            if (ScreenDoors.Busy) pausePressed = false;   // the doors are taking you somewhere
            if (InShift && pausePressed && !pause.Visible && !settings.Visible && !guide.Visible && !results.Visible)
                Pause();
            else if (InShift && Controls.PausePressed && pause.Visible && !settings.Visible && !guide.Visible)
                Resume();
            if (Controls.FullscreenPressed) ToggleFullscreen();
            FollowPageFullscreen();
            ReportToPage();
            if (kb == null) return;
            // developer shortcuts: F1..F10 start a shift
            for (int i = 0; i < 10; i++)
            {
                var k = kb[Key.F1 + i];
                if (k != null && k.wasPressedThisFrame && (Debug.isDebugBuild || Application.isEditor)) BeginShift(i);
            }
            if (kb.f12Key.wasPressedThisFrame)
                Shots.Capture(System.IO.Path.Combine(Application.persistentDataPath, $"shot_{System.DateTime.Now:HHmmss}.png"));
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace OneMoreFloor
{
    /// <summary>
    /// Records the footage for the feature trailer (assembled by Tools/make_trailer.sh) and the README stills.
    /// Every shot is a short, scripted clip: a shift started from a fixed seed, skipped ahead to a moment that
    /// <see cref="Probe"/> found by playing the same seed headlessly, then recorded with an animated caption in
    /// the game's Art Deco style. Launch with -omfTrailer &lt;outdir&gt; -omfTrailerPass video|audio
    /// [-omfTrailerOnly a,b,c] [-omfFidelity 0-3] (the GRAPHICS FIDELITY step to record at; the throwaway save's
    /// setting is set to it, so the settings card shows it too).
    ///
    /// The video pass renders offline at a locked 30 fps into outdir/shots/&lt;name&gt;.mp4 (and outdir/stills/*.png).
    /// The audio pass plays the same script in real time under -omfRecordAudio with the music muted (the
    /// trailer's music bed is mixed from the game's stems afterwards) and logs where each shot sits in the
    /// recording (outdir/audio_shots.txt). Each shot starts paused on the same simulation tick in both passes,
    /// and scripted commands run inside the fixed step (<see cref="ShiftRunner.PreStep"/>), so the passes match.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class TrailerReel : MonoBehaviour
    {
        const int Fps = 30, W = 1920, H = 1080;
        const float Settle = 1.6f;

        string outDir;
        bool video;
        int fidelity;
        HashSet<string> only;
        GameRoot root;
        ShiftRunner Runner => root.Runner;

        public static void TryStart(GameRoot root)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-omfTrailer");
            if (i < 0 || i + 1 >= args.Length) return;
            int p = Array.IndexOf(args, "-omfTrailerPass");
            int o = Array.IndexOf(args, "-omfTrailerOnly");
            var reel = root.gameObject.AddComponent<TrailerReel>();
            reel.root = root;
            reel.outDir = args[i + 1];
            reel.video = p < 0 || p + 1 >= args.Length || args[p + 1] != "audio";
            if (o >= 0 && o + 1 < args.Length) reel.only = new HashSet<string>(args[o + 1].Split(','));
            Directory.CreateDirectory(Path.Combine(reel.outDir, "shots"));
            Directory.CreateDirectory(Path.Combine(reel.outDir, "stills"));
            UnityEngine.Random.InitState(1929);
            Application.runInBackground = true;
            if (reel.video)
            {
                Time.captureFramerate = Fps;
                AudioListener.volume = 0f;
            }
        }

        void Start()
        {
            var save = SaveData.Current;
            if (GameRoot.GraphicsOverride >= 0)
            {
                // record at this step as a player would have it: the setting itself (the save is throwaway)
                save.Fidelity = GameRoot.GraphicsOverride;
                GameRoot.GraphicsOverride = -1;
            }
            save.Music = 0f;
            save.ScreenShake = true;
            save.ShowForecast = true;
            root.ApplySettings();
            fidelity = save.Fidelity;
            // the settings card was built from the save before this ran: show the step being recorded
            foreach (var c in FindObjectsByType<UiChoice>(FindObjectsInactive.Include))
                if (c.Notched && c.Options == GraphicsQuality.Names) c.Set(fidelity, false);
            Debug.Log($"[Trailer] GRAPHICS FIDELITY {GraphicsQuality.Names[GraphicsQuality.Applied]}, capture MSAA {Msaa}x, {W}x{H} at {Fps} fps");
            Runner.InputEnabled = false;
            var module = EventSystem.current ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            if (module) module.enabled = false;
            if (root.Coach) root.Coach.gameObject.SetActive(false);
            BuildOverlay();
            Runner.BotActed += OnBotActed;
            StartCoroutine(Script());
        }

        void OnDestroy()
        {
            if (root && root.Runner) root.Runner.BotActed -= OnBotActed;
            Controls.ReleaseForcedPad();
        }

        bool Want(string name) => only == null || only.Contains(name);

        /// <summary>The running pipeline's antialiasing (it follows GRAPHICS FIDELITY), used for the offscreen capture too.</summary>
        static int Msaa => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a ? a.msaaSampleCount : 4;

        // ================================================================ the script

        static Sprite Bellhop => Icons.Get("kind_bellhop");
        static readonly Func<ulong, Bot> Strong = s => Bot.Strong(s);
        static readonly Func<ulong, Bot> Decent = s => Bot.Decent(s);
        static readonly Func<ulong, Bot> Sloppy = s => Bot.Sloppy(s);
        static readonly Func<ulong, Bot> Novice = s => Bot.Novice(s);

        static bool Delivered(SimEvent e, DeliveryFlags f) =>
            e.Type == Ev.Delivered && (((DeliveryFlags)int.Parse(e.Text ?? "0")) & f) != 0;

        IEnumerator Script()
        {
            for (float t = 0f; t < 1.5f; t += UiTime.Dt) yield return null;
            SetProgress(6);

            // ---- cold open: Saturday's trailer moment, the doors open onto the ocean
            if (Want("cold_ocean"))
            {
                // hold at the ocean so "Lobby, please." lands before anyone boards (the moment runs in slow motion)
                const int shift = 5; const ulong seed = 21;
                Func<Plan> plan = () => new Plan().Delay(0.9f).Board(Kind.Commuter).Delay(0.6f).Send(FloorId.Library).Docked().Delay(2.1f)
                    .Board(Kind.Swimmer).Delay(0.45f).Board(Kind.Swimmer).Delay(0.7f).Send(FloorId.Lobby);
                float beat = Probe(shift, seed, Strong, plan, (s, e) => e.Type == Ev.Beat && e.Text == "saturday:ocean");
                yield return Prepare(shift, seed, Strong, plan, 0f, 0.55f);
                cursorOn = true;
                yield return Record("cold_ocean", beat + 9.0f, StillAt(beat + 2.3f, "ocean_moment"));
            }

            // ---- title card over the tower at dusk (and the real title screen as a still)
            if (Want("title"))
            {
                root.ShowTitle();
                Runner.PreStep = null;
                yield return Wait(2.6f);
                Still("title");
                HideScreen<TitleScreen>();
                yield return Wait(1.0f);
                yield return Record("title", 6.5f, TitleCard());
            }

            // ---- the core loop: board, send, shuffle (Monday)
            if (Want("core"))
            {
                Func<Plan> plan = () => new Plan().Delay(0.9f).Board(Kind.Commuter).Delay(0.7f).Send(FloorId.Office);
                yield return Prepare(0, 7, Strong, plan, 0f, 0f);
                cursorOn = true;
                yield return Record("core", 10f,
                    Caption(0.35f, 3.0f, "RUN THE ELEVATOR", "Let guests in, pick a floor, and the car goes. Fast service means bigger tips.", Icons.Kind(Kind.Commuter)),
                    Caption(3.55f, 6.1f, "EVERY STOP, THE HOTEL SHUFFLES", "Floors swap places whenever the car stops. The panel and floor tags show what's next.", Icons.Floor(FloorId.Library)),
                    StillAt(3.25f, "core_shuffle"));
            }

            // ---- bigger shuffle cards: rise, sink, roll
            if (Want("cards"))
            {
                var cards = new (int Shift, CardType Type, string Word)[] { (2, CardType.Rise, "RISE"), (2, CardType.Sink, "SINK"), (3, CardType.Roll, "ROLL") };
                for (int k = 0; k < cards.Length; k++)
                {
                    var c = cards[k];
                    var (seed, at) = Find(c.Shift, 31, Strong, null, (s, e) => e.Type == Ev.Shuffled && e.Card.Type == c.Type && s.Time > 6f);
                    if (at < 0f) continue;
                    yield return Prepare(c.Shift, seed, Strong, null, at - 0.9f, 0f);
                    yield return Record("cards_" + c.Word.ToLowerInvariant(), 2.6f,
                        Caption(0f, 2.6f, "THE WEEK GETS WILDER", "New cards make floors rise to the top, sink to the bottom, or roll.", Icons.Floor(FloorId.Penthouse),
                            fadeIn: k == 0, fadeOut: k == cards.Length - 1),
                        Stamp(0.55f, c.Word, 1.9f));
                }
            }

            // ---- the anchor rule: a card that names the docked floor jams
            if (Want("jam"))
            {
                var (seed, at) = Find(1, 41, Strong, null, (s, e) => e.Type == Ev.Jammed && s.Time > 8f);
                if (at >= 0f)
                {
                    yield return Prepare(1, seed, Strong, null, at - 1.5f, 0.35f);
                    cursorOn = true;
                    yield return Record("jam", 4f,
                        Caption(0.2f, 3.8f, "HOLD A FLOOR IN PLACE", "The floor you're docked at can't move. A card that names it jams instead.", Bellhop));
                }
            }

            // ---- tips, streaks, group drops
            if (Want("tips"))
            {
                var (seed, at) = Find(2, 51, Strong, null, (s, e) => e.Type == Ev.Delivered && e.Aux >= 2 && s.Streak >= 6 && s.Time > 10f);
                if (at >= 0f)
                {
                    yield return Prepare(2, seed, Strong, null, at - 2.2f, 0.65f);
                    cursorOn = true;
                    yield return Record("tips", 4.4f,
                        Caption(0.2f, 4.2f, "TIPS, STREAKS & GROUP DROPS", "Keep everyone happy to build a streak. Drop several guests at once for a bonus.", Icons.Floor(FloorId.Lobby)),
                        StillAt(2.55f, "triple_drop"));
                }
            }

            // ---- houseplant: won't get off until it has had sun (Tuesday, scripted)
            if (Want("plant"))
            {
                Func<Plan> plan = () => new Plan().Delay(0.6f).Board(Kind.Houseplant).Delay(0.35f).Board(Kind.Commuter).Delay(0.4f)
                    .Send(FloorId.Library).Docked().Delay(1.1f).Send(FloorId.Greenhouse).Docked().Delay(0.9f).Send(FloorId.Library);
                float need = Probe(1, 61, Strong, plan, (s, e) => e.Type == Ev.NeedsSun);
                float sun = Probe(1, 61, Strong, plan, (s, e) => e.Type == Ev.Sunned);
                if (need >= 0f && sun >= 0f)
                {
                    float from = need - 2.0f;
                    yield return Prepare(1, 61, Strong, plan, from, 0.8f);
                    cursorOn = true;
                    yield return Record("plant", sun - from + 2.0f,
                        Caption(0.25f, sun - from + 1.6f, "HOUSEPLANTS", "They won't get off until the doors have opened on a sunny floor.", Icons.Kind(Kind.Houseplant)),
                        StillAt(sun - from + 0.45f, "houseplant_sun"));
                }
            }

            // ---- mirror mover: two spaces (Wednesday, scripted: the third commuter doesn't fit)
            if (Want("mirror"))
            {
                Func<Plan> plan = () => new Plan().Delay(0.5f).Board(Kind.Mirror).Delay(0.3f).Board(Kind.Commuter).Delay(0.3f).Board(Kind.Commuter)
                    .Delay(0.35f).Board(Kind.Commuter).Delay(0.8f).Send(FloorId.Penthouse);
                yield return Prepare(2, 71, Strong, plan, 0f, 0.85f);
                cursorOn = true;
                yield return Record("mirror", 4.8f,
                    Caption(0.25f, 4.4f, "MIRROR MOVERS", "They take up two spaces in the car, and they won't ride with a vampire.", Icons.Kind(Kind.Mirror)));
            }

            // ---- vampire: refuses the mirror, then sunlight (Thursday, scripted)
            if (Want("vampire"))
            {
                Func<Plan> plan = () => new Plan().Delay(0.5f).Board(Kind.Vampire).Delay(0.4f).Send(FloorId.Lobby).Docked().Delay(0.5f)
                    .Board(Kind.Mirror).Delay(0.6f).Board(Kind.Commuter).Delay(0.5f).Send(FloorId.Greenhouse);
                float poof = Probe(3, 81, Strong, plan, (s, e) => e.Type == Ev.Poofed);
                if (poof >= 0f)
                {
                    yield return Prepare(3, 81, Strong, plan, 0f, 0.8f);
                    cursorOn = true;
                    yield return Record("vampire", poof + 2.2f,
                        Caption(0.25f, poof + 1.8f, "VAMPIRES", "They won't share the car with a mirror, and sunlight turns them into bats.", Icons.Kind(Kind.Vampire)),
                        StillAt(poof + 0.35f, "vampire_poof"));
                }
            }

            // ---- courier: the floor is leaving the building (Friday)
            if (Want("courier"))
            {
                var (seed, at) = Find(4, 91, Strong, null, (s, e) => Delivered(e, DeliveryFlags.JustInTime) && s.Time > 15f);
                var (seed2, gone) = Find(4, 95, Strong, null, (s, e) => e.Type == Ev.FloorDeparted && e.Floor != FloorId.Ocean && e.Slot >= 3 && s.Time > 15f);
                if (at >= 0f)
                {
                    yield return Prepare(4, seed, Strong, null, at - 2.6f, 0.6f);
                    cursorOn = true;
                    yield return Record("courier_jit", 4.0f,
                        Caption(0.2f, 4.0f, "COURIERS", "Their floor is leaving the building. Deliver the parcel before it drifts away.", Icons.Kind(Kind.Courier), fadeOut: gone < 0f));
                }
                if (gone >= 0f)
                {
                    yield return Prepare(4, seed2, Strong, null, gone - 1.2f, 0f);
                    yield return Record("courier_gone", 3.4f,
                        Caption(0f, 3.4f, "COURIERS", "Their floor is leaving the building. Deliver the parcel before it drifts away.", Icons.Kind(Kind.Courier), fadeIn: at < 0f));
                }
            }

            // ---- swimmer: rescued from the ocean
            if (Want("swimmer"))
            {
                var (seed, at) = Find(5, 101, Strong, null, (s, e) => Delivered(e, DeliveryFlags.Rescued) && s.Time > 10f);
                if (at >= 0f)
                {
                    yield return Prepare(5, seed, Strong, null, at - 2.8f, 0.55f);
                    cursorOn = true;
                    yield return Record("swimmer", 4.4f,
                        Caption(0.2f, 4.2f, "THE OCEAN DROPS IN", "Swimmers wait on the Ocean, which only stays a few stops. Always \"Lobby, please.\"", Icons.Kind(Kind.Swimmer)));
                }
            }

            // ---- kid: every button (Sunday's opening)
            if (Want("kid"))
            {
                Func<Plan> plan = () => new Plan().Delay(0.6f).Board(Kind.Kid).Delay(0.7f).Send(FloorId.Penthouse);
                float stop = Probe(6, 111, Strong, plan, (s, e) => e.Type == Ev.QuickStop);
                yield return Prepare(6, 111, Strong, plan, 0f, 0.45f);
                cursorOn = true;
                yield return Record("kid", 6.4f,
                    Caption(0.25f, 6.0f, "KIDS", "They pressed every button, so the car stops at every floor on the way.", Icons.Kind(Kind.Kid)),
                    StillAt(Mathf.Max(2.5f, stop + 1.6f), "kid_every_button"));
            }

            // ---- tycoon: express only
            if (Want("tycoon"))
            {
                var (seed, at) = Find(7, 121, Strong, null, (s, e) => Delivered(e, DeliveryFlags.Express) && e.Floor == FloorId.Penthouse);
                if (at >= 0f)
                {
                    float from = Mathf.Max(0f, at - 3.6f);
                    yield return Prepare(7, seed, Strong, null, from, 0f);
                    cursorOn = true;
                    yield return Record("tycoon", at - from + 1.8f,
                        Caption(0.2f, at - from + 1.5f, "TYCOONS", "Express only: stop anywhere else first and the tip is gone. They pay the most.", Icons.Kind(Kind.Tycoon)));
                }
            }

            // ---- patience: storming off, complaints
            if (Want("patience"))
            {
                var (seed, at) = Find(3, 131, Novice, null, (s, e) => e.Type == Ev.StormedOff && s.Time > 15f);
                if (at >= 0f)
                {
                    yield return Prepare(3, seed, Novice, null, at - 2.0f, 0.3f);
                    yield return Record("patience", 4.2f,
                        Caption(0.2f, 4.0f, "MIND THEIR PATIENCE", "Leave guests waiting and they take the stairs. Five complaints and you're fired.", Bellhop));
                }
            }

            // ---- reactive music: a moment of real trouble
            if (Want("trouble"))
            {
                var (seed, at) = FindState(8, 141, Novice, null, s => s.Trouble > 0.9f && s.Complaints >= 2 && s.Time > 40f);
                if (at >= 0f)
                {
                    yield return Prepare(8, seed, Novice, null, at - 1.0f, 0f);
                    yield return Record("trouble", 4.4f,
                        Caption(0.2f, 4.2f, "MUZAK THAT PANICS", "The lounge music gets nervous when you do, and relaxes when you recover.", Icons.Floor(FloorId.Boiler)));
                }
            }

            // ---- route preview: hover a guest, then a floor (Sunday, with a kid aboard)
            if (Want("route"))
            {
                var (seed, at) = FindState(6, 151, Strong, null, s => s.Time > 8f && s.Car.IsOpen && s.Car.Has(Kind.Kid) && s.WaitingCount >= 2);
                if (at >= 0f)
                {
                    var sim = SimAt(6, seed, Strong, null, at);
                    var kid = sim.Car.Riders.Find(r => r.Kind == Kind.Kid);
                    var dest = FarDest(sim, kid);
                    var guest = PickWaiting(sim);
                    Func<Plan> plan = () => new Plan().Until(s => s.Time >= at, true).Delay(4.0f).Send(dest);
                    yield return Prepare(6, seed, Strong, plan, at, 0.25f);
                    yield return Record("route", 6.0f,
                        Caption(0.2f, 5.6f, "PLAN EVERY TRIP", "Hover a guest to see where they're going, or a floor to preview every stop.", Icons.Badge("sun")),
                        HoverScript(guest, dest, 0.35f, 1.9f, 4.0f),
                        StillAt(3.3f, "route_preview"));
                }
            }

            // ---- zoom and gamepad
            if (Want("pad"))
            {
                var (seed, at) = FindState(1, 161, Strong, null, s => s.Time > 12f && s.Car.IsOpen && s.Car.Riders.Count >= 1 && s.WaitingCount >= 2);
                if (at >= 0f)
                {
                    var sim = SimAt(1, seed, Strong, null, at);
                    var rider = sim.Car.Riders[0];
                    var dest = sim.B.Has(rider.Dest) ? rider.Dest : sim.B.At(0);
                    Func<Plan> plan = () => new Plan().Until(s => s.Time >= at, true).Delay(3.4f).Send(dest).Docked().Delay(0.5f).BoardAll();
                    yield return Prepare(1, seed, Strong, plan, at, 0f);
                    Runner.ScriptCursor(Runner.Sim.Car.DockedSlot, -1);
                    Controls.ForcePad = true;
                    yield return Record("pad", 6.4f,
                        Caption(0.2f, 6.0f, "ZOOM IN, OR PICK UP A PAD", "Ride along in close-up, and play with a mouse, the keyboard or a gamepad.", Bellhop, true, true, new Vector2(-518, -352)),
                        PadScript(sim, rider.Id, dest),
                        StillAt(1.6f, "gamepad_closeup"));
                    Controls.ReleaseForcedPad();
                }
            }

            // ---- settings: the pause card's controls panel, then GRAPHICS FIDELITY through every step and LARGER TEXT
            if (Want("settings"))
            {
                yield return Prepare(1, 61, Strong, null, 24f, 0f);
                root.Pause();
                yield return Wait(Settle);
                yield return Record("settings", 6.4f,
                    Caption(0.2f, 6.0f, "SETTINGS", "Graphics from LOW to ULTRA, larger text, reduced motion and relaxed shifts.", Bellhop,
                        true, true, new Vector2(-960f + 36f + 220f, -540f + 40f + 120f), 440f, 240f),
                    SettingsScript());
                var save = SaveData.Current;
                save.LargeText = false;
                save.Fidelity = fidelity;   // the slider ends where it started; make sure the rest records at the same step
                root.ApplySettings();
                HideScreen<SettingsScreen>();
                HideScreen<PauseScreen>();
            }

            // ---- rush hour
            if (Want("rush"))
            {
                var (seed, at) = Find(4, 171, Strong, null, (s, e) => e.Type == Ev.RushHour);
                if (at >= 0f)
                {
                    yield return Prepare(4, seed, Strong, null, at - 1.0f, 0f);
                    cursorOn = true;
                    yield return Record("rush", 4.2f,
                        Caption(0.2f, 4.0f, "RUSH HOUR", "The last thirty seconds get hectic, and every tip pays half again.", Icons.Badge("tophat")));
                }
            }

            // ---- the week: duty roster and a shift intro
            if (Want("week"))
            {
                SetProgress(6);
                root.ShowRoster();
                Runner.PreStep = null;
                yield return Wait(2.2f);
                Still("roster");
                cursorOn = true;
                cursorPos = new Vector2(620, -380);
                yield return Record("week", 7.0f,
                    Caption(0.2f, 6.6f, "A WEEK AT THE SHUFFLETON", "Ten shifts, each with a new guest or twist. Earn a star to unlock the next.", null, true, true, new Vector2(-655, 428), 560f, 164f),
                    RosterScript());
                Still("intro");
            }

            // ---- clock in: the intro card's CLOCK IN, the brass doors close (naming the shift) and open on it
            if (Want("clockin"))
            {
                const int shift = 6;
                if (!Want("week")) { SetProgress(7); root.ShowIntro(shift); yield return Wait(2.2f); }
                Runner.PreStep = null;
                root.FixedSeed = 111;   // the doors start the shift: the same one in both passes
                cursorOn = false;
                cursorPos = new Vector2(620, -380);
                yield return Record("clockin", 4.8f,
                    Caption(0.2f, 4.0f, "CLOCK IN", "A note from The Management, then the brass doors open on the shift.", Icons.Kind(Kind.Kid)),
                    ClockInScript());
                root.FixedSeed = 0;
            }

            // ---- clock out
            if (Want("clockout"))
            {
                const int shift = 7;
                ulong seed = 0;
                for (ulong s = 181; s < 211 && seed == 0; s++)
                    if (Bot.PlayOut(ShiftCatalog.Get(shift), s, Bot.Strong(s), ShiftRunner.Step).StarCount >= 3) seed = s;
                if (seed == 0) seed = 181;
                SaveData.Current.Best[shift] = 21000;
                SaveData.Current.Stars[shift] = 1;
                var def = ShiftCatalog.Get(shift);
                yield return Prepare(shift, seed, Strong, null, def.Duration - 1.2f, 0f);
                yield return Record("clockout", 7.2f,
                    Caption(2.6f, 4.4f, "CLOCK OUT", "Earn up to three stars, then chase your best. Then, one more shift.", Icons.Badge("tophat"), true, true, new Vector2(-660, -120), 520f, 196f),
                    StillAt(6.95f, "results"));
            }

            // ---- graveyard shift: night, everyone, and the first flip
            if (Want("graveyard"))
            {
                float flip = Probe(8, 191, Strong, null, (s, e) => e.Type == Ev.Shuffled && e.Card.Type == CardType.Flip);
                yield return Prepare(8, 191, Strong, null, 0f, 0f);
                cursorOn = true;
                yield return Record("graveyard", Mathf.Max(5f, flip + 2.6f),
                    Caption(0.25f, Mathf.Max(4.6f, flip + 2.2f), "FRIDAY THE 13TH", "The Graveyard Shift: every guest at once, by candlelight, and floors that flip.", Icons.Floor(FloorId.Crypt)),
                    Stamp(flip - 0.1f, "FLIP", 2.0f),
                    StillAt(flip + 0.5f, "graveyard_flip"));
            }

            // ---- overtime
            if (Want("overtime"))
            {
                yield return Prepare(9, 201, Strong, null, 250f, 0f);
                cursorOn = true;
                yield return Record("overtime", 4.4f,
                    Caption(0.2f, 4.2f, "OVERTIME", "No clock, just busier and busier until five complaints. Today's Shift is new every day.", Bellhop),
                    StillAt(2.2f, "overtime"));
            }

            // ---- montage: short, captionless moments for the escalation
            if (Want("montage"))
            {
                yield return Moment("m_flip", 9, 211, Strong, (s, e) => e.Type == Ev.Shuffled && e.Card.Type == CardType.Flip && s.Time > 40f, 0f);
                yield return Moment("m_poof", 3, 221, Strong, (s, e) => e.Type == Ev.Poofed, 1f, 2.8f,
                    () => new Plan().Delay(0.4f).Board(Kind.Vampire).Delay(0.4f).Send(FloorId.Greenhouse));
                yield return Moment("m_ocean", 8, 231, Strong, (s, e) => e.Type == Ev.FloorDeparted && e.Floor2 == FloorId.Ocean && s.Time > 10f, 0.3f);
                yield return Moment("m_triple", 9, 241, Strong, (s, e) => e.Type == Ev.Delivered && e.Aux >= 2 && s.Time > 60f, 0.6f);
                yield return Moment("m_express", 8, 251, Strong, (s, e) => Delivered(e, DeliveryFlags.Express) && s.Time > 20f, 0.5f);
                yield return Moment("m_jam", 9, 261, Strong, (s, e) => e.Type == Ev.Jammed && s.Time > 60f, 0.4f);
                yield return Moment("m_depart", 7, 271, Strong, (s, e) => e.Type == Ev.FloorDeparted && e.Floor != FloorId.Ocean && s.Time > 20f, 0f);
                yield return Moment("m_fired", 9, 281, Novice, (s, e) => e.Type == Ev.Fired, 0f, 3.2f);
            }

            // ---- end card
            if (Want("end"))
            {
                root.ShowTitle();
                Runner.PreStep = null;
                HideScreen<TitleScreen>();
                yield return Wait(2.6f);
                yield return Record("end", 7.5f, EndCard());
            }

            Finish();
        }

        /// <summary>A short captionless clip around an event, for the escalation montage.</summary>
        IEnumerator Moment(string name, int shift, ulong seed0, Func<ulong, Bot> bot, Func<ShiftSim, SimEvent, bool> match, float zoom, float length = 2.8f,
            Func<Plan> plan = null)
        {
            var (seed, at) = Find(shift, seed0, bot, plan, match);
            if (at < 0f) { Debug.LogWarning($"[Trailer] no moment for {name}"); yield break; }
            yield return Prepare(shift, seed, bot, plan, Mathf.Max(0f, at - 1.3f), zoom);
            cursorOn = false;
            yield return Record(name, length);
        }

        /// <summary>Unlock the week up to (but not including) <paramref name="upTo"/> with plausible stars and bests.</summary>
        void SetProgress(int upTo)
        {
            var save = SaveData.Current;
            int[] stars = { 3, 3, 2, 3, 2, 2, 1, 0, 0, 0 };
            for (int k = 0; k < ShiftCatalog.All.Count; k++)
            {
                var def = ShiftCatalog.Get(k);
                save.Stars[k] = k < upTo ? stars[k] : 0;
                save.Best[k] = k < upTo ? def.Stars[Mathf.Max(0, stars[k] - 1)] + 650 * (k + 1) : 0;
                save.Plays[k] = k < upTo && stars[k] > 0 ? 2 + k : 0; // no tries without a star: no late passes
            }
            save.EndingSeen = false;
        }

        // ================================================================ shots

        float shotClock;
        bool recording;
        string shotName;
        int frames;
        double audioStart;
        Process ffmpeg;
        Stream pipe;
        readonly StringBuilder manifest = new StringBuilder();
        readonly List<Coroutine> running = new List<Coroutine>();
        readonly List<GameObject> transient = new List<GameObject>();

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UiTime.Dt) yield return null;
        }

        IEnumerator At(float t)
        {
            while (shotClock < t) yield return null;
        }

        /// <summary>Start <paramref name="shift"/> from a seed, skip ahead quietly, and hold on the first frame until the views settle.</summary>
        IEnumerator Prepare(int shift, ulong seed, Func<ulong, Bot> bot, Func<Plan> plan, float at, float zoom)
        {
            root.StartShift(shift, seed);
            var r = Runner;
            r.AutoBot = bot?.Invoke(seed);
            var p = plan?.Invoke();
            if (p != null) { p.Runner = r; p.Acted = OnBotActed; }
            r.PreStep = p != null ? p.Step : (Func<ShiftSim, bool>)null;
            r.Paused = true;
            if (at > 0f) r.FastForward(at, true);
            r.SnapViews();
            r.Rig.Zoom = zoom;
            r.ShowHover(-1);
            yield return Wait(Settle);
            r.Paused = false;
        }

        IEnumerator Record(string name, float seconds, params IEnumerator[] actions)
        {
            BeginShot(name);
            foreach (var a in actions) if (a != null) running.Add(StartCoroutine(a));
            while (shotClock < seconds - 1e-4f) yield return null;
            EndShot();
            foreach (var c in running) if (c != null) StopCoroutine(c);
            running.Clear();
            foreach (var g in transient) if (g) Destroy(g);
            transient.Clear();
            cursorOn = false;
            clicks.Clear();
            cursorTarget = null;
            if (Runner.Sim != null) Runner.ShowHover(-1);
        }

        void BeginShot(string name)
        {
            shotName = name;
            shotClock = 0f;
            frames = 0;
            recording = true;
            if (video)
            {
                var file = Path.Combine(outDir, "shots", name + ".mp4");
                var psi = new ProcessStartInfo("nice",
                    $"-n 10 ffmpeg -y -loglevel error -f rawvideo -pix_fmt rgba -s {W}x{H} -r {Fps} -i - -vf vflip -c:v libx264 -preset medium -crf 14 -pix_fmt yuv420p \"{file}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                };
                ffmpeg = Process.Start(psi);
                pipe = ffmpeg.StandardInput.BaseStream;
            }
            else audioStart = AudioTap.Instance ? AudioTap.Instance.SecondsWritten : 0.0;
            Debug.Log($"[Trailer] shot {name} at sim {(Runner.Sim != null ? Runner.Sim.Time : 0f):0.00}s");
        }

        void EndShot()
        {
            recording = false;
            var inv = CultureInfo.InvariantCulture;
            if (video)
            {
                pipe.Flush();
                pipe.Close();
                ffmpeg.WaitForExit();
                manifest.AppendLine(string.Format(inv, "{0} frames={1} seconds={2:0.000}", shotName, frames, frames / (float)Fps));
            }
            else
            {
                double end = AudioTap.Instance ? AudioTap.Instance.SecondsWritten : 0.0;
                manifest.AppendLine(string.Format(inv, "{0} start={1:0.0000} end={2:0.0000}", shotName, audioStart, end));
            }
            File.WriteAllText(Path.Combine(outDir, video ? "video_shots.txt" : "audio_shots.txt"), manifest.ToString());
        }

        void Finish()
        {
            if (!video) AudioTap.Instance?.Finish();
            Debug.Log("[Trailer] done");
            Application.Quit(0);
        }

        void Update()
        {
            if (recording) shotClock += UiTime.Dt;
        }

        void LateUpdate()
        {
            TickCursor(UiTime.Dt);
            TickRipples(UiTime.Dt);
            if (!recording || !video) return;
            Canvas.ForceUpdateCanvases();
            var tex = Shots.CaptureTexture(W, H, true, Msaa);
            var bytes = tex.GetRawTextureData();
            pipe.Write(bytes, 0, bytes.Length);
            Destroy(tex);
            frames++;
        }

        void Still(string name)
        {
            if (!video) return;
            overlay.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Shots.Capture(Path.Combine(outDir, "stills", name + ".png"), W, H, true, Msaa);
            overlay.gameObject.SetActive(true);
        }

        IEnumerator StillAt(float t, string name)
        {
            yield return At(t);
            Still(name);
        }

        void HideScreen<T>() where T : UiScreen
        {
            var s = FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (s != null && s.Visible) s.Hide();
        }

        // ================================================================ probes

        /// <summary>Play a shift headlessly (same seed, bot and plan as the recording) and return the sim time of the first matching event, or -1.</summary>
        static float Probe(int shift, ulong seed, Func<ulong, Bot> bot, Func<Plan> plan, Func<ShiftSim, SimEvent, bool> match, float limit = 420f)
        {
            float found = -1f;
            Play(shift, seed, bot, plan, limit, sim =>
            {
                foreach (var e in sim.Events) if (match(sim, e)) { found = sim.Time; return true; }
                return false;
            });
            return found;
        }

        static float ProbeState(int shift, ulong seed, Func<ulong, Bot> bot, Func<Plan> plan, Func<ShiftSim, bool> match, float limit = 420f)
        {
            float found = -1f;
            Play(shift, seed, bot, plan, limit, sim => { if (match(sim)) { found = sim.Time; return true; } return false; });
            return found;
        }

        static ShiftSim SimAt(int shift, ulong seed, Func<ulong, Bot> bot, Func<Plan> plan, float time)
        {
            return Play(shift, seed, bot, plan, time + ShiftRunner.Step * 0.5f, null);
        }

        /// <summary>Steps exactly like ShiftRunner: plan, then bot, then the sim, one fixed step at a time.</summary>
        static ShiftSim Play(int shift, ulong seed, Func<ulong, Bot> bot, Func<Plan> plan, float limit, Func<ShiftSim, bool> stop)
        {
            var sim = new ShiftSim(ShiftCatalog.Get(shift), seed);
            var b = bot?.Invoke(seed);
            var p = plan?.Invoke();
            int steps = Mathf.CeilToInt(limit / ShiftRunner.Step);
            for (int i = 0; i < steps && !sim.Ended; i++)
            {
                bool scripted = p != null && p.Step(sim);
                if (!scripted) b?.Tick(sim, ShiftRunner.Step, out _);
                sim.Tick(ShiftRunner.Step);
                if (stop != null && stop(sim)) break;
                sim.Events.Clear();
            }
            return sim;
        }

        static (ulong Seed, float At) Find(int shift, ulong seed0, Func<ulong, Bot> bot, Func<Plan> plan, Func<ShiftSim, SimEvent, bool> match)
        {
            for (ulong s = seed0; s < seed0 + 40; s++)
            {
                float t = Probe(shift, s, bot, plan, match);
                if (t >= 0f) return (s, t);
            }
            Debug.LogWarning($"[Trailer] no match in shift {shift}");
            return (seed0, -1f);
        }

        static (ulong Seed, float At) FindState(int shift, ulong seed0, Func<ulong, Bot> bot, Func<Plan> plan, Func<ShiftSim, bool> match)
        {
            for (ulong s = seed0; s < seed0 + 40; s++)
            {
                float t = ProbeState(shift, s, bot, plan, match);
                if (t >= 0f) return (s, t);
            }
            Debug.LogWarning($"[Trailer] no state match in shift {shift}");
            return (seed0, -1f);
        }

        static FloorId FarDest(ShiftSim sim, Passenger prefer)
        {
            int here = sim.Car.DockedSlot;
            if (prefer != null && sim.B.Has(prefer.Dest) && Mathf.Abs(sim.B.SlotOf(prefer.Dest) - here) >= 2) return prefer.Dest;
            FloorId best = sim.B.At(here == 0 ? sim.B.Count - 1 : 0);
            int dist = -1;
            foreach (var r in sim.Car.Riders)
                if (sim.B.Has(r.Dest) && Mathf.Abs(sim.B.SlotOf(r.Dest) - here) > dist) { dist = Mathf.Abs(sim.B.SlotOf(r.Dest) - here); best = r.Dest; }
            return best;
        }

        static int PickWaiting(ShiftSim sim)
        {
            int pick = -1;
            float best = -1f;
            foreach (var p in sim.All)
            {
                if (p.State != PState.Waiting || !sim.B.Has(p.Dest)) continue;
                float score = Mathf.Abs(sim.B.SlotOf(p.Dest) - sim.B.SlotOf(p.At)) + (p.Kind != Kind.Commuter ? 2f : 0f);
                if (score > best) { best = score; pick = p.Id; }
            }
            return pick;
        }

        /// <summary>
        /// A scripted operator that runs inside the fixed step: board this, send there, wait for the doors. While a
        /// step is pending the bot sits out (unless the step says otherwise); after the last step the bot takes over.
        /// </summary>
        sealed class Plan
        {
            struct Item { public Func<ShiftSim, bool> Run; public bool BotPlays; public string What; }
            readonly List<Item> items = new List<Item>();
            int index;
            float waited;
            public ShiftRunner Runner;
            public Action<Bot.Action, int> Acted;

            Plan Add(string what, Func<ShiftSim, bool> run, bool botPlays = false)
            {
                items.Add(new Item { Run = run, BotPlays = botPlays, What = what });
                return this;
            }

            /// <summary>Returns true while the bot should sit out this step.</summary>
            public bool Step(ShiftSim sim)
            {
                if (index >= items.Count) return false;
                var it = items[index];
                waited += ShiftRunner.Step;
                bool done = it.Run(sim);
                if (!done && !it.BotPlays && waited > 6f)
                {
                    Debug.LogWarning($"[Trailer] plan step '{it.What}' timed out");
                    done = true;
                }
                if (done) { index++; waited = 0f; }
                return !it.BotPlays;
            }

            public Plan Delay(float seconds)
            {
                float left = seconds;
                return Add("delay", s => (left -= ShiftRunner.Step) <= 0f);
            }

            public Plan Until(Func<ShiftSim, bool> condition, bool botPlays) => Add("until", condition, botPlays);

            public Plan Docked() => Add("docked", s => s.Car.State == CarState.Docked);

            public Plan Board(Kind kind) => Add("board " + kind, s =>
            {
                if (!s.Car.IsOpen) return false;
                foreach (var p in s.Waiting[(int)s.DockedFloor])
                {
                    if (p.Kind != kind) continue;
                    Acted?.Invoke(Bot.Action.Board, p.Id);
                    s.Board(p.Id);
                    return true;
                }
                return false;
            });

            public Plan BoardAll() => Add("board all", s =>
            {
                if (!s.Car.IsOpen) return false;
                s.BoardAll();
                return true;
            });

            public Plan Send(FloorId floor) => Add("send " + floor, s =>
            {
                if (!s.Car.IsOpen) return false;
                int slot = s.B.SlotOf(floor);
                if (slot < 0 || slot == s.Car.DockedSlot) return true;
                if (s.SendTo(slot) && Runner != null && !Runner.Paused)
                {
                    Runner.Hud.Panel.Press(slot);
                    AudioDirector.Instance?.Sfx("btn_press", 1f, 1f + slot * 0.02f, 0.6f);
                }
                Acted?.Invoke(Bot.Action.Send, slot);
                return true;
            });
        }

        // ================================================================ scripted UI moments

        /// <summary>Hover a waiting guest (tooltip + destination), then a floor (route preview), then "click" it.</summary>
        IEnumerator HoverScript(int pid, FloorId floor, float t0, float t1, float t2)
        {
            var r = Runner;
            yield return At(t0);
            if (pid >= 0)
            {
                Func<Vector2?> at = () => { var w = r.GuestPoint(pid); return w.HasValue ? OfWorld(w.Value) : null; };
                cursorAlpha = 1f;
                cursorTarget = at;
                yield return At(t0 + 0.55f);
                r.ShowHover(pid);
            }
            yield return At(t1);
            r.ShowHover(-1);
            var fv = r.Building[floor];
            cursorTarget = () => OfWorld(fv.transform.position + new Vector3(4.4f, 1.7f, Layout.FrontZ));
            yield return At(t1 + 0.5f);
            r.ShowHoverFloor(floor);
            yield return At(t2);
            cursorPress = 1f;
            Ripple(cursorPos, Deco.Gold);
            yield return At(t2 + 0.3f);
            r.ShowHoverFloor(null);
            cursorTarget = null;
            cursorAlpha = 0f;
        }

        /// <summary>The gamepad cursor: zoom in, step the floor cursor to the rider's floor, pick the rider, press A.</summary>
        IEnumerator PadScript(ShiftSim at, int riderId, FloorId dest)
        {
            var r = Runner;
            int here = at.Car.DockedSlot;
            int target = at.B.SlotOf(dest);
            yield return At(0.3f);
            r.Rig.Zoom = 1f;
            r.ScriptCursor(here, -1);
            yield return At(1.0f);
            r.ScriptCursor(here, riderId);
            yield return At(1.7f);
            int dir = target > here ? 1 : -1;
            float t = 1.7f;
            for (int s = here; s != target; s += dir)
            {
                r.ScriptCursor(s + dir, -1);
                AudioDirector.Instance?.Sfx("ui_hover", 0.35f, 0.9f + (s + dir) * 0.04f, 0f, 0.02f, 0.03f);
                t += 0.38f;
                yield return At(t);
            }
            while (r.Sim.Car.IsOpen && shotClock < 4.2f) yield return null;
            yield return At(4.6f);
            r.ScriptCursor(Mathf.RoundToInt(r.Sim.Car.Pos), -1);
            while (shotClock < 6.4f)
            {
                if (r.Sim.B.Has(dest)) r.ScriptCursor(r.Sim.B.SlotOf(dest), -1);
                yield return null;
            }
        }

        IEnumerator RosterScript()
        {
            UiButton Card(string text) => Find(b => HasText(b, text));
            var hover = new[] { "Fangs", "Special", "Surf" };
            float t = 0.3f;
            foreach (var h in hover)
            {
                var b = Card(h);
                if (b == null) continue;
                var rt = (RectTransform)b.transform;
                cursorAlpha = 1f;
                cursorTarget = () => OfRect(rt);
                yield return At(t + 0.45f);
                ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
                yield return At(t + 0.8f);
                ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
                t += 0.8f;
            }
            var pick = Card("Family");
            if (pick != null)
            {
                var rt = (RectTransform)pick.transform;
                cursorTarget = () => OfRect(rt);
                yield return At(t + 0.5f);
                ExecuteEvents.Execute(pick.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
                yield return At(t + 0.8f);
                cursorPress = 1f;
                Ripple(cursorPos, Deco.Gold);
                ExecuteEvents.Execute(pick.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
                pick.Click();
                cursorTarget = null;
            }
            yield return At(t + 1.4f);
            cursorAlpha = 0f;
        }

        /// <summary>Press CLOCK IN on the intro card; the doors do the rest.</summary>
        IEnumerator ClockInScript()
        {
            var b = Find(x => HasText(x, "CLOCK IN"));
            if (b == null) { Debug.LogWarning("[Trailer] no CLOCK IN button"); yield break; }
            var rt = (RectTransform)b.transform;
            cursorAlpha = 1f;
            cursorTarget = () => OfRect(rt);
            yield return At(0.55f);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
            yield return At(0.85f);
            cursorPress = 1f;
            Ripple(cursorPos, Deco.Gold);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
            b.Click();
            cursorTarget = null;
            yield return At(1.1f);
            cursorAlpha = 0f;
        }

        /// <summary>From the pause card: SETTINGS, drag GRAPHICS FIDELITY down to LOW and back up to ULTRA, then LARGER TEXT on.</summary>
        IEnumerator SettingsScript()
        {
            var open = Find(x => HasText(x, "SETTINGS"));
            if (open == null) { Debug.LogWarning("[Trailer] no SETTINGS button"); yield break; }
            var ort = (RectTransform)open.transform;
            cursorAlpha = 1f;
            cursorPos = new Vector2(330, -260);
            cursorTarget = () => OfRect(ort);
            yield return At(0.5f);
            ExecuteEvents.Execute(open.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
            yield return At(0.8f);
            cursorPress = 1f;
            Ripple(cursorPos, Deco.Gold);
            ExecuteEvents.Execute(open.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
            open.Click();
            yield return At(1.0f);

            UiChoice slider = null;
            foreach (var c in FindObjectsByType<UiChoice>(FindObjectsInactive.Exclude))
                if (c.isActiveAndEnabled && c.Notched && c.Options == GraphicsQuality.Names) slider = c;
            if (slider == null) { Debug.LogWarning("[Trailer] no GRAPHICS FIDELITY slider"); yield break; }
            var frt = (RectTransform)slider.transform;
            // the notches sit along the row from x = 124 to 310 (UiChoice's track)
            Func<int, Func<Vector2?>> notch = i => () =>
                ToOverlay(RectTransformUtility.WorldToScreenPoint(root.UiCam, frt.TransformPoint(new Vector3(Mathf.Lerp(124f, 310f, i / 3f), 0f, 0f))));
            cursorTarget = notch(slider.Index);
            yield return At(1.55f);
            ExecuteEvents.Execute(slider.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
            cursorPress = 1f;
            int from = slider.Index;
            float t = 1.7f;
            // down to LOW, a beat there, then back up to where it was
            var path = new List<int>();
            for (int i = from - 1; i >= GraphicsQuality.Low; i--) path.Add(i);
            path.Add(-1);
            for (int i = GraphicsQuality.Low + 1; i <= from; i++) path.Add(i);
            foreach (int i in path)
            {
                t += i < 0 ? 0.35f : 0.32f;
                if (i < 0) continue;
                cursorTarget = notch(i);
                yield return At(t);
                slider.Set(i);
                AudioDirector.Instance?.Sfx("ui_click", 0.45f, 0.9f + 0.1f * i);
            }
            ExecuteEvents.Execute(slider.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);

            UiToggle large = null;
            foreach (var tg in FindObjectsByType<UiToggle>(FindObjectsInactive.Exclude))
                if (tg.isActiveAndEnabled && tg.name.Contains("LARGER TEXT")) large = tg;
            if (large == null) { Debug.LogWarning("[Trailer] no LARGER TEXT toggle"); yield break; }
            var lrt = (RectTransform)large.transform;
            // the switch sits at x = 278 in the row (UiToggle)
            cursorTarget = () => ToOverlay(RectTransformUtility.WorldToScreenPoint(root.UiCam, lrt.TransformPoint(new Vector3(278f, 0f, 0f))));
            yield return At(t + 0.55f);
            cursorPress = 1f;
            Ripple(cursorPos, Deco.Gold);
            AudioDirector.Instance?.Sfx("ui_click", 0.8f);
            large.Set(true);
            yield return At(t + 1.2f);
            cursorTarget = null;
            cursorAlpha = 0f;
        }

        static bool HasText(UiButton b, string text)
        {
            foreach (var t in b.GetComponentsInChildren<TMP_Text>())
                if (t.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static UiButton Find(Func<UiButton, bool> match)
        {
            foreach (var b in FindObjectsByType<UiButton>(FindObjectsInactive.Exclude))
                if (b.isActiveAndEnabled && b.Interactable && match(b)) return b;
            return null;
        }

        static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

        // ================================================================ overlay: captions, stamps, cards

        RectTransform overlay;

        void BuildOverlay()
        {
            var go = new GameObject("TrailerOverlay");
            go.layer = GameRoot.UiLayer;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = root.UiCam;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.6f;
            overlay = (RectTransform)go.transform;

            var arrow = MakeArrow();
            var c = UiKit.Rect("Cursor", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.95f), Vector2.zero, new Vector2(40, 60));
            cursor = c;
            cursorGroup = c.gameObject.AddComponent<CanvasGroup>();
            cursorGroup.alpha = 0f;
            UiKit.Image("Shadow", c, arrow, new Color(0, 0, 0, 0.35f), new Vector2(40, 60), new Vector2(3, -5));
            UiKit.Image("Arrow", c, arrow, Color.white, new Vector2(40, 60));
            cursorPos = new Vector2(520, -470);
            GameRoot.SetLayerRecursive(go, GameRoot.UiLayer);
        }

        RectTransform Transient(RectTransform rt)
        {
            transient.Add(rt.gameObject);
            rt.SetSiblingIndex(cursor.GetSiblingIndex());
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            return rt;
        }

        /// <summary>
        /// A lower-third in the game's style: an enamel plate in a brass frame, an optional icon in a well, a Bungee
        /// heading and a line or two of body text. Slides in from the left and fades; fadeIn/fadeOut false holds it
        /// still so one caption can run across consecutive clips.
        /// </summary>
        IEnumerator Caption(float at, float dur, string title, string body, Sprite icon = null, bool fadeIn = true, bool fadeOut = true,
            Vector2? pos = null, float Wd = 780f, float Ht = 168f)
        {
            yield return At(at);
            var home = pos ?? new Vector2(-960f + 52f + Wd * 0.5f, -540f + 46f + Ht * 0.5f);
            var rt = Transient(Deco.Panel("Caption", overlay, new Vector2(Wd, Ht), home, true));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            float textX = -Wd * 0.5f + 34f, textW = Wd - 68f;
            if (icon != null)
            {
                UiKit.Image("Well", rt, Deco.Well, Color.white, new Vector2(116, 116), new Vector2(-Wd * 0.5f + 28f + 58f, 0f));
                var img = UiKit.Image("Icon", rt, icon, Color.white, new Vector2(98, 98), new Vector2(-Wd * 0.5f + 28f + 58f, 2f));
                img.preserveAspect = true;
                textX = -Wd * 0.5f + 164f;
                textW = Wd - 164f - 30f;
            }
            float top = Ht * 0.5f;
            var head = Deco.Label("Title", rt, title, 30, new Vector2(textW, 38), new Vector2(textX + textW * 0.5f, top - 39f));
            head.enableAutoSizing = true;
            head.fontSizeMin = 20;
            head.fontSizeMax = 30;
            var rule = UiKit.Image("Rule", rt, null, new Color(0.84f, 0.66f, 0.29f, 0.85f), new Vector2(textW, 2f), new Vector2(textX + textW * 0.5f, top - 63f));
            rule.rectTransform.pivot = new Vector2(0f, 0.5f);
            rule.rectTransform.anchoredPosition = new Vector2(textX, top - 63f);
            float bodyH = Ht - 96f;
            var text = Deco.Shadowed(UiKit.Text("Body", rt, body, 26, Palette.Cream, UiKit.Body, TextAlignmentOptions.TopLeft,
                new Vector2(textW, bodyH), new Vector2(textX + textW * 0.5f, top - 75f - bodyH * 0.5f)));
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 26;
            text.lineSpacing = 2f;
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);

            float t0 = shotClock;
            while (shotClock - t0 < dur)
            {
                float t = shotClock - t0;
                float a = fadeIn ? Mathf.Clamp01(t / 0.45f) : 1f;
                float b = fadeOut ? Mathf.Clamp01((dur - t) / 0.35f) : 1f;
                g.alpha = Mathf.Min(a, b);
                float slide = fadeIn ? (1f - Ease.OutCubic(a)) * -70f : 0f;
                float drop = fadeOut ? (1f - b) * -12f : 0f;
                rt.anchoredPosition = home + new Vector2(slide, drop);
                float wipe = fadeIn ? Ease.OutCubic(Mathf.Clamp01((t - 0.15f) / 0.55f)) : 1f;
                rule.rectTransform.sizeDelta = new Vector2(textW * wipe, 2f);
                yield return null;
            }
            g.alpha = 0f;
        }

        /// <summary>A big gilded word that slams in (the name of a shuffle card).</summary>
        IEnumerator Stamp(float at, string word, float dur)
        {
            yield return At(at);
            var rt = Transient(UiKit.Rect("Stamp", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-690, -150), new Vector2(560, 200)));
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            var t1 = Deco.Gilded(UiKit.Text("Word", rt, word, 150, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(560, 200)), 0.16f);
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            float t0 = shotClock;
            while (shotClock - t0 < dur)
            {
                float t = shotClock - t0;
                float k = Mathf.Clamp01(t / 0.22f);
                rt.localScale = Vector3.one * (k < 1f ? Mathf.Lerp(1.9f, 1f, Ease.OutBack(k, 2.2f)) : 1f + (t - 0.22f) * 0.03f);
                g.alpha = Mathf.Min(Mathf.Clamp01(t / 0.1f), Mathf.Clamp01((dur - t) / 0.3f));
                rt.localRotation = Quaternion.Euler(0, 0, -3f);
                yield return null;
            }
            g.alpha = 0f;
            t1.text = "";
        }

        /// <summary>The title card: the tower at dusk on the right, the logo assembling on the left.</summary>
        IEnumerator TitleCard()
        {
            var rt = Transient(UiKit.Stretch("TitleCard", overlay));
            var scrim = UiKit.Image("Scrim", rt, Deco.Scrim, new Color(0.07f, 0.03f, 0.09f, 0.9f), new Vector2(1300, 10));
            scrim.rectTransform.anchorMin = new Vector2(0, 0);
            scrim.rectTransform.anchorMax = new Vector2(0, 1);
            scrim.rectTransform.pivot = new Vector2(0, 0.5f);
            scrim.rectTransform.anchoredPosition = Vector2.zero;
            scrim.rectTransform.sizeDelta = new Vector2(1300, 0);
            var scrimG = scrim.gameObject.AddComponent<CanvasGroup>();

            var left = UiKit.Rect("Left", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(820, 900));
            var eyebrow = Deco.Label("Eyebrow", left, "THE SHUFFLETON  ·  EST. 1929", 24, new Vector2(760, 34), new Vector2(0, 250));
            eyebrow.characterSpacing = 14f;
            var eyeG = eyebrow.gameObject.AddComponent<CanvasGroup>();
            var logo = UiKit.Text("Logo", left, "One More\nFloor", 152, Color.white, UiKit.Display, TextAlignmentOptions.BottomLeft, new Vector2(900, 380), new Vector2(40, 40));
            logo.lineSpacing = -30;
            Deco.Gilded(logo, 0.14f);
            var logoG = logo.gameObject.AddComponent<CanvasGroup>();
            var divider = Deco.Divider(left, 600, new Vector2(-110, -172));
            var tag = Deco.Shadowed(UiKit.Text("Tag", left, "Run the elevator in a hotel that rearranges itself every time you stop.", 34,
                Palette.Hex(0xEADFC8), UiKit.Body, TextAlignmentOptions.TopLeft, new Vector2(720, 100), new Vector2(-50, -242)), 0.7f, 0.6f, 0.4f);
            tag.textWrappingMode = TextWrappingModes.Normal;
            var tagG = tag.gameObject.AddComponent<CanvasGroup>();
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);

            while (true)
            {
                float t = shotClock;
                scrimG.alpha = Ease.Smooth(t / 0.6f);
                eyeG.alpha = Ease.Smooth((t - 0.3f) / 0.5f);
                eyebrow.characterSpacing = Mathf.Lerp(30f, 14f, Ease.OutCubic((t - 0.3f) / 1.2f));
                float k = Ease.OutCubic((t - 0.45f) / 1.0f);
                logoG.alpha = Ease.Smooth((t - 0.45f) / 0.7f);
                logo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, k);
                logo.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.8f) * 0.5f);
                divider.localScale = new Vector3(Ease.OutCubic((t - 1.1f) / 0.8f), 1f, 1f);
                tagG.alpha = Ease.Smooth((t - 1.6f) / 0.6f);
                tag.rectTransform.anchoredPosition = new Vector2(-50, -242 - 14f * (1f - Ease.OutCubic((t - 1.6f) / 0.6f)));
                yield return null;
            }
        }

        /// <summary>The end card: the tower on the right, the logo, tagline and where to get it on the left (like the title).</summary>
        IEnumerator EndCard()
        {
            var rt = Transient(UiKit.Stretch("EndCard", overlay));
            var scrim = UiKit.Image("Scrim", rt, Deco.Scrim, new Color(0.07f, 0.03f, 0.09f, 0.94f), new Vector2(1400, 10));
            scrim.rectTransform.anchorMin = new Vector2(0, 0);
            scrim.rectTransform.anchorMax = new Vector2(0, 1);
            scrim.rectTransform.pivot = new Vector2(0, 0.5f);
            scrim.rectTransform.anchoredPosition = Vector2.zero;
            scrim.rectTransform.sizeDelta = new Vector2(1400, 0);
            var scrimG = scrim.gameObject.AddComponent<CanvasGroup>();

            var left = UiKit.Rect("Left", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(820, 900));
            var eyebrow = Deco.Label("Eyebrow", left, "THE SHUFFLETON  ·  EST. 1929", 24, new Vector2(760, 34), new Vector2(0, 290));
            eyebrow.characterSpacing = 14f;
            var logo = UiKit.Text("Logo", left, "One More\nFloor", 152, Color.white, UiKit.Display, TextAlignmentOptions.BottomLeft, new Vector2(900, 380), new Vector2(40, 80));
            logo.lineSpacing = -30;
            Deco.Gilded(logo, 0.14f);
            var divider = Deco.Divider(left, 600, new Vector2(-110, -132));
            var tag = Deco.Shadowed(UiKit.Text("Tag", left, "A hotel that rearranges its floors\nevery time you stop.", 34, Palette.Hex(0xEADFC8), UiKit.Body,
                TextAlignmentOptions.TopLeft, new Vector2(760, 100), new Vector2(-30, -200)), 0.7f, 0.6f, 0.4f);
            tag.textWrappingMode = TextWrappingModes.Normal;
            var get = Deco.Label("Get", left, "PLAY IT  ·  READ THE SOURCE", 22, new Vector2(760, 32), new Vector2(-30, -298));
            get.characterSpacing = 12f;
            var url = Deco.Shadowed(UiKit.Text("Url", left, "github.com/nearbycoder/OneMoreFloor", 40, Palette.Hex(0xFFE9B0), UiKit.Body, TextAlignmentOptions.Left,
                new Vector2(760, 60), new Vector2(-30, -348)), 0.7f, 0.8f, 0.4f);
            var items = new (Component C, float At)[] { (eyebrow, 0.4f), (logo, 0.55f), (tag, 1.4f), (get, 2.0f), (url, 2.2f) };
            var groups = new List<(CanvasGroup G, RectTransform R, float At, Vector2 Home)>();
            foreach (var it in items)
            {
                var g = it.C.gameObject.AddComponent<CanvasGroup>();
                var r = (RectTransform)it.C.transform;
                groups.Add((g, r, it.At, r.anchoredPosition));
            }
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            while (true)
            {
                float t = shotClock;
                scrimG.alpha = Ease.Smooth(t / 0.6f);
                foreach (var it in groups)
                {
                    float k = Mathf.Clamp01((t - it.At) / 0.6f);
                    it.G.alpha = Ease.Smooth(k);
                    it.R.anchoredPosition = it.Home + new Vector2(0, -16f * (1f - Ease.OutCubic(k)));
                }
                logo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, Ease.OutCubic((t - 0.55f) / 1.1f));
                logo.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.8f) * 0.5f);
                divider.localScale = new Vector3(Ease.OutCubic((t - 1.1f) / 0.8f), 1f, 1f);
                yield return null;
            }
        }

        // ================================================================ cursor (shows where the bot or script clicks)

        RectTransform cursor;
        CanvasGroup cursorGroup;
        Vector2 cursorPos, cursorVel;
        float cursorAlpha, cursorPress;
        Func<Vector2?> cursorTarget;
        bool cursorOn;
        readonly List<(Func<Vector2?> At, Color Col)> clicks = new List<(Func<Vector2?>, Color)>();
        float clickTravel;
        readonly List<(Image Img, float T)> ripples = new List<(Image, float)>();

        Vector2? ToOverlay(Vector2 screenPoint) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screenPoint, root.UiCam, out var local) ? local : (Vector2?)null;

        Vector2? OfRect(RectTransform rt) =>
            rt ? ToOverlay(RectTransformUtility.WorldToScreenPoint(root.UiCam, rt.TransformPoint(rt.rect.center))) : null;

        Vector2? OfWorld(Vector3 world)
        {
            var sp = root.WorldCam.WorldToScreenPoint(world);
            return sp.z > 0 ? ToOverlay(sp) : null;
        }

        void OnBotActed(Bot.Action action, int arg)
        {
            if (!cursorOn || !recording) return;
            var runner = Runner;
            Func<Vector2?> at;
            Color col = Deco.Gold;
            if (action == Bot.Action.Send)
            {
                var rt = runner.Hud.Panel.SlotRect(arg);
                at = () => OfRect(rt);
            }
            else
            {
                var w = runner.GuestPoint(arg);
                if (!w.HasValue) return;
                var fixedAt = OfWorld(w.Value);
                at = () => fixedAt;
                if (action == Bot.Action.Drop) col = Palette.Hex(0xE0604A);
            }
            clicks.Add((at, col));
            while (clicks.Count > 2) clicks.RemoveAt(0);
        }

        void TickCursor(float dt)
        {
            if (cursorOn && clicks.Count > 0)
            {
                cursorAlpha = 1f;
                var target = clicks[0].At();
                clickTravel += dt;
                if (!target.HasValue) { clicks.RemoveAt(0); clickTravel = 0f; }
                else
                {
                    cursorPos = Vector2.SmoothDamp(cursorPos, target.Value, ref cursorVel, 0.06f, 9000f, dt);
                    if ((cursorPos - target.Value).sqrMagnitude < 14f * 14f || clickTravel > 0.3f)
                    {
                        cursorPos = target.Value;
                        cursorVel = Vector2.zero;
                        cursorPress = 1f;
                        Ripple(cursorPos, clicks[0].Col);
                        clicks.RemoveAt(0);
                        clickTravel = 0f;
                    }
                }
            }
            else if (cursorTarget != null)
            {
                var target = cursorTarget();
                if (target.HasValue) cursorPos = Vector2.SmoothDamp(cursorPos, target.Value, ref cursorVel, 0.16f, 6000f, dt);
            }
            else if (!cursorOn) cursorAlpha = 0f;

            cursorPress = Mathf.Max(0f, cursorPress - dt * 7f);
            cursorGroup.alpha = Mathf.MoveTowards(cursorGroup.alpha, recording ? cursorAlpha : 0f, dt * 4f);
            cursor.anchoredPosition = cursorPos;
            cursor.localScale = Vector3.one * (1f - 0.14f * cursorPress);
        }

        void TickRipples(float dt)
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.T += dt / 0.42f;
                if (r.T >= 1f || !recording) { Destroy(r.Img.gameObject); ripples.RemoveAt(i); continue; }
                float e = 1f - (1f - r.T) * (1f - r.T);
                r.Img.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(14f, 84f, e);
                var c = r.Img.color; c.a = 0.95f * (1f - r.T); r.Img.color = c;
                ripples[i] = r;
            }
        }

        void Ripple(Vector2 at, Color col)
        {
            var img = UiKit.Image("Ripple", overlay, UiKit.Ring, col, new Vector2(14, 14), at);
            img.transform.SetSiblingIndex(cursor.GetSiblingIndex());
            img.gameObject.layer = GameRoot.UiLayer;
            ripples.Add((img, 0f));
        }

        static Sprite MakeArrow()
        {
            const int w = 80, h = 120;
            var pts = new[] { new Vector2(6, 6), new Vector2(6, 96), new Vector2(27, 76), new Vector2(42, 110), new Vector2(56, 104), new Vector2(41, 71), new Vector2(70, 71) };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var fill = (Color)Palette.Hex(0xFFF6E2);
            var edge = (Color)Palette.Hex(0x24160E);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    Vector2 a = pts[j], b = pts[i];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (p - (a + ab * t)).magnitude);
                    if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
                }
                float sd = inside ? -d : d;
                var c = Color.Lerp(edge, fill, Mathf.Clamp01(-sd - 4f));
                c.a = Mathf.Clamp01(4.5f - sd);
                px[(h - 1 - y) * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

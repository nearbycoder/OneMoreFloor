using System;
using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// Teaches without walls of text: one short tip at a time with an arrow at the thing it's about.
    /// Monday has a scripted walkthrough; every rule gets a one-time tip the first time it matters.
    /// </summary>
    public sealed class Coach : MonoBehaviour
    {
        ShiftRunner runner;
        RectTransform box, arrow;
        TextMeshProUGUI text;
        CanvasGroup group;
        RectTransform canvasRt;

        sealed class Tip
        {
            public string Id, Text;
            /// <summary>Wording for gamepad players and for arrow-key players (null = same as Text).</summary>
            public string Pad, Keys;
            public string For() => Controls.Pad && Pad != null ? PadGlyphs.Words(Pad) : Controls.KeyNav && Keys != null ? Keys : Text;
            public Func<Vector3?> World;      // world target (arrow points down at it)
            public Func<RectTransform> Ui;    // or a UI target
            public float MinTime = 1.2f, MaxTime = 7f;
            public Func<bool> Done;
            public bool Once = true;
        }

        readonly Queue<Tip> queue = new Queue<Tip>();
        Tip current;
        float shown, alpha;
        string step = "";
        bool monday;

        public static Coach Create(Transform canvas, ShiftRunner runner)
        {
            var rt = UiKit.Stretch("Coach", canvas);
            var c = rt.gameObject.AddComponent<Coach>();
            c.runner = runner;
            c.canvasRt = (RectTransform)canvas;
            c.group = rt.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;
            c.group.interactable = false;
            c.box = Deco.Panel("Box", rt, new Vector2(360, 196), Vector2.zero, true);
            c.box.anchorMin = c.box.anchorMax = Vector2.zero;
            c.box.pivot = new Vector2(0.5f, 1f);
            var lbl = Deco.Label("Lbl", c.box, "TIP", 16, new Vector2(300, 22), Vector2.zero, TextAlignmentOptions.Center);
            lbl.characterSpacing = 14f;
            lbl.rectTransform.anchorMin = lbl.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            lbl.rectTransform.anchoredPosition = new Vector2(0, -26);
            var rule = Deco.Divider(c.box, 140, Vector2.zero, 0.7f);
            rule.anchorMin = rule.anchorMax = new Vector2(0.5f, 1f);
            rule.anchoredPosition = new Vector2(0, -44);
            c.text = UiKit.Text("Text", c.box, "", 25, Palette.Cream, UiKit.Body, TextAlignmentOptions.Center, new Vector2(300, 130));
            c.text.textWrappingMode = TextWrappingModes.Normal;
            c.text.richText = true;
            c.text.rectTransform.anchorMin = Vector2.zero; c.text.rectTransform.anchorMax = Vector2.one;
            c.text.rectTransform.sizeDelta = new Vector2(-48, -70);
            c.text.rectTransform.anchoredPosition = new Vector2(0, -22);
            c.arrow = UiKit.Image("Marker", rt, UiKit.Ring, Palette.Hex(0xFFC857), new Vector2(64, 64)).rectTransform;
            UiKit.Image("Halo", c.arrow, UiKit.SoftCircle, new Color(1f, 0.8f, 0.4f, 0.35f), new Vector2(110, 110)).transform.SetAsFirstSibling();
            UiKit.Image("Dot", c.arrow, UiKit.Circle, Palette.Hex(0xFFC857), new Vector2(16, 16));
            UiKit.Image("Pointer", c.arrow, Deco.Diamond, Color.white, new Vector2(26, 34), new Vector2(0, 56));
            c.arrow.anchorMin = c.arrow.anchorMax = Vector2.zero;
            c.group.alpha = 0f;
            runner.OnEvent += c.OnSimEvent;
            return c;
        }

        public void BeginShift(ShiftDef def)
        {
            queue.Clear();
            current = null;
            shown = 0f;
            monday = def.Beat == "monday";
            step = monday ? "board" : "";
            if (monday) Push(BoardTip());
            else if (def.Beat == "saturday") step = "ocean";
            if (def.Floors.Length >= 8)
                Push(Simple("zoom", "Guests looking small? <b>Scroll</b> (or press <b>Z</b>) to zoom in. The camera follows the car.", null, 6.5f,
                    pad: "Guests looking small? Hold <b>RT</b> to zoom in. The camera follows the car.",
                    keys: "Guests looking small? Press <b>Z</b> to zoom in. The camera follows the car."));
        }

        void Push(Tip t)
        {
            if (t.Once && SaveData.Current.HintSeen(t.Id) && !monday) return;
            foreach (var q in queue) if (q.Id == t.Id) return;
            if (current != null && current.Id == t.Id) return;
            queue.Enqueue(t);
        }

        // ---------------------------------------------------------------- tips

        Vector3? FirstWaiting()
        {
            var sim = runner.Sim;
            if (sim == null || !sim.Car.IsOpen) return null;
            var q = sim.Waiting[(int)sim.DockedFloor];
            if (q.Count == 0) return null;
            var v = runner.ViewOf(q[0].Id);
            return v ? v.BubbleAnchor + Vector3.up * 0.9f : (Vector3?)null;
        }

        Vector3? FloorOfFirstRider()
        {
            var sim = runner.Sim;
            if (sim == null || sim.Car.Riders.Count == 0) return null;
            var f = runner.Building[sim.Car.Riders[0].Dest];
            return f.transform.position + new Vector3(-3.6f, 2.6f, Layout.FrontZ);
        }

        Tip BoardTip() => new Tip
        {
            Id = "board", Text = "A guest! <b>Click them</b> to let them in. (Space boards everyone.)",
            Pad = "A guest! Press <b>A</b> to let them in. (<b>LB/RB</b> picks one guest.)",
            Keys = "A guest! Press <b>Enter</b> to let them in. (<b>Left/Right</b> picks one guest.)",
            World = FirstWaiting, Done = () => runner.Sim.Car.Riders.Count > 0, MaxTime = 999f, Once = false,
        };

        Tip SendTip() => new Tip
        {
            Id = "send", Text = "Their bubble shows where they're going. <b>Click that floor</b>, or press its number.",
            Pad = "Their bubble shows where they're going. Pick that floor with <b>up/down</b> and press <b>A</b>.",
            Keys = "Their bubble shows where they're going. Pick that floor with <b>Up/Down</b> and press <b>Enter</b>, or press its number.",
            World = FloorOfFirstRider, Done = () => runner.Sim.Car.State != CarState.Docked && runner.Sim.Car.State != CarState.Opening, MaxTime = 999f, Once = false,
        };

        Tip ShuffleTip() => new Tip
        {
            Id = "shuffle", Text = "Every stop, <b>the building rearranges itself</b>. This shows what moves next.",
            Ui = () => runner.Hud.Panel.ForecastAnchor, MinTime = 4.5f, MaxTime = 6.5f, Once = false,
        };

        Tip ChipTip() => new Tip
        {
            Id = "chips", Text = "The tags on the floor names show <b>where each floor lands</b> at your next stop.",
            Ui = () => runner.Hud.FloorLabels.FirstChip, MinTime = 4f, MaxTime = 6f, Once = false,
        };

        Tip Simple(string id, string text, Func<Vector3?> world = null, float dur = 5.5f, string pad = null, string keys = null) => new Tip
        {
            Id = id, Text = text, World = world, MinTime = 3.5f, MaxTime = dur, Pad = pad, Keys = keys,
        };

        Vector3? PassengerTarget(int pid)
        {
            var v = runner.ViewOf(pid);
            return v ? v.BubbleAnchor + Vector3.up * 0.9f : (Vector3?)null;
        }

        Vector3? FloorTarget(FloorId f) => runner.Building[f].transform.position + new Vector3(3.8f, 2.6f, Layout.FrontZ);

        void OnSimEvent(SimEvent e)
        {
            if (runner.Attract) return;
            var sim = runner.Sim;
            switch (e.Type)
            {
                case Ev.Boarded:
                    if (monday && step == "board") { step = "send"; Push(SendTip()); }
                    var p = sim.Find(e.Pid);
                    if (p != null && p.Kind == Kind.Kid)
                        Push(Simple("kid", "The kid pressed <b>every button</b>: you'll stop at every floor on the way.", () => PassengerTarget(e.Pid)));
                    if (p != null && p.Kind == Kind.Tycoon)
                        Push(Simple("tycoon", "Tycoons go <b>express</b>. Take them straight to their floor for a huge tip.", () => PassengerTarget(e.Pid)));
                    if (sim.Car.Has(Kind.Kid) && sim.Car.Has(Kind.Vampire))
                        Push(Simple("vampkid", "Careful: with a kid aboard you stop at <b>every</b> floor, sunny ones included. Hover a floor to preview the trip.",
                            pad: "Careful: with a kid aboard you stop at <b>every</b> floor, sunny ones included. Pick a floor to preview the trip.",
                            keys: "Careful: with a kid aboard you stop at <b>every</b> floor, sunny ones included. Pick a floor to preview the trip."));
                    break;
                case Ev.Arrived:
                    if (monday && step == "send") step = "shuffle-wait";
                    break;
                case Ev.Shuffled:
                    if (monday && step == "shuffle-wait")
                    {
                        step = "tips";
                        Push(ShuffleTip());
                        if (SaveData.Current.ShowForecast) Push(ChipTip());
                    }
                    break;
                case Ev.Delivered:
                    if (monday && step == "tips")
                    {
                        step = "done";
                        Push(new Tip { Id = "tipsplate", Text = "Tips! Quick service pays more, and every delivery grows your <b>streak</b>.",
                                       Ui = () => runner.Hud.TipsAnchor, MinTime = 4f, MaxTime = 6f, Once = false });
                        SaveData.Current.MarkHint("board");
                    }
                    break;
                case Ev.BoardRefused:
                    if (e.Aux == (int)BoardResult.Full)
                        Push(Simple("full", "The car is full. Drop someone off first, or <b>right-click a rider</b> to let them off here.", () => PassengerTarget(e.Pid),
                            pad: "The car is full. Drop someone off first, or pick a rider and press <b>X</b> to let them off here.",
                            keys: "The car is full. Drop someone off first, or pick a rider and press <b>F</b> to let them off here."));
                    else
                        Push(Simple("conflict", "Vampires and mirrors <b>won't ride together</b>.", () => PassengerTarget(e.Pid)));
                    break;
                case Ev.NeedsSun:
                    Push(Simple("needsun", "Plants won't get off until they've had <b>sun</b>. Stop at a sunny floor first.", () => PassengerTarget(e.Pid)));
                    break;
                case Ev.Poofed:
                    Push(Simple("poof", "Sunlight! Keep vampires away from <b>sunny floors</b>."));
                    break;
                case Ev.LeavingScheduled:
                    if (e.Floor != FloorId.Ocean)
                        Push(Simple("leaving", "That floor is <b>leaving the building</b>! Couriers must get there first.", () => FloorTarget(e.Floor)));
                    break;
                case Ev.Jammed:
                    Push(Simple("jam", "JAMMED! The floor you're docked at <b>can't move</b>. Docking can protect a floor."));
                    break;
                case Ev.FloorDeparted:
                    if (e.Floor2 == FloorId.Ocean)
                        Push(Simple("ocean", "The Ocean dropped in! Get swimmers to the <b>Lobby</b> before the tide goes out.", () => FloorTarget(FloorId.Ocean), 6.5f));
                    break;
                case Ev.Fuming:
                    Push(Simple("fuming", "Out of patience! They'll storm off at the next stop."));
                    break;
            }
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            float dt = UiTime.Dt;
            if (runner == null || runner.Sim == null || runner.Attract || runner.Paused || runner.Sim.Ended)
            {
                alpha = Mathf.MoveTowards(alpha, 0f, dt * 4f);
                group.alpha = alpha;
                return;
            }
            if (current == null && queue.Count > 0)
            {
                current = queue.Dequeue();
                shown = 0f;
                if (current.Once) SaveData.Current.MarkHint(current.Id);
            }
            if (current != null)
            {
                shown += dt;
                bool done = shown >= current.MaxTime || (shown >= current.MinTime && current.Done != null && current.Done());
                if (current.Done == null && shown >= current.MinTime && queue.Count > 0) done = true;
                if (done) { current = null; }
            }
            alpha = Mathf.MoveTowards(alpha, current != null ? 1f : 0f, dt * 4f);
            group.alpha = alpha;
            if (current == null) return;
            text.text = current.For();

            // position: above the world target, or beside the UI target, else top-centre of the play area
            Vector2 anchorPos;
            bool hasArrow = false;
            float side = 0f; // UI targets: the marker sits beside the panel and points at it (-1 left of it, +1 right)
            var size = canvasRt.rect.size;
            Vector3? w = current.World?.Invoke();
            RectTransform ui = current.Ui?.Invoke();
            var cam = runner.Cam;
            var canvasCam = GetComponentInParent<Canvas>().worldCamera;
            if (w.HasValue)
            {
                var sp = cam.WorldToScreenPoint(w.Value);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, canvasCam, out var lp);
                anchorPos = lp + size * 0.5f;
                hasArrow = true;
            }
            else if (ui != null)
            {
                var sp = RectTransformUtility.WorldToScreenPoint(canvasCam, ui.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, canvasCam, out var lp);
                // beside the target rather than on it, so the marker never covers the text it points at
                side = lp.x > 0f ? -1f : 1f;
                anchorPos = lp + size * 0.5f + new Vector2(side * (ui.rect.width * 0.5f + 58f), 0f);
                hasArrow = true;
            }
            else anchorPos = new Vector2(size.x * 0.5f, size.y * 0.78f);

            float bob = Mathf.Sin(UiTime.Now * 5f) * 8f;
            // the tip lives in the free space of the left column, clear of the HUD card; only the marker goes on the target
            var boxPos = new Vector2(218f, Mathf.Min(size.y * 0.43f, size.y - Hud.CardBottom - 28f));
            box.anchoredPosition = boxPos + new Vector2(0, bob * 0.15f);
            arrow.gameObject.SetActive(hasArrow);
            arrow.anchoredPosition = anchorPos + (side != 0f ? new Vector2(-side * bob * 0.6f, 0f) : new Vector2(0, bob * 0.6f));
            arrow.localRotation = Quaternion.Euler(0f, 0f, side * 90f);
            arrow.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(UiTime.Now * 6f));
            box.localScale = Vector3.one * (0.9f + 0.1f * Ease.OutBack(Mathf.Clamp01(shown * 4f), 2f));
        }
    }
}

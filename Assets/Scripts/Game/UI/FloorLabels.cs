using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// Screen-space floor labels in the gutter left of the tower: the floor's name, its icon and slot number, how
    /// many guests wait there, and a countdown when it's leaving. The in-world nameplates are only a few pixels
    /// tall with nine floors on screen, so these carry the names at full zoom-out. They follow each floor through
    /// shuffles and fade out in the close-up, where the plates are big enough. This also tells the camera how
    /// much room the HUD, the gutter and the panel leave for the tower.
    /// Each label also carries the shuffle forecast: a cream chip (the colour of the panel's forecast cards) with
    /// the slot the floor lands in at the next stop, or JAM on a floor the next card names when the car is
    /// about to dock there. It follows the hovered or picked floor, else the car's target, else assumes the car
    /// stops somewhere the card leaves alone. The floor it assumes you dock at is marked in the chip's place:
    /// a brass STOP for the car's target, a dark STOP? for a floor you're only pointing at.
    /// </summary>
    public sealed class FloorLabels : MonoBehaviour
    {
        /// <summary>Canvas units the labels need left of the tower (widest label plus gaps).</summary>
        public const float Gutter = 300f;
        // the HUD card ends at x = 28 + 360 (Hud.Build); the panel is 400 wide, 30 in from the right (PanelUi.Create)
        const float HudRight = 400f, PanelLeftInset = 440f;
        const float ChipW = 72f, ChipH = 30f;

        ShiftRunner runner;
        Camera worldCam;
        RectTransform canvasRt, root;
        CanvasGroup group;
        readonly List<Label> labels = new List<Label>();

        sealed class Label
        {
            public RectTransform Rt;
            public CanvasGroup Group;
            public Image Bg, Disc, Icon, Tag;
            public WaitBadge Badge;
            public TextMeshProUGUI Name, Number, TagText;
            public FloorId Shown = (FloorId)(-1);
            public RectTransform Chip;
            public Image ChipBg, ChipArrow;
            public TextMeshProUGUI ChipText;
            public string ChipShown = "";
            public bool Moving;
            public float ChipPop;
        }

        public static FloorLabels Create(Transform parent, ShiftRunner runner, Camera worldCam, RectTransform canvasRt)
        {
            var rt = UiKit.Stretch("FloorLabels", parent);
            rt.SetAsFirstSibling(); // under bubbles, popups and the HUD cards
            var l = rt.gameObject.AddComponent<FloorLabels>();
            l.runner = runner;
            l.worldCam = worldCam;
            l.canvasRt = canvasRt;
            l.root = rt;
            l.group = rt.gameObject.AddComponent<CanvasGroup>();
            l.group.blocksRaycasts = false;
            l.group.interactable = false;
            return l;
        }

        Camera CanvasCam => GetComponentInParent<Canvas>().worldCamera;

        Label Get(int i)
        {
            while (labels.Count <= i)
            {
                var rt = UiKit.Rect("Floor" + labels.Count, root, Vector2.zero, Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(288, 44));
                var bg = UiKit.Image("Bg", rt, UiKit.Pill, new Color(0.11f, 0.08f, 0.13f, 0.86f), new Vector2(288, 44));
                bg.type = Image.Type.Sliced;
                Deco.Fill(bg.rectTransform);
                var disc = UiKit.Image("Disc", rt, UiKit.Circle, Palette.Brass, new Vector2(30, 30), new Vector2(-19, 0));
                disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                var num = UiKit.Text("Num", disc.transform, "1", 18, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(30, 30));
                var icon = UiKit.Image("Icon", rt, null, Color.white, new Vector2(28, 28), new Vector2(-52, 0));
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                icon.preserveAspect = true;
                var name = UiKit.Text("Name", rt, "", 28, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(206, 38));
                name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                name.rectTransform.pivot = new Vector2(1f, 0.5f);
                name.rectTransform.anchoredPosition = new Vector2(-72, 0);
                name.enableAutoSizing = true;
                name.fontSizeMin = 20;
                name.fontSizeMax = 28;
                // guests waiting: a count on the pill's top-left corner, like the panel's buttons, ringed with the
                // patience of the most impatient one
                var badge = WaitBadge.Create(rt, new Vector2(8, -4));
                badge.Root.anchorMin = badge.Root.anchorMax = new Vector2(0f, 1f);
                // leaving: a countdown tag hanging under the pill
                var tag = UiKit.Image("Tag", rt, UiKit.Pill, Palette.Warn, new Vector2(120, 24));
                tag.type = Image.Type.Sliced;
                tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(1f, 0f);
                tag.rectTransform.pivot = new Vector2(1f, 1f);
                tag.rectTransform.anchoredPosition = new Vector2(-8, 4);
                var tt = UiKit.Text("Left", tag.transform, "", 15, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(120, 24));
                Deco.Fill(tt.rectTransform);
                // the forecast: a chip sitting on the pill's top edge above the slot disc
                var chip = UiKit.Rect("Forecast", rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-2f, -10f), new Vector2(ChipW, ChipH));
                var chipBg = UiKit.Image("Bg", chip, UiKit.Pill, Palette.Cream, new Vector2(ChipW, ChipH));
                chipBg.type = Image.Type.Sliced;
                Deco.Fill(chipBg.rectTransform);
                var arrow = UiKit.Image("Arrow", chip, UiKit.Triangle, Palette.Ink, new Vector2(17, 14), new Vector2(-15f, 0f));
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var ct = UiKit.Text("Slot", chip, "", 22, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(36, ChipH), new Vector2(11f, 0f));
                chip.gameObject.SetActive(false);
                labels.Add(new Label { Group = rt.gameObject.AddComponent<CanvasGroup>(), Rt = rt, Bg = bg, Disc = disc, Icon = icon, Badge = badge, Tag = tag, Name = name, Number = num, TagText = tt,
                                       Chip = chip, ChipBg = chipBg, ChipArrow = arrow, ChipText = ct });
            }
            return labels[i];
        }

        public void Tick(float dt)
        {
            var sim = runner.Sim;
            var rig = runner.Rig;
            float w = canvasRt.rect.width;
            if (w > 1f && rig != null)
                rig.PlayBand = new Vector2((HudRight + Gutter) / w, (w - PanelLeftInset) / w);
            if (rig != null) rig.RoomForPrompts = runner.CursorMode;
            // hidden in the close-up and in a scripted push-in, where the in-world plates are big enough
            bool on = sim != null && !sim.Ended && !runner.Attract && rig != null && rig.ZoomShown < 0.35f && !rig.PushedIn;
            group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, dt * 5f);
            if (sim == null || runner.Building == null) return;

            var size = canvasRt.rect.size;
            var cam = CanvasCam;
            int n = sim.B.Count;
            // where the next card puts every floor, for the stop the player is looking at
            Building after = null;
            bool jam = false;
            FloorId? dock = PreviewDock(sim);
            if (SaveData.Current.ShowForecast && !sim.Ended) after = sim.PreviewStop(dock, out jam);
            PreviewedDock = dock;
            // a floor the player is pointing at that the car isn't already heading for: a what-if
            WhatIf = dock.HasValue && dock == runner.HoverFloor && dock != sim.Car.Target;
            for (int slot = 0; slot < n; slot++)
            {
                var f = sim.B.At(slot);
                var view = runner.Building[f];
                var l = Get(slot);
                l.Rt.gameObject.SetActive(view != null);
                if (view == null) continue;
                if (l.Shown != f)
                {
                    l.Shown = f;
                    l.Name.text = Defs.Floor(f).Name.ToUpperInvariant();
                    l.Icon.sprite = Icons.Floor(f);
                    l.Icon.enabled = l.Icon.sprite != null;
                    l.Bg.color = Color.Lerp(new Color(0.11f, 0.08f, 0.13f, 0.88f), Palette.Floor(f), 0.22f);
                }
                l.Number.text = (slot + 1).ToString();
                // floors crossing in a shuffle drag their labels through each other: fade them until they land
                l.Group.alpha = Mathf.MoveTowards(l.Group.alpha, view.Moving ? 0.25f : 1f, dt * 6f);
                l.Moving = view.Moving;

                // right edge of the label just left of the floor's left wall, at mid-height, wherever the floor is now
                var anchor = view.Content.position + new Vector3(-Layout.HalfWidth - 0.9f, Layout.SlotHeight * 0.45f, Layout.FrontZ);
                var sp = worldCam.WorldToScreenPoint(anchor);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, cam, out var lp);
                l.Rt.anchoredPosition = lp + size * 0.5f + new Vector2(-10f, 0f);

                int waiting = sim.Waiting[(int)f].Count;
                bool leaving = sim.B.IsLeaving(f);
                l.Badge.Show(waiting, sim.LowestWaitingPatience(f));
                l.Tag.gameObject.SetActive(leaving);
                if (leaving)
                {
                    int stops = sim.B.Leaving[(int)f];
                    string t = f == FloorId.Ocean ? (stops <= 1 ? "TIDE OUT NEXT STOP" : $"TIDE OUT IN {stops}") : (stops <= 1 ? "LEAVING NEXT STOP" : $"LEAVING IN {stops}");
                    if (l.TagText.text != t)
                    {
                        l.TagText.text = t;
                        l.Tag.rectTransform.sizeDelta = new Vector2(l.TagText.GetPreferredValues(t).x + 20f, 24f);
                    }
                }

                string chip = "";
                int to = after != null ? after.SlotOf(f) : -1;
                if (after != null && jam && dock == f) chip = "JAM";
                else if (to >= 0 && to != slot) chip = (to > slot ? "+" : "-") + (to + 1);
                else if (after != null && dock == f && to == slot) chip = WhatIf ? StopIf : Stop;
                ShowChip(l, chip, dt);
            }
            for (int i = n; i < labels.Count; i++) labels[i].Rt.gameObject.SetActive(false);
        }

        /// <summary>
        /// The stop the forecast chips describe: the floor being hovered or picked (unless it's the one the car is
        /// already docked at), else the car's target, else none.
        /// </summary>
        FloorId? PreviewDock(ShiftSim sim)
        {
            var h = runner.HoverFloor;
            if (h.HasValue && sim.B.Has(h.Value) && (sim.Car.Target.HasValue || h.Value != sim.DockedFloor)) return h;
            return sim.Car.Target;
        }

        public const string Stop = "STOP", StopIf = "STOP?";
        static readonly Color StopIfBg = Palette.Hex(0x3B2A40), Gold = Palette.Hex(0xFFC857);

        void ShowChip(Label l, string chip, float dt)
        {
            bool on = chip.Length > 0;
            if (chip != l.ChipShown)
            {
                l.ChipShown = chip;
                l.Chip.gameObject.SetActive(on);
                if (on)
                {
                    // a move ("+9", "-2") gets an arrow and a slot number; JAM, STOP and STOP? are words
                    bool move = chip[0] == '+' || chip[0] == '-';
                    l.ChipText.text = move ? chip.Substring(1) : chip;
                    l.ChipArrow.gameObject.SetActive(move);
                    l.ChipArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, chip[0] == '-' ? 180f : 0f);
                    l.ChipBg.color = chip == "JAM" ? Palette.Bad : chip == Stop ? Palette.Brass : chip == StopIf ? StopIfBg : Palette.Cream;
                    l.ChipText.color = chip == "JAM" ? Color.white : chip == StopIf ? Gold : Palette.Ink;
                    float w = move ? ChipW : l.ChipText.GetPreferredValues(chip).x + 24f;
                    l.Chip.sizeDelta = new Vector2(w, ChipH);
                    l.ChipText.rectTransform.sizeDelta = new Vector2(move ? 36f : w, ChipH);
                    l.ChipText.rectTransform.anchoredPosition = new Vector2(move ? 11f : 0f, 0f);
                    l.ChipPop = SaveData.Current.ReducedMotion ? 0f : 1f;
                }
            }
            if (!on) return;
            l.ChipPop = Mathf.MoveTowards(l.ChipPop, 0f, dt * 5f);
            l.Chip.localScale = Vector3.one * (1f + 0.25f * Ease.OutCubic(l.ChipPop));
        }

        /// <summary>Whether the labels are on screen (they fade out in the close-up and the ocean push-in).</summary>
        public bool Showing => group.alpha > 0.02f;
        public float Alpha => group.alpha;

        /// <summary>Canvas x of the resting labels' left edge, including the waiting badge that pokes out of a pill's
        /// corner (+infinity before any are up; the last value while every floor is mid-shuffle).</summary>
        public float LeftEdge
        {
            get
            {
                // labels of floors mid-shuffle slide about faded; the resting ones all share the tower's wall
                float x = float.PositiveInfinity;
                foreach (var l in labels)
                    if (l.Rt.gameObject.activeSelf && !l.Moving) x = Mathf.Min(x, l.Rt.anchoredPosition.x - l.Rt.rect.width - BadgeOverhang);
                if (!float.IsPositiveInfinity(x)) restLeft = x;
                return restLeft;
            }
        }
        // the badge's ring (26 + 12 across) is centred 8 in from the pill's left edge
        const float BadgeOverhang = 11f;
        float restLeft = float.PositiveInfinity;

        /// <summary>Self-test: the stop the chips were last worked out for, and whether it's only being pointed at.</summary>
        public FloorId? PreviewedDock { get; private set; }
        public bool WhatIf { get; private set; }

        /// <summary>Self-test: the forecast chip on the label for a slot: "" for none, "JAM", "STOP", "STOP?", or "+9" / "-2" (up or down to that slot).</summary>
        public string ChipAt(int slot) => slot >= 0 && slot < labels.Count && labels[slot].Rt.gameObject.activeSelf ? labels[slot].ChipShown : "";

        /// <summary>Self-test: the label's pill for a slot in screen pixels (null if hidden, or mid-shuffle when
        /// <paramref name="resting"/>).</summary>
        public Rect? ScreenRectAt(int slot, bool resting = false)
        {
            if (slot < 0 || slot >= labels.Count || !labels[slot].Rt.gameObject.activeInHierarchy) return null;
            if (resting && labels[slot].Moving) return null;
            return UiKit.ScreenRect(labels[slot].Bg.rectTransform, CanvasCam);
        }

        /// <summary>Self-test: the first visible forecast chip (for the coach to point at).</summary>
        public RectTransform FirstChip
        {
            get
            {
                foreach (var l in labels) if (l.Rt.gameObject.activeSelf && l.ChipShown.Length > 0 && !l.ChipShown.StartsWith(Stop)) return l.Chip;
                return null;
            }
        }

        /// <summary>Self-test: the waiting badge on the label for a slot (null if that label isn't built).</summary>
        public WaitBadge BadgeAt(int slot) => slot >= 0 && slot < labels.Count ? labels[slot].Badge : null;
    }

    /// <summary>
    /// How many guests wait on a floor, with a ring for the patience of the most impatient one, in the colours of
    /// the guests' own rings. The badge is dark while everyone's calm, amber under half and red (pulsing) under a
    /// quarter, so a floor about to lose someone stands out in the label column and on the panel.
    /// </summary>
    public sealed class WaitBadge
    {
        public RectTransform Root;
        public Image Disc, Track, Ring;
        public TextMeshProUGUI Count;
        public static readonly Color Calm = Palette.Hex(0x3B2A40);

        public static WaitBadge Create(Transform parent, Vector2 pos, float size = 26f)
        {
            var w = new WaitBadge();
            w.Root = UiKit.Rect("WaitBadge", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            float ring = size + 12f;
            w.Track = UiKit.Image("Track", w.Root, UiKit.Ring, new Color(0.08f, 0.05f, 0.1f, 0.85f), new Vector2(ring, ring));
            w.Ring = UiKit.Image("Ring", w.Root, UiKit.Ring, Palette.Good, new Vector2(ring, ring));
            w.Ring.type = Image.Type.Filled;
            w.Ring.fillMethod = Image.FillMethod.Radial360;
            w.Ring.fillOrigin = (int)Image.Origin360.Top;
            w.Ring.fillClockwise = false;
            w.Disc = UiKit.Image("Disc", w.Root, UiKit.Circle, Calm, new Vector2(size, size));
            UiKit.Image("Rim", w.Disc.transform, UiKit.Ring, new Color(1f, 0.89f, 0.66f, 0.9f), new Vector2(size, size));
            w.Count = UiKit.Text("Count", w.Disc.transform, "", 15, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(size, size));
            return w;
        }

        /// <summary>The colour the disc is showing (self-test).</summary>
        public Color Shown => Disc.color;
        public bool Visible => Root.gameObject.activeSelf;

        public void Show(int waiting, float lowestPatience)
        {
            Root.gameObject.SetActive(waiting > 0);
            if (waiting <= 0) return;
            float frac = Mathf.Clamp01(lowestPatience);
            Count.text = waiting.ToString();
            Ring.fillAmount = frac;
            Ring.color = Palette.Patience(frac);
            Disc.color = frac > 0.5f ? Calm : Palette.Patience(frac);
            Count.color = frac > 0.5f ? Palette.Cream : frac > 0.25f ? Palette.Ink : Color.white;
            float pulse = frac <= 0.25f && !SaveData.Current.ReducedMotion ? 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f)) : 0f;
            Root.localScale = Vector3.one * (1f + pulse);
        }
    }
}

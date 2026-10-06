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
    /// </summary>
    public sealed class FloorLabels : MonoBehaviour
    {
        /// <summary>Canvas units the labels need left of the tower (widest label plus gaps).</summary>
        public const float Gutter = 300f;
        // the HUD card ends at x = 28 + 360 (Hud.Build); the panel is 400 wide, 30 in from the right (PanelUi.Create)
        const float HudRight = 400f, PanelLeftInset = 440f;

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
                labels.Add(new Label { Group = rt.gameObject.AddComponent<CanvasGroup>(), Rt = rt, Bg = bg, Disc = disc, Icon = icon, Badge = badge, Tag = tag, Name = name, Number = num, TagText = tt });
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
            bool on = sim != null && !sim.Ended && !runner.Attract && rig != null && rig.ZoomShown < 0.35f;
            group.alpha = Mathf.MoveTowards(group.alpha, on ? 1f : 0f, dt * 5f);
            if (sim == null || runner.Building == null) return;

            var size = canvasRt.rect.size;
            var cam = CanvasCam;
            int n = sim.B.Count;
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
            }
            for (int i = n; i < labels.Count; i++) labels[i].Rt.gameObject.SetActive(false);
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

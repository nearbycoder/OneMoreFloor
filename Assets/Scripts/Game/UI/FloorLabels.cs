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
        public const float Gutter = 266f;
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
            public Image Bg, Disc, Icon, Badge, Tag;
            public TextMeshProUGUI Name, Number, BadgeText, TagText;
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
                var rt = UiKit.Rect("Floor" + labels.Count, root, Vector2.zero, Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(254, 40));
                var bg = UiKit.Image("Bg", rt, UiKit.Pill, new Color(0.11f, 0.08f, 0.13f, 0.86f), new Vector2(254, 40));
                bg.type = Image.Type.Sliced;
                Deco.Fill(bg.rectTransform);
                var disc = UiKit.Image("Disc", rt, UiKit.Circle, Palette.Brass, new Vector2(30, 30), new Vector2(-19, 0));
                disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                var num = UiKit.Text("Num", disc.transform, "1", 18, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(30, 30));
                var icon = UiKit.Image("Icon", rt, null, Color.white, new Vector2(28, 28), new Vector2(-52, 0));
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                icon.preserveAspect = true;
                var name = UiKit.Text("Name", rt, "", 24, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(174, 34));
                name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                name.rectTransform.pivot = new Vector2(1f, 0.5f);
                name.rectTransform.anchoredPosition = new Vector2(-72, 0);
                name.enableAutoSizing = true;
                name.fontSizeMin = 16;
                name.fontSizeMax = 24;
                // guests waiting: a count on the pill's top-left corner, like the panel's buttons
                var badge = UiKit.Image("Badge", rt, UiKit.Circle, Palette.Bad, new Vector2(26, 26));
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
                badge.rectTransform.anchoredPosition = new Vector2(8, -4);
                var bt = UiKit.Text("Count", badge.transform, "", 15, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(26, 26));
                // leaving: a countdown tag hanging under the pill
                var tag = UiKit.Image("Tag", rt, UiKit.Pill, Palette.Warn, new Vector2(120, 24));
                tag.type = Image.Type.Sliced;
                tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(1f, 0f);
                tag.rectTransform.pivot = new Vector2(1f, 1f);
                tag.rectTransform.anchoredPosition = new Vector2(-8, 4);
                var tt = UiKit.Text("Left", tag.transform, "", 15, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(120, 24));
                Deco.Fill(tt.rectTransform);
                labels.Add(new Label { Rt = rt, Bg = bg, Disc = disc, Icon = icon, Badge = badge, Tag = tag, Name = name, Number = num, BadgeText = bt, TagText = tt });
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
            bool on = sim != null && !runner.Attract && rig != null && rig.ZoomShown < 0.35f;
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

                // right edge of the label just left of the floor's left wall, at mid-height, wherever the floor is now
                var anchor = view.Content.position + new Vector3(-Layout.HalfWidth - 0.9f, Layout.SlotHeight * 0.45f, Layout.FrontZ);
                var sp = worldCam.WorldToScreenPoint(anchor);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, cam, out var lp);
                l.Rt.anchoredPosition = lp + size * 0.5f + new Vector2(-10f, 0f);

                int waiting = sim.Waiting[(int)f].Count;
                bool leaving = sim.B.IsLeaving(f);
                l.Badge.gameObject.SetActive(waiting > 0);
                if (waiting > 0) l.BadgeText.text = waiting.ToString();
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
    }
}

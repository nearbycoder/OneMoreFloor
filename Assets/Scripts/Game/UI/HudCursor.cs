using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// The parts of the HUD that depend on how you play:
    ///  - gamepad / arrow keys: gold brackets on the selected floor, a marker over the selected guest, and a
    ///    strip of button prompts that says what each button will do right now;
    ///  - zoomed in: an indicator at the top or bottom edge for every floor out of view with guests waiting
    ///    (where it is, how many, how impatient). Click one to send the car there.
    /// </summary>
    public sealed class HudCursor : MonoBehaviour
    {
        ShiftRunner runner;
        Camera worldCam;
        RectTransform canvasRt, root;
        readonly Image[] brackets = new Image[8];
        RectTransform guestMark;
        RectTransform prompts;
        readonly List<Prompt> promptItems = new List<Prompt>();
        readonly List<Edge> edges = new List<Edge>();
        readonly List<(string key, Color col, string label)> items = new List<(string, Color, string)>();
        readonly List<int> above = new List<int>(), below = new List<int>();
        float t;

        sealed class Prompt
        {
            public RectTransform Rt;
            public Image Glyph;
            public TextMeshProUGUI Key, Label;
            public string ShownKey, ShownLabel;
            public float Width;
        }

        sealed class Edge
        {
            public RectTransform Rt;
            public Image Arrow, Disc, Icon, Ring;
            public TextMeshProUGUI Count;
            public int Slot;
        }

        /// <summary>Forwards clicks on an edge indicator to the runner.</summary>
        sealed class EdgeClick : MonoBehaviour, IPointerClickHandler
        {
            public System.Action Click;
            public void OnPointerClick(PointerEventData e) => Click?.Invoke();
        }

        public static HudCursor Create(Transform parent, ShiftRunner runner, Camera worldCam, RectTransform canvasRt)
        {
            var rt = UiKit.Stretch("Cursor", parent);
            var c = rt.gameObject.AddComponent<HudCursor>();
            c.runner = runner;
            c.worldCam = worldCam;
            c.canvasRt = canvasRt;
            c.root = rt;
            c.Build();
            return c;
        }

        void Build()
        {
            var gold = Palette.Hex(0xFFC857);
            for (int i = 0; i < brackets.Length; i++)
            {
                brackets[i] = UiKit.Image("Bracket" + i, root, null, gold, new Vector2(6, 6));
                brackets[i].rectTransform.anchorMin = brackets[i].rectTransform.anchorMax = Vector2.zero;
            }
            guestMark = UiKit.Image("GuestMark", root, UiKit.Triangle, gold, new Vector2(34, 28)).rectTransform;
            guestMark.localRotation = Quaternion.Euler(0, 0, 180);
            guestMark.anchorMin = guestMark.anchorMax = Vector2.zero;
            UiKit.Image("Halo", guestMark, UiKit.SoftCircle, new Color(1f, 0.8f, 0.4f, 0.35f), new Vector2(70, 70)).transform.SetAsFirstSibling();

            prompts = UiKit.Rect("Prompts", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(1100, 56));
            var bg = UiKit.Image("Bg", prompts, UiKit.Pill, new Color(0.11f, 0.08f, 0.13f, 0.82f), new Vector2(1100, 56));
            bg.type = Image.Type.Sliced;
        }

        public void Tick()
        {
            var sim = runner.Sim;
            t += UiTime.Dt;
            bool cursor = runner.CursorMode && sim != null && !sim.Ended && !runner.Paused;
            UpdateBrackets(cursor);
            UpdateGuestMark(cursor);
            UpdatePrompts(cursor);
            UpdateEdges();
        }

        Vector2 ToCanvas(Vector3 world, out bool behind)
        {
            var screen = worldCam.WorldToScreenPoint(world);
            behind = screen.z < 0f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
            return local + canvasRt.rect.size * 0.5f;
        }

        Camera CanvasCam => GetComponentInParent<Canvas>().worldCamera;

        void UpdateBrackets(bool on)
        {
            var sim = runner.Sim;
            on &= runner.CursorSlot >= 0 && runner.CursorSlot < (sim?.B.Count ?? 0);
            foreach (var b in brackets) b.gameObject.SetActive(on);
            if (!on) return;
            var floor = runner.Building[sim.B.At(runner.CursorSlot)].transform.position;
            float x0 = -Layout.HalfWidth - 0.15f, x1 = Layout.HalfWidth + 0.15f;
            var bl = ToCanvas(floor + new Vector3(x0, 0.05f, Layout.FrontZ), out _);
            var tr = ToCanvas(floor + new Vector3(x1, Layout.SlotHeight - 0.1f, Layout.FrontZ), out _);
            var br = ToCanvas(floor + new Vector3(x1, 0.05f, Layout.FrontZ), out _);
            var tl = ToCanvas(floor + new Vector3(x0, Layout.SlotHeight - 0.1f, Layout.FrontZ), out _);
            float pulse = 1f + 0.08f * Mathf.Sin(t * 6f);
            float len = Mathf.Clamp((tr.y - br.y) * 0.32f, 18f, 44f) * pulse, w = 6f;
            void Corner(int i, Vector2 p, float sx, float sy)
            {
                var h = brackets[i].rectTransform;
                h.pivot = new Vector2(sx > 0 ? 0f : 1f, sy > 0 ? 0f : 1f);
                h.sizeDelta = new Vector2(len, w);
                h.anchoredPosition = p;
                var v = brackets[i + 1].rectTransform;
                v.pivot = h.pivot;
                v.sizeDelta = new Vector2(w, len);
                v.anchoredPosition = p;
            }
            Corner(0, bl, 1, 1);
            Corner(2, br, -1, 1);
            Corner(4, tl, 1, -1);
            Corner(6, tr, -1, -1);
        }

        void UpdateGuestMark(bool on)
        {
            var v = on && runner.CursorPid >= 0 ? runner.ViewOf(runner.CursorPid) : null;
            guestMark.gameObject.SetActive(v != null);
            if (v == null) return;
            var p = ToCanvas(v.BubbleAnchor, out _);
            guestMark.anchoredPosition = p + new Vector2(0, 92f + 6f * Mathf.Sin(t * 7f));
        }

        // ---------------------------------------------------------------- prompts

        Prompt GetPrompt(int i)
        {
            while (promptItems.Count <= i)
            {
                var rt = UiKit.Rect("P" + promptItems.Count, prompts, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(200, 44));
                var glyph = UiKit.Image("Glyph", rt, UiKit.Circle, Color.white, new Vector2(38, 38), new Vector2(19, 0));
                var key = UiKit.Text("Key", glyph.transform, "", 19, Palette.Hex(0x1C1820), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(80, 38));
                var label = UiKit.Text("Label", rt, "", 20, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(240, 40));
                label.rectTransform.pivot = new Vector2(0f, 0.5f);
                promptItems.Add(new Prompt { Rt = rt, Glyph = glyph, Key = key, Label = label });
            }
            return promptItems[i];
        }

        static readonly Color PadA = Palette.Hex(0x6CCB5F), PadB = Palette.Hex(0xE5484D), PadX = Palette.Hex(0x3FA9F5), PadY = Palette.Hex(0xFFD23F);
        static readonly Color KeyCap = Palette.Hex(0xF3E6C8), Shoulder = Palette.Hex(0xC9BBA0);

        void UpdatePrompts(bool on)
        {
            prompts.gameObject.SetActive(on);
            if (!on) return;
            var sim = runner.Sim;
            bool pad = Controls.Pad;
            var guest = runner.CursorPid >= 0 ? sim.Find(runner.CursorPid) : null;
            var floor = sim.B.At(runner.CursorSlot);
            string confirm = guest != null
                ? (guest.State == PState.Waiting && sim.Car.IsOpen && guest.At == sim.DockedFloor ? "Let in"
                    : guest.State == PState.Waiting ? "Fetch" : "Go to " + Defs.Floor(guest.Dest).Name)
                : sim.Car.IsOpen && floor == sim.DockedFloor ? "Everyone in" : "Send car";
            items.Clear();
            if (pad)
            {
                items.Add(("A", PadA, confirm));
                if (guest != null && guest.State == PState.Riding && sim.Car.IsOpen) items.Add(("X", PadX, "Let off"));
                items.Add(("Y", PadY, "Everyone in"));
                items.Add(("LB RB", Shoulder, "Guest"));
                if (guest != null) items.Add(("B", PadB, "Unpick"));
                items.Add(("RT", Shoulder, "Zoom"));
            }
            else
            {
                items.Add(("ENTER", KeyCap, confirm));
                if (guest != null && guest.State == PState.Riding && sim.Car.IsOpen) items.Add(("F", KeyCap, "Let off"));
                items.Add(("SPACE", KeyCap, "Everyone in"));
                items.Add(("← →", KeyCap, "Guest"));
                items.Add(("Z", KeyCap, "Zoom"));
            }
            float x = 22f;
            for (int i = 0; i < items.Count; i++)
            {
                var p = GetPrompt(i);
                p.Rt.gameObject.SetActive(true);
                var (key, col, label) = items[i];
                bool round = key.Length == 1 && pad;
                float gw = round ? 38f : Mathf.Max(38f, 16f + key.Length * 15f);
                p.Glyph.sprite = round ? UiKit.Circle : UiKit.Pill;
                p.Glyph.type = round ? Image.Type.Simple : Image.Type.Sliced;
                p.Glyph.color = col;
                p.Glyph.rectTransform.sizeDelta = new Vector2(gw, 38f);
                p.Glyph.rectTransform.anchoredPosition = new Vector2(gw * 0.5f, 0);
                if (p.ShownKey != key || p.ShownLabel != label)
                {
                    // only re-layout text when it changes
                    p.ShownKey = key;
                    p.ShownLabel = label;
                    p.Key.text = key;
                    p.Key.fontSize = key.Length > 2 ? 15 : 19;
                    p.Label.text = label;
                    p.Width = p.Label.GetPreferredValues(label).x;
                }
                p.Key.rectTransform.sizeDelta = new Vector2(gw, 38f);
                float lw = p.Width;
                p.Label.rectTransform.anchoredPosition = new Vector2(gw + 10f, 0);
                p.Label.rectTransform.sizeDelta = new Vector2(lw + 4f, 40f);
                p.Rt.anchoredPosition = new Vector2(x, 0);
                x += gw + 10f + lw + 26f;
            }
            for (int i = items.Count; i < promptItems.Count; i++) promptItems[i].Rt.gameObject.SetActive(false);
            float width = x - 4f;
            prompts.sizeDelta = new Vector2(width, 56f);
            ((RectTransform)prompts.Find("Bg")).sizeDelta = new Vector2(width, 56f);
        }

        // ---------------------------------------------------------------- off-screen floors

        Edge GetEdge(int i)
        {
            while (edges.Count <= i)
            {
                var rt = UiKit.Rect("Edge" + edges.Count, root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 58));
                var bg = UiKit.Image("Bg", rt, UiKit.Pill, new Color(0.11f, 0.08f, 0.13f, 0.88f), new Vector2(150, 58), Vector2.zero, true);
                bg.type = Image.Type.Sliced;
                var e = new Edge { Rt = rt };
                rt.gameObject.AddComponent<EdgeClick>().Click = () => { if (e.Slot >= 0) runner.RequestSend(e.Slot, false); };
                e.Arrow = UiKit.Image("Arrow", rt, UiKit.Triangle, Palette.Hex(0xFFC857), new Vector2(22, 18), new Vector2(-50, 0));
                e.Ring = UiKit.Image("Ring", rt, UiKit.Ring, Palette.Good, new Vector2(48, 48), new Vector2(-8, 0));
                e.Disc = UiKit.Image("Disc", rt, UiKit.Circle, Color.white, new Vector2(38, 38), new Vector2(-8, 0));
                e.Icon = UiKit.Image("Icon", e.Disc.transform, null, Color.white, new Vector2(32, 32));
                e.Icon.preserveAspect = true;
                e.Count = UiKit.Text("Count", rt, "", 22, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(60, 40), new Vector2(46, 0));
                edges.Add(e);
            }
            return edges[i];
        }

        void UpdateEdges()
        {
            var sim = runner.Sim;
            int used = 0;
            if (sim != null && !sim.Ended && !runner.Attract && runner.Rig.ZoomShown > 0.05f)
            {
                float top = canvasRt.rect.height, margin = 40f;
                above.Clear();
                below.Clear();
                for (int slot = 0; slot < sim.B.Count; slot++)
                {
                    var f = sim.B.At(slot);
                    if (sim.Waiting[(int)f].Count == 0) continue;
                    var p = ToCanvas(runner.Building[f].transform.position + new Vector3(0, Layout.SlotHeight * 0.5f, Layout.FrontZ), out _);
                    if (p.y > top - margin) above.Add(slot);
                    else if (p.y < margin) below.Add(slot);
                }
                var mid = ToCanvas(new Vector3(0, runner.Rig.FollowY, Layout.FrontZ), out _);
                void Row(List<int> slots, bool up)
                {
                    // nearest floor next to the centre line, further ones outward
                    slots.Sort((a, b) => up ? a.CompareTo(b) : b.CompareTo(a));
                    for (int i = 0; i < slots.Count; i++)
                    {
                        var e = GetEdge(used++);
                        var f = sim.B.At(slots[i]);
                        var q = sim.Waiting[(int)f];
                        float worst = 1f;
                        foreach (var g in q) worst = Mathf.Min(worst, g.PatienceFrac);
                        e.Slot = slots[i];
                        e.Rt.gameObject.SetActive(true);
                        e.Disc.color = Palette.Floor(f);
                        e.Icon.sprite = Icons.Floor(f);
                        e.Icon.enabled = e.Icon.sprite != null;
                        e.Ring.color = worst > 0.5f ? Palette.Good : worst > 0.25f ? Palette.Warn : Palette.Bad;
                        e.Ring.fillAmount = 1f;
                        e.Count.text = "×" + q.Count;
                        e.Arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, up ? 0 : 180);
                        float pulse = worst < 0.25f ? 1f + 0.06f * Mathf.Sin(t * 12f) : 1f;
                        e.Rt.localScale = Vector3.one * pulse;
                        float x = mid.x + (i - (slots.Count - 1) * 0.5f) * 166f;
                        e.Rt.anchoredPosition = new Vector2(x, up ? top - 44f : 44f + 60f);
                    }
                }
                Row(above, true);
                Row(below, false);
            }
            for (int i = used; i < edges.Count; i++) edges[i].Rt.gameObject.SetActive(false);
        }
    }
}

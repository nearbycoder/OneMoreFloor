using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>In-shift UI: score, streak, clock, complaints, operator panel, bubbles and popups.</summary>
    public sealed class Hud : MonoBehaviour
    {
        ShiftRunner runner;
        Camera worldCam;
        RectTransform canvasRt, bubbleLayer, coinLayer, popupLayer, top;
        TextMeshProUGUI score, streak, clock, clockLabel, shiftName, shiftDay;
        Image streakBadge;
        Image[] complaintSlots = new Image[Tuning.MaxComplaints];
        readonly Dictionary<int, Bubble> bubbles = new Dictionary<int, Bubble>();
        readonly List<Popup> popups = new List<Popup>();
        public PanelUi Panel;
        public RectTransform TipsAnchor { get; private set; }
        int shownScore;
        float scorePunch, streakPunch, clockPunch;
        int lastStreak;

        public static string Code(FloorId f)
        {
            switch (f)
            {
                case FloorId.Lobby: return "LOB";
                case FloorId.Office: return "OFF";
                case FloorId.Library: return "LIB";
                case FloorId.Laundromat: return "WSH";
                case FloorId.Boiler: return "BLR";
                case FloorId.Greenhouse: return "GRN";
                case FloorId.Penthouse: return "PH";
                case FloorId.Crypt: return "CRY";
                case FloorId.Ocean: return "SEA";
                case FloorId.Daycare: return "KID";
            }
            return "?";
        }

        public static Hud Create(Transform canvas, ShiftRunner runner, Camera worldCam)
        {
            var rt = UiKit.Stretch("Hud", canvas);
            var h = rt.gameObject.AddComponent<Hud>();
            h.runner = runner;
            h.worldCam = worldCam;
            h.canvasRt = (RectTransform)canvas;
            h.Build();
            return h;
        }

        const float CardTop = 24f, CardHeight = 524f;
        /// <summary>How far down from the top of the canvas the left HUD card reaches (the coach tip sits below it).</summary>
        public const float CardBottom = CardTop + CardHeight;

        void Build()
        {
            bubbleLayer = UiKit.Stretch("Bubbles", transform);
            // flying coins burst from where a tip pops up, so they draw under the popups and never hide the amount
            coinLayer = UiKit.Stretch("Coins", transform);
            popupLayer = UiKit.Stretch("Popups", transform);
            top = UiKit.Stretch("Top", transform);

            // Left column: one framed card with the shift name, clock, tips + streak, and complaints.
            const float H = CardHeight, W = 360f;
            float Y(float fromTop) => H * 0.5f - fromTop;
            var col = Deco.Panel("LeftColumn", top, new Vector2(W, H), Vector2.zero);
            col.anchorMin = col.anchorMax = new Vector2(0, 1);
            col.pivot = new Vector2(0, 1);
            col.anchoredPosition = new Vector2(28, -CardTop);
            CardRect = col;
            const float X = -W * 0.5f + 28f;

            shiftDay = Deco.Label("Day", col, "MONDAY", 18, new Vector2(300, 24), new Vector2(0, Y(36)));
            shiftDay.characterSpacing = 10f;
            shiftName = Deco.Shadowed(UiKit.Text("ShiftName", col, "First Day", 38, Palette.Cream, UiKit.Display, TextAlignmentOptions.Left, new Vector2(300, 48), new Vector2(0, Y(72))));
            shiftName.enableAutoSizing = true;
            shiftName.fontSizeMin = 24;
            shiftName.fontSizeMax = 38;
            Deco.Divider(col, W - 56f, new Vector2(0, Y(110)), 0.7f);

            clockLabel = Deco.Label("ClockLbl", col, "SHIFT ENDS IN", 15, new Vector2(300, 22), new Vector2(0, Y(136)), TextAlignmentOptions.Left, Deco.Muted);
            clockLabel.enableAutoSizing = true;
            clockLabel.fontSizeMin = 11;
            clockLabel.fontSizeMax = 15;
            TextFloor.Refresh(clockLabel);
            clock = Deco.Shadowed(UiKit.Text("Clock", col, "2:00", 72, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(300, 80), new Vector2(X, Y(186))));
            clock.rectTransform.pivot = new Vector2(0, 0.5f);
            Deco.Divider(col, W - 56f, new Vector2(0, Y(236)), 0.7f);

            TipsAnchor = UiKit.Rect("Tips", col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, Y(292)), new Vector2(W - 40f, 100));
            Deco.Label("TipsLabel", col, "TIPS", 15, new Vector2(300, 22), new Vector2(0, Y(262)), TextAlignmentOptions.Left, Deco.Muted);
            score = Deco.Shadowed(UiKit.Text("Score", col, "$0", 60, Deco.GoldLight, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(300, 72), new Vector2(X, Y(310))));
            score.rectTransform.pivot = new Vector2(0, 0.5f);
            streakBadge = UiKit.Image("StreakBadge", col, Deco.GoldPill, Color.white, new Vector2(104, 38), new Vector2(W * 0.5f - 28f - 52f, Y(262)));
            streak = UiKit.Text("Streak", streakBadge.transform, "x1.00", 20, Palette.Hex(0x3A230C), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(104, 38));

            // star track: the shift's three star targets, lit as the tips pass them, and what the next one needs
            for (int i = 0; i < trackStars.Length; i++)
            {
                var at = new Vector2(X + 14f + i * 32f, Y(372));
                trackGlow[i] = UiKit.Image("StarGlow" + i, col, UiKit.SoftCircle, new Color(1f, 0.75f, 0.3f, 0f), new Vector2(54, 54), at);
                trackStars[i] = UiKit.Image("Star" + i, col, Stars.Sprite, TrackUnlit, new Vector2(28, 28), at);
            }
            starCaption = UiKit.Text("StarCaption", col, "", 15, Deco.Muted, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(204, 24), new Vector2(50f, Y(373)));
            starCaption.enableAutoSizing = true;
            starCaption.fontSizeMin = 11;
            starCaption.fontSizeMax = 15;
            starCaption.characterSpacing = 2f;
            const float BarW = W - 56f;
            UiKit.Image("StarBarBack", col, UiKit.Pill, new Color(1f, 1f, 1f, 0.1f), new Vector2(BarW, 6), new Vector2(0, Y(396)));
            starBar = UiKit.Image("StarBar", col, UiKit.Pill, Deco.Gold, new Vector2(BarW, 6), new Vector2(-BarW * 0.5f, Y(396)));
            starBar.rectTransform.pivot = new Vector2(0, 0.5f);
            starBarWidth = BarW;
            Deco.Divider(col, W - 56f, new Vector2(0, Y(414)), 0.7f);

            complaintsLabel = Deco.Label("CLabel", col, "COMPLAINTS", 15, new Vector2(300, 22), new Vector2(0, Y(440)), TextAlignmentOptions.Left, Deco.Muted);
            for (int i = 0; i < complaintSlots.Length; i++)
            {
                var slot = UiKit.Image("Slot" + i, col, Deco.Recess, Color.white, new Vector2(42, 42), new Vector2(X + 21f + i * 58f, Y(482)));
                complaintSlots[i] = UiKit.Image("Bead", slot.transform, UiKit.Circle, Palette.Bad, new Vector2(30, 30));
                complaintSlots[i].rectTransform.localScale = Vector3.zero;
                UiKit.Image("Shine", complaintSlots[i].transform, UiKit.SoftCircle, new Color(1, 1, 1, 0.55f), new Vector2(14, 10), new Vector2(-5, 7));
            }

            Panel = PanelUi.Create(transform, runner);
            Cursor = HudCursor.Create(transform, runner, worldCam, canvasRt);
            FloorLabels = FloorLabels.Create(transform, runner, worldCam, canvasRt);
        }

        TextMeshProUGUI complaintsLabel, starCaption;
        readonly Image[] trackStars = new Image[3], trackGlow = new Image[3];
        readonly float[] starPunch = new float[3];
        Image starBar;
        float starBarWidth;

        /// <summary>Stars lit on the HUD's star track, and its caption (what the next star needs).</summary>
        public int StarsLit { get; private set; }
        public string StarCaption => starCaption.text;
        /// <summary>The score to beat once all three stars are lit: the player's best on this shift (today's best on the
        /// daily), 0 when there's none or bests aren't saved (a relaxed shift). Set after each shift begins.</summary>
        public int BestToBeat { get; set; }
        /// <summary>Times this shift's track announced a new best (at most once a shift).</summary>
        public int NewBestCount { get; private set; }
        /// <summary>Self-test: stars actually drawn lit on the track (read back from the images).</summary>
        public int StarsDrawnLit
        {
            get { int n = 0; foreach (var st in trackStars) if (st.color == Stars.Lit) n++; return n; }
        }
        public HudCursor Cursor { get; private set; }
        public FloorLabels FloorLabels { get; private set; }
        /// <summary>Recordings: no popups, banners or flying coins while the simulation is skipped ahead.</summary>
        public bool Quiet;

        public void Clear()
        {
            foreach (var b in bubbles.Values) if (b.Root) Destroy(b.Root.gameObject);
            bubbles.Clear();
            foreach (var p in popups) if (p.Text) Destroy(p.Text.gameObject);
            popups.Clear();
            foreach (var c in flying) if (c.Rt) Destroy(c.Rt.gameObject);
            flying.Clear();
            shownScore = 0;
            lastStreak = 0;
            StarsLit = 0;
            BestToBeat = 0;
            NewBestCount = 0;
            for (int i = 0; i < trackStars.Length; i++) { trackStars[i].color = TrackUnlit; starPunch[i] = 0f; }
        }

        CanvasGroup group;

        public void SetVisible(bool on)
        {
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = on ? 1f : 0f;
            group.blocksRaycasts = on;
        }

        public void SetShiftName(string day, string title)
        {
            shiftDay.text = day.ToUpperInvariant();
            shiftName.text = title;
        }

        // ---------------------------------------------------------------- bubbles

        sealed class Bubble
        {
            public RectTransform Root;
            public Image Disc, Ring, Back, Badge, Icon, BadgeIcon;
            public TextMeshProUGUI Code, BadgeText;
            public FloorId Dest;
            public float Pop, Wobble;
        }

        Bubble MakeBubble(Passenger p)
        {
            var b = new Bubble();
            b.Root = UiKit.Rect("B" + p.Id, bubbleLayer, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(64, 64));
            b.Back = UiKit.Image("Back", b.Root, UiKit.Circle, Palette.Cream, new Vector2(64, 64));
            var tail = UiKit.Image("Tail", b.Root, UiKit.Circle, Palette.Cream, new Vector2(16, 16), new Vector2(0, -30));
            b.Ring = UiKit.Image("Ring", b.Root, UiKit.Ring, Palette.Good, new Vector2(64, 64));
            b.Ring.type = Image.Type.Filled;
            b.Ring.fillMethod = Image.FillMethod.Radial360;
            b.Ring.fillOrigin = (int)Image.Origin360.Top;
            b.Ring.fillClockwise = false;
            b.Disc = UiKit.Image("Disc", b.Root, UiKit.Circle, Palette.Floor(p.Dest), new Vector2(46, 46));
            b.Code = UiKit.Text("Code", b.Disc.transform, Code(p.Dest), 16, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(46, 46));
            b.Icon = UiKit.Image("Icon", b.Disc.transform, Icons.Floor(p.Dest), Color.white, new Vector2(40, 40));
            b.Icon.preserveAspect = true;
            b.Code.gameObject.SetActive(b.Icon.sprite == null);
            b.Icon.gameObject.SetActive(b.Icon.sprite != null);
            b.Badge = UiKit.Image("Badge", b.Root, UiKit.Circle, Palette.Ink, new Vector2(30, 30), new Vector2(27, 25));
            b.BadgeIcon = UiKit.Image("BadgeIcon", b.Badge.transform, null, Color.white, new Vector2(26, 26));
            b.BadgeIcon.preserveAspect = true;
            b.BadgeText = UiKit.Text("BadgeText", b.Badge.transform, "", 15, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(28, 28));
            b.Dest = p.Dest;
            return b;
        }

        void UpdateBubble(Bubble b, Passenger p, PassengerView v, float dt)
        {
            if (b.Dest != p.Dest)
            {
                b.Dest = p.Dest;
                b.Disc.color = Palette.Floor(p.Dest);
                b.Code.text = Code(p.Dest);
                b.Icon.sprite = Icons.Floor(p.Dest);
                b.Pop = 0f;
            }
            float frac = Mathf.Clamp01(p.PatienceFrac);
            b.Ring.fillAmount = frac;
            b.Ring.color = Palette.Patience(frac);
            b.Pop = Mathf.Min(1f, b.Pop + dt * 4f);
            b.Wobble = Mathf.Max(0f, b.Wobble - dt * 2f);
            bool riding = p.State == PState.Riding;
            float sizeK = Mathf.Lerp(1.05f, 0.82f, Mathf.InverseLerp(5f, 9f, runner.Sim.B.Count));
            float scale = Ease.OutBack(b.Pop, 2f) * (riding ? 0.78f : 1f) * sizeK;
            if (frac < 0.25f && !p.Fuming) scale *= 1f + 0.08f * Mathf.Sin(Time.time * 14f);
            b.Root.localScale = Vector3.one * scale;
            b.Root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30f) * 12f * b.Wobble);

            // rule badge: an icon for the rule, or a number (courier countdown)
            string badge = "";
            string icon = null;
            Color badgeCol = new Color(0.98f, 0.94f, 0.84f);
            switch (p.Kind)
            {
                case Kind.Houseplant: if (!p.Sunned) icon = "sun"; break;
                case Kind.Mirror: icon = "mirror"; break;
                case Kind.Vampire: icon = "fangs"; break;
                case Kind.Courier:
                {
                    int left = runner.Sim.B.Leaving[(int)p.Dest];
                    badge = left >= 0 ? left.ToString() : "";
                    if (badge.Length == 0) icon = "parcel";
                    badgeCol = left >= 0 && left <= 1 ? Palette.Bad : Palette.Hex(0xA4662C);
                    break;
                }
                case Kind.Swimmer: icon = "wave"; break;
                case Kind.Kid: icon = "balloon"; break;
                case Kind.Tycoon: icon = p.ExpressBroken ? null : "tophat"; badge = p.ExpressBroken ? "$" : ""; badgeCol = p.ExpressBroken ? Palette.Hex(0x777777) : badgeCol; break;
            }
            var iconSprite = icon != null ? Icons.Badge(icon) : null;
            if (icon != null && iconSprite == null) badge = icon.Substring(0, 1).ToUpperInvariant();
            b.Badge.gameObject.SetActive(badge.Length > 0 || iconSprite != null);
            b.Badge.color = badgeCol;
            b.BadgeIcon.sprite = iconSprite;
            b.BadgeIcon.gameObject.SetActive(iconSprite != null);
            b.BadgeText.text = badge;
            TextFloor.SetSize(b.BadgeText, 17);
            if (p.Fuming) b.Disc.color = Palette.Bad;

            var anchor = v.BubbleAnchor;
            if (p.State == PState.Waiting && runner.Sim.Waiting[(int)p.At].IndexOf(p) % 2 == 1) anchor += Vector3.up * 0.75f;
            var screen = worldCam.WorldToScreenPoint(anchor);
            if (screen.z < 0f) { b.Root.gameObject.SetActive(false); return; }
            b.Root.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
            b.Root.anchoredPosition = local + canvasRt.rect.size * 0.5f;
        }

        Camera CanvasCam => GetComponentInParent<Canvas>().worldCamera;

        public void Wobble(int pid)
        {
            if (bubbles.TryGetValue(pid, out var b)) b.Wobble = 1f;
        }

        // ---------------------------------------------------------------- popups

        sealed class Popup
        {
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float T, Dur, Rise;
            /// <summary>Height risen so far (never sinks back when an update restarts its life) and the pop-in clock.</summary>
            public float Risen, PopT;
            /// <summary>Canvas size the layout reserves (the text's preferred size plus its outline).</summary>
            public Vector2 Size;
            /// <summary>The space it takes this frame (its size, grown while it pops in).</summary>
            public Vector2 Box;
            /// <summary>Extra height that keeps it clear of older popups (eases back down when they go).</summary>
            public float Lift;
            public string Key;
            public Vector2 Pos;
        }

        const float PopupGap = 6f;

        /// <summary>
        /// A floating word or amount over the tower. Popups never draw over each other: each one is lifted clear of
        /// the ones already showing. With a <paramref name="key"/>, a popup with the same key that's still showing is
        /// updated in place (and pops again) instead of stacking a new one, e.g. a stop's running tip total.
        /// </summary>
        public void PopupAt(Vector3 world, string text, Color color, float size = 40f, float dur = 1.3f, string key = null)
        {
            if (Quiet) return;
            if (key != null)
                foreach (var other in popups)
                    if (other.Key == key && other.Text && other.T < 0.75f)
                    {
                        other.Text.text = text;
                        other.Text.color = color;
                        TextFloor.SetSize(other.Text, size);
                        other.Size = PopupSize(other.Text, text);
                        other.T = 0f;          // a full life from the latest update
                        other.Dur = dur;
                        other.PopT = 0.35f;    // and a smaller punch than a new popup's
                        return;
                    }
            var t = UiKit.Text("Popup", popupLayer, text, size, color, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(500, size * 1.3f));
            t.fontSharedMaterial = PopupMaterial();
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
            popups.Add(new Popup { Text = t, World = world, Dur = dur, Rise = 70f, Size = PopupSize(t, text), Key = key });
        }

        static Material popupMat;

        /// <summary>
        /// One material for every popup, with a dark edge and halo so a gold amount reads over gold coins, sunny floors
        /// and the cream lobby. The signage font uses TMP's mobile SDF shader, which only draws an outline or underlay
        /// with its OUTLINE_ON / UNDERLAY_ON keywords (setting outlineWidth on the text doesn't turn them on), and the
        /// build keeps that pair because TMP's "Drop Shadow" material uses it.
        /// </summary>
        public static Material PopupMaterial()
        {
            if (popupMat) return popupMat;
            popupMat = new Material(UiKit.Signage.material) { name = "Popup (outline + halo)" };
            popupMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            popupMat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            popupMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            popupMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(30, 20, 34, 255));
            popupMat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.03f, 0.01f, 0.05f, 0.8f));
            popupMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            popupMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
            popupMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
            ShaderUtilities.UpdateShaderRatios(popupMat);
            return popupMat;
        }

        static Vector2 PopupSize(TextMeshProUGUI t, string text)
        {
            var v = t.GetPreferredValues(text);
            return new Vector2(Mathf.Min(v.x, 500f) + t.fontSize * 0.25f, v.y + t.fontSize * 0.1f);
        }

        void UpdatePopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.T += dt / p.Dur;
                if (p.T >= 1f || !p.Text) { if (p.Text) Destroy(p.Text.gameObject); popups.RemoveAt(i); }
            }
            // oldest first: each popup sits where it would be, then is lifted until it clears every older one
            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                var screen = worldCam.WorldToScreenPoint(p.World);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
                p.Risen = Mathf.Max(p.Risen, p.Rise * Ease.OutCubic(p.T));
                var basePos = local + canvasRt.rect.size * 0.5f + Vector2.up * (p.Risen + 20f);
                p.PopT = Mathf.Min(1f, p.PopT + dt / (0.15f * p.Dur));
                float s = p.PopT < 1f ? Ease.OutBack(p.PopT, 3f) : 1f;
                p.Box = p.Size * Mathf.Max(1f, s);   // room for the pop-in overshoot too
                // jump up at once (so it never overlaps), drift back down once the way is clear, but never into another
                float clear = LiftClear(i, basePos, 0f);
                float eased = clear >= p.Lift ? clear : Mathf.MoveTowards(p.Lift, clear, 240f * dt);
                p.Lift = LiftClear(i, basePos, eased);
                p.Pos = basePos + Vector2.up * p.Lift;
                p.Text.rectTransform.anchoredPosition = p.Pos;
                p.Text.rectTransform.localScale = Vector3.one * s;
                p.Text.alpha = p.T > 0.7f ? 1f - (p.T - 0.7f) / 0.3f : 1f;
            }
            MeasurePopupOverlap();
        }

        void MeasureCoinsOverPopups()
        {
            if (flying.Count == 0 || popups.Count == 0) return;
            bool cross = false;
            foreach (var c in flying)
            {
                if (!c.Rt.gameObject.activeSelf) continue;
                var half = Vector2.one * 13f * c.Rt.localScale.x;
                var cr = new Rect(c.Rt.anchoredPosition - half, half * 2f);
                foreach (var p in popups)
                    if (p.Text && p.Text.alpha >= 0.35f && DrawnRect(p).Overlaps(cr)) { cross = true; break; }
                if (cross) break;
            }
            if (!cross) return;
            CoinCrossFrames++;
            if (coinLayer.GetSiblingIndex() > popupLayer.GetSiblingIndex()) CoinOverTextFrames++;
        }

        /// <summary>The smallest lift, at or above <paramref name="from"/>, that clears every popup older than popup i.</summary>
        float LiftClear(int i, Vector2 basePos, float from)
        {
            var p = popups[i];
            // and never below the bottom of the screen (the close-up can put the car's floor there)
            float need = Mathf.Max(from, PopupGap + p.Box.y * 0.5f - basePos.y);
            for (int pass = 0; pass <= i; pass++)
            {
                bool moved = false;
                for (int j = 0; j < i; j++)
                {
                    var o = popups[j];
                    var at = basePos + Vector2.up * need;
                    float halfW = (p.Box.x + o.Box.x) * 0.5f, halfH = (p.Box.y + o.Box.y) * 0.5f + PopupGap;
                    if (Mathf.Abs(at.x - o.Pos.x) >= halfW || Mathf.Abs(at.y - o.Pos.y) >= halfH) continue;
                    need = o.Pos.y + halfH - basePos.y;
                    moved = true;
                }
                if (!moved) break;
            }
            return need;
        }

        /// <summary>Self-test: the worst overlap seen between two clearly visible popups' drawn text, in canvas
        /// units (the smaller of the x and y overlaps, so a value means the glyph boxes really cross).</summary>
        public float WorstPopupOverlap { get; set; }
        /// <summary>Self-test: frames where a flying coin crossed a popup's text, and how many of those drew a coin on top of it.</summary>
        public int CoinCrossFrames { get; set; }
        public int CoinOverTextFrames { get; set; }
        public int PopupCount => popups.Count;
        public string WorstPopupPair { get; private set; }

        void MeasurePopupOverlap()
        {
            for (int i = 0; i < popups.Count; i++)
            {
                var a = popups[i];
                if (a.Text.alpha < 0.35f) continue;
                var ra = DrawnRect(a);
                for (int j = i + 1; j < popups.Count; j++)
                {
                    var b = popups[j];
                    if (b.Text.alpha < 0.35f) continue;
                    var rb = DrawnRect(b);
                    float ox = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
                    float oy = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
                    float o = Mathf.Min(ox, oy);
                    if (o > WorstPopupOverlap) { WorstPopupOverlap = o; WorstPopupPair = a.Text.text + " / " + b.Text.text; }
                }
            }
        }

        static Rect DrawnRect(Popup p)
        {
            var b = p.Text.textBounds;
            float s = p.Text.rectTransform.localScale.x;
            var c = p.Pos + (Vector2)b.center * s;
            return new Rect(c - (Vector2)b.size * s * 0.5f, (Vector2)b.size * s);
        }

        // ---------------------------------------------------------------- frame

        public void PunchScore() => scorePunch = 1f;

        sealed class FlyCoin { public RectTransform Rt; public Vector2 From, Ctrl; public float T, Dur; }
        readonly List<FlyCoin> flying = new List<FlyCoin>();

        /// <summary>Coins arc from a world position into the tips counter.</summary>
        public void FlyCoins(Vector3 world, int count)
        {
            if (Quiet) return;
            var screen = worldCam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
            var from = local + canvasRt.rect.size * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var img = UiKit.Image("Coin", coinLayer, UiKit.Circle, Palette.Hex(0xFFC83D), new Vector2(26, 26));
                UiKit.Image("In", img.transform, UiKit.Circle, Palette.Hex(0xE8A317), new Vector2(15, 15));
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                var jitter = new Vector2(Random.Range(-40f, 40f), Random.Range(-20f, 30f));
                flying.Add(new FlyCoin { Rt = img.rectTransform, From = from + jitter, Ctrl = from + jitter + new Vector2(Random.Range(-160f, 40f), Random.Range(160f, 320f)),
                                         T = -i * 0.05f, Dur = Random.Range(0.55f, 0.75f) });
            }
        }

        void UpdateCoins(float dt)
        {
            if (flying.Count == 0) return;
            var target = (Vector2)canvasRt.InverseTransformPoint(TipsAnchor.position) + canvasRt.rect.size * 0.5f + new Vector2(-60, -10);
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var c = flying[i];
                c.T += dt;
                if (c.T < 0f) { c.Rt.gameObject.SetActive(false); continue; }
                c.Rt.gameObject.SetActive(true);
                float k = Mathf.Clamp01(c.T / c.Dur);
                float e = Ease.InCubic(k) * 0.6f + k * 0.4f;
                var p = (1 - e) * (1 - e) * c.From + 2 * (1 - e) * e * c.Ctrl + e * e * target;
                c.Rt.anchoredPosition = p;
                c.Rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.7f, k) * (1f + 0.15f * Mathf.Sin(c.T * 30f));
                if (k >= 1f)
                {
                    Destroy(c.Rt.gameObject);
                    flying.RemoveAt(i);
                    scorePunch = Mathf.Max(scorePunch, 0.6f);
                    AudioDirector.Instance?.Sfx("tick", 0.18f, 2.2f, -0.8f, 0.1f, 0.03f);
                }
            }
        }

        // ---------------------------------------------------------------- star track

        static readonly Color TrackUnlit = new Color(1f, 1f, 1f, 0.24f);

        void UpdateStarTrack(ShiftSim sim, float dt)
        {
            var targets = sim.Def.Stars;
            int lit = sim.StarCount;
            for (int i = 0; i < trackStars.Length; i++)
            {
                bool on = i < lit;
                if (on && i >= StarsLit && !Quiet)
                {
                    starPunch[i] = 1f;
                    AudioDirector.Instance?.Sfx("star_" + (i + 1), 0.55f, 1f, 0f, 0f, 0f);
                }
                trackStars[i].color = on ? Stars.Lit : TrackUnlit;
                starPunch[i] = Mathf.Max(0f, starPunch[i] - dt * 2.2f);
                float k = Ease.OutCubic(starPunch[i]);
                trackStars[i].rectTransform.localScale = Vector3.one * (1f + 0.7f * k);
                trackGlow[i].color = new Color(1f, 0.75f, 0.3f, on ? 0.18f + 0.5f * k : 0f);
            }
            StarsLit = lit;

            if (lit >= targets.Length && BestToBeat > 0 && sim.Score > BestToBeat)
            {
                // past the best: once a shift, the record sound and a punch of the stars
                if (NewBestCount == 0)
                {
                    NewBestCount++;
                    if (!Quiet)
                    {
                        for (int i = 0; i < starPunch.Length; i++) starPunch[i] = 1f;
                        AudioDirector.Instance?.SfxLater("new_record", 0.35f, 0.7f);
                    }
                }
                starCaption.text = "NEW BEST!";
                starCaption.color = Deco.GoldLight;
                SetStarBar(1f);
            }
            else if (lit >= targets.Length && BestToBeat > 0)
            {
                // three stars and a best to chase: the bar runs from the third star to the best
                int need = BestToBeat - sim.Score;
                starCaption.text = TextFloor.Fit(starCaption, $"${need:N0} TO YOUR BEST", $"${need:N0} TO BEST");   // shorter if a raised text floor needs it
                starCaption.color = Deco.Gold;
                int from = Mathf.Min(targets[targets.Length - 1], BestToBeat - 1);
                SetStarBar(Mathf.Clamp01((float)(sim.Score - from) / Mathf.Max(1, BestToBeat - from)));
            }
            else if (lit >= targets.Length)
            {
                starCaption.text = "ALL THREE STARS!";
                starCaption.color = Deco.Gold;
                SetStarBar(1f);
            }
            else
            {
                int need = targets[lit] - sim.Score;
                starCaption.text = sim.Relaxed && lit >= 1 ? "RELAXED CLEAR!"
                    : sim.Relaxed ? (TextFloor.Large ? TextFloor.Fit(starCaption, $"${need:N0} TO A RELAXED CLEAR", $"${need:N0} TO CLEAR") : $"${need:N0} TO A RELAXED CLEAR")
                    : TextFloor.Large ? TextFloor.Fit(starCaption, $"${need:N0} TO STAR {lit + 1}", $"${need:N0} TO GO")   // LARGER TEXT on a small screen
                    : $"${need:N0} TO STAR {lit + 1}";
                starCaption.color = sim.Relaxed && lit >= 1 ? Palette.Hex(0x8FD6C4) : Deco.Muted;
                int from = lit > 0 ? targets[lit - 1] : 0;
                SetStarBar(Mathf.Clamp01((float)(sim.Score - from) / Mathf.Max(1, targets[lit] - from)));
            }
        }

        void SetStarBar(float frac)
        {
            var rt = starBar.rectTransform;
            float w = Mathf.Lerp(rt.sizeDelta.x, Mathf.Max(6f, starBarWidth * frac), 0.25f);
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        }

        // ---------------------------------------------------------------- route preview

        sealed class RouteMark { public RectTransform Rt; public Image Bg, Icon; public TextMeshProUGUI Text; }
        readonly List<RouteMark> marks = new List<RouteMark>();
        RectTransform routeLayer;

        RouteMark Mark(int i)
        {
            while (marks.Count <= i)
            {
                if (routeLayer == null) routeLayer = UiKit.Stretch("Route", transform);
                var bg = UiKit.Image("Mark", routeLayer, UiKit.Circle, Color.white, new Vector2(40, 40));
                bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = Vector2.zero;
                var icon = UiKit.Image("Icon", bg.transform, null, Color.white, new Vector2(30, 30));
                icon.preserveAspect = true;
                var t = UiKit.Text("T", bg.transform, "", 18, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(60, 30));
                t.outlineWidth = 0.2f;
                t.outlineColor = new Color32(30, 20, 34, 255);
                marks.Add(new RouteMark { Rt = bg.rectTransform, Bg = bg, Icon = icon, Text = t });
            }
            return marks[i];
        }

        /// <summary>While hovering a destination: where will the car stop, and what will happen there?</summary>
        void UpdateRoutePreview()
        {
            var sim = runner.Sim;
            int used = 0;
            FloorId? target = runner.HoverFloor;
            if (target.HasValue && sim.B.Has(target.Value) && !sim.Ended && !runner.Attract)
            {
                int to = sim.B.SlotOf(target.Value);
                float from = sim.Car.Pos;
                bool kid = sim.Car.Has(Kind.Kid);
                bool vamp = sim.Car.Has(Kind.Vampire);
                int dir = to > from ? 1 : -1;
                int start = dir > 0 ? Mathf.FloorToInt(from + 0.001f) + 1 : Mathf.CeilToInt(from - 0.001f) - 1;
                if (Mathf.Abs(to - from) > 0.01f)
                    for (int slot = kid ? start : to; dir > 0 ? slot <= to : slot >= to; slot += dir)
                    {
                        var f = sim.B.At(slot);
                        bool sunny = Defs.Sunny(f);
                        int off = 0;
                        foreach (var r in sim.Car.Riders)
                            if (r.Dest == f && (r.Kind != Kind.Houseplant || r.Sunned || sunny)) off++;
                        bool plantSun = sunny && sim.Car.Riders.Exists(r => r.Kind == Kind.Houseplant && !r.Sunned);
                        var m = Mark(used++);
                        m.Rt.gameObject.SetActive(true);
                        bool danger = sunny && vamp;
                        bool dest = slot == to;
                        m.Bg.color = danger ? Palette.Bad : plantSun ? Palette.Hex(0xFFC857) : dest ? Palette.Hex(0x6CCB5F) : new Color(1f, 0.9f, 0.7f, 0.9f);
                        m.Icon.sprite = danger ? Icons.Badge("fangs") : plantSun ? Icons.Badge("sun") : null;
                        m.Icon.enabled = m.Icon.sprite != null;
                        m.Text.text = off > 0 ? "+" + off : danger ? "!" : "";
                        m.Text.rectTransform.anchoredPosition = m.Icon.enabled ? new Vector2(34, -14) : Vector2.zero;
                        float size = dest ? 46f : 30f;
                        m.Rt.sizeDelta = new Vector2(size, size);
                        float pulse = danger ? 1f + 0.15f * Mathf.Sin(UiTime.Now * 12f) : 1f;
                        m.Rt.localScale = Vector3.one * pulse;
                        var world = runner.Building[f].transform.position + new Vector3(Layout.ShaftHalf + 0.55f, 1.25f, -1.4f);
                        var screen = worldCam.WorldToScreenPoint(world);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
                        m.Rt.anchoredPosition = local + canvasRt.rect.size * 0.5f;
                    }
            }
            for (int i = used; i < marks.Count; i++) marks[i].Rt.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- guest tooltip

        RectTransform tip;
        TextMeshProUGUI tipTitle, tipBody;
        Image tipIcon;

        void UpdateTooltip()
        {
            var sim = runner.Sim;
            Passenger p = runner.HoverPid >= 0 ? sim.Find(runner.HoverPid) : null;
            var v = p != null ? runner.ViewOf(p.Id) : null;
            if (tip == null)
            {
                tip = Deco.Panel("Tooltip", transform, new Vector2(TipW, 120), Vector2.zero, true);
                tip.anchorMin = tip.anchorMax = Vector2.zero;
                tip.pivot = new Vector2(0f, 0f);
                tipWell = UiKit.Image("Well", tip, Deco.Well, Color.white, new Vector2(78, 78)).rectTransform;
                tipIcon = UiKit.Image("Icon", tipWell, null, Color.white, new Vector2(70, 70));
                tipIcon.preserveAspect = true;
                tipTitle = Deco.Label("T", tip, "", 20, new Vector2(TipW - 124, 28), Vector2.zero);
                tipBody = UiKit.Text("B", tip, "", 19, Palette.Cream, UiKit.Body, TextAlignmentOptions.TopLeft, new Vector2(TipW - 124, 80), Vector2.zero);
                tipBody.textWrappingMode = TextWrappingModes.Normal;
                tipBody.lineSpacing = 4;
            }
            bool show = p != null && v != null && p.State != PState.Done && !runner.Attract;
            tip.gameObject.SetActive(show);
            if (!show) return;
            var kd = Defs.Of(p.Kind);
            tipTitle.text = kd.Name.ToUpperInvariant();
            tipIcon.sprite = Icons.Kind(p.Kind);
            tipIcon.enabled = tipIcon.sprite != null;
            string where = p.State == PState.Riding ? "Riding to" : "Waiting for";
            string extra = p.Kind == Kind.Houseplant && !p.Sunned ? " ·\u00A0needs\u00A0sun" : p.Kind == Kind.Tycoon && p.ExpressBroken ? " ·\u00A0express\u00A0ruined" : "";
            tipBody.text = $"{kd.Rule}\n<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(Palette.Floor(p.Dest), Color.white, 0.35f))}>{where} {Defs.Floor(p.Dest).Name}</color>{extra}" +
                           (p.State == PState.Riding && sim.Car.IsOpen ? $"\n<size=80%><color=#C9BBA0>{(Controls.Touch ? "Hold them or tap LET OFF" : Controls.Pad ? "Press X" : Controls.KeyNav ? "Press F" : "Right-click")} to let them off here</color></size>" : "");
            tipBody.richText = true;
            var screen = worldCam.WorldToScreenPoint(v.BubbleAnchor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
            var pos = local + canvasRt.rect.size * 0.5f + new Vector2(46, -20);
            pos.x = Mathf.Min(pos.x, canvasRt.rect.width - 420f - TipW);
            float colW = TipW - 124f, colX = -TipW * 0.5f + 104f + colW * 0.5f;
            tipBody.rectTransform.sizeDelta = new Vector2(colW, 80f);
            float bodyH = tipBody.GetPreferredValues(tipBody.text, colW, 0f).y;
            float h = Mathf.Max(112f, 66f + bodyH);
            tip.sizeDelta = new Vector2(TipW, h);
            pos.y = Mathf.Clamp(pos.y, 16f, canvasRt.rect.height - h - 16f);
            tip.anchoredPosition = pos;
            tipWell.anchoredPosition = new Vector2(-TipW * 0.5f + 57f, h * 0.5f - 56f);
            tipTitle.rectTransform.anchoredPosition = new Vector2(colX, h * 0.5f - 32f);
            tipBody.rectTransform.sizeDelta = new Vector2(colW, bodyH + 4f);
            tipBody.rectTransform.anchoredPosition = new Vector2(colX, h * 0.5f - 50f - bodyH * 0.5f);
        }

        const float TipW = 420f;
        RectTransform tipWell;

        TextMeshProUGUI banner;
        RectTransform bannerStrip;
        float bannerT = 1f;
        /// <summary>A banner grows slowly after it lands (see UpdateBanner), up to this much.</summary>
        const float BannerGrowth = 1.08f;

        /// <summary>
        /// A big word that slams in and fades. It's centred in the strip between the HUD card and the panel, shrinks to
        /// fit it, and is clipped to it, so not even the slam-in covers the clock, the score or the panel.
        /// </summary>
        public void Banner(string text, Color color)
        {
            if (Quiet) return;
            if (banner == null)
            {
                EnsureBannerStrip();
                banner = UiKit.Text("Banner", bannerStrip, "", 120, color, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1000, 180), new Vector2(0, 120));
                banner.textWrappingMode = TextWrappingModes.NoWrap;
                banner.enableAutoSizing = true;
                banner.fontSizeMin = 48;
                banner.fontSizeMax = 120;
                Deco.Outlined(Deco.Shadowed(banner, 0.75f, 1f, 0.45f), 0.18f, new Color32(30, 20, 34, 255));
            }
            banner.text = text;
            banner.color = color;
            bannerT = 0f;
            FitBanner();
        }

        /// <summary>The strip between the HUD card and the panel that banners and the resume count are centred in and clipped to.</summary>
        void EnsureBannerStrip()
        {
            if (bannerStrip != null) return;
            bannerStrip = UiKit.Stretch("BannerStrip", transform);
            bannerStrip.offsetMin = new Vector2(FloorLabels.HudRight, 0f);
            bannerStrip.offsetMax = new Vector2(-FloorLabels.PanelLeftInset, 0f);
            bannerStrip.gameObject.AddComponent<RectMask2D>();
        }

        TextMeshProUGUI count;
        int countShown;

        /// <summary>Self-test: the digit of the resume count showing ("" when none).</summary>
        public string CountShown => count != null && count.gameObject.activeSelf ? count.text : "";

        /// <summary>The count after a resume (3, 2, 1) over the tower while <see cref="ShiftRunner.Holding"/> runs, with a tick on each digit.</summary>
        void UpdateCount()
        {
            float h = runner.Holding;
            if (h <= 0f || runner.Paused)
            {
                if (count != null) count.gameObject.SetActive(false);
                countShown = 0;
                return;
            }
            if (count == null)
            {
                EnsureBannerStrip();
                count = UiKit.Text("Count", bannerStrip, "", 150, Palette.Cream, UiKit.Display, TextAlignmentOptions.Center, new Vector2(300, 200));
                Deco.Outlined(Deco.Shadowed(count, 0.75f, 1f, 0.45f), 0.18f, new Color32(30, 20, 34, 255));
            }
            float beat = ShiftRunner.ResumeCount / 3f;
            int n = Mathf.Clamp(Mathf.CeilToInt(h / beat), 1, 3);
            if (n != countShown)
            {
                countShown = n;
                AudioDirector.Instance?.Sfx("tick", 0.9f, n == 1 ? 1.25f : 1f, 0f, 0f);
            }
            count.gameObject.SetActive(true);
            count.text = n.ToString();
            float k = 1f - (h - (n - 1) * beat) / beat;   // 0 -> 1 through this digit's beat
            bool still = SaveData.Current.ReducedMotion;
            count.rectTransform.localScale = Vector3.one * (still ? 1f : Mathf.Lerp(1.35f, 1f, Ease.OutCubic(Mathf.Clamp01(k * 3f))));
            count.alpha = k > 0.8f ? 1f - (k - 0.8f) / 0.2f * 0.6f : 1f;
        }

        void FitBanner() => banner.rectTransform.sizeDelta = new Vector2(Mathf.Max(100f, bannerStrip.rect.width / BannerGrowth), 180f);

        /// <summary>Self-test: the banner showing (null when none), whether it has landed after its slam-in, its text's
        /// drawn bounds in screen pixels, and the HUD card's and panel's.</summary>
        public TextMeshProUGUI ShownBanner => banner != null && banner.gameObject.activeSelf ? banner : null;
        public bool BannerLanded => bannerT >= 0.12f;
        public Rect BannerScreenRect(Camera canvasCam)
        {
            var b = banner.textBounds;
            var rt = banner.rectTransform;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(canvasCam, rt.TransformPoint(b.min));
            Vector2 c = RectTransformUtility.WorldToScreenPoint(canvasCam, rt.TransformPoint(b.max));
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
        public RectTransform CardRect { get; private set; }
        public RectTransform BannerStrip => bannerStrip;

        void UpdateBanner(float dt)
        {
            if (banner == null) return;
            bannerT = Mathf.Min(1f, bannerT + dt / 1.6f);
            float t = bannerT;
            if (t < 1f) FitBanner();
            float s = t < 0.12f ? Mathf.Lerp(2.2f, 1f, Ease.OutBack(t / 0.12f, 2f)) : 1f + (t - 0.12f) * (BannerGrowth - 1f);
            banner.rectTransform.localScale = Vector3.one * s;
            banner.alpha = t < 0.08f ? t / 0.08f : t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
            banner.gameObject.SetActive(t < 1f);
        }

        public void Tick(float dt)
        {
            var sim = runner.Sim;
            if (sim == null) return;

            // bubbles follow passengers
            var seen = new HashSet<int>();
            foreach (var p in sim.All)
            {
                if (p.State == PState.Done) continue;
                var v = runner.ViewOf(p.Id);
                if (v == null || v.Leaving) continue;
                seen.Add(p.Id);
                if (!bubbles.TryGetValue(p.Id, out var b)) bubbles[p.Id] = b = MakeBubble(p);
                UpdateBubble(b, p, v, dt);
            }
            var dead = new List<int>();
            foreach (var kv in bubbles) if (!seen.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) { Destroy(bubbles[id].Root.gameObject); bubbles.Remove(id); }

            // score count-up
            if (shownScore != sim.Score)
            {
                int diff = sim.Score - shownScore;
                shownScore += Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diff) * Ease.Damp(9f, dt))) * System.Math.Sign(diff);
                if (System.Math.Sign(sim.Score - shownScore) != System.Math.Sign(diff)) shownScore = sim.Score;
            }
            score.text = "$" + shownScore.ToString("N0");
            scorePunch = Mathf.Max(0f, scorePunch - dt * 3f);
            score.rectTransform.localScale = Vector3.one * (1f + 0.18f * Ease.OutCubic(scorePunch));

            if (sim.Streak != lastStreak) { streakPunch = sim.Streak > lastStreak ? 1f : 0f; lastStreak = sim.Streak; }
            streakPunch = Mathf.Max(0f, streakPunch - dt * 3f);
            streak.text = "x" + sim.Multiplier.ToString("0.00");
            float m = sim.Multiplier;
            streakBadge.color = m >= 2.5f ? Palette.Hex(0xFF9CC8) : m >= 2f ? Palette.Hex(0xFFB27A) : m > 1.001f ? Color.white : new Color(0.7f, 0.66f, 0.62f);
            streakBadge.rectTransform.localScale = Vector3.one * (1f + 0.25f * streakPunch);

            UpdateStarTrack(sim, dt);

            // on a small screen the floor draws this label bigger, and the long line wouldn't fit the card
            // (and with LARGER TEXT, the longest wording that fits)
            bool tight = TextFloor.Raised(clockLabel) || TextFloor.Large;
            clockLabel.text = sim.Def.Endless ? "ON SHIFT FOR"
                            : !sim.ClockRunning ? (TextFloor.Large ? TextFloor.Fit(clockLabel, "CLOCK STARTS ON YOUR FIRST DROP", "CLOCK STARTS AT FIRST DROP", "STARTS AT FIRST DROP")
                                                   : tight ? "CLOCK STARTS AT FIRST DROP" : "CLOCK STARTS ON YOUR FIRST DROP")
                            : sim.RushHour ? "RUSH HOUR! TIPS x1.5" : "SHIFT ENDS IN";
            clockLabel.characterSpacing = tight && !sim.ClockRunning ? 2f : 6f;
            if (sim.Def.Endless)
            {
                int secs = Mathf.FloorToInt(sim.Time);
                clock.text = $"{secs / 60}:{secs % 60:00}";
                clock.color = Palette.Cream;
            }
            else
            {
                int secs = Mathf.CeilToInt(sim.TimeLeft);
                clock.text = $"{secs / 60}:{secs % 60:00}";
                bool hurry = sim.TimeLeft <= 10f && sim.ClockRunning;
                clock.color = hurry ? Palette.Hex(0xFF5A5F) : sim.RushHour ? Palette.Warn : Palette.Cream;
                clockLabel.color = sim.RushHour ? Palette.Warn : Deco.Muted;
                if (hurry && Mathf.Repeat(sim.TimeLeft, 1f) > 0.85f) clockPunch = 1f;
            }
            clockPunch = Mathf.Max(0f, clockPunch - dt * 4f);
            clock.rectTransform.localScale = Vector3.one * (1f + 0.15f * clockPunch);

            complaintsLabel.text = sim.Relaxed ? "COMPLAINTS  ·  RELAXED, NO FIRING" : "COMPLAINTS";
            for (int i = 0; i < complaintSlots.Length; i++)
            {
                bool on = i < sim.Complaints;
                var c = complaintSlots[i];
                c.rectTransform.localScale = Vector3.Lerp(c.rectTransform.localScale, Vector3.one * (on ? 1f : 0f), Ease.Damp(12f, dt));
            }

            UpdatePopups(dt);
            UpdateBanner(dt);
            UpdateCount();
            UpdateCoins(dt);
            MeasureCoinsOverPopups();
            UpdateRoutePreview();
            UpdateTooltip();
            FloorLabels.Tick(dt);
            Cursor.Tick();
            Panel.Tick(dt);
        }
    }
}

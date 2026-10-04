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
        RectTransform canvasRt, bubbleLayer, popupLayer, top;
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

        void Build()
        {
            bubbleLayer = UiKit.Stretch("Bubbles", transform);
            popupLayer = UiKit.Stretch("Popups", transform);
            top = UiKit.Stretch("Top", transform);

            // Left column: shift name, clock, tips + streak, complaints.
            var col = UiKit.Rect("LeftColumn", top, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -26), new Vector2(380, 1000));

            var namePlate = Plate(col, "NamePlate", new Vector2(0, 0), new Vector2(380, 104));
            shiftDay = UiKit.Text("Day", namePlate, "MONDAY", 22, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 28), new Vector2(0, 26));
            shiftName = UiKit.Text("ShiftName", namePlate, "First Day", 40, Palette.Cream, UiKit.Display, TextAlignmentOptions.Left, new Vector2(340, 50), new Vector2(0, -14));

            var clockPlate = Plate(col, "ClockPlate", new Vector2(0, -118), new Vector2(380, 128));
            clockLabel = UiKit.Text("ClockLbl", clockPlate, "SHIFT ENDS IN", 18, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 24), new Vector2(0, 40));
            clock = UiKit.Text("Clock", clockPlate, "2:00", 74, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 84), new Vector2(0, -12));

            var tips = Plate(col, "TipsPlate", new Vector2(0, -260), new Vector2(380, 128));
            TipsAnchor = tips;
            UiKit.Text("TipsLabel", tips, "TIPS", 18, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 24), new Vector2(0, 40));
            score = UiKit.Text("Score", tips, "$0", 62, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 76), new Vector2(0, -12));
            streakBadge = UiKit.Image("StreakBadge", tips, UiKit.Pill, Palette.Brass, new Vector2(116, 44), new Vector2(110, 40));
            streak = UiKit.Text("Streak", streakBadge.transform, "x1.00", 24, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(116, 44));

            var cplate = Plate(col, "Complaints", new Vector2(0, -402), new Vector2(380, 104));
            UiKit.Text("CLabel", cplate, "COMPLAINTS", 18, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(340, 24), new Vector2(0, 30));
            for (int i = 0; i < complaintSlots.Length; i++)
                complaintSlots[i] = UiKit.Image("Slot" + i, cplate, UiKit.Circle, new Color(1, 1, 1, 0.15f), new Vector2(46, 46), new Vector2(-136 + i * 62, -14));

            Panel = PanelUi.Create(transform, runner);
        }

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

        static RectTransform Plate(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = UiKit.Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.12f, 0.08f, 0.13f, 0.86f);
            var inner = UiKit.Rect("Content", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40, 0));
            return inner;
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
            b.Ring.color = frac > 0.5f ? Palette.Good : frac > 0.25f ? Palette.Warn : Palette.Bad;
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
            b.BadgeText.fontSize = 17;
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
        }

        public void PopupAt(Vector3 world, string text, Color color, float size = 40f, float dur = 1.3f)
        {
            foreach (var other in popups)
                if (other.T < 0.35f && Vector3.Distance(other.World, world) < 1.6f) world += Vector3.up * 0.9f;
            var t = UiKit.Text("Popup", popupLayer, text, size, color, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(500, size * 1.3f));
            t.outlineWidth = 0.22f;
            t.outlineColor = new Color32(30, 20, 34, 255);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
            popups.Add(new Popup { Text = t, World = world, Dur = dur, Rise = 70f });
        }

        void UpdatePopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.T += dt / p.Dur;
                if (p.T >= 1f || !p.Text) { if (p.Text) Destroy(p.Text.gameObject); popups.RemoveAt(i); continue; }
                var screen = worldCam.WorldToScreenPoint(p.World);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
                p.Text.rectTransform.anchoredPosition = local + canvasRt.rect.size * 0.5f + Vector2.up * (p.Rise * Ease.OutCubic(p.T) + 20f);
                float s = p.T < 0.15f ? Ease.OutBack(p.T / 0.15f, 3f) : 1f;
                p.Text.rectTransform.localScale = Vector3.one * s;
                p.Text.alpha = p.T > 0.7f ? 1f - (p.T - 0.7f) / 0.3f : 1f;
            }
        }

        // ---------------------------------------------------------------- frame

        public void PunchScore() => scorePunch = 1f;

        sealed class FlyCoin { public RectTransform Rt; public Vector2 From, Ctrl; public float T, Dur; }
        readonly List<FlyCoin> flying = new List<FlyCoin>();

        /// <summary>Coins arc from a world position into the tips counter.</summary>
        public void FlyCoins(Vector3 world, int count)
        {
            var screen = worldCam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
            var from = local + canvasRt.rect.size * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var img = UiKit.Image("Coin", popupLayer, UiKit.Circle, Palette.Hex(0xFFC83D), new Vector2(26, 26));
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
                        float pulse = danger ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
                        m.Rt.localScale = Vector3.one * pulse;
                        var world = runner.Building[f].transform.position + new Vector3(Layout.ShaftHalf + 0.55f, 1.25f, -1.4f);
                        var screen = worldCam.WorldToScreenPoint(world);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, CanvasCam, out var local);
                        m.Rt.anchoredPosition = local + canvasRt.rect.size * 0.5f;
                    }
            }
            for (int i = used; i < marks.Count; i++) marks[i].Rt.gameObject.SetActive(false);
        }

        TextMeshProUGUI banner;
        float bannerT = 1f;

        /// <summary>A big word that slams in across the middle of the screen and fades.</summary>
        public void Banner(string text, Color color)
        {
            if (banner == null)
            {
                banner = UiKit.Text("Banner", transform, "", 120, color, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1600, 180), new Vector2(-180, 120));
                banner.outlineWidth = 0.18f;
                banner.outlineColor = new Color32(30, 20, 34, 255);
            }
            banner.text = text;
            banner.color = color;
            bannerT = 0f;
        }

        void UpdateBanner(float dt)
        {
            if (banner == null) return;
            bannerT = Mathf.Min(1f, bannerT + dt / 1.6f);
            float t = bannerT;
            float s = t < 0.12f ? Mathf.Lerp(2.2f, 1f, Ease.OutBack(t / 0.12f, 2f)) : 1f + (t - 0.12f) * 0.08f;
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
            streakBadge.color = m >= 2.5f ? Palette.Hex(0xFF5DA2) : m >= 2f ? Palette.Hex(0xFF8A3D) : m >= 1.5f ? Palette.Hex(0xFFC857) : Palette.Brass;
            streakBadge.rectTransform.localScale = Vector3.one * (1f + 0.25f * streakPunch);

            clockLabel.text = sim.Def.Endless ? "ON SHIFT FOR" : !sim.ClockRunning ? "CLOCK STARTS AFTER YOUR FIRST DROP" : sim.RushHour ? "RUSH HOUR! TIPS x1.5" : "SHIFT ENDS IN";
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
                clock.color = hurry ? Palette.Bad : sim.RushHour ? Palette.Warn : Palette.Cream;
                if (hurry && Mathf.Repeat(sim.TimeLeft, 1f) > 0.85f) clockPunch = 1f;
            }
            clockPunch = Mathf.Max(0f, clockPunch - dt * 4f);
            clock.rectTransform.localScale = Vector3.one * (1f + 0.15f * clockPunch);

            for (int i = 0; i < complaintSlots.Length; i++)
            {
                bool on = i < sim.Complaints;
                var c = complaintSlots[i];
                c.color = on ? Palette.Bad : new Color(1, 1, 1, 0.15f);
                c.rectTransform.localScale = Vector3.Lerp(c.rectTransform.localScale, Vector3.one * (on ? 1.05f : 0.85f), Ease.Damp(10f, dt));
            }

            UpdatePopups(dt);
            UpdateBanner(dt);
            UpdateCoins(dt);
            UpdateRoutePreview();
            Panel.Tick(dt);
        }
    }
}

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
            shownScore = 0;
            lastStreak = 0;
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
            public Image Disc, Ring, Back, Badge;
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
            b.Badge = UiKit.Image("Badge", b.Root, UiKit.Circle, Palette.Ink, new Vector2(28, 28), new Vector2(26, 24));
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
                b.Pop = 0f;
            }
            float frac = Mathf.Clamp01(p.PatienceFrac);
            b.Ring.fillAmount = frac;
            b.Ring.color = frac > 0.5f ? Palette.Good : frac > 0.25f ? Palette.Warn : Palette.Bad;
            b.Pop = Mathf.Min(1f, b.Pop + dt * 4f);
            b.Wobble = Mathf.Max(0f, b.Wobble - dt * 2f);
            bool riding = p.State == PState.Riding;
            float scale = Ease.OutBack(b.Pop, 2f) * (riding ? 0.78f : 1f);
            if (frac < 0.25f && !p.Fuming) scale *= 1f + 0.08f * Mathf.Sin(Time.time * 14f);
            b.Root.localScale = Vector3.one * scale;
            b.Root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30f) * 12f * b.Wobble);

            // rule badge
            string badge = "";
            Color badgeCol = Palette.Ink;
            switch (p.Kind)
            {
                case Kind.Houseplant: if (!p.Sunned) { badge = "SUN"; badgeCol = Palette.Hex(0xE8A317); } break;
                case Kind.Mirror: badge = "x2"; badgeCol = Palette.Hex(0x3C7DD9); break;
                case Kind.Vampire: badge = "V"; badgeCol = Palette.Hex(0xB3122E); break;
                case Kind.Courier:
                {
                    int left = runner.Sim.B.Leaving[(int)p.Dest];
                    badge = left >= 0 ? left.ToString() : "!";
                    badgeCol = Palette.Hex(0xA4662C);
                    break;
                }
                case Kind.Swimmer: badge = "~"; badgeCol = Palette.Hex(0x1F86D0); break;
                case Kind.Kid: badge = "K"; badgeCol = Palette.Hex(0xF07F3C); break;
                case Kind.Tycoon: badge = p.ExpressBroken ? "$" : "$$"; badgeCol = p.ExpressBroken ? Palette.Hex(0x777777) : Palette.Hex(0x6B4E9B); break;
            }
            b.Badge.gameObject.SetActive(badge.Length > 0);
            b.Badge.color = badgeCol;
            b.BadgeText.text = badge;
            b.BadgeText.fontSize = badge.Length > 2 ? 10 : 15;
            if (p.Fuming) b.Disc.color = Palette.Bad;

            var screen = worldCam.WorldToScreenPoint(v.BubbleAnchor);
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
            Panel.Tick(dt);
        }
    }
}

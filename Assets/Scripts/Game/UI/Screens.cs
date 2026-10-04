using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    public static class Stars
    {
        static Sprite star;

        public static Sprite Sprite => star ? star : (star = Make(128));

        static Sprite Make(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                float r = (i % 2 == 0 ? 0.48f : 0.21f) * size;
                pts[i] = new Vector2(size / 2f + Mathf.Cos(a) * r, size / 2f + Mathf.Sin(a) * r - size * 0.03f);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 4x supersampled point-in-polygon for smooth edges
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (Inside(pts, new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f))) hits++;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    c = !c;
            return c;
        }

        public static Image[] Row(Transform parent, int count, int lit, Vector2 pos, float size, float gap, bool onPaper = false)
        {
            var imgs = new Image[count];
            for (int i = 0; i < count; i++)
            {
                imgs[i] = UiKit.Image("Star" + i, parent, Sprite, i < lit ? Palette.Hex(0xFFC857) : onPaper ? new Color(0.16f, 0.12f, 0.18f, 0.18f) : new Color(1, 1, 1, 0.18f), new Vector2(size, size),
                    pos + new Vector2((i - (count - 1) / 2f) * (size + gap), 0));
            }
            return imgs;
        }
    }

    // ----------------------------------------------------------------------------- title

    public sealed class TitleScreen : UiScreen
    {
        UiButton start;
        TextMeshProUGUI startSub, totals;
        TextMeshProUGUI logo;
        float t;

        public static TitleScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Title", parent);
            var s = rt.gameObject.AddComponent<TitleScreen>();
            s.Init("Title");
            s.Build(game);
            return s;
        }

        void Build(GameRoot game)
        {
            var left = UiKit.Rect("Left", Root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(760, 1000));
            var glow = UiKit.Image("Glow", left, UiKit.SoftCircle, new Color(0.08f, 0.05f, 0.1f, 0.55f), new Vector2(1500, 1300), new Vector2(150, 0));
            logo = UiKit.Text("Logo", left, "One More\nFloor", 132, Palette.Cream, UiKit.Display, TextAlignmentOptions.BottomLeft, new Vector2(760, 330), new Vector2(0, 290));
            logo.lineSpacing = -38;
            logo.outlineWidth = 0.12f;
            logo.outlineColor = new Color32(140, 90, 30, 255);
            logo.textWrappingMode = TextWrappingModes.Normal;
            var tag = UiKit.Text("Tag", left, "Run the elevator in a hotel that rearranges itself between stops.", 30, Palette.Hex(0xF3E6C8), UiKit.Body,
                TextAlignmentOptions.TopLeft, new Vector2(700, 90), new Vector2(-30, 70));
            tag.textWrappingMode = TextWrappingModes.Normal;

            start = UiButton.Create(left, "START SHIFT", new Vector2(-130, -80), new Vector2(460, 92), () => game.ShowIntro(SaveData.Current.NextShift()), true, 40);
            startSub = UiKit.Text("Sub", left, "", 24, Palette.Brass, UiKit.Body, TextAlignmentOptions.Left, new Vector2(460, 30), new Vector2(-130, -142));
            UiButton.Create(left, "DUTY ROSTER", new Vector2(-130, -210), new Vector2(460, 76), () => game.ShowRoster(), false, 32);
            UiButton.Create(left, "SETTINGS", new Vector2(-130, -300), new Vector2(460, 76), () => game.ShowSettings(this), false, 32);
            UiButton.Create(left, "QUIT", new Vector2(-130, -390), new Vector2(460, 76), () => game.Quit(), false, 32);
            totals = UiKit.Text("Totals", left, "", 24, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(600, 30), new Vector2(-50, -470));
            var foot = UiKit.Text("Foot", Root, "Models in Blender · music & sound synthesized from scratch", 20, new Color(1, 1, 1, 0.55f), UiKit.Body,
                TextAlignmentOptions.Right, new Vector2(900, 30), Vector2.zero);
            UiKit.SetAnchorsPoint(foot.rectTransform, new Vector2(1, 0));
            foot.rectTransform.pivot = new Vector2(1, 0);
            foot.rectTransform.anchoredPosition = new Vector2(-30, 20);
            Primary = () => start.Click();
        }

        public override void Show()
        {
            base.Show();
            var save = SaveData.Current;
            var next = ShiftCatalog.Get(save.NextShift());
            startSub.text = $"Next up: {next.Day} · {next.Title}";
            totals.text = $"STARS {save.TotalStars()} / {ShiftCatalog.All.Count * 3}";
            start.Focused = true;
        }

        protected override void Update()
        {
            base.Update();
            t += Time.unscaledDeltaTime;
            if (logo) logo.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.8f) * 0.6f);
        }
    }

    // ----------------------------------------------------------------------------- duty roster

    public sealed class RosterScreen : UiScreen
    {
        GameRoot game;
        readonly List<GameObject> cards = new List<GameObject>();
        RectTransform grid;

        public static RosterScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Roster", parent);
            var s = rt.gameObject.AddComponent<RosterScreen>();
            s.Init("Roster");
            s.game = game;
            Dim(rt, 0.82f);
            UiKit.Text("Title", rt, "Duty Roster", 92, Palette.Cream, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1200, 120), new Vector2(0, 420));
            UiKit.Text("Sub", rt, "A week at The Shuffleton. Earn a star to unlock the next shift.", 28, Palette.Brass, UiKit.Body, TextAlignmentOptions.Center, new Vector2(1400, 40), new Vector2(0, 345));
            s.grid = UiKit.Rect("Grid", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1700, 640));
            UiButton.Create(rt, "BACK", new Vector2(0, -440), new Vector2(300, 70), () => game.ShowTitle(), false, 30);
            s.Back = () => game.ShowTitle();
            return s;
        }

        public override void Show()
        {
            Rebuild();
            base.Show();
        }

        void Rebuild()
        {
            foreach (var c in cards) Destroy(c);
            cards.Clear();
            var save = SaveData.Current;
            var all = ShiftCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                int row = i / 5, colI = i % 5;
                var pos = new Vector2(-680 + colI * 340, 155 - row * 330);
                bool unlocked = save.Unlocked(i);
                var rim = UiKit.Image("Card" + i, grid, UiKit.Rounded, unlocked ? Palette.Brass : new Color(0.4f, 0.35f, 0.3f), new Vector2(316, 306), pos, true);
                cards.Add(rim.gameObject);
                var paper = UiKit.Image("Paper", rim.transform, UiKit.Rounded, unlocked ? Palette.Hex(0xF6EBD3) : Palette.Hex(0x7A7068), new Vector2(300, 290));
                var stripe = UiKit.Image("Stripe", paper.transform, UiKit.Rounded, def.NewKind.HasValue ? Palette.KindColor(def.NewKind.Value) : Palette.Oxblood, new Vector2(300, 50), new Vector2(0, 120));
                UiKit.Text("Day", stripe.transform, def.Day.ToUpperInvariant(), 22, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(290, 40));
                var title = UiKit.Text("T", paper.transform, def.Title, 36, Palette.Ink, UiKit.Display, TextAlignmentOptions.Center, new Vector2(290, 90), new Vector2(0, 50));
                title.textWrappingMode = TextWrappingModes.Normal;
                string what = def.NewKind.HasValue ? "New: " + Defs.Of(def.NewKind.Value).Name : def.Endless ? "Endless" : "Everyone";
                UiKit.Text("New", paper.transform, what, 22, Palette.Oxblood, UiKit.Body, TextAlignmentOptions.Center, new Vector2(290, 30), new Vector2(0, -12));
                if (unlocked)
                {
                    Stars.Row(paper.transform, 3, save.Stars[i], new Vector2(0, -62), 46, 8, true);
                    UiKit.Text("Best", paper.transform, save.Best[i] > 0 ? "Best $" + save.Best[i].ToString("N0") : "Not played yet", 22, Palette.Ink, UiKit.Signage,
                        TextAlignmentOptions.Center, new Vector2(290, 30), new Vector2(0, -112));
                    var btn = rim.gameObject.AddComponent<UiButton>();
                    btn.Bg = rim;
                    btn.Label = title;
                    int idx = i;
                    btn.OnClick = () => game.ShowIntro(idx);
                    btn.SetColors(rim.color, Palette.Hex(0xFFE08A));
                }
                else
                {
                    UiKit.Text("Lock", paper.transform, "LOCKED", 34, Palette.Hex(0x3A322E), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(290, 40), new Vector2(0, -70));
                    UiKit.Text("How", paper.transform, $"Earn a star on {all[i - 1].Day}", 20, Palette.Hex(0x3A322E), UiKit.Body, TextAlignmentOptions.Center, new Vector2(290, 30), new Vector2(0, -110));
                }
            }
            GameRoot.SetLayerRecursive(gameObject, GameRoot.UiLayer);
        }
    }

    // ----------------------------------------------------------------------------- shift intro

    public sealed class IntroScreen : UiScreen
    {
        GameRoot game;
        TextMeshProUGUI day, title, note, newTitle, newRule;
        RectTransform goalsRoot;
        Image kindDisc;
        RectTransform noteRt;
        int shift;
        float t;

        public static IntroScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Intro", parent);
            var s = rt.gameObject.AddComponent<IntroScreen>();
            s.Init("Intro");
            s.game = game;
            s.Build();
            return s;
        }

        void Build()
        {
            Dim(Root, 0.6f);
            day = UiKit.Text("Day", Root, "", 36, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(1200, 50), new Vector2(0, 400));
            title = UiKit.Text("Title", Root, "", 110, Palette.Cream, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1600, 140), new Vector2(0, 310));
            title.outlineWidth = 0.1f;
            title.outlineColor = new Color32(140, 90, 30, 255);

            // sticky note from The Management
            var paper = UiKit.Image("Note", Root, null, Palette.Hex(0xFFE987), new Vector2(560, 380), new Vector2(-380, 0));
            noteRt = paper.rectTransform;
            var tape = UiKit.Image("Tape", paper.transform, null, new Color(1, 1, 1, 0.45f), new Vector2(160, 40), new Vector2(0, 180));
            tape.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
            note = UiKit.Text("NoteText", paper.transform, "", 40, Palette.Hex(0x3A2A1A), UiKit.Hand, TextAlignmentOptions.TopLeft, new Vector2(480, 260), new Vector2(0, 20));
            note.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Text("Sign", paper.transform, "— The Management", 34, Palette.Hex(0x7A2F38), UiKit.Hand, TextAlignmentOptions.Right, new Vector2(480, 50), new Vector2(0, -145));

            // new today
            var card = Card(Root, new Vector2(620, 380), new Vector2(380, 0));
            UiKit.Text("NewLbl", card, "NEW TODAY", 28, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(560, 40), new Vector2(0, 150));
            kindDisc = UiKit.Image("Disc", card, UiKit.Circle, Palette.Floor(FloorId.Lobby), new Vector2(170, 170), new Vector2(-195, 20));
            kindDisc.preserveAspect = true;
            newTitle = UiKit.Text("NewTitle", card, "", 48, Palette.Cream, UiKit.Display, TextAlignmentOptions.Left, new Vector2(380, 70), new Vector2(110, 80));
            newRule = UiKit.Text("Rule", card, "", 29, Palette.Cream, UiKit.Body, TextAlignmentOptions.TopLeft, new Vector2(370, 220), new Vector2(110, -40));
            newRule.textWrappingMode = TextWrappingModes.Normal;

            goalsRoot = UiKit.Rect("Goals", Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1400, 60));
            var go = UiButton.Create(Root, "CLOCK IN", new Vector2(110, -360), new Vector2(380, 96), () => game.BeginShift(shift), true, 44);
            UiButton.Create(Root, "BACK", new Vector2(-230, -360), new Vector2(240, 80), () => game.ShowRoster(), false, 30);
            Primary = () => go.Click();
            Back = () => game.ShowRoster();
        }

        public void Setup(int index)
        {
            shift = index;
            var def = ShiftCatalog.Get(index);
            day.text = def.Day.ToUpperInvariant();
            title.text = def.Title;
            note.text = def.Note;
            if (def.NewKind.HasValue)
            {
                var k = def.NewKind.Value;
                newTitle.text = Defs.Of(k).Name;
                kindDisc.color = Palette.KindColor(k);
                kindDisc.sprite = Icons.Kind(k) ?? UiKit.Circle;
                if (Icons.Kind(k) != null) kindDisc.color = Color.white;
            }
            else
            {
                newTitle.text = def.Endless ? "Overtime" : "Everyone";
                var bell = Icons.Kind(Kind.Commuter) != null ? Icons.Get("kind_bellhop") : null;
                kindDisc.sprite = bell ?? UiKit.Circle;
                kindDisc.color = bell != null ? Color.white : Palette.Bad;
            }
            newRule.text = def.NewText;
            foreach (Transform c in goalsRoot) Destroy(c.gameObject);
            if (def.Endless)
                UiKit.Text("G", goalsRoot, $"Last as long as you can.   Best: ${SaveData.Current.Best[index]:N0}", 32, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(1400, 50));
            else
                for (int i = 0; i < 3; i++)
                {
                    float x = -420 + i * 420;
                    Stars.Row(goalsRoot, i + 1, i + 1, new Vector2(x - 70, 0), 40, 4);
                    UiKit.Text("G" + i, goalsRoot, "$" + def.Stars[i].ToString("N0"), 34, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left,
                        new Vector2(220, 50), new Vector2(x + 110, 0));
                }
            GameRoot.SetLayerRecursive(gameObject, GameRoot.UiLayer);
        }

        protected override void Update()
        {
            base.Update();
            t += Time.unscaledDeltaTime;
            if (noteRt) noteRt.localRotation = Quaternion.Euler(0, 0, -3f + Mathf.Sin(t * 1.3f) * 0.4f);
        }
    }

    // ----------------------------------------------------------------------------- pause

    public sealed class PauseScreen : UiScreen
    {
        public static PauseScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Pause", parent);
            var s = rt.gameObject.AddComponent<PauseScreen>();
            s.Init("Pause");
            Dim(rt, 0.6f);
            var card = Card(rt, new Vector2(560, 560), Vector2.zero);
            UiKit.Text("T", card, "Paused", 80, Palette.Cream, UiKit.Display, TextAlignmentOptions.Center, new Vector2(500, 100), new Vector2(0, 190));
            var resume = UiButton.Create(card, "RESUME", new Vector2(0, 70), new Vector2(400, 80), () => game.Resume(), true, 34);
            UiButton.Create(card, "RESTART SHIFT", new Vector2(0, -25), new Vector2(400, 70), () => game.RestartShift(), false, 28);
            UiButton.Create(card, "SETTINGS", new Vector2(0, -110), new Vector2(400, 70), () => game.ShowSettings(s), false, 28);
            UiButton.Create(card, "QUIT TO ROSTER", new Vector2(0, -195), new Vector2(400, 70), () => game.QuitShift(), false, 28);
            s.Primary = () => resume.Click();
            s.Back = () => game.Resume();
            return s;
        }
    }

    // ----------------------------------------------------------------------------- settings

    public sealed class SettingsScreen : UiScreen
    {
        UiScreen returnTo;
        GameRoot game;
        UiSlider master, music, sfx;
        UiToggle full, shake, forecast;

        public static SettingsScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Settings", parent);
            var s = rt.gameObject.AddComponent<SettingsScreen>();
            s.Init("Settings");
            s.game = game;
            Dim(rt, 0.65f);
            var card = Card(rt, new Vector2(860, 720), Vector2.zero);
            UiKit.Text("T", card, "Settings", 80, Palette.Cream, UiKit.Display, TextAlignmentOptions.Center, new Vector2(700, 100), new Vector2(0, 280));
            var save = SaveData.Current;
            s.master = UiSlider.Create(card, "MASTER", new Vector2(0, 170), save.Master, v => { save.Master = v; game.ApplySettings(); });
            s.music = UiSlider.Create(card, "MUSIC", new Vector2(0, 90), save.Music, v => { save.Music = v; game.ApplySettings(); });
            s.sfx = UiSlider.Create(card, "EFFECTS", new Vector2(0, 10), save.Sfx, v => { save.Sfx = v; game.ApplySettings(); AudioDirector.Instance?.Sfx("coin", 0.6f, 1f, 0f, 0f, 0.15f); });
            s.full = UiToggle.Create(card, "FULLSCREEN", new Vector2(0, -80), save.Fullscreen, v => { save.Fullscreen = v; game.ApplySettings(); });
            s.shake = UiToggle.Create(card, "SCREEN SHAKE", new Vector2(0, -150), save.ScreenShake, v => { save.ScreenShake = v; game.ApplySettings(); });
            s.forecast = UiToggle.Create(card, "SHOW SHUFFLE FORECAST", new Vector2(0, -220), save.ShowForecast, v => { save.ShowForecast = v; game.ApplySettings(); });
            UiButton.Create(card, "DONE", new Vector2(0, -305), new Vector2(300, 74), () => s.Close(), true, 32);
            s.Primary = () => s.Close();
            s.Back = () => s.Close();
            return s;
        }

        public void Open(UiScreen from)
        {
            returnTo = from;
            from?.Hide();
            Show();
        }

        void Close()
        {
            SaveData.Current.Save();
            Hide();
            returnTo?.Show();
        }
    }

    // ----------------------------------------------------------------------------- results

    public sealed class ResultsScreen : UiScreen
    {
        GameRoot game;
        TextMeshProUGUI stamp, tips, lines, hint, bestStamp;
        Image[] stars;
        UiButton retry, next;
        RectTransform paper;
        int targetScore, shownScore, starCount, shift;
        float t;
        bool newBest;

        public static ResultsScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Results", parent);
            var s = rt.gameObject.AddComponent<ResultsScreen>();
            s.Init("Results");
            s.game = game;
            s.Build();
            return s;
        }

        void Build()
        {
            Dim(Root, 0.55f);
            var p = UiKit.Image("TimeCard", Root, UiKit.Rounded, Palette.Hex(0xF6EBD3), new Vector2(760, 740), new Vector2(0, 40));
            paper = p.rectTransform;
            var band = UiKit.Image("Band", p.transform, UiKit.Rounded, Palette.Oxblood, new Vector2(760, 90), new Vector2(0, 325));
            UiKit.Text("Hdr", band.transform, "THE SHUFFLETON · TIME CARD", 28, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(740, 60));
            stamp = UiKit.Text("Stamp", p.transform, "CLOCKED OUT", 70, Palette.Hex(0x2F6F6A), UiKit.Display, TextAlignmentOptions.Center, new Vector2(740, 100), new Vector2(0, 220));
            tips = UiKit.Text("Tips", p.transform, "$0", 96, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(740, 110), new Vector2(0, 110));
            stars = Stars.Row(p.transform, 3, 0, new Vector2(0, 10), 96, 20, true);
            lines = UiKit.Text("Lines", p.transform, "", 30, Palette.Ink, UiKit.Body, TextAlignmentOptions.Center, new Vector2(700, 140), new Vector2(0, -115));
            lines.textWrappingMode = TextWrappingModes.Normal;
            hint = UiKit.Text("Hint", p.transform, "", 26, Palette.Oxblood, UiKit.Body, TextAlignmentOptions.Center, new Vector2(700, 40), new Vector2(0, -205));
            bestStamp = UiKit.Text("Best", p.transform, "NEW BEST!", 56, Palette.Bad, UiKit.Display, TextAlignmentOptions.Center, new Vector2(400, 80), new Vector2(240, 160));
            bestStamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 14);
            retry = UiButton.Create(Root, "ONE MORE SHIFT", new Vector2(0, -400), new Vector2(460, 96), () => game.RestartShift(), true, 38);
            next = UiButton.Create(Root, "NEXT SHIFT", new Vector2(370, -400), new Vector2(260, 80), () => game.ShowIntro(shift + 1), false, 28);
            UiButton.Create(Root, "ROSTER", new Vector2(-370, -400), new Vector2(260, 80), () => game.ShowRoster(), false, 28);
            Primary = () => retry.Click();
            Back = () => game.ShowRoster();
        }

        public void Setup(ShiftSim sim, bool best)
        {
            shift = sim.Def.Index;
            targetScore = sim.Score;
            shownScore = 0;
            starCount = sim.StarCount;
            newBest = best;
            t = 0f;
            stamp.text = sim.Fired ? "YOU'RE FIRED!" : sim.Def.Endless ? "OFF THE CLOCK" : "CLOCKED OUT";
            stamp.color = sim.Fired ? Palette.Bad : Palette.Hex(0x2F6F6A);
            string time = sim.Def.Endless ? $" · Lasted {Mathf.FloorToInt(sim.Time) / 60}:{Mathf.FloorToInt(sim.Time) % 60:00}" : "";
            lines.text = $"Guests delivered: <b>{sim.DeliveredCount}</b>    Best streak: <b>{sim.BestStreak}</b>\nComplaints: <b>{sim.Complaints}</b>    Stops: <b>{sim.Stops}</b>{time}";
            if (starCount < 3)
                hint.text = $"{starCount + 1} star{(starCount + 1 > 1 ? "s" : "")} at ${sim.Def.Stars[starCount]:N0}";
            else
                hint.text = "A perfect shift. The Management is speechless.";
            foreach (var s in stars) { s.color = new Color(0.16f, 0.12f, 0.18f, 0.18f); s.rectTransform.localScale = Vector3.one; }
            bestStamp.gameObject.SetActive(false);
            bool hasNext = shift + 1 < ShiftCatalog.All.Count && SaveData.Current.Unlocked(shift + 1);
            next.gameObject.SetActive(hasNext);
            tips.text = "$0";
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            paper.localRotation = Quaternion.Euler(0, 0, -1.5f);
            if (t > 0.4f && shownScore < targetScore)
            {
                int step = Mathf.Max(1, Mathf.CeilToInt(targetScore * dt / 1.6f));
                shownScore = Mathf.Min(targetScore, shownScore + step);
                if (Random.value < 0.5f) AudioDirector.Instance?.Sfx("tick", 0.25f, 1.6f, 0f, 0.05f, 0.04f);
                tips.text = "$" + shownScore.ToString("N0");
            }
            for (int i = 0; i < 3; i++)
            {
                float at = 2.1f + i * 0.45f;
                if (i < starCount && t >= at && stars[i].color.a < 0.5f)
                {
                    stars[i].color = Palette.Hex(0xFFC857);
                    AudioDirector.Instance?.Sfx("star_" + (i + 1), 0.8f, 1f, 0f, 0f, 0f);
                }
                float k = Mathf.Clamp01((t - at) / 0.35f);
                if (i < starCount) stars[i].rectTransform.localScale = Vector3.one * (k <= 0f ? 1f : Mathf.Lerp(1.8f, 1f, Ease.OutBack(k, 3f)));
            }
            float bestAt = 2.2f + starCount * 0.45f;
            if (newBest && t >= bestAt && !bestStamp.gameObject.activeSelf)
            {
                bestStamp.gameObject.SetActive(true);
                AudioDirector.Instance?.Sfx("new_record", 0.8f);
            }
            if (bestStamp.gameObject.activeSelf)
                bestStamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, Ease.OutBack(Mathf.Clamp01((t - bestAt) / 0.3f), 2f));
            retry.Focused = true;
        }
    }

    // ----------------------------------------------------------------------------- ending

    public sealed class EndingScreen : UiScreen
    {
        RectTransform crawl;
        float t;
        GameRoot game;

        static readonly string Credits =
            "<size=120%><b>Stay.</b></size>\n<size=70%>— The Building</size>\n\n\n" +
            "You made it through a whole week at The Shuffleton.\nThe floors have stopped arguing about who goes on top.\nMostly.\n\n\n" +
            "<b>ONE MORE FLOOR</b>\n\n" +
            "Design, code, models, music and sound\n<i>made from scratch for this game</i>\n\n" +
            "Engine: Unity 6 (URP)\nModels: Blender 4.5, built by script\nMusic & effects: synthesized in Python with numpy\n\n" +
            "Fonts (SIL Open Font License):\nLimelight · Bungee · Varela Round · Patrick Hand\n\n\n" +
            "Overtime is now open.\nThe building never sleeps.\n\n\n\nThanks for riding.";

        public static EndingScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Ending", parent);
            var s = rt.gameObject.AddComponent<EndingScreen>();
            s.Init("Ending");
            s.game = game;
            Dim(rt, 0.85f);
            s.crawl = UiKit.Rect("Crawl", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200, 2400));
            var txt = UiKit.Text("Text", s.crawl, Credits, 44, Palette.Cream, UiKit.Body, TextAlignmentOptions.Top, new Vector2(1200, 2400));
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.richText = true;
            UiButton.Create(rt, "CONTINUE", new Vector2(700, -460), new Vector2(300, 74), () => game.ShowRoster(), true, 30);
            s.Primary = () => game.ShowRoster();
            s.Back = () => game.ShowRoster();
            return s;
        }

        public override void Show()
        {
            base.Show();
            t = 0f;
        }

        protected override void Update()
        {
            base.Update();
            t += Time.unscaledDeltaTime;
            crawl.anchoredPosition = new Vector2(0, 440f + Mathf.Max(0f, t - 3f) * 55f);
        }
    }
}

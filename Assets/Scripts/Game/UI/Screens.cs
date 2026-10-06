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

        public static readonly Color Lit = Palette.Hex(0xFFC857);
        public static Color Unlit(bool onPaper) => onPaper ? new Color(0.35f, 0.25f, 0.18f, 0.2f) : new Color(1, 1, 1, 0.14f);

        public static Image[] Row(Transform parent, int count, int lit, Vector2 pos, float size, float gap, bool onPaper = false)
        {
            var imgs = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector2((i - (count - 1) / 2f) * (size + gap), 0);
                if (i < lit) UiKit.Image("StarGlow" + i, parent, UiKit.SoftCircle, new Color(1f, 0.75f, 0.3f, onPaper ? 0f : 0.25f), new Vector2(size * 1.8f, size * 1.8f), p);
                imgs[i] = UiKit.Image("Star" + i, parent, Sprite, i < lit ? Lit : Unlit(onPaper), new Vector2(size, size), p);
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
            var scrim = UiKit.Image("Scrim", Root, Deco.Scrim, new Color(0.07f, 0.03f, 0.09f, 0.88f), new Vector2(1250, 10));
            scrim.rectTransform.anchorMin = new Vector2(0, 0);
            scrim.rectTransform.anchorMax = new Vector2(0, 1);
            scrim.rectTransform.pivot = new Vector2(0, 0.5f);
            scrim.rectTransform.anchoredPosition = Vector2.zero;
            scrim.rectTransform.sizeDelta = new Vector2(1250, 0);

            const float W = 760f;
            float L(float w) => -W * 0.5f + w * 0.5f;
            var left = UiKit.Rect("Left", Root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(W, 1000));

            var eyebrow = Deco.Label("Eyebrow", left, "THE SHUFFLETON  ·  EST. 1929", 20, new Vector2(600, 30), new Vector2(L(600), 380));
            eyebrow.characterSpacing = 12f;
            logo = UiKit.Text("Logo", left, "One More\nFloor", 136, Color.white, UiKit.Display, TextAlignmentOptions.BottomLeft, new Vector2(W + 100, 300), new Vector2(50, 216));
            logo.lineSpacing = -30;
            Deco.Gilded(logo, 0.14f);
            Deco.Divider(left, 520, new Vector2(L(520), 44));
            var tag = UiKit.Text("Tag", left, "Run the elevator in a hotel that rearranges itself between stops.", 29, Palette.Hex(0xEADFC8), UiKit.Body,
                TextAlignmentOptions.TopLeft, new Vector2(600, 80), new Vector2(L(600), -12));
            tag.textWrappingMode = TextWrappingModes.Normal;
            Deco.Shadowed(tag, 0.7f, 0.6f, 0.4f);

            start = UiButton.Create(left, "START SHIFT", new Vector2(L(480), -120), new Vector2(480, 96), () => game.ShowIntro(SaveData.Current.NextShift()), true, 40);
            startSub = UiKit.Text("Sub", left, "", 22, Deco.Muted, UiKit.Body, TextAlignmentOptions.Center, new Vector2(480, 30), new Vector2(L(480), -190));
            UiButton.Create(left, "DUTY ROSTER", new Vector2(L(480), -260), new Vector2(480, 70), () => game.ShowRoster(), false, 28);
            UiButton.Create(left, "SETTINGS", new Vector2(L(480), -345), new Vector2(480, 70), () => game.ShowSettings(this), false, 28);
            UiButton.Create(left, "QUIT", new Vector2(L(480), -430), new Vector2(480, 70), () => game.Quit(), false, 28);

            // bottom right: star total over the credit line
            var foot = UiKit.Rect("Foot", Root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 24), new Vector2(900, 80));
            UiKit.Image("Star", foot, Stars.Sprite, Stars.Lit, new Vector2(30, 30), new Vector2(432, 18));
            totals = UiKit.Text("Totals", foot, "", 26, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(300, 40), new Vector2(262, 18));
            Deco.Shadowed(totals);
            UiKit.Text("Credit", foot, "Models built in Blender  ·  music and sound synthesized from scratch", 18, new Color(1, 1, 1, 0.5f), UiKit.Body,
                TextAlignmentOptions.Right, new Vector2(900, 30), new Vector2(0, -22));
            Primary = () => start.Click();
        }

        public override void Show()
        {
            base.Show();
            var save = SaveData.Current;
            var next = ShiftCatalog.Get(save.NextShift());
            startSub.text = $"Next up:  <color=#F2C66B>{next.Day}</color>  ·  {next.Title}";
            totals.text = $"{save.TotalStars()} / {ShiftCatalog.All.Count * 3}";
            start.Focused = true;
        }

        protected override void Update()
        {
            base.Update();
            t += UiTime.Dt;
            if (logo) logo.rectTransform.localRotation = Quaternion.Euler(0, 0, SaveData.Current.ReducedMotion ? 0f : Mathf.Sin(t * 0.8f) * 0.5f);
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
            Dim(rt, 0.88f);
            var eb = Deco.Label("Eyebrow", rt, "THE SHUFFLETON  ·  STAFF ONLY", 20, new Vector2(800, 30), new Vector2(0, 470), TextAlignmentOptions.Center);
            eb.characterSpacing = 12f;
            Deco.Gilded(UiKit.Text("Title", rt, "Duty Roster", 96, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1200, 120), new Vector2(0, 400)));
            Deco.Divider(rt, 560, new Vector2(0, 336));
            UiKit.Text("Sub", rt, "A week at The Shuffleton. Earn a star to unlock the next shift, or clock out three times trying.", 24, Deco.Muted, UiKit.Body, TextAlignmentOptions.Center, new Vector2(1400, 36), new Vector2(0, 302));
            s.grid = UiKit.Rect("Grid", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(1700, 680));
            UiButton.Create(rt, "BACK", new Vector2(0, -474), new Vector2(280, 66), () => game.ShowTitle(), false, 26);
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
            int nextUp = save.NextShift();
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                int row = i / 5, colI = i % 5;
                var pos = new Vector2(-680 + colI * 340, 172 - row * 350);
                bool unlocked = save.Unlocked(i);
                var card = Deco.Panel("Card" + i, grid, new Vector2(304, 326), pos, out var face, true);
                cards.Add(card.gameObject);
                var accent = def.NewKind.HasValue ? Palette.KindColor(def.NewKind.Value) : Palette.Hex(0xC8323F);
                var muted = new Color(1, 1, 1, 0.38f);

                Deco.Label("Day", card, def.Day.ToUpperInvariant(), 19, new Vector2(280, 28), new Vector2(0, 134), TextAlignmentOptions.Center,
                    unlocked ? Deco.Gold : muted);
                UiKit.Image("Accent", card, UiKit.Pill, unlocked ? Color.Lerp(accent, Color.white, 0.15f) : new Color(1, 1, 1, 0.15f), new Vector2(64, 5), new Vector2(0, 114));

                UiKit.Image("DiscBg", card, Deco.Well, unlocked ? Color.white : new Color(1, 1, 1, 0.5f), new Vector2(92, 92), new Vector2(0, 58));
                var icon = def.NewKind.HasValue ? Icons.Kind(def.NewKind.Value) : Icons.Get("kind_bellhop");
                var disc = UiKit.Image("Icon", card, icon ?? UiKit.Circle, icon != null ? (unlocked ? Color.white : new Color(0.1f, 0.07f, 0.12f, 0.85f)) : accent, new Vector2(80, 80), new Vector2(0, 60));
                disc.preserveAspect = true;

                var title = UiKit.Text("T", card, def.Title, 34, unlocked ? Palette.Cream : muted, UiKit.Display, TextAlignmentOptions.Center, new Vector2(272, 74), new Vector2(0, -16));
                title.textWrappingMode = TextWrappingModes.Normal;
                title.enableAutoSizing = true;
                title.fontSizeMin = 24;
                title.fontSizeMax = 34;
                title.lineSpacing = -18;
                if (unlocked) Deco.Shadowed(title);
                string what = def.NewKind.HasValue ? "NEW: " + Defs.Of(def.NewKind.Value).Name.ToUpperInvariant() : def.Endless ? "ENDLESS" : "EVERYONE";
                Deco.Label("New", card, what, 15, new Vector2(272, 24), new Vector2(0, -64), TextAlignmentOptions.Center, unlocked ? Color.Lerp(accent, Color.white, 0.45f) : muted);

                if (unlocked)
                {
                    Stars.Row(card, 3, save.Stars[i], new Vector2(0, -102), 34, 6);
                    bool relaxedOnly = save.Stars[i] == 0 && save.RelaxedClear[i];
                    var best = UiKit.Text("Best", card, relaxedOnly ? "RELAXED CLEAR" : save.Best[i] > 0 ? "BEST  $" + save.Best[i].ToString("N0") : i == nextUp ? "UP NEXT" : "NOT PLAYED", 17,
                        relaxedOnly ? Palette.Hex(0x8FD6C4) : save.Best[i] > 0 ? Palette.Cream : i == nextUp ? Deco.Gold : Deco.Muted, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(272, 26), new Vector2(0, -136));
                    best.characterSpacing = 4f;
                    int idx = i;
                    var btn = UiButton.Attach(card, face, () => game.ShowIntro(idx));
                    btn.Focused = i == nextUp;
                }
                else
                {
                    face.color = new Color(0.62f, 0.58f, 0.62f, 0.92f);
                    Deco.Label("Lock", card, "LOCKED", 20, new Vector2(272, 30), new Vector2(0, -104), TextAlignmentOptions.Center, new Color(1, 1, 1, 0.5f));
                    int tries = Progress.TriesToLatePass(i, save.Stars, save.Plays);
                    string how = tries > 0 && save.Plays[i - 1] > 0
                        ? $"A star on {all[i - 1].Day}, or {tries} more {(tries == 1 ? "try" : "tries")}"
                        : $"Earn a star on {all[i - 1].Day}";
                    var howText = UiKit.Text("How", card, how, 17, new Color(1, 1, 1, 0.38f), UiKit.Body, TextAlignmentOptions.Center, new Vector2(272, 26), new Vector2(0, -134));
                    howText.enableAutoSizing = true;
                    howText.fontSizeMin = 12;
                    howText.fontSizeMax = 17;
                }
            }
            GameRoot.SetLayerRecursive(gameObject, GameRoot.UiLayer);
        }
    }

    // ----------------------------------------------------------------------------- shift intro

    public sealed class IntroScreen : UiScreen
    {
        GameRoot game;
        TextMeshProUGUI day, title, note, newLabel, newTitle, newRule, relaxedLine;
        UiButton daily;
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
            Dim(Root, 0.72f);
            day = Deco.Label("Day", Root, "", 30, new Vector2(1200, 44), new Vector2(0, 440), TextAlignmentOptions.Center);
            day.characterSpacing = 16f;
            title = Deco.Gilded(UiKit.Text("Title", Root, "", 112, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1600, 140), new Vector2(0, 350)));
            Deco.Divider(Root, 640, new Vector2(0, 272));

            // sticky note from The Management
            var noteHolder = UiKit.Rect("NoteHolder", Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, 10), new Vector2(560, 390));
            noteRt = noteHolder;
            UiKit.Image("Shadow", noteHolder, Deco.Shadow, new Color(1, 1, 1, 0.9f), new Vector2(620, 450), new Vector2(8, -16));
            var paper = UiKit.Image("Note", noteHolder, null, Palette.Hex(0xFFE987), new Vector2(560, 390));
            UiKit.Image("Fold", paper.transform, null, new Color(0.85f, 0.7f, 0.2f, 0.25f), new Vector2(560, 60), new Vector2(0, -165));
            var tape = UiKit.Image("Tape", paper.transform, null, new Color(1, 1, 0.95f, 0.5f), new Vector2(170, 42), new Vector2(0, 190));
            tape.rectTransform.localRotation = Quaternion.Euler(0, 0, 4);
            note = UiKit.Text("NoteText", paper.transform, "", 40, Palette.Hex(0x3A2A1A), UiKit.Hand, TextAlignmentOptions.TopLeft, new Vector2(480, 260), new Vector2(0, 30));
            note.textWrappingMode = TextWrappingModes.Normal;
            note.enableAutoSizing = true;
            note.fontSizeMin = 28;
            note.fontSizeMax = 40;
            UiKit.Text("Sign", paper.transform, "— The Management", 34, Palette.Hex(0x7A2F38), UiKit.Hand, TextAlignmentOptions.Right, new Vector2(480, 50), new Vector2(0, -150));

            // new today
            var card = Card(Root, new Vector2(640, 390), new Vector2(380, 10));
            newLabel = Deco.Label("NewLbl", card, "NEW TODAY", 22, new Vector2(560, 32), new Vector2(0, 150), TextAlignmentOptions.Center);
            newLabel.characterSpacing = 12f;
            Deco.Divider(card, 360, new Vector2(0, 122), 0.8f);
            UiKit.Image("DiscBg", card, Deco.Well, Color.white, new Vector2(188, 188), new Vector2(-196, -20));
            kindDisc = UiKit.Image("Disc", card, UiKit.Circle, Palette.Floor(FloorId.Lobby), new Vector2(160, 160), new Vector2(-196, -16));
            kindDisc.preserveAspect = true;
            newTitle = UiKit.Text("NewTitle", card, "", 46, Palette.Cream, UiKit.Display, TextAlignmentOptions.Left, new Vector2(370, 64), new Vector2(110, 60));
            Deco.Shadowed(newTitle);
            newRule = UiKit.Text("Rule", card, "", 26, Palette.Hex(0xE6DAC2), UiKit.Body, TextAlignmentOptions.TopLeft, new Vector2(370, 200), new Vector2(110, -64));
            newRule.textWrappingMode = TextWrappingModes.Normal;
            newRule.lineSpacing = 6;

            relaxedLine = Deco.Label("Relaxed", Root, "", 18, new Vector2(1400, 28), new Vector2(0, -329), TextAlignmentOptions.Center);
            relaxedLine.characterSpacing = 6f;
            goalsRoot = UiKit.Rect("Goals", Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -268), new Vector2(1400, 80));
            var go = UiButton.Create(Root, "CLOCK IN", new Vector2(120, -400), new Vector2(400, 96), () => game.BeginShift(shift), true, 42);
            UiButton.Create(Root, "BACK", new Vector2(-220, -400), new Vector2(220, 72), () => game.ShowRoster(), false, 26);
            daily = UiButton.Create(Root, "TODAY'S SHIFT", new Vector2(520, -400), new Vector2(330, 72), () => game.BeginDaily(), false, 26);
            go.Focused = true;
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
                newLabel.text = "NEW TODAY";
                newTitle.text = Defs.Of(k).Name;
                kindDisc.color = Palette.KindColor(k);
                kindDisc.sprite = Icons.Kind(k) ?? UiKit.Circle;
                if (Icons.Kind(k) != null) kindDisc.color = Color.white;
            }
            else
            {
                newLabel.text = def.Endless ? "NO END IN SIGHT" : "ALL HANDS";
                newTitle.text = def.Endless ? "Overtime" : "Everyone";
                var bell = Icons.Get("kind_bellhop");
                kindDisc.sprite = bell ?? UiKit.Circle;
                kindDisc.color = bell != null ? Color.white : Palette.Bad;
            }
            newRule.text = def.Id == "monday" && Controls.Pad ? "Press A to let a guest in, then pick their floor with up/down and press A."
                         : def.Id == "monday" && Controls.KeyNav ? "Press Enter to let a guest in, then pick their floor with Up/Down and press Enter."
                         : def.NewText;
            relaxedLine.text = SaveData.Current.Relaxed && !def.Endless
                ? "RELAXED SHIFT  ·  MORE PATIENCE  ·  NO FIRING  ·  A 1-STAR SCORE OPENS THE NEXT SHIFT  ·  STARS AREN'T SAVED" : "";
            foreach (Transform c in goalsRoot) Destroy(c.gameObject);
            daily.gameObject.SetActive(def.Endless);
            if (def.Endless)
            {
                var key = GameRoot.DateKey(game.Today);
                int today = SaveData.Current.DailyBestOn(key);
                var p = Deco.Panel("Goal", goalsRoot, new Vector2(1100, 76), Vector2.zero, true);
                UiKit.Text("G", p, $"Last as long as you can.    <color=#F2C66B>Best ${SaveData.Current.Best[index]:N0}</color>    " +
                    $"Today's shift ({GameRoot.DateLabel(game.Today)}): " + (today > 0 ? $"<color=#F2C66B>${today:N0}</color>" : "not played"), 26, Palette.Cream, UiKit.Signage,
                    TextAlignmentOptions.Center, new Vector2(1060, 50));
            }
            else
                for (int i = 0; i < 3; i++)
                {
                    var p = Deco.Panel("Goal" + i, goalsRoot, new Vector2(300, 76), new Vector2(-330 + i * 330, 0), true);
                    Stars.Row(p, i + 1, i + 1, new Vector2(-64, 0), 30, 2);
                    UiKit.Text("Amt", p, "$" + def.Stars[i].ToString("N0"), 30, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(150, 50), new Vector2(60, 0));
                }
            GameRoot.SetLayerRecursive(gameObject, GameRoot.UiLayer);
        }

        protected override void Update()
        {
            base.Update();
            t += UiTime.Dt;
            if (noteRt) noteRt.localRotation = Quaternion.Euler(0, 0, -3f + (SaveData.Current.ReducedMotion ? 0f : Mathf.Sin(t * 1.3f) * 0.4f));
        }
    }

    // ----------------------------------------------------------------------------- pause

    public sealed class PauseScreen : UiScreen
    {
        TextMeshProUGUI reason;

        /// <summary>Why the game paused on its own ("" when the player paused).</summary>
        public string Reason
        {
            get => reason.text;
            set => reason.text = string.IsNullOrEmpty(value) ? "" : value.ToUpperInvariant();
        }

        public static PauseScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Pause", parent);
            var s = rt.gameObject.AddComponent<PauseScreen>();
            s.Init("Pause");
            Dim(rt, 0.7f);
            var card = Card(rt, new Vector2(560, 600), Vector2.zero);
            Heading(card, "Paused", 214, 460, 80);
            s.reason = Deco.Label("Reason", card, "", 17, new Vector2(480, 26), new Vector2(0, 268), TextAlignmentOptions.Center);
            s.reason.characterSpacing = 6f;
            var resume = UiButton.Create(card, "RESUME", new Vector2(0, 86), new Vector2(420, 86), () => game.Resume(), true, 34);
            UiButton.Create(card, "RESTART SHIFT", new Vector2(0, -12), new Vector2(420, 68), () => game.RestartShift(), false, 25);
            UiButton.Create(card, "SETTINGS", new Vector2(0, -94), new Vector2(420, 68), () => game.ShowSettings(s), false, 25);
            UiButton.Create(card, "QUIT TO ROSTER", new Vector2(0, -176), new Vector2(420, 68), () => game.QuitShift(), false, 25);
            UiKit.Text("Hint", card, "ESC TO RESUME", 16, Deco.Muted, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(400, 24), new Vector2(0, -250)).characterSpacing = 8f;
            resume.Focused = true;
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
        UiToggle full, shake, forecast, motion, relaxed;

        public static SettingsScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Settings", parent);
            var s = rt.gameObject.AddComponent<SettingsScreen>();
            s.Init("Settings");
            s.game = game;
            Dim(rt, 0.75f);
            var card = Card(rt, new Vector2(900, 900), Vector2.zero);
            Heading(card, "Settings", 356, 600, 80);
            var save = SaveData.Current;
            Deco.Label("Audio", card, "SOUND", 18, new Vector2(720, 26), new Vector2(0, 262), TextAlignmentOptions.Left, Deco.Muted).characterSpacing = 12f;
            s.master = UiSlider.Create(card, "MASTER", new Vector2(0, 210), save.Master, v => { save.Master = v; game.ApplySettings(); });
            s.music = UiSlider.Create(card, "MUSIC", new Vector2(0, 144), save.Music, v => { save.Music = v; game.ApplySettings(); });
            s.sfx = UiSlider.Create(card, "EFFECTS", new Vector2(0, 78), save.Sfx, v => { save.Sfx = v; game.ApplySettings(); AudioDirector.Instance?.Sfx("coin", 0.6f, 1f, 0f, 0f, 0.15f); });
            Deco.Label("Game", card, "GAME", 18, new Vector2(720, 26), new Vector2(0, 10), TextAlignmentOptions.Left, Deco.Muted).characterSpacing = 12f;
            s.full = UiToggle.Create(card, "FULLSCREEN", new Vector2(0, -38), save.Fullscreen, v => { save.Fullscreen = v; game.ApplySettings(); });
            s.shake = UiToggle.Create(card, "SCREEN SHAKE", new Vector2(0, -100), save.ScreenShake, v => { save.ScreenShake = v; game.ApplySettings(); });
            s.forecast = UiToggle.Create(card, "SHUFFLE FORECAST", new Vector2(0, -162), save.ShowForecast, v => { save.ShowForecast = v; game.ApplySettings(); });
            s.motion = UiToggle.Create(card, "REDUCED MOTION", new Vector2(0, -224), save.ReducedMotion, v => { save.ReducedMotion = v; game.ApplySettings(); });
            s.relaxed = UiToggle.Create(card, "RELAXED SHIFTS", new Vector2(0, -286), save.Relaxed, v => { save.Relaxed = v; game.ApplySettings(); });
            var done = UiButton.Create(card, "DONE", new Vector2(0, -382), new Vector2(300, 74), () => s.Close(), true, 30);
            done.Focused = true;
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
        TextMeshProUGUI stamp, tips, hint, causes, advice, bestStamp, bestLine;
        readonly TextMeshProUGUI[] statLabel = new TextMeshProUGUI[4], statValue = new TextMeshProUGUI[4];
        Image[] stars;
        UiButton retry, next;
        RectTransform paper, bestBox;
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
            Dim(Root, 0.65f);
            paper = Deco.Panel("TimeCard", Root, new Vector2(760, 800), new Vector2(0, 40), false, Deco.Paper);
            var ink = Palette.Hex(0x2A1E2E);
            var band = UiKit.Image("Band", paper, UiKit.Rounded, Palette.Oxblood, new Vector2(720, 70), new Vector2(0, 346));
            var hdr = Deco.Label("Hdr", band.transform, "THE SHUFFLETON  ·  TIME CARD", 22, new Vector2(700, 40), Vector2.zero, TextAlignmentOptions.Center, Palette.Cream);
            hdr.characterSpacing = 10f;
            stamp = UiKit.Text("Stamp", paper, "CLOCKED OUT", 64, Palette.Hex(0x2F6F6A), UiKit.Display, TextAlignmentOptions.Center, new Vector2(700, 90), new Vector2(0, 254));
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 2f);
            tips = UiKit.Text("Tips", paper, "$0", 104, ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(700, 120), new Vector2(0, 152));
            stars = Stars.Row(paper, 3, 0, new Vector2(0, 48), 88, 18, true);
            UiKit.Image("Rule", paper, null, new Color(0.5f, 0.35f, 0.2f, 0.3f), new Vector2(620, 2), new Vector2(0, -18));
            for (int i = 0; i < 4; i++)
            {
                float x = -255 + i * 170;
                statLabel[i] = UiKit.Text("L" + i, paper, "", 15, Palette.Hex(0x8C6424), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(170, 24), new Vector2(x, -44));
                statLabel[i].characterSpacing = 6f;
                statValue[i] = UiKit.Text("V" + i, paper, "", 40, ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(170, 50), new Vector2(x, -82));
                if (i > 0) UiKit.Image("Sep" + i, paper, null, new Color(0.5f, 0.35f, 0.2f, 0.25f), new Vector2(2, 60), new Vector2(x - 85, -64));
            }
            UiKit.Image("Rule2", paper, null, new Color(0.5f, 0.35f, 0.2f, 0.3f), new Vector2(620, 2), new Vector2(0, -124));
            hint = UiKit.Text("Hint", paper, "", 24, Palette.Oxblood, UiKit.Body, TextAlignmentOptions.Center, new Vector2(700, 36), new Vector2(0, -150));
            hint.richText = true;
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 16;
            hint.fontSizeMax = 24;
            // what the complaints were about, and one tip for the biggest cause
            causes = UiKit.Text("Causes", paper, "", 15, Palette.Hex(0x8C6424), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(700, 26), new Vector2(0, -184));
            causes.characterSpacing = 3f;
            causes.enableAutoSizing = true;
            causes.fontSizeMin = 11;
            causes.fontSizeMax = 15;
            advice = UiKit.Text("Advice", paper, "", 20, Palette.Hex(0x2A1E2E), UiKit.Body, TextAlignmentOptions.Top, new Vector2(640, 56), new Vector2(0, -226));
            advice.textWrappingMode = TextWrappingModes.Normal;
            var stampInk = Palette.Hex(0xC8303A);
            stampInk.a = 0.88f;
            bestBox = UiKit.Rect("BestStamp", paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -318), new Vector2(330, 84));
            bestBox.localRotation = Quaternion.Euler(0, 0, -5f);
            var ring = UiKit.Image("Ring", bestBox, Deco.StampRing, stampInk, new Vector2(330, 84));
            Deco.Fill(ring.rectTransform);
            bestStamp = UiKit.Text("Best", bestBox, "NEW BEST!", 46, Palette.Hex(0xC8303A), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(310, 70), new Vector2(0, -2));
            bestStamp.characterSpacing = 8f;
            bestLine = UiKit.Text("BestLine", paper, "", 18, Palette.Hex(0x8C6424), UiKit.Signage, TextAlignmentOptions.Center, new Vector2(600, 30), new Vector2(0, -318));
            bestLine.characterSpacing = 6f;
            retry = UiButton.Create(Root, "ONE MORE SHIFT", new Vector2(0, -420), new Vector2(460, 96), () => game.RestartShift(), true, 36);
            next = UiButton.Create(Root, "NEXT SHIFT", new Vector2(380, -420), new Vector2(260, 74), () => game.ShowIntro(shift + 1), false, 26);
            UiButton.Create(Root, "ROSTER", new Vector2(-380, -420), new Vector2(260, 74), () => game.ShowRoster(), false, 26);
            Primary = () => retry.Click();
            Back = () => game.ShowRoster();
        }

        public string CausesText => causes.text;
        public string AdviceText => advice.text;
        public string HintText => hint.text;
        public string BestLineText => bestLine.text;

        public void Setup(ShiftSim sim, bool best, bool latePass = false, bool relaxedOpened = false)
        {
            shift = sim.Def.Index;
            targetScore = sim.Score;
            shownScore = 0;
            starCount = sim.StarCount;
            newBest = best;
            t = 0f;
            stamp.text = sim.Fired ? "YOU'RE FIRED!" : sim.Def.Endless ? "OFF THE CLOCK" : "CLOCKED OUT";
            stamp.color = sim.Fired ? Palette.Bad : Palette.Hex(0x2F6F6A);
            int secs = Mathf.FloorToInt(sim.Time);
            SetStat(0, "DELIVERED", sim.DeliveredCount.ToString());
            SetStat(1, "BEST STREAK", sim.BestStreak.ToString());
            SetStat(2, "COMPLAINTS", sim.Complaints.ToString());
            if (sim.Def.Endless) SetStat(3, "LASTED", $"{secs / 60}:{secs % 60:00}");
            else SetStat(3, "STOPS", sim.Stops.ToString());
            statValue[2].color = sim.Complaints > 0 ? Palette.Oxblood : Palette.Hex(0x2A1E2E);
            if (sim.Def.Endless)
                hint.text = "The building never sleeps.";
            else if (latePass)
                hint.text = $"<b>Late pass:</b> we'll pretend we didn't see that. <b>{ShiftCatalog.Get(shift + 1).Day}</b> is open.";
            else if (relaxedOpened)
                hint.text = $"<b>Relaxed clear!</b> <b>{ShiftCatalog.Get(shift + 1).Day}</b> is open.";
            else if (sim.Relaxed)
                hint.text = starCount >= 1 ? "<b>Relaxed clear.</b> Turn Relaxed off in Settings to go for stars."
                    : $"A relaxed clear needs <b>${sim.Def.Stars[0]:N0}</b>  ·  ${sim.Def.Stars[0] - sim.Score:N0} to go";
            else if (sim.Fired && SaveData.Current.FiredCount[shift] >= 2 && !SaveData.Current.Relaxed)
                hint.text = "Fired twice here? <b>Relaxed shifts</b> in Settings give guests more patience.";
            else if (starCount < 3)
                hint.text = $"Next star at <b>${sim.Def.Stars[starCount]:N0}</b>  ·  ${sim.Def.Stars[starCount] - sim.Score:N0} to go";
            else
                hint.text = "A perfect shift. The Management is speechless.";
            causes.text = Advice.Summary(sim.ComplaintsBy).ToUpperInvariant();
            var biggest = Advice.Biggest(sim.ComplaintsBy);
            advice.text = biggest.HasValue ? Advice.Tip(biggest.Value) : "";
            foreach (var s in stars) { s.color = Stars.Unlit(true); s.rectTransform.localScale = Vector3.one; }
            bestBox.gameObject.SetActive(false);
            bestStamp.text = "NEW BEST!";
            int prev = SaveData.Current.Best[shift];
            bestLine.text = sim.Relaxed ? "RELAXED SHIFT  ·  STARS AND BESTS AREN'T SAVED" : !best && prev > 0 ? $"PERSONAL BEST  ${prev:N0}" : "";
            bool hasNext = shift + 1 < ShiftCatalog.All.Count && SaveData.Current.Unlocked(shift + 1);
            next.gameObject.SetActive(hasNext);
            tips.text = "$0";
        }

        /// <summary>Today's Overtime: today's best and the best day so far instead of the all-time line.</summary>
        public void SetupDaily(string dateKey, string dateLabel, bool newToday)
        {
            var save = SaveData.Current;
            hint.text = $"<b>Today's shift</b> · {dateLabel} · the same building all day. Tomorrow brings a new one.";
            string record = save.DailyRecord > 0 && save.DailyRecordDate != dateKey
                ? $"  ·  BEST DAY  ${save.DailyRecord:N0}" : "";
            // a new best today gets the stamp instead (they share a spot on the card)
            bestLine.text = newToday ? "" : $"TODAY'S BEST  ${save.DailyBestOn(dateKey):N0}{record}";
            newBest = newToday;
            bestStamp.text = "BEST TODAY!";
        }

        void SetStat(int i, string label, string value)
        {
            statLabel[i].text = label;
            statValue[i].text = value;
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;
            float dt = UiTime.Dt;
            t += dt;
            paper.localRotation = Quaternion.Euler(0, 0, -0.4f);
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
                    stars[i].color = Stars.Lit;
                    AudioDirector.Instance?.Sfx("star_" + (i + 1), 0.8f, 1f, 0f, 0f, 0f);
                }
                float k = Mathf.Clamp01((t - at) / 0.35f);
                if (i < starCount) stars[i].rectTransform.localScale = Vector3.one * (k <= 0f ? 1f : Mathf.Lerp(1.8f, 1f, Ease.OutBack(k, 3f)));
            }
            float bestAt = 2.2f + starCount * 0.45f;
            if (newBest && t >= bestAt && !bestBox.gameObject.activeSelf)
            {
                bestBox.gameObject.SetActive(true);
                AudioDirector.Instance?.Sfx("new_record", 0.8f);
            }
            if (bestBox.gameObject.activeSelf)
                bestBox.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, Ease.OutBack(Mathf.Clamp01((t - bestAt) / 0.3f), 2f));
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
            "<size=140%><color=#F2C66B><b>Stay.</b></color></size>\n<size=70%>— The Building</size>\n\n\n" +
            "You made it through a whole week at The Shuffleton.\nThe floors have stopped arguing about who goes on top.\nMostly.\n\n\n" +
            "<color=#F2C66B><b>ONE MORE FLOOR</b></color>\n\n" +
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
            UiKit.Stretch("Backdrop", rt).gameObject.AddComponent<Image>().color = new Color(0.05f, 0.03f, 0.07f, 0.6f);
            Dim(rt, 0.9f);
            s.crawl = UiKit.Rect("Crawl", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200, 2400));
            var txt = UiKit.Text("Text", s.crawl, Credits, 44, Palette.Cream, UiKit.Body, TextAlignmentOptions.Top, new Vector2(1200, 2400));
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.richText = true;
            Deco.Shadowed(txt);
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
            t += UiTime.Dt;
            crawl.anchoredPosition = new Vector2(0, 440f + Mathf.Max(0f, t - 3f) * 55f);
        }
    }
}

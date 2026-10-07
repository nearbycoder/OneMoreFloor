using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// What the guide lists: every guest and every way the building misbehaves, each with the shift that introduces it.
    /// An entry is open once that shift is.
    /// </summary>
    public static class Guide
    {
        public struct Entry
        {
            public string Name, Rule;
            public Kind? Kind;
            /// <summary>The shift that introduces it.</summary>
            public int Shift;
        }

        static List<Entry> guests, building;

        public static List<Entry> Guests
        {
            get
            {
                if (guests != null) return guests;
                guests = new List<Entry>();
                foreach (Kind k in System.Enum.GetValues(typeof(Kind)))
                    guests.Add(new Entry { Name = Defs.Of(k).Name, Rule = Defs.Of(k).Rule, Kind = k, Shift = First(d => d.NewKind == k) });
                return guests;
            }
        }

        public static List<Entry> Building
        {
            get
            {
                if (building != null) return building;
                int Card(CardType c) => First(d => d.Deck[(int)c] > 0f);
                building = new List<Entry>
                {
                    new Entry { Name = "Swap", Rule = "Two floors trade places.", Shift = Card(CardType.Swap) },
                    new Entry { Name = "Calm", Rule = "Nothing moves this stop.", Shift = Card(CardType.Calm) },
                    new Entry { Name = "Jammed", Rule = "The floor you're docked at can't move. A card that names it jams.", Shift = 0 },
                    new Entry { Name = "Rise", Rule = "A floor jumps to the top.", Shift = Card(CardType.Rise) },
                    new Entry { Name = "Sink", Rule = "A floor drops to the bottom.", Shift = Card(CardType.Sink) },
                    new Entry { Name = "Roll", Rule = "A block of three floors rotates.", Shift = Card(CardType.Roll) },
                    new Entry { Name = "Leaving", Rule = "A floor counts down its stops, then drifts off. Another slides in.", Shift = First(d => d.DepartPeriod > 0f) },
                    new Entry { Name = "Ocean", Rule = "Drops in for a few stops. Sunny, and full of swimmers.", Shift = First(d => d.HasOcean) },
                    new Entry { Name = "Flip", Rule = "A block of four or five floors turns upside down.", Shift = Card(CardType.Flip) },
                };
                return building;
            }
        }

        static int First(System.Func<ShiftDef, bool> f)
        {
            for (int i = 0; i < ShiftCatalog.All.Count; i++) if (f(ShiftCatalog.Get(i))) return i;
            return 0;
        }

        /// <summary>Entries open with this progress (the shift that introduces them is open).</summary>
        public static int OpenCount(List<Entry> list, System.Func<int, bool> unlocked)
        {
            int n = 0;
            foreach (var e in list) if (unlocked(e.Shift)) n++;
            return n;
        }
    }

    /// <summary>The guest guide: every guest and shuffle card met so far, from the pause card or the roster.</summary>
    public sealed class GuideScreen : UiScreen
    {
        UiScreen returnTo;
        RectTransform body;
        readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>Self-test: guests and building entries shown open on the last build.</summary>
        public int GuestsOpen { get; private set; }
        public int BuildingOpen { get; private set; }

        public static GuideScreen Create(Transform parent, GameRoot game)
        {
            var rt = UiKit.Stretch("Guide", parent);
            var s = rt.gameObject.AddComponent<GuideScreen>();
            s.Init("Guide");
            Dim(rt, 0.8f);
            var card = Card(rt, new Vector2(1560, 860), Vector2.zero);
            Heading(card, "Guest Guide", 360, 640, 72);
            s.body = UiKit.Rect("Body", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 860));
            Deco.Label("GuestsLbl", card, "GUESTS", 18, new Vector2(860, 26), new Vector2(-300, 268), TextAlignmentOptions.Left, Deco.Muted).characterSpacing = 12f;
            Deco.Label("BuildingLbl", card, "THE BUILDING", 18, new Vector2(580, 26), new Vector2(440, 268), TextAlignmentOptions.Left, Deco.Muted).characterSpacing = 12f;
            var back = UiButton.Create(card, "BACK", new Vector2(0, -366), new Vector2(300, 70), () => s.Close(), true, 28);
            back.Focused = true;
            s.Primary = () => s.Close();
            s.Back = () => s.Close();
            return s;
        }

        public void Open(UiScreen from)
        {
            returnTo = from;
            from?.Hide();
            Rebuild();
            Show();
        }

        void Close()
        {
            Hide();
            returnTo?.Show();
        }

        void Rebuild()
        {
            foreach (var r in rows) Destroy(r);
            rows.Clear();
            var save = SaveData.Current;
            var all = ShiftCatalog.All;
            string Arrives(int shift) => "Arrives on " + all[shift].Day + ".";
            var muted = new Color(1, 1, 1, 0.38f);

            GuestsOpen = 0;
            var guests = Guide.Guests;
            for (int i = 0; i < guests.Count; i++)
            {
                var e = guests[i];
                bool open = save.Unlocked(e.Shift);
                if (open) GuestsOpen++;
                var pos = new Vector2(-520 + (i % 2) * 450, 190 - (i / 2) * 136);
                var row = UiKit.Rect("Guest" + i, body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(430, 124));
                rows.Add(row.gameObject);
                var accent = Palette.KindColor(e.Kind.Value);
                UiKit.Image("Disc", row, Deco.Well, open ? Color.white : new Color(1, 1, 1, 0.5f), new Vector2(88, 88), new Vector2(-165, 4));
                var icon = Icons.Kind(e.Kind.Value);
                var img = UiKit.Image("Icon", row, icon ?? UiKit.Circle, icon != null ? (open ? Color.white : new Color(0.1f, 0.07f, 0.12f, 0.85f)) : accent, new Vector2(74, 74), new Vector2(-165, 6));
                img.preserveAspect = true;
                var name = UiKit.Text("Name", row, open ? e.Name : "???", 30, open ? Palette.Cream : muted, UiKit.Display, TextAlignmentOptions.Left, new Vector2(310, 40), new Vector2(50, 36));
                if (open) Deco.Shadowed(name);
                var rule = UiKit.Text("Rule", row, open ? e.Rule : Arrives(e.Shift), 19, open ? Palette.Hex(0xE6DAC2) : muted, UiKit.Body, TextAlignmentOptions.TopLeft, new Vector2(300, 72), new Vector2(45, -22));
                rule.textWrappingMode = TextWrappingModes.Normal;
                rule.enableAutoSizing = true;
                rule.fontSizeMin = 15;
                rule.fontSizeMax = 19;
            }

            BuildingOpen = 0;
            var building = Guide.Building;
            for (int i = 0; i < building.Count; i++)
            {
                var e = building[i];
                bool open = save.Unlocked(e.Shift);
                if (open) BuildingOpen++;
                var row = UiKit.Rect("Card" + i, body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(440, 222 - i * 62), new Vector2(580, 58));
                rows.Add(row.gameObject);
                var name = UiKit.Text("Name", row, open ? e.Name.ToUpperInvariant() : "???", 20, open ? Deco.Gold : muted, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(150, 30), new Vector2(-215, 0));
                name.characterSpacing = 3f;
                var rule = UiKit.Text("Rule", row, open ? e.Rule : Arrives(e.Shift), 19, open ? Palette.Hex(0xE6DAC2) : muted, UiKit.Body, TextAlignmentOptions.Left, new Vector2(420, 56), new Vector2(75, 0));
                rule.textWrappingMode = TextWrappingModes.Normal;
                rule.enableAutoSizing = true;
                rule.fontSizeMin = 14;
                rule.fontSizeMax = 19;
                if (i < building.Count - 1) UiKit.Image("Rule", row, UiKit.Pill, new Color(1, 1, 1, 0.06f), new Vector2(560, 2), new Vector2(0, -31));
            }
            GameRoot.SetLayerRecursive(gameObject, GameRoot.UiLayer);
        }
    }
}

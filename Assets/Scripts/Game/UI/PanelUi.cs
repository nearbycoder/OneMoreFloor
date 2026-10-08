using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>The operator's panel: indicator dial, shuffle forecast, and the floor buttons.</summary>
    public sealed class PanelUi : MonoBehaviour
    {
        ShiftRunner runner;
        RectTransform root;
        TextMeshProUGUI dialNumber, dialName, nextLabel;
        // the forecast's heading, and the shorter words it uses when the text floor draws it bigger on a small screen
        const string NextLong = "NEXT STOP, THE BUILDING WILL...", NextShort = "NEXT STOP, THE BUILDING...", NextShortest = "NEXT STOP, THE HOTEL...";
        RectTransform needle;
        float needleAngle;
        readonly ForecastCard[] cards = new ForecastCard[2];
        readonly SlotButton[] buttons = new SlotButton[9];
        Image boardAll;
        public int HoverSlot = -1;
        public RectTransform Root => root;
        public RectTransform ForecastAnchor => cards[0].Root;
        public RectTransform SlotRect(int slot) => slot >= 0 && slot < buttons.Length ? buttons[slot]?.Root : null;

        sealed class SlotButton
        {
            public RectTransform Root;
            public Image Cap, Plate, Glow, Ring, Icon;
            public TextMeshProUGUI Number, Name;
            public WaitBadge WaitBadge;
            public Image[] Pips = new Image[4];
            public float Press, Lit;
            public int Slot;
        }

        sealed class ForecastCard
        {
            public RectTransform Root;
            public TextMeshProUGUI Title, Body;
            public Image Back;
        }

        public static PanelUi Create(Transform parent, ShiftRunner runner)
        {
            var rt = UiKit.Rect("Panel", parent, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, -20), new Vector2(400, 900));
            var p = rt.gameObject.AddComponent<PanelUi>();
            p.runner = runner;
            p.root = rt;
            p.Build();
            return p;
        }

        void Build()
        {
            var plateArt = Icons.Get("panel_plate");
            var plate = root.gameObject.AddComponent<Image>();
            if (plateArt != null) { plate.sprite = plateArt; plate.color = Color.white; }
            else
            {
                plate.sprite = UiKit.Rounded;
                plate.type = Image.Type.Sliced;
                plate.color = Palette.Hex(0xB8893A);
                UiKit.Image("Inner", root, UiKit.Rounded, Palette.Hex(0x2A1E2E), new Vector2(368, 868));
            }

            // dial (the plate art has a sunburst behind it and a brass name bar below it)
            var dialArt = Icons.Get("panel_dial");
            var dial = UiKit.Image("Dial", root, dialArt ?? UiKit.Circle, dialArt != null ? Color.white : Palette.Hex(0xF3E6C8), new Vector2(236, 236), new Vector2(0, 262));
            if (dialArt == null) UiKit.Image("DialRim", dial.transform, UiKit.Ring, Palette.Brass, new Vector2(236, 236));
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(150f, 30f, i / 8f) * Mathf.Deg2Rad;
                TextFloor.Ornament(UiKit.Text("Tick" + i, dial.transform, (i + 1).ToString(), 15, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center,
                    new Vector2(24, 24), new Vector2(Mathf.Cos(a) * 66f, Mathf.Sin(a) * 66f - 18f)));
            }
            needle = UiKit.Rect("Needle", dial.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, -18), new Vector2(7, 92));
            needle.gameObject.AddComponent<Image>().color = Palette.Hex(0xC0262D);
            UiKit.Image("Hub", dial.transform, UiKit.Circle, Palette.Brass, new Vector2(26, 26), new Vector2(0, -18));
            dialNumber = UiKit.Text("Num", dial.transform, "1", 30, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(100, 40), new Vector2(0, -62));
            dialName = UiKit.Text("Name", root, "LOBBY", 24, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(250, 40), new Vector2(0, 86));

            // forecast
            nextLabel = UiKit.Text("NextLbl", root, NextLong, 15, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(340, 24), new Vector2(0, 44));
            for (int i = 0; i < 2; i++)
            {
                var c = new ForecastCard();
                c.Back = UiKit.Image("Card" + i, root, Deco.Paper, i == 0 ? Color.white : new Color(0.84f, 0.8f, 0.74f), new Vector2(320, 54), new Vector2(0, 2 - i * 60));
                c.Back.pixelsPerUnitMultiplier = 2f;
                c.Root = c.Back.rectTransform;
                c.Title = UiKit.Text("T", c.Root, "", 14, Palette.Oxblood, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(292, 20), new Vector2(0, 13));
                c.Title.characterSpacing = 4f;
                c.Body = CardBody(c.Root);
                cards[i] = c;
            }

            // buttons: 3x3 like a real panel, 1 at the bottom left
            var btnArt = Icons.Get("panel_button");
            for (int s = 0; s < 9; s++)
            {
                int row = s / 3, colI = s % 3;
                var pos = new Vector2(-108 + colI * 108, -385 + row * 114);
                var b = new SlotButton { Slot = s };
                b.Root = UiKit.Rect("Btn" + (s + 1), root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(100, 112));
                b.Glow = UiKit.Image("Glow", b.Root, UiKit.SoftCircle, new Color(1f, 0.75f, 0.3f, 0f), new Vector2(150, 150), new Vector2(0, 18));
                if (btnArt == null) b.Ring = UiKit.Image("Bezel", b.Root, UiKit.Circle, Palette.Brass, new Vector2(78, 78), new Vector2(0, 18));
                b.Cap = UiKit.Image("Cap", b.Root, btnArt ?? UiKit.Circle, btnArt != null ? Color.white : Palette.Hex(0xF3E6C8), new Vector2(btnArt != null ? 82 : 64, btnArt != null ? 82 : 64), new Vector2(0, 18), true);
                b.Number = UiKit.Text("N", b.Cap.transform, (s + 1).ToString(), 30, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(64, 64));
                b.Plate = UiKit.Image("Plate", b.Root, UiKit.Pill, Palette.Floor(FloorId.Lobby), new Vector2(100, 30), new Vector2(0, -38));
                b.Name = UiKit.Text("Name", b.Plate.transform, "", 15, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(60, 30), new Vector2(16, 0));
                Deco.Shadowed(b.Name, 0.55f, 0.6f, 0.2f);
                b.Icon = UiKit.Image("Icon", b.Plate.transform, null, Color.white, new Vector2(30, 30), new Vector2(-28, 1));
                b.Icon.preserveAspect = true;
                b.WaitBadge = WaitBadge.Create(b.Root, new Vector2(36, 50));
                for (int k = 0; k < 4; k++)
                    b.Pips[k] = UiKit.Image("Pip" + k, b.Root, UiKit.Circle, Palette.Good, new Vector2(12, 12), new Vector2(-42, 50 - k * 14));
                int slot = s;
                var trig = b.Cap.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trig, EventTriggerType.PointerClick, _ => runner.RequestSend(slot, true));
                AddTrigger(trig, EventTriggerType.PointerEnter, _ => HoverSlot = slot);
                AddTrigger(trig, EventTriggerType.PointerExit, _ => { if (HoverSlot == slot) HoverSlot = -1; });
                buttons[s] = b;
            }
            litSprite = Icons.Get("panel_button_lit");
            unlitSprite = btnArt;
        }

        Sprite litSprite, unlitSprite;

        static void AddTrigger(EventTrigger trig, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(action);
            trig.triggers.Add(e);
        }

        GameObject forecastRoot;

        public void ShowForecast(bool on)
        {
            foreach (var c in cards) if (c != null) c.Root.gameObject.SetActive(on);
            forecastOn = on;
        }
        bool forecastOn = true;

        public void Press(int slot)
        {
            if (slot >= 0 && slot < buttons.Length) buttons[slot].Press = 1f;
        }

        bool tallCards;
        /// <summary>Self-test: the forecast cards are in their two-line layout (LARGER TEXT on a small screen).</summary>
        public bool TallCards => tallCards;
        public const float TallCardH = 92f, TallBodyH = 60f, TitleOnlyH = 28f;

        /// <summary>Whether a card's line fits on one line (at the size it will be drawn).</summary>
        public static bool FitsOneLine(TMP_Text body) => body.GetPreferredValues(body.text).x <= body.rectTransform.rect.width;

        void LayoutCards(bool tall)
        {
            tallCards = tall;
            var c0 = cards[0];
            var c1 = cards[1];
            // the first card's top edge stays put; both stop short of the buttons' caps
            c0.Root.sizeDelta = new Vector2(320, tall ? TallCardH : 54);
            c0.Root.anchoredPosition = new Vector2(0, tall ? 29f - TallCardH * 0.5f : 2f);
            c0.Title.rectTransform.anchoredPosition = new Vector2(0, tall ? TallCardH * 0.5f - 14f : 13f);
            c0.Body.textWrappingMode = tall ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            c0.Body.alignment = tall ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            c0.Body.rectTransform.sizeDelta = new Vector2(292, tall ? TallBodyH : 26);
            c0.Body.rectTransform.anchoredPosition = new Vector2(0, tall ? TallCardH * 0.5f - 26f - TallBodyH * 0.5f : -9f);
            c1.Root.sizeDelta = new Vector2(320, tall ? TitleOnlyH : 54);
            c1.Root.anchoredPosition = new Vector2(0, tall ? 29f - TallCardH - 4f - TitleOnlyH * 0.5f : -58f);
            c1.Title.rectTransform.anchoredPosition = new Vector2(0, tall ? 0f : 13f);
            c1.Body.gameObject.SetActive(!tall);
        }

        /// <summary>A forecast card's line of text ("Library and Boiler Room").</summary>
        public static TextMeshProUGUI CardBody(Transform card)
        {
            var t = UiKit.Text("B", card, "", 19, Palette.Ink, UiKit.Body, TextAlignmentOptions.Left, new Vector2(292, 26), new Vector2(0, -9));
            t.richText = true;
            return t;
        }

        /// <summary>What a card does, for a forecast card's <see cref="CardBody"/>.</summary>
        public static string Describe(Card c)
        {
            string F(FloorId f) => $"<b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(Palette.Floor(f), Palette.Ink, 0.4f))}>{Defs.Floor(f).Name}</color></b>";
            switch (c.Type)
            {
                case CardType.Swap: return $"{F(c.A)} and {F(c.B)}";
                case CardType.Rise: return $"{F(c.A)} to the top";
                case CardType.Sink: return $"{F(c.A)} to the bottom";
                case CardType.Roll: return $"Roll floors {c.From + 1}-{c.From + c.Len} {(c.Up ? "up" : "down")}";
                case CardType.Flip: return $"Flip floors {c.From + 1}-{c.From + c.Len}!";
                default: return "Stay put";
            }
        }

        string TitleOf(Card c)
        {
            switch (c.Type)
            {
                case CardType.Swap: return "SWAP";
                case CardType.Rise: return "RISE";
                case CardType.Sink: return "SINK";
                case CardType.Roll: return "ROLL";
                case CardType.Flip: return "FLIP";
                default: return "CALM";
            }
        }

        public void Tick(float dt)
        {
            var sim = runner.Sim;
            if (sim == null) return;
            // LARGER TEXT: the longest heading that fits
            string heading = TextFloor.Large ? TextFloor.Fit(nextLabel, NextLong, NextShort, NextShortest) : TextFloor.Raised(nextLabel) ? NextShort : NextLong;
            if (nextLabel.text != heading) nextLabel.text = heading;
            var b = sim.B;

            // dial follows the car
            float target = Mathf.Lerp(150f, 30f, sim.Car.Pos / 8f) - 90f;
            dialName.color = Palette.Ink;
            needleAngle = Mathf.Lerp(needleAngle, target, Ease.Damp(12f, dt));
            needle.localRotation = Quaternion.Euler(0, 0, needleAngle);
            int nearest = Mathf.Clamp(Mathf.RoundToInt(sim.Car.Pos), 0, b.Count - 1);
            dialNumber.text = (nearest + 1).ToString();
            dialName.text = Defs.Floor(b.At(nearest)).Name.ToUpperInvariant();
            dialName.color = Color.Lerp(Palette.Floor(b.At(nearest)), Palette.Ink, 0.82f);

            // forecast
            var pred = new Building(b);
            for (int i = 0; i < cards.Length; i++)
            {
                if (i >= sim.Forecast.Count || !forecastOn) { cards[i].Root.gameObject.SetActive(false); continue; }
                var c = sim.Forecast[i];
                cards[i].Root.gameObject.SetActive(true);
                cards[i].Title.text = (i == 0 ? "NEXT: " : "THEN: ") + TitleOf(c);
                cards[i].Body.text = Describe(c);
                pred.Apply(c, null, null);
            }
            // LARGER TEXT on a small screen: a line that won't fit makes the next card two lines tall, and the one after
            // shows only its title
            bool tall = false;
            if (TextFloor.Large && forecastOn)
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].Root.gameObject.activeSelf && !FitsOneLine(cards[i].Body)) tall = true;
            if (tall != tallCards) LayoutCards(tall);

            // buttons
            int targetSlot = sim.Car.Target.HasValue ? b.SlotOf(sim.Car.Target.Value) : -1;
            for (int s = 0; s < buttons.Length; s++)
            {
                var btn = buttons[s];
                bool exists = s < b.Count;
                btn.Root.gameObject.SetActive(exists);
                if (!exists) continue;
                var f = b.At(s);
                btn.Plate.color = Palette.Floor(f);
                btn.Name.text = Hud.Code(f);
                btn.Icon.sprite = Icons.Floor(f);
                btn.Icon.enabled = btn.Icon.sprite != null;
                bool lit = s == targetSlot || (sim.Car.Has(Kind.Kid) && targetSlot >= 0 && (s - sim.Car.Pos) * (targetSlot - sim.Car.Pos) > 0 && Mathf.Abs(s - sim.Car.Pos) < Mathf.Abs(targetSlot - sim.Car.Pos));
                btn.Lit = Mathf.Lerp(btn.Lit, lit ? 1f : 0f, Ease.Damp(14f, dt));
                btn.Press = Mathf.Max(0f, btn.Press - dt * 5f);
                bool hover = HoverSlot == s || runner.HoverFloor == f;
                if (unlitSprite != null)
                {
                    btn.Cap.sprite = btn.Lit > 0.5f ? litSprite : unlitSprite;
                    btn.Cap.color = hover ? new Color(1f, 0.97f, 0.9f) : Color.white;
                }
                else btn.Cap.color = Color.Lerp(hover ? Palette.Hex(0xFFF6DE) : Palette.Hex(0xF3E6C8), Palette.Hex(0xFFC857), btn.Lit);
                btn.Glow.color = new Color(1f, 0.72f, 0.28f, 0.55f * btn.Lit);
                btn.Cap.rectTransform.localScale = Vector3.one * (1f - 0.12f * Ease.OutCubic(btn.Press) + (hover ? 0.05f : 0f));
                int waiting = sim.Waiting[(int)f].Count;
                btn.WaitBadge.Show(waiting, sim.LowestWaitingPatience(f));
                int pip = 0;
                foreach (var r in sim.Car.Riders)
                {
                    if (r.Dest != f || pip >= btn.Pips.Length) continue;
                    btn.Pips[pip].gameObject.SetActive(true);
                    btn.Pips[pip].color = Palette.KindColor(r.Kind);
                    pip++;
                }
                for (int k = pip; k < btn.Pips.Length; k++) btn.Pips[k].gameObject.SetActive(false);
            }
        }

        /// <summary>Self-test: the waiting badge on the button for a slot.</summary>
        public WaitBadge BadgeAt(int slot) => slot >= 0 && slot < buttons.Length && buttons[slot] != null ? buttons[slot].WaitBadge : null;
    }
}

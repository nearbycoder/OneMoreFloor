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
        TextMeshProUGUI dialNumber, dialName;
        RectTransform needle;
        float needleAngle;
        readonly ForecastCard[] cards = new ForecastCard[2];
        readonly SlotButton[] buttons = new SlotButton[9];
        Image boardAll;
        public int HoverSlot = -1;

        sealed class SlotButton
        {
            public RectTransform Root;
            public Image Cap, Plate, Glow, Ring;
            public TextMeshProUGUI Number, Name;
            public TextMeshProUGUI Waiting;
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
            var plate = root.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Rounded;
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Hex(0xB8893A);
            var inner = UiKit.Image("Inner", root, UiKit.Rounded, Palette.Hex(0x2A1E2E), new Vector2(368, 868));

            // dial
            var dial = UiKit.Image("Dial", root, UiKit.Circle, Palette.Hex(0xF3E6C8), new Vector2(230, 230), new Vector2(0, 300));
            UiKit.Image("DialRim", dial.transform, UiKit.Ring, Palette.Brass, new Vector2(236, 236));
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(150f, 30f, i / 8f) * Mathf.Deg2Rad;
                UiKit.Text("Tick" + i, dial.transform, (i + 1).ToString(), 20, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center,
                    new Vector2(30, 30), new Vector2(Mathf.Cos(a) * 88f, Mathf.Sin(a) * 88f - 10f));
            }
            needle = UiKit.Rect("Needle", dial.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, -10), new Vector2(8, 84));
            needle.gameObject.AddComponent<Image>().color = Palette.Bad;
            UiKit.Image("Hub", dial.transform, UiKit.Circle, Palette.Brass, new Vector2(26, 26), new Vector2(0, -10));
            dialNumber = UiKit.Text("Num", dial.transform, "1", 34, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(100, 40), new Vector2(0, -52));
            dialName = UiKit.Text("Name", root, "LOBBY", 24, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(340, 30), new Vector2(0, 162));

            // forecast
            UiKit.Text("NextLbl", root, "NEXT STOP, THE BUILDING WILL...", 15, Palette.Brass, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(340, 24), new Vector2(0, 124));
            for (int i = 0; i < 2; i++)
            {
                var c = new ForecastCard();
                c.Back = UiKit.Image("Card" + i, root, UiKit.Rounded, i == 0 ? Palette.Hex(0xF3E6C8) : Palette.Hex(0xC9BBA0), new Vector2(330, 58), new Vector2(0, 82 - i * 64));
                c.Root = c.Back.rectTransform;
                c.Title = UiKit.Text("T", c.Root, "", 16, Palette.Oxblood, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(300, 20), new Vector2(0, 14));
                c.Body = UiKit.Text("B", c.Root, "", 20, Palette.Ink, UiKit.Body, TextAlignmentOptions.Left, new Vector2(300, 26), new Vector2(0, -9));
                c.Body.richText = true;
                cards[i] = c;
            }

            // buttons: 3x3 like a real panel, 1 at the bottom left
            for (int s = 0; s < 9; s++)
            {
                int row = s / 3, colI = s % 3;
                var pos = new Vector2(-112 + colI * 112, -360 + row * 132);
                var b = new SlotButton { Slot = s };
                b.Root = UiKit.Rect("Btn" + (s + 1), root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(104, 124));
                b.Glow = UiKit.Image("Glow", b.Root, UiKit.SoftCircle, new Color(1f, 0.75f, 0.3f, 0f), new Vector2(150, 150), new Vector2(0, 20));
                b.Ring = UiKit.Image("Bezel", b.Root, UiKit.Circle, Palette.Brass, new Vector2(78, 78), new Vector2(0, 20));
                b.Cap = UiKit.Image("Cap", b.Root, UiKit.Circle, Palette.Hex(0xF3E6C8), new Vector2(64, 64), new Vector2(0, 20), true);
                b.Number = UiKit.Text("N", b.Cap.transform, (s + 1).ToString(), 34, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(64, 64));
                b.Plate = UiKit.Image("Plate", b.Root, UiKit.Pill, Palette.Floor(FloorId.Lobby), new Vector2(98, 30), new Vector2(0, -40));
                b.Name = UiKit.Text("Name", b.Plate.transform, "", 15, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(98, 30));
                b.Waiting = UiKit.Text("W", b.Root, "", 16, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(40, 22), new Vector2(40, 52));
                for (int k = 0; k < 4; k++)
                {
                    b.Pips[k] = UiKit.Image("Pip" + k, b.Root, UiKit.Circle, Palette.Good, new Vector2(12, 12), new Vector2(-36 + k * 0, 46 - k * 14));
                }
                int slot = s;
                var trig = b.Cap.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trig, EventTriggerType.PointerClick, _ => runner.RequestSend(slot, true));
                AddTrigger(trig, EventTriggerType.PointerEnter, _ => HoverSlot = slot);
                AddTrigger(trig, EventTriggerType.PointerExit, _ => { if (HoverSlot == slot) HoverSlot = -1; });
                buttons[s] = b;
            }

            boardAll = UiKit.Image("BoardAll", root, UiKit.Pill, Palette.Hex(0x4C8C5A), new Vector2(330, 0), new Vector2(0, -430), true);
        }

        static void AddTrigger(EventTrigger trig, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(action);
            trig.triggers.Add(e);
        }

        public void Press(int slot)
        {
            if (slot >= 0 && slot < buttons.Length) buttons[slot].Press = 1f;
        }

        string Describe(Card c, Building pred)
        {
            string F(FloorId f) => $"<color=#{ColorUtility.ToHtmlStringRGB(Palette.Floor(f))}>{Defs.Floor(f).Name}</color>";
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
            var b = sim.B;

            // dial follows the car
            float target = Mathf.Lerp(150f, 30f, sim.Car.Pos / 8f) - 90f;
            needleAngle = Mathf.Lerp(needleAngle, target, Ease.Damp(12f, dt));
            needle.localRotation = Quaternion.Euler(0, 0, needleAngle);
            int nearest = Mathf.Clamp(Mathf.RoundToInt(sim.Car.Pos), 0, b.Count - 1);
            dialNumber.text = (nearest + 1).ToString();
            dialName.text = Defs.Floor(b.At(nearest)).Name.ToUpperInvariant();
            dialName.color = Color.Lerp(Palette.Floor(b.At(nearest)), Palette.Cream, 0.35f);

            // forecast
            var pred = new Building(b);
            for (int i = 0; i < cards.Length; i++)
            {
                if (i >= sim.Forecast.Count) { cards[i].Root.gameObject.SetActive(false); continue; }
                var c = sim.Forecast[i];
                cards[i].Root.gameObject.SetActive(true);
                cards[i].Title.text = (i == 0 ? "NEXT: " : "THEN: ") + TitleOf(c);
                cards[i].Body.text = Describe(c, pred);
                pred.Apply(c, null, null);
            }

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
                bool lit = s == targetSlot || (sim.Car.Has(Kind.Kid) && targetSlot >= 0 && (s - sim.Car.Pos) * (targetSlot - sim.Car.Pos) > 0 && Mathf.Abs(s - sim.Car.Pos) < Mathf.Abs(targetSlot - sim.Car.Pos));
                btn.Lit = Mathf.Lerp(btn.Lit, lit ? 1f : 0f, Ease.Damp(14f, dt));
                btn.Press = Mathf.Max(0f, btn.Press - dt * 5f);
                bool hover = HoverSlot == s || runner.HoverFloor == f;
                btn.Cap.color = Color.Lerp(hover ? Palette.Hex(0xFFF6DE) : Palette.Hex(0xF3E6C8), Palette.Hex(0xFFC857), btn.Lit);
                btn.Glow.color = new Color(1f, 0.72f, 0.28f, 0.55f * btn.Lit);
                btn.Cap.rectTransform.localScale = Vector3.one * (1f - 0.12f * Ease.OutCubic(btn.Press) + (hover ? 0.05f : 0f));
                int waiting = sim.Waiting[(int)f].Count;
                btn.Waiting.text = waiting > 0 ? waiting.ToString() : "";
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
    }
}

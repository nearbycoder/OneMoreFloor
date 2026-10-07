using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace OneMoreFloor
{
    public enum ControlScheme { Mouse, Keys, Pad }

    /// <summary>
    /// What every input does, written for one way of playing: mouse and keyboard, arrow keys (or WASD), or a gamepad named the
    /// way the controller in use prints its buttons. The same tables as the README's "How to play".
    /// </summary>
    public static class ControlsSheet
    {
        public struct Row { public string Key, Action; }

        /// <summary>The scheme the player is using right now.</summary>
        public static ControlScheme Current => Controls.Pad ? ControlScheme.Pad : Controls.KeyNav ? ControlScheme.Keys : ControlScheme.Mouse;

        public static string Title(ControlScheme s, PadFamily f)
        {
            if (s == ControlScheme.Mouse) return "MOUSE AND KEYBOARD";
            if (s == ControlScheme.Keys) return "ARROW KEYS OR WASD";
            return f == PadFamily.PlayStation ? "PLAYSTATION CONTROLLER" : f == PadFamily.Nintendo ? "NINTENDO CONTROLLER" : "CONTROLLER";
        }

        public static List<Row> Rows(ControlScheme s, PadFamily f)
        {
            var rows = new List<Row>();
            void Add(string key, string action) => rows.Add(new Row { Key = key, Action = action });
            string N(GamepadButton b) => PadGlyphs.Name(b, f).ToUpperInvariant();
            switch (s)
            {
                case ControlScheme.Pad:
                    Add("D-PAD UP / DOWN", "Pick a floor and preview the trip");
                    Add($"D-PAD LEFT / RIGHT, {N(GamepadButton.LeftShoulder)} / {N(GamepadButton.RightShoulder)}", "Pick a guest on that floor or in the car");
                    Add(N(GamepadButton.South), "Send the car, let them in, or go get them");
                    Add(N(GamepadButton.North), "Let everyone in");
                    Add(N(GamepadButton.West), "Let the picked rider off here");
                    Add(N(GamepadButton.East), "Unpick");
                    Add($"{N(GamepadButton.RightTrigger)} / {N(GamepadButton.LeftTrigger)}", "Zoom in / out");
                    Add(N(GamepadButton.Start), "Pause");
                    break;
                case ControlScheme.Keys:
                    Add("UP / DOWN, W / S", "Pick a floor and preview the trip");
                    Add("LEFT / RIGHT, A / D, Q / E", "Pick a guest on that floor or in the car");
                    Add("ENTER", "Send the car, let them in, or go get them");
                    Add("SPACE", "Let everyone in");
                    Add("F", "Let the picked rider off here");
                    Add("BACKSPACE", "Unpick");
                    Add("Z, - / =", "Zoom in and out");
                    Add("ESC / P", "Pause");
                    break;
                default:
                    Add("CLICK A GUEST", "Let them in at your floor, or go and get them");
                    Add("CLICK A RIDER", "Send the car to their floor");
                    Add("CLICK A FLOOR, 1-9", "Send the car there, even mid-trip");
                    Add("RIGHT-CLICK A RIDER", "Let them off here while the doors are open");
                    Add("SPACE", "Let everyone in who fits");
                    Add("HOVER", "Preview the trip and the next shuffle");
                    Add("WHEEL, Z, - / =", "Zoom in and out");
                    Add("ESC / P", "Pause");
                    break;
            }
            return rows;
        }
    }

    /// <summary>The controls panel beside the pause card. It rewrites itself when the player switches input.</summary>
    public sealed class ControlsCard
    {
        public RectTransform Rt;
        TextMeshProUGUI title;
        readonly List<(TextMeshProUGUI key, TextMeshProUGUI action)> cells = new List<(TextMeshProUGUI, TextMeshProUGUI)>();
        string shown;
        /// <summary>The key cells' text without the &lt;nobr&gt; tags, for <see cref="Text"/>.</summary>
        readonly List<string> plain = new List<string>();
        const float RowH = 62f, KeyW = 200f, ActionW = 234f;

        public static ControlsCard Create(Transform parent, Vector2 size, Vector2 pos)
        {
            var c = new ControlsCard { Rt = Deco.Panel("Controls", parent, size, pos) };
            var head = Deco.Label("Head", c.Rt, "CONTROLS", 20, new Vector2(size.x - 60, 28), new Vector2(0, size.y * 0.5f - 52), TextAlignmentOptions.Center);
            head.characterSpacing = 14f;
            c.title = UiKit.Text("Scheme", c.Rt, "", 18, Deco.Muted, UiKit.Body, TextAlignmentOptions.Center, new Vector2(size.x - 60, 26), new Vector2(0, size.y * 0.5f - 84));
            Deco.Divider(c.Rt, size.x * 0.5f, new Vector2(0, size.y * 0.5f - 108));
            float top = size.y * 0.5f - 152f;
            for (int i = 0; i < 8; i++)
            {
                float y = top - i * RowH;
                var key = UiKit.Text("Key" + i, c.Rt, "", 17, Palette.Hex(0xFFC857), UiKit.Signage, TextAlignmentOptions.Right, new Vector2(KeyW, RowH - 4), new Vector2(-KeyW * 0.5f - 22f, y));
                key.textWrappingMode = TextWrappingModes.Normal;
                key.characterSpacing = 2f;
                var act = UiKit.Text("Action" + i, c.Rt, "", 19, Palette.Cream, UiKit.Body, TextAlignmentOptions.Left, new Vector2(ActionW, RowH - 4), new Vector2(ActionW * 0.5f - 6f, y));
                act.textWrappingMode = TextWrappingModes.Normal;
                act.lineSpacing = -8f;
                c.cells.Add((key, act));
            }
            return c;
        }

        /// <summary>Rewrite the panel if the input in use (or the pad's family) changed.</summary>
        public void Refresh()
        {
            var s = ControlsSheet.Current;
            var f = PadGlyphs.Family;
            string id = s + "/" + f;
            if (id == shown) return;
            shown = id;
            title.text = ControlsSheet.Title(s, f);
            var rows = ControlsSheet.Rows(s, f);
            plain.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                string key = i < rows.Count ? rows[i].Key : "";
                plain.Add(key);
                // a pair like "Q / E" never breaks across lines
                cells[i].key.text = System.Text.RegularExpressions.Regex.Replace(key, @"(\S+ / \S+)", "<nobr>$1</nobr>");
                cells[i].action.text = i < rows.Count ? rows[i].Action : "";
            }
        }

        /// <summary>Self-test: the scheme line and every row as "KEY: action", one per line.</summary>
        public string Text
        {
            get
            {
                var sb = new System.Text.StringBuilder(title.text);
                for (int i = 0; i < cells.Count; i++) sb.Append('\n').Append(i < plain.Count ? plain[i] : "").Append(": ").Append(cells[i].action.text);
                return sb.ToString();
            }
        }

        /// <summary>Self-test: cells whose text needs more room than they have ("" when everything fits).</summary>
        public string Overflow
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                foreach (var (k, a) in cells)
                    foreach (var t in new[] { k, a })
                    {
                        var r = t.rectTransform.rect;
                        var p = t.GetPreferredValues(t.text, r.width, 0f);
                        if (p.y > r.height + 1f) sb.Append($" '{t.text}' {p.y:0}>{r.height:0}");
                    }
                return sb.ToString();
            }
        }
    }
}

using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Switch;

namespace OneMoreFloor
{
    public enum PadFamily { Xbox, PlayStation, Nintendo }

    /// <summary>
    /// Button names and prompt glyphs for the controller in use: Xbox letters, PlayStation shapes, or Nintendo letters
    /// (whose A and B, X and Y sit the other way round: the bottom button is B). The Input System names buttons by
    /// position (south, east, ...), so this only changes what the screen says, never what a button does.
    /// </summary>
    public static class PadGlyphs
    {
        /// <summary>Automation: force a family (null = detect from the current gamepad).</summary>
        public static PadFamily? Override;

        public static PadFamily Family => Override ?? Detect(Gamepad.current);

        public static PadFamily Detect(Gamepad gp)
        {
            if (gp == null) return PadFamily.Xbox;
            if (gp is DualShockGamepad) return PadFamily.PlayStation;
            if (gp is SwitchProControllerHID) return PadFamily.Nintendo;
            // on Linux, pads often arrive as generic devices: go by the name
            string id = (gp.description.manufacturer + " " + gp.description.product + " " + gp.displayName).ToLowerInvariant();
            if (id.Contains("sony") || id.Contains("playstation") || id.Contains("dualshock") || id.Contains("dualsense")) return PadFamily.PlayStation;
            if (id.Contains("nintendo") || id.Contains("pro controller") || id.Contains("joy-con")) return PadFamily.Nintendo;
            return PadFamily.Xbox;
        }

        /// <summary>The button's name as printed on the controller ("A", "Cross", "B", "R2", ...).</summary>
        public static string Name(GamepadButton b, PadFamily f)
        {
            switch (f)
            {
                case PadFamily.PlayStation:
                    switch (b)
                    {
                        case GamepadButton.South: return "Cross";
                        case GamepadButton.East: return "Circle";
                        case GamepadButton.West: return "Square";
                        case GamepadButton.North: return "Triangle";
                        case GamepadButton.LeftShoulder: return "L1";
                        case GamepadButton.RightShoulder: return "R1";
                        case GamepadButton.LeftTrigger: return "L2";
                        case GamepadButton.RightTrigger: return "R2";
                        case GamepadButton.Start: return "Options";
                    }
                    break;
                case PadFamily.Nintendo:
                    switch (b)
                    {
                        case GamepadButton.South: return "B";
                        case GamepadButton.East: return "A";
                        case GamepadButton.West: return "Y";
                        case GamepadButton.North: return "X";
                        case GamepadButton.LeftShoulder: return "L";
                        case GamepadButton.RightShoulder: return "R";
                        case GamepadButton.LeftTrigger: return "ZL";
                        case GamepadButton.RightTrigger: return "ZR";
                        case GamepadButton.Start: return "+";
                    }
                    break;
            }
            switch (b)
            {
                case GamepadButton.South: return "A";
                case GamepadButton.East: return "B";
                case GamepadButton.West: return "X";
                case GamepadButton.North: return "Y";
                case GamepadButton.LeftShoulder: return "LB";
                case GamepadButton.RightShoulder: return "RB";
                case GamepadButton.LeftTrigger: return "LT";
                case GamepadButton.RightTrigger: return "RT";
                case GamepadButton.Start: return "Start";
            }
            return b.ToString();
        }

        public static string Name(GamepadButton b) => Name(b, Family);

        static readonly Regex Token = new Regex(@"<b>(A|B|X|Y|LB/RB|LT|RT|Start)</b>");

        /// <summary>
        /// Rewrites Xbox button names written as <c>&lt;b&gt;A&lt;/b&gt;</c> (and LB/RB, RT, ...) in a hint for the
        /// controller in use.
        /// </summary>
        public static string Words(string text)
        {
            var f = Family;
            if (f == PadFamily.Xbox || string.IsNullOrEmpty(text)) return text;
            return Token.Replace(text, m =>
            {
                string n;
                switch (m.Groups[1].Value)
                {
                    case "A": n = Name(GamepadButton.South, f); break;
                    case "B": n = Name(GamepadButton.East, f); break;
                    case "X": n = Name(GamepadButton.West, f); break;
                    case "Y": n = Name(GamepadButton.North, f); break;
                    case "LB/RB": n = Name(GamepadButton.LeftShoulder, f) + "/" + Name(GamepadButton.RightShoulder, f); break;
                    case "LT": n = Name(GamepadButton.LeftTrigger, f); break;
                    case "RT": n = Name(GamepadButton.RightTrigger, f); break;
                    default: n = Name(GamepadButton.Start, f); break;
                }
                return "<b>" + n + "</b>";
            });
        }

        // ------------------------------------------------------------- prompt-strip glyphs

        public struct Glyph
        {
            /// <summary>Text on the cap ("" when a symbol is drawn instead).</summary>
            public string Key;
            public Color Cap, Ink;
            /// <summary>PlayStation shape, drawn in <see cref="Ink"/> on a dark cap.</summary>
            public Sprite Symbol;
            /// <summary>Round cap (face buttons) or pill (shoulders, triggers).</summary>
            public bool Round;
        }

        static readonly Color Dark = Palette.Hex(0x1C1820), Shoulder = Palette.Hex(0xC9BBA0), NxCap = Palette.Hex(0x2E2C33);
        static readonly Color XA = Palette.Hex(0x6CCB5F), XB = Palette.Hex(0xE5484D), XX = Palette.Hex(0x3FA9F5), XY = Palette.Hex(0xFFD23F);
        static readonly Color PsCross = Palette.Hex(0x8FB8F0), PsCircle = Palette.Hex(0xF07A80), PsSquare = Palette.Hex(0xE79BD0), PsTriangle = Palette.Hex(0x52D6B4);

        /// <summary>A face button (south/east/west/north) as the prompt strip draws it.</summary>
        public static Glyph Face(GamepadButton b)
        {
            var f = Family;
            if (f == PadFamily.PlayStation)
            {
                Sprite s; Color c;
                switch (b)
                {
                    case GamepadButton.South: s = Cross; c = PsCross; break;
                    case GamepadButton.East: s = CircleShape; c = PsCircle; break;
                    case GamepadButton.West: s = Square; c = PsSquare; break;
                    default: s = TriangleShape; c = PsTriangle; break;
                }
                return new Glyph { Key = "", Cap = Dark, Ink = c, Symbol = s, Round = true };
            }
            if (f == PadFamily.Nintendo)
                return new Glyph { Key = Name(b, f), Cap = NxCap, Ink = Palette.Cream, Round = true };
            Color cap = b == GamepadButton.South ? XA : b == GamepadButton.East ? XB : b == GamepadButton.West ? XX : XY;
            return new Glyph { Key = Name(b, f), Cap = cap, Ink = Dark, Round = true };
        }

        /// <summary>Shoulder or trigger buttons as a pill ("LB RB", "R2", "ZR").</summary>
        public static Glyph Pill(params GamepadButton[] buttons)
        {
            var f = Family;
            var names = new string[buttons.Length];
            for (int i = 0; i < buttons.Length; i++) names[i] = Name(buttons[i], f);
            return new Glyph { Key = string.Join(" ", names), Cap = Shoulder, Ink = Dark, Round = false };
        }

        static Sprite cross, circleShape, square, triangleShape;
        static Sprite Cross => cross ? cross : (cross = Shape(0));
        static Sprite CircleShape => circleShape ? circleShape : (circleShape = Shape(1));
        static Sprite Square => square ? square : (square = Shape(2));
        static Sprite TriangleShape => triangleShape ? triangleShape : (triangleShape = Shape(3));

        /// <summary>Draws one PlayStation shape as a white stroke on transparent (0 cross, 1 circle, 2 square, 3 triangle).</summary>
        static Sprite Shape(int kind)
        {
            const int size = 64;
            const float stroke = 4.2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var tri = new[] { new Vector2(32, 52), new Vector2(13, 18), new Vector2(51, 18) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            var p = new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f);
                            float d;
                            switch (kind)
                            {
                                case 0:
                                    d = Mathf.Min(Seg(p, new Vector2(16, 16), new Vector2(48, 48)), Seg(p, new Vector2(16, 48), new Vector2(48, 16)));
                                    break;
                                case 1:
                                    d = Mathf.Abs(Vector2.Distance(p, new Vector2(32, 32)) - 17f);
                                    break;
                                case 2:
                                    float qx = Mathf.Abs(p.x - 32f) - 15f, qy = Mathf.Abs(p.y - 32f) - 15f;
                                    d = Mathf.Abs(Mathf.Max(qx, qy));
                                    break;
                                default:
                                    d = Mathf.Min(Seg(p, tri[0], tri[1]), Mathf.Min(Seg(p, tri[1], tri[2]), Seg(p, tri[2], tri[0])));
                                    break;
                            }
                            if (d <= stroke * 0.5f) hits++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}

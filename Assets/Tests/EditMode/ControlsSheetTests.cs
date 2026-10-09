using NUnit.Framework;

namespace OneMoreFloor.Tests
{
    public class ControlsSheetTests
    {
        [Test]
        public void EverySchemeListsItsInputsAndTheKeyboardOnesListFullscreen()
        {
            foreach (ControlScheme s in System.Enum.GetValues(typeof(ControlScheme)))
                foreach (PadFamily f in System.Enum.GetValues(typeof(PadFamily)))
                {
                    var rows = ControlsSheet.Rows(s, f);
                    // the controller's and the touchscreen's eight, and the keyboard's F11 / Alt+Enter as a ninth on the other two
                    bool keyboard = s == ControlScheme.Mouse || s == ControlScheme.Keys;
                    Assert.AreEqual(keyboard ? 9 : 8, rows.Count, $"{s}/{f}");
                    Assert.LessOrEqual(rows.Count, ControlsCard.MaxRows);
                    if (keyboard) Assert.AreEqual("F11 / ALT+ENTER", rows[rows.Count - 1].Key, $"{s}");
                    foreach (var r in rows)
                    {
                        Assert.IsNotEmpty(r.Key, $"{s}/{f}");
                        Assert.IsNotEmpty(r.Action, $"{s}/{f} {r.Key}");
                    }
                    Assert.IsNotEmpty(ControlsSheet.Title(s, f));
                }
        }

        [Test]
        public void PadRowsUseTheNamesOnTheController()
        {
            string Keys(PadFamily f)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var r in ControlsSheet.Rows(ControlScheme.Pad, f)) sb.Append('|').Append(r.Key);
                return sb.Append('|').ToString();
            }
            Assert.AreEqual("|D-PAD UP / DOWN|D-PAD LEFT / RIGHT, LB / RB|A|Y|X|B|RT / LT|START|", Keys(PadFamily.Xbox));
            Assert.AreEqual("|D-PAD UP / DOWN|D-PAD LEFT / RIGHT, L1 / R1|CROSS|TRIANGLE|SQUARE|CIRCLE|R2 / L2|OPTIONS|", Keys(PadFamily.PlayStation));
            // Nintendo pads swap the letters: the bottom button (send) is B, the top one (let everyone in) is X
            Assert.AreEqual("|D-PAD UP / DOWN|D-PAD LEFT / RIGHT, L / R|B|X|Y|A|ZR / ZL|+|", Keys(PadFamily.Nintendo));
        }

        [Test]
        public void KeyboardRowsNameWasdAlongsideTheArrows()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in ControlsSheet.Rows(ControlScheme.Keys, PadFamily.Xbox)) sb.Append('|').Append(r.Key);
            Assert.AreEqual("|UP / DOWN, W / S|LEFT / RIGHT, A / D, Q / E|ENTER|SPACE|F|BACKSPACE|Z, - / =|ESC / P|F11 / ALT+ENTER|", sb.Append('|').ToString());
            Assert.AreEqual("ARROW KEYS OR WASD", ControlsSheet.Title(ControlScheme.Keys, PadFamily.Xbox));
        }

        [Test]
        public void TouchRowsNameTheOnScreenButtonsAndNoKeys()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in ControlsSheet.Rows(ControlScheme.Touch, PadFamily.Xbox)) sb.Append('|').Append(r.Key);
            Assert.AreEqual("|TAP A GUEST|TAP THEM AGAIN|TAP A FLOOR TWICE|PANEL BUTTON|HOLD A RIDER|ALL IN|PINCH, ZOOM|PAUSE BUTTON|",
                sb.Append('|').ToString());
            Assert.AreEqual("TOUCHSCREEN", ControlsSheet.Title(ControlScheme.Touch, PadFamily.Xbox));
        }
    }
}

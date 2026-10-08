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
                    // the controller's eight, and the keyboard's F11 / Alt+Enter as a ninth on the other two
                    Assert.AreEqual(s == ControlScheme.Pad ? 8 : 9, rows.Count, $"{s}/{f}");
                    Assert.LessOrEqual(rows.Count, ControlsCard.MaxRows);
                    if (s != ControlScheme.Pad) Assert.AreEqual("F11 / ALT+ENTER", rows[rows.Count - 1].Key, $"{s}");
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
    }
}

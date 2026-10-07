using NUnit.Framework;

namespace OneMoreFloor.Tests
{
    public class ControlsSheetTests
    {
        [Test]
        public void EverySchemeListsEightInputs()
        {
            foreach (ControlScheme s in System.Enum.GetValues(typeof(ControlScheme)))
                foreach (PadFamily f in System.Enum.GetValues(typeof(PadFamily)))
                {
                    var rows = ControlsSheet.Rows(s, f);
                    Assert.AreEqual(8, rows.Count, $"{s}/{f}");
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
    }
}

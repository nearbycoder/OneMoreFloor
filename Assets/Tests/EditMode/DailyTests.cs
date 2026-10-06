using NUnit.Framework;
using OneMoreFloor.Core;

namespace OneMoreFloor.Tests
{
    public class DailyTests
    {
        bool wasEphemeral;
        [SetUp] public void NoDisk() { wasEphemeral = SaveData.Ephemeral; SaveData.Ephemeral = true; }
        [TearDown] public void Restore() => SaveData.Ephemeral = wasEphemeral;

        static ShiftDef Overtime()
        {
            foreach (var d in ShiftCatalog.All) if (d.Endless) return d;
            return null;
        }

        [Test]
        public void TheSeedIsStableForADateAndChangesDaily()
        {
            ulong a = Progress.DailySeed(2026, 10, 6);
            Assert.AreEqual(a, Progress.DailySeed(2026, 10, 6));
            Assert.AreNotEqual(0UL, a);
            Assert.AreNotEqual(a, Progress.DailySeed(2026, 10, 7));
            Assert.AreNotEqual(a, Progress.DailySeed(2027, 10, 6));
            Assert.AreNotEqual(Progress.DailySeed(2026, 1, 11), Progress.DailySeed(2026, 11, 1));
        }

        [Test]
        public void TwoRunsOfTodaysShiftMatchTickForTick()
        {
            ulong seed = Progress.DailySeed(2026, 10, 6);
            var a = Bot.PlayOut(Overtime(), seed, Bot.Decent(3), maxSeconds: 240f);
            var b = Bot.PlayOut(Overtime(), seed, Bot.Decent(3), maxSeconds: 240f);
            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.Complaints, b.Complaints);
            Assert.AreEqual(a.All.Count, b.All.Count);
            for (int i = 0; i < a.All.Count; i++)
            {
                Assert.AreEqual(a.All[i].Kind, b.All[i].Kind);
                Assert.AreEqual(a.All[i].Origin, b.All[i].Origin);
            }
            // another day's building starts differently
            var c = new ShiftSim(Overtime(), Progress.DailySeed(2026, 10, 7));
            var d = new ShiftSim(Overtime(), seed);
            bool differs = false;
            for (int i = 0; i < 1800 && !differs; i++)
            {
                c.Tick(1f / 30f); d.Tick(1f / 30f);
                if (c.All.Count != d.All.Count) differs = true;
                else for (int k = 0; k < c.All.Count; k++) if (c.All[k].Kind != d.All[k].Kind || c.All[k].Origin != d.All[k].Origin) differs = true;
            }
            Assert.IsTrue(differs, "two days' opening guests differ");
        }

        [Test]
        public void TheDailyRecordRollsOverAtMidnight()
        {
            var s = new SaveData();
            Assert.IsTrue(s.RecordDaily("2026-10-06", 5000));
            Assert.IsFalse(s.RecordDaily("2026-10-06", 4000));
            Assert.IsTrue(s.RecordDaily("2026-10-06", 7000));
            Assert.AreEqual(7000, s.DailyBestOn("2026-10-06"));
            Assert.AreEqual(3, s.DailyPlays);
            Assert.AreEqual(0, s.DailyBestOn("2026-10-07"));
            Assert.IsTrue(s.RecordDaily("2026-10-07", 3000), "a new day starts from zero");
            Assert.AreEqual(1, s.DailyPlays);
            Assert.AreEqual(7000, s.DailyRecord);
            Assert.AreEqual("2026-10-06", s.DailyRecordDate);
            var back = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s));
            Assert.AreEqual(3000, back.DailyBestOn("2026-10-07"));
            Assert.AreEqual(7000, back.DailyRecord);
        }
    }
}

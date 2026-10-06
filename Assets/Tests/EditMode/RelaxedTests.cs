using NUnit.Framework;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class RelaxedTests
    {
        const float Dt = 1f / 30f;

        // SaveData.Record saves: keep it off the real save in persistentDataPath
        bool wasEphemeral;
        [SetUp] public void NoDisk() { wasEphemeral = SaveData.Ephemeral; SaveData.Ephemeral = true; }
        [TearDown] public void Restore() => SaveData.Ephemeral = wasEphemeral;

        static ShiftSim Idle(ShiftDef def, bool relaxed, float seconds)
        {
            // nobody runs the elevator: complaints pile up
            var sim = new ShiftSim(def, 99, relaxed);
            for (float t = 0; t < seconds && !sim.Ended; t += Dt) { sim.Tick(Dt); sim.Events.Clear(); }
            return sim;
        }

        [Test]
        public void ARelaxedShiftIsNeverFired()
        {
            var def = ShiftCatalog.Get(2);
            var standard = Idle(def, false, 400f);
            Assert.IsTrue(standard.Fired, "an idle standard shift gets fired");
            var relaxed = Idle(def, true, 400f);
            Assert.IsTrue(relaxed.Relaxed);
            Assert.IsFalse(relaxed.Fired);
            Assert.IsTrue(relaxed.Ended, "it runs to the bell");
            Assert.Greater(relaxed.Complaints, Tuning.MaxComplaints);
        }

        [Test]
        public void RelaxedPatienceIsLonger()
        {
            var def = ShiftCatalog.Get(1);
            var a = new ShiftSim(def, 5, false);
            var b = new ShiftSim(def, 5, true);
            for (int i = 0; i < 600; i++) { a.Tick(Dt); b.Tick(Dt); a.Events.Clear(); b.Events.Clear(); }
            Assert.Greater(a.All.Count, 0);
            Assert.AreEqual(a.All.Count, b.All.Count, "same spawns");
            for (int i = 0; i < a.All.Count; i++)
                Assert.AreEqual(a.All[i].PatienceMax * Tuning.RelaxedPatience, b.All[i].PatienceMax, 1e-3f);
        }

        [Test]
        public void EndlessShiftsIgnoreRelaxed()
        {
            ShiftDef overtime = null;
            foreach (var d in ShiftCatalog.All) if (d.Endless) overtime = d;
            Assert.IsFalse(new ShiftSim(overtime, 1, true).Relaxed);
        }

        [Test]
        public void ARelaxedClearOpensTheNextShiftButSavesNoStars()
        {
            var s = new SaveData();
            s.Stars[0] = 1; s.Plays[0] = 1;
            var tue = ShiftCatalog.Get(1);
            Assert.IsFalse(s.Record(1, tue.Stars[0] - 1, 0, relaxed: true));
            Assert.IsFalse(s.Unlocked(2), "below the 1-star score: no clear");
            Assert.IsFalse(s.Record(1, tue.Stars[0] + 500, 1, relaxed: true), "relaxed runs never set a best");
            Assert.AreEqual(0, s.Stars[1]);
            Assert.AreEqual(0, s.Best[1]);
            Assert.IsTrue(s.RelaxedClear[1]);
            Assert.IsTrue(s.Unlocked(2));
            Assert.IsFalse(s.LatePassed(2));
            Assert.AreEqual(2, s.NextShift());
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s));
            Assert.IsTrue(back.RelaxedClear[1] && back.Unlocked(2));
        }

        [Test]
        public void FiringsAreCountedForStandardShiftsOnly()
        {
            var s = new SaveData();
            s.Record(3, 100, 0, relaxed: false, fired: true);
            s.Record(3, 100, 0, relaxed: true, fired: false);
            Assert.AreEqual(1, s.FiredCount[3]);
            Assert.AreEqual(2, s.Plays[3]);
        }
    }
}

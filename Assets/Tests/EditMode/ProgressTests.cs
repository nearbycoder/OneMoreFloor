using NUnit.Framework;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class ProgressTests
    {
        static int[] Zeros() => new int[16];

        [Test]
        public void StarOpensTheNextShift()
        {
            var stars = Zeros(); var plays = Zeros();
            Assert.IsTrue(Progress.Unlocked(0, stars, plays));
            Assert.IsFalse(Progress.Unlocked(1, stars, plays));
            stars[0] = 1; plays[0] = 1;
            Assert.IsTrue(Progress.Unlocked(1, stars, plays));
            Assert.IsFalse(Progress.ByLatePass(1, stars, plays));
            Assert.IsFalse(Progress.Unlocked(2, stars, plays));
        }

        [Test]
        public void ThreeTriesWithoutAStarGiveALatePass()
        {
            var stars = Zeros(); var plays = Zeros();
            stars[0] = 2; plays[0] = 1;
            for (int t = 0; t < Progress.LatePassAttempts; t++)
            {
                Assert.IsFalse(Progress.Unlocked(2, stars, plays), $"open after {t} tries");
                Assert.AreEqual(Progress.LatePassAttempts - t, Progress.TriesToLatePass(2, stars, plays));
                plays[1]++;
            }
            Assert.IsTrue(Progress.Unlocked(2, stars, plays));
            Assert.IsTrue(Progress.ByLatePass(2, stars, plays));
            Assert.AreEqual(-1, Progress.TriesToLatePass(2, stars, plays));
            // a late-passed shift doesn't open the one after it
            Assert.IsFalse(Progress.Unlocked(3, stars, plays));
            // and the title offers the new shift, not the one that was passed
            Assert.AreEqual(2, Progress.NextShift(stars, plays));
        }

        [Test]
        public void LatePassNeedsTheShiftBeforeToBeOpen()
        {
            var stars = Zeros(); var plays = Zeros();
            plays[1] = 5; // impossible in play, but must not open anything on its own
            Assert.IsFalse(Progress.Unlocked(2, stars, plays));
            Assert.AreEqual(-1, Progress.TriesToLatePass(2, stars, plays));
        }

        [Test]
        public void OvertimeStillNeedsAGraveyardStar()
        {
            int graveyard = -1, overtime = -1;
            for (int i = 0; i < ShiftCatalog.All.Count; i++)
            {
                if (ShiftCatalog.Get(i).Id == "graveyard") graveyard = i;
                if (ShiftCatalog.Get(i).Endless) overtime = i;
            }
            Assert.AreEqual(graveyard + 1, overtime);
            var stars = Zeros(); var plays = Zeros();
            for (int i = 0; i < graveyard; i++) { stars[i] = 1; plays[i] = 1; }
            plays[graveyard] = 10;
            Assert.IsTrue(Progress.Unlocked(graveyard, stars, plays));
            Assert.IsFalse(Progress.Unlocked(overtime, stars, plays));
            Assert.AreEqual(-1, Progress.TriesToLatePass(overtime, stars, plays));
            stars[graveyard] = 1;
            Assert.IsTrue(Progress.Unlocked(overtime, stars, plays));
        }

        [Test]
        public void SaveRoundTripKeepsProgress()
        {
            var s = new SaveData();
            s.Stars[0] = 3; s.Plays[0] = 2; s.Plays[1] = 3; s.Best[1] = 12345;
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s));
            Assert.AreEqual(3, back.Stars[0]);
            Assert.AreEqual(3, back.Plays[1]);
            Assert.AreEqual(12345, back.Best[1]);
            Assert.IsTrue(back.Unlocked(2));
            Assert.IsTrue(back.LatePassed(2));
            Assert.AreEqual(2, back.NextShift());
        }
    }
}

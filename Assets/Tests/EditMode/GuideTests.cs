using NUnit.Framework;
using OneMoreFloor.Core;

namespace OneMoreFloor.Tests
{
    public sealed class GuideTests
    {
        static int Index(string id)
        {
            for (int i = 0; i < ShiftCatalog.All.Count; i++) if (ShiftCatalog.Get(i).Id == id) return i;
            return -1;
        }

        [Test]
        public void EveryGuestAndCardHasARuleAndTheShiftThatIntroducesIt()
        {
            Assert.AreEqual(System.Enum.GetValues(typeof(Kind)).Length, Guide.Guests.Count);
            foreach (var e in Guide.Guests)
            {
                Assert.IsFalse(string.IsNullOrEmpty(e.Rule), e.Name);
                Assert.AreEqual(e.Kind, ShiftCatalog.Get(e.Shift).NewKind, e.Name + " is introduced on " + ShiftCatalog.Get(e.Shift).Day);
            }
            foreach (var e in Guide.Building) Assert.IsFalse(string.IsNullOrEmpty(e.Rule), e.Name);
            Shift("Swap", 0); Shift("Calm", 0); Shift("Jammed", 0);
            Shift("Rise", Index("wednesday")); Shift("Sink", Index("wednesday"));
            Shift("Roll", Index("thursday"));
            Shift("Leaving", Index("friday"));
            Shift("Ocean", Index("saturday"));
            Shift("Flip", Index("graveyard"));
        }

        static void Shift(string name, int shift)
        {
            var e = Guide.Building.Find(x => x.Name == name);
            Assert.AreEqual(name, e.Name);
            Assert.AreEqual(shift, e.Shift, name);
        }

        [Test]
        public void OpenEntriesFollowTheOpenShifts()
        {
            int[] stars = new int[16], plays = new int[16];
            bool Open(int s) => Progress.Unlocked(s, stars, plays);
            Assert.AreEqual(1, Guide.OpenCount(Guide.Guests, Open), "a fresh save knows the commuter");
            Assert.AreEqual(3, Guide.OpenCount(Guide.Building, Open), "swap, calm and the jam");
            for (int i = 0; i < stars.Length; i++) stars[i] = 1;
            Assert.AreEqual(Guide.Guests.Count, Guide.OpenCount(Guide.Guests, Open));
            Assert.AreEqual(Guide.Building.Count, Guide.OpenCount(Guide.Building, Open));
        }
    }
}

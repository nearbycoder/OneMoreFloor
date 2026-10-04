using System.Linq;
using NUnit.Framework;
using OneMoreFloor.Core;

namespace OneMoreFloor.Tests
{
    public class RulesTests
    {
        const float Dt = 1f / 60f;

        static ShiftDef Quiet(params FloorId[] floors)
        {
            // A shift with no random spawns or shuffles, for isolating one rule at a time.
            return new ShiftDef
            {
                Id = "test", Floors = floors, Duration = 600f, Mix = new float[Defs.KindCount],
                Deck = new[] { 1f, 0, 0, 0, 0, 0 }, PatienceBase = 100f, InitialSpawns = 0, Beat = "none",
            };
        }

        static void RunUntilDocked(ShiftSim sim, float max = 20f)
        {
            float t = 0;
            do { sim.Tick(Dt); t += Dt; } while (!(sim.Car.State == CarState.Docked) && t < max);
        }

        static Passenger Add(ShiftSim sim, Kind k, FloorId at, FloorId dest)
        {
            var p = new Passenger { Id = 1000 + sim.All.Count, Kind = k, Origin = at, At = at, Dest = dest, State = PState.Waiting, Patience = 100, PatienceMax = 100 };
            sim.All.Add(p);
            sim.Waiting[(int)at].Add(p);
            return p;
        }

        [Test]
        public void CommuterIsDeliveredAndScores()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Office, FloorId.Library), 1);
            var p = Add(sim, Kind.Commuter, FloorId.Lobby, FloorId.Library);
            Assert.AreEqual(BoardResult.Ok, sim.Board(p.Id));
            Assert.IsTrue(sim.SendToFloor(FloorId.Library));
            RunUntilDocked(sim);
            Assert.AreEqual(Outcome.Delivered, p.Outcome);
            Assert.Greater(sim.Score, 0);
            Assert.AreEqual(1, sim.Streak);
        }

        [Test]
        public void CapacityCountsMirrorAsTwo()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Office), 1);
            var m = Add(sim, Kind.Mirror, FloorId.Lobby, FloorId.Office);
            var a = Add(sim, Kind.Commuter, FloorId.Lobby, FloorId.Office);
            var b = Add(sim, Kind.Commuter, FloorId.Lobby, FloorId.Office);
            var c = Add(sim, Kind.Commuter, FloorId.Lobby, FloorId.Office);
            Assert.AreEqual(BoardResult.Ok, sim.Board(m.Id));
            Assert.AreEqual(BoardResult.Ok, sim.Board(a.Id));
            Assert.AreEqual(BoardResult.Ok, sim.Board(b.Id));
            Assert.AreEqual(BoardResult.Full, sim.Board(c.Id));
        }

        [Test]
        public void VampireAndMirrorRefuseToShare()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Office), 1);
            var v = Add(sim, Kind.Vampire, FloorId.Lobby, FloorId.Office);
            var m = Add(sim, Kind.Mirror, FloorId.Lobby, FloorId.Office);
            Assert.AreEqual(BoardResult.Ok, sim.Board(v.Id));
            Assert.AreEqual(BoardResult.Conflict, sim.Board(m.Id));
        }

        [Test]
        public void VampirePoofsWhenDoorsOpenOnSun()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Greenhouse, FloorId.Office), 1);
            var v = Add(sim, Kind.Vampire, FloorId.Lobby, FloorId.Office);
            sim.Board(v.Id);
            sim.SendToFloor(FloorId.Greenhouse);
            RunUntilDocked(sim);
            Assert.AreEqual(Outcome.Poofed, v.Outcome);
            Assert.AreEqual(1, sim.Complaints);
        }

        [Test]
        public void HouseplantNeedsSunBeforeLeaving()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Office, FloorId.Greenhouse), 1);
            var p = Add(sim, Kind.Houseplant, FloorId.Lobby, FloorId.Office);
            sim.Board(p.Id);
            sim.SendToFloor(FloorId.Office);
            RunUntilDocked(sim);
            Assert.AreEqual(PState.Riding, p.State, "plant should refuse to get off without sun");
            sim.SendToFloor(FloorId.Greenhouse);
            RunUntilDocked(sim);
            Assert.IsTrue(p.Sunned);
            sim.SendToFloor(FloorId.Office);
            RunUntilDocked(sim);
            Assert.AreEqual(Outcome.Delivered, p.Outcome);
        }

        [Test]
        public void KidMakesQuickStopsWithoutShuffling()
        {
            var def = Quiet(FloorId.Lobby, FloorId.Office, FloorId.Library, FloorId.Laundromat, FloorId.Boiler);
            def.Deck = new[] { 0f, 1f, 0, 0, 0, 0 };
            var sim = new ShiftSim(def, 3);
            var k = Add(sim, Kind.Kid, FloorId.Lobby, FloorId.Boiler);
            sim.Board(k.Id);
            int quick = 0, shuffles = 0;
            sim.SendTo(4);
            for (int i = 0; i < 60 * 20 && k.State != PState.Done; i++)
            {
                sim.Tick(Dt);
                quick += sim.Events.Count(e => e.Type == Ev.QuickStop);
                shuffles += sim.Events.Count(e => e.Type == Ev.Shuffled || e.Type == Ev.Jammed || e.Type == Ev.Calm);
                sim.Events.Clear();
            }
            Assert.AreEqual(3, quick, "one quick stop per floor on the way");
            Assert.AreEqual(1, shuffles, "only the full stop plays a shuffle card");
            Assert.AreEqual(Outcome.Delivered, k.Outcome);
        }

        [Test]
        public void TycoonLosesTipAfterAnotherStop()
        {
            var sim = new ShiftSim(Quiet(FloorId.Lobby, FloorId.Office, FloorId.Penthouse), 1);
            var t = Add(sim, Kind.Tycoon, FloorId.Lobby, FloorId.Penthouse);
            sim.Board(t.Id);
            sim.SendToFloor(FloorId.Office);
            RunUntilDocked(sim);
            Assert.IsTrue(t.ExpressBroken);
            int before = sim.Score;
            sim.SendToFloor(FloorId.Penthouse);
            RunUntilDocked(sim);
            Assert.AreEqual(Outcome.Delivered, t.Outcome);
            // fare only, times the (first-delivery) multiplier of 1
            Assert.AreEqual(Defs.Of(Kind.Tycoon).Fare, sim.Score - before);
        }

        [Test]
        public void DockedFloorJamsCardsThatNameIt()
        {
            var b = new Building(new[] { FloorId.Lobby, FloorId.Office, FloorId.Library }, null);
            var card = new Card { Type = CardType.Swap, A = FloorId.Office, B = FloorId.Library };
            Assert.IsFalse(b.Apply(card, FloorId.Office, null));
            CollectionAssert.AreEqual(new[] { FloorId.Lobby, FloorId.Office, FloorId.Library }, b.Slots);
        }

        [Test]
        public void BlockMovesFlowAroundTheDockedFloor()
        {
            var b = new Building(new[] { FloorId.Lobby, FloorId.Office, FloorId.Library, FloorId.Laundromat, FloorId.Boiler }, null);
            Assert.IsTrue(b.Apply(new Card { Type = CardType.Flip, From = 0, Len = 5 }, FloorId.Library, null));
            Assert.AreEqual(FloorId.Library, b.At(2), "anchor holds its slot");
            CollectionAssert.AreEquivalent(new[] { FloorId.Lobby, FloorId.Office, FloorId.Library, FloorId.Laundromat, FloorId.Boiler }, b.Slots);
            Assert.AreEqual(FloorId.Boiler, b.At(0));
            Assert.IsTrue(b.Apply(new Card { Type = CardType.Rise, A = FloorId.Boiler }, FloorId.Library, null));
            Assert.AreEqual(FloorId.Library, b.At(2));
            Assert.AreEqual(FloorId.Boiler, b.At(4));
        }

        [Test]
        public void CourierPackageIsLostWhenFloorLeaves()
        {
            var def = Quiet(FloorId.Lobby, FloorId.Office, FloorId.Library);
            def.Offsite = new[] { FloorId.Laundromat };
            var sim = new ShiftSim(def, 1);
            var c = Add(sim, Kind.Courier, FloorId.Lobby, FloorId.Library);
            sim.B.Leaving[(int)FloorId.Library] = 1;
            sim.SendToFloor(FloorId.Office);
            RunUntilDocked(sim);
            Assert.IsFalse(sim.B.Has(FloorId.Library));
            Assert.IsTrue(sim.B.Has(FloorId.Laundromat));
            Assert.AreEqual(Outcome.PackageLost, c.Outcome);
            Assert.AreEqual(1, sim.Complaints);
        }

        [Test]
        public void DockingHoldsALeavingFloor()
        {
            var def = Quiet(FloorId.Lobby, FloorId.Office, FloorId.Library);
            def.Offsite = new[] { FloorId.Laundromat };
            var sim = new ShiftSim(def, 1);
            sim.B.Leaving[(int)FloorId.Library] = 1;
            sim.SendToFloor(FloorId.Library);
            RunUntilDocked(sim);
            Assert.IsTrue(sim.B.Has(FloorId.Library), "docked floor can't leave");
            sim.SendToFloor(FloorId.Lobby);
            RunUntilDocked(sim);
            Assert.IsFalse(sim.B.Has(FloorId.Library), "leaves at the next stop");
        }

        [Test]
        public void SwimmerIsSweptAwayWhenTheOceanLeaves()
        {
            var def = Quiet(FloorId.Lobby, FloorId.Office, FloorId.Library);
            def.Offsite = new[] { FloorId.Ocean };
            def.OceanStay = 1;
            def.SwimmersOnArrival = 1;
            var sim = new ShiftSim(def, 1);
            sim.B.Leaving[(int)FloorId.Library] = 1;
            sim.SendToFloor(FloorId.Office);
            RunUntilDocked(sim);
            Assert.IsTrue(sim.B.Has(FloorId.Ocean));
            var swimmer = sim.All.First(p => p.Kind == Kind.Swimmer);
            sim.SendToFloor(FloorId.Lobby);
            RunUntilDocked(sim);
            Assert.IsFalse(sim.B.Has(FloorId.Ocean));
            Assert.IsTrue(sim.B.Has(FloorId.Library), "the displaced floor comes back");
            Assert.AreEqual(Outcome.SweptAway, swimmer.Outcome);
        }

        [Test]
        public void FiveComplaintsGetsYouFired()
        {
            var def = Quiet(FloorId.Lobby, FloorId.Office);
            def.PatienceBase = 1f;
            var sim = new ShiftSim(def, 1);
            for (int i = 0; i < 5; i++) Add(sim, Kind.Commuter, FloorId.Office, FloorId.Lobby).Patience = 0.5f;
            for (int i = 0; i < 120 && !sim.Ended; i++) sim.Tick(Dt);
            Assert.IsTrue(sim.Fired);
            Assert.IsTrue(sim.Ended);
        }

        [Test]
        public void SameSeedSameCommandsSameResult()
        {
            foreach (var def in ShiftCatalog.All)
            {
                var a = Bot.PlayOut(def, 42, Bot.Decent(42));
                var b = Bot.PlayOut(def, 42, Bot.Decent(42));
                Assert.AreEqual(a.StateHash(), b.StateHash(), def.Id);
            }
        }
    }

    public class BalanceTests
    {
        [Test]
        public void EveryShiftIsBeatable()
        {
            // A decent operator must reach at least one star on every shift, on every seed tried,
            // and a strong one must be able to reach three stars somewhere.
            foreach (var def in ShiftCatalog.All)
            {
                int best = 0;
                for (ulong seed = 1; seed <= 5; seed++)
                {
                    var decent = Bot.PlayOut(def, seed * 7919, Bot.Decent(seed));
                    Assert.GreaterOrEqual(decent.StarCount, 1, $"{def.Id} seed {seed}: decent bot scored {decent.Score}");
                    var strong = Bot.PlayOut(def, seed * 7919, Bot.Strong(seed));
                    best = System.Math.Max(best, strong.StarCount);
                }
                Assert.AreEqual(3, best, $"{def.Id}: three stars should be reachable");
            }
        }

        [Test]
        public void RandomInputNeverBreaksInvariants()
        {
            foreach (var def in ShiftCatalog.All)
            {
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    var sim = new ShiftSim(def, seed);
                    var rng = new Rng(seed * 31);
                    while (!sim.Ended && sim.Time < 240f)
                    {
                        int r = rng.Range(0, 10);
                        if (r == 0) sim.SendTo(rng.Range(-1, sim.B.Count + 1));
                        else if (r == 1) sim.BoardAll();
                        else if (r == 2 && sim.All.Count > 0) sim.Board(sim.All[rng.Range(0, sim.All.Count)].Id);
                        else if (r == 3 && sim.Car.Riders.Count > 0 && rng.Chance(0.1f)) sim.DropHere(sim.Car.Riders[0].Id);
                        sim.Tick(1f / 20f);
                        sim.Events.Clear();
                        Assert.LessOrEqual(sim.Car.Load, sim.Car.Capacity);
                        Assert.AreEqual(sim.B.Slots.Count, sim.B.Slots.Distinct().Count());
                        Assert.AreEqual(sim.All.Count(p => p.State == PState.Riding), sim.Car.Riders.Count);
                        Assert.AreEqual(sim.All.Count(p => p.State == PState.Waiting), sim.WaitingCount);
                        Assert.IsFalse(sim.Car.Has(Kind.Vampire) && sim.Car.Has(Kind.Mirror));
                    }
                }
            }
        }
    }
}

using NUnit.Framework;
using OneMoreFloor.Core;

namespace OneMoreFloor.Tests
{
    /// <summary>
    /// The floor labels show where the next card will put each floor (<see cref="ShiftSim.PreviewStop"/>). These
    /// check the prediction against what the stop really does.
    /// </summary>
    public class ForecastTests
    {
        const float Dt = 1f / 60f;

        static ShiftDef Quiet(FloorId[] floors, FloorId[] offsite = null) => new ShiftDef
        {
            Id = "test", Floors = floors, Offsite = offsite ?? new FloorId[0], Duration = 600f, Mix = new float[Defs.KindCount],
            Deck = new[] { 1f, 0, 0, 0, 0, 0 }, PatienceBase = 100f, InitialSpawns = 0, Beat = "none",
        };

        static readonly FloorId[] Five = { FloorId.Lobby, FloorId.Office, FloorId.Library, FloorId.Laundromat, FloorId.Boiler };

        static void StopAt(ShiftSim sim, FloorId f)
        {
            Assert.IsTrue(sim.SendToFloor(f));
            int stops = sim.Stops;
            for (float t = 0f; sim.Stops == stops && t < 20f; t += Dt) sim.Tick(Dt);
            Assert.AreEqual(stops + 1, sim.Stops, "the car made its stop");
        }

        [Test]
        public void ACardNamingTheDockedFloorJamsAndMovesNothing()
        {
            var sim = new ShiftSim(Quiet(Five), 1);
            sim.Forecast[0] = new Card { Type = CardType.Swap, A = FloorId.Office, B = FloorId.Boiler };
            var before = sim.B.Slots.ToArray();

            var jam = sim.PreviewStop(FloorId.Office, out bool jammed);
            Assert.IsTrue(jammed);
            CollectionAssert.AreEqual(before, jam.Slots);

            var elsewhere = sim.PreviewStop(FloorId.Library, out jammed);
            Assert.IsFalse(jammed);
            Assert.AreEqual(FloorId.Boiler, elsewhere.At(1));
            Assert.AreEqual(FloorId.Office, elsewhere.At(4));
            CollectionAssert.AreEqual(before, sim.B.Slots, "a preview changes nothing");

            StopAt(sim, FloorId.Office);
            CollectionAssert.AreEqual(jam.Slots, sim.B.Slots);
        }

        [Test]
        public void ABlockMoveFlowsAroundTheDockedFloor()
        {
            var sim = new ShiftSim(Quiet(Five), 1);
            sim.Forecast[0] = new Card { Type = CardType.Roll, From = 0, Len = 3, Up = true };
            var pred = sim.PreviewStop(FloorId.Office, out bool jammed);
            Assert.IsFalse(jammed);
            CollectionAssert.AreEqual(new[] { FloorId.Library, FloorId.Office, FloorId.Lobby, FloorId.Laundromat, FloorId.Boiler }, pred.Slots);
            StopAt(sim, FloorId.Office);
            CollectionAssert.AreEqual(pred.Slots, sim.B.Slots);
        }

        [Test]
        public void AFloorThatLeavesGoesBeforeTheCardIsPlayed()
        {
            var sim = new ShiftSim(Quiet(Five, new[] { FloorId.Greenhouse }), 1);
            sim.B.Leaving[(int)FloorId.Boiler] = 1;
            sim.Forecast[0] = new Card { Type = CardType.Swap, A = FloorId.Boiler, B = FloorId.Lobby };
            var pred = sim.PreviewStop(FloorId.Library, out bool jammed);
            Assert.IsFalse(jammed);
            Assert.AreEqual(FloorId.Greenhouse, pred.At(4), "the Greenhouse swings into the Boiler Room's slot");
            Assert.AreEqual(FloorId.Lobby, pred.At(0), "the swap fizzles with the Boiler Room gone");
            StopAt(sim, FloorId.Library);
            CollectionAssert.AreEqual(pred.Slots, sim.B.Slots);

            // docked at the leaving floor, it stays (and the card still plays around it)
            var sim2 = new ShiftSim(Quiet(Five, new[] { FloorId.Greenhouse }), 1);
            sim2.B.Leaving[(int)FloorId.Boiler] = 1;
            sim2.Forecast[0] = new Card { Type = CardType.Swap, A = FloorId.Office, B = FloorId.Lobby };
            var pred2 = sim2.PreviewStop(FloorId.Boiler, out _);
            Assert.AreEqual(FloorId.Boiler, pred2.At(4));
            StopAt(sim2, FloorId.Boiler);
            CollectionAssert.AreEqual(pred2.Slots, sim2.B.Slots);
        }

        [Test]
        public void ThePreviewMatchesEveryFullStopOnEveryShift()
        {
            int checkedStops = 0, jams = 0, departures = 0;
            for (int k = 0; k < ShiftCatalog.All.Count; k++)
            {
                var def = ShiftCatalog.Get(k);
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    var sim = new ShiftSim(def, seed * 1009 + (ulong)k);
                    var bot = Bot.Decent(seed);
                    const float dt = 1f / 30f;
                    for (float t = 0f; !sim.Ended && t < (def.Endless ? 240f : 900f); t += dt)
                    {
                        bot.Tick(sim, dt, out _);
                        Building pred = null;
                        bool jam = false;
                        if (sim.Car.Target.HasValue) pred = sim.PreviewStop(sim.Car.Target.Value, out jam);
                        int stops = sim.Stops;
                        sim.Tick(dt);
                        if (sim.Stops != stops && pred != null)
                        {
                            bool sawJam = false;
                            foreach (var e in sim.Events)
                            {
                                if (e.Type == Ev.Jammed) sawJam = true;
                                if (e.Type == Ev.FloorDeparted) departures++;
                            }
                            CollectionAssert.AreEqual(pred.Slots, sim.B.Slots, $"{def.Id} seed {seed} stop {sim.Stops}");
                            Assert.AreEqual(jam, sawJam, $"{def.Id} seed {seed} stop {sim.Stops}: jam");
                            if (jam) jams++;
                            checkedStops++;
                        }
                        sim.Events.Clear();
                    }
                }
            }
            UnityEngine.Debug.Log($"[ForecastTests] {checkedStops} stops checked, {jams} jams, {departures} departures");
            Assert.Greater(checkedStops, 500);
            Assert.Greater(jams, 0, "the jam case came up");
            Assert.Greater(departures, 0, "the departure case came up");
        }
    }
}

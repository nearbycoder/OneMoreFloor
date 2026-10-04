using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    public sealed class ShiftDef
    {
        public int Index;
        public string Id, Day, Title, Note;
        public Kind? NewKind;
        public FloorId? NewFloor;
        public string NewText;

        public float Duration = 150f;
        public bool Endless;

        public FloorId[] Floors;                       // bottom to top
        public FloorId[] Offsite = new FloorId[0];

        public float[] Mix = new float[Defs.KindCount];  // spawn weights by Kind (couriers are event-driven)
        public float PatienceBase = 28f;
        public float SpawnStart = 4f, SpawnEnd = 2f;     // seconds between spawns, start -> end of shift
        public int WaitCapStart = 5, WaitCapEnd = 9;     // max people waiting across the building

        public float[] Deck = new float[6];              // weights by CardType: Calm Swap Rise Sink Roll Flip

        public float DepartPeriod;                       // seconds between departures, 0 = none
        public int DepartStops = 6;
        public int CouriersPerDeparture;
        public int OceanStay = 5;
        public int SwimmersOnArrival = 2;

        public int[] Stars = { 1000, 2000, 3000 };
        public string Lighting = "day";
        public string Music = "day";
        public string Beat = "";
        public int InitialSpawns = 2;

        public int StarsFor(int score)
        {
            int s = 0;
            for (int i = 0; i < Stars.Length; i++) if (score >= Stars[i]) s = i + 1;
            return s;
        }

        public bool Uses(Kind k) => Mix[(int)k] > 0 || (k == Kind.Courier && CouriersPerDeparture > 0)
                                    || (k == Kind.Swimmer && HasOcean);

        public bool HasOcean => System.Array.IndexOf(Floors, FloorId.Ocean) >= 0 || System.Array.IndexOf(Offsite, FloorId.Ocean) >= 0;
    }

    /// <summary>A week at The Shuffleton: every shift, in order.</summary>
    public static class ShiftCatalog
    {
        static List<ShiftDef> all;

        public static IReadOnlyList<ShiftDef> All => all ?? (all = Build());

        public static ShiftDef Get(int index) => All[index];

        static float[] Mix(float commuter = 0, float plant = 0, float mirror = 0, float vampire = 0, float courier = 0,
                           float swimmer = 0, float kid = 0, float tycoon = 0)
            => new[] { commuter, plant, mirror, vampire, courier, swimmer, kid, tycoon };

        static float[] Deck(float calm, float swap, float rise = 0, float sink = 0, float roll = 0, float flip = 0)
            => new[] { calm, swap, rise, sink, roll, flip };

        static List<ShiftDef> Build()
        {
            var L = FloorId.Lobby; var O = FloorId.Office; var Li = FloorId.Library; var La = FloorId.Laundromat;
            var Bo = FloorId.Boiler; var G = FloorId.Greenhouse; var P = FloorId.Penthouse; var C = FloorId.Crypt;
            var Oc = FloorId.Ocean; var D = FloorId.Daycare;

            var list = new List<ShiftDef>
            {
                new ShiftDef
                {
                    Id = "monday", Day = "Monday", Title = "First Day",
                    Note = "Welcome aboard! The building moves around a bit. Don't take it personally.",
                    NewKind = Kind.Commuter, NewText = "Click someone to let them in, then send the car to their floor.",
                    Duration = 120f, Floors = new[] { L, O, Li, La, Bo },
                    Mix = Mix(commuter: 1f), PatienceBase = 32f, SpawnStart = 4.0f, SpawnEnd = 2.1f,
                    WaitCapStart = 4, WaitCapEnd = 8, Deck = Deck(0.32f, 0.68f),
                    Lighting = "morning", Beat = "monday", InitialSpawns = 0,
                    Stars = new[] { 16000, 20000, 21500 },
                },
                new ShiftDef
                {
                    Id = "tuesday", Day = "Tuesday", Title = "Green Thumb",
                    Note = "A plant has checked in. It has needs. Mostly sunlight.",
                    NewKind = Kind.Houseplant, NewFloor = G,
                    NewText = "Houseplants won't get off until the doors have opened on a sunny floor.",
                    Duration = 150f, Floors = new[] { L, La, O, G, Li, Bo },
                    Mix = Mix(commuter: 0.7f, plant: 0.3f), PatienceBase = 30f, SpawnStart = 3.4f, SpawnEnd = 1.75f,
                    WaitCapStart = 5, WaitCapEnd = 10, Deck = Deck(0.3f, 0.7f),
                    Lighting = "noon", Beat = "tuesday",
                    Stars = new[] { 26000, 30000, 33000 },
                },
                new ShiftDef
                {
                    Id = "wednesday", Day = "Wednesday", Title = "Heavy Lifting",
                    Note = "Someone is moving a very large mirror to the Penthouse. Please don't break it.",
                    NewKind = Kind.Mirror, NewFloor = P,
                    NewText = "Mirror Movers take two spaces. Floors can now Rise to the top or Sink to the bottom.",
                    Duration = 150f, Floors = new[] { L, O, Bo, Li, G, La, P },
                    Mix = Mix(commuter: 0.56f, plant: 0.19f, mirror: 0.25f), PatienceBase = 29f, SpawnStart = 3.35f, SpawnEnd = 1.7f,
                    WaitCapStart = 6, WaitCapEnd = 11, Deck = Deck(0.24f, 0.48f, 0.14f, 0.14f),
                    Lighting = "afternoon", Beat = "wednesday",
                    Stars = new[] { 23500, 29500, 37000 },
                },
                new ShiftDef
                {
                    Id = "thursday", Day = "Thursday", Title = "Fangs for Nothing",
                    Note = "The night guests are up early. Keep them out of the sun and away from mirrors.",
                    NewKind = Kind.Vampire, NewFloor = C,
                    NewText = "Vampires won't ride with a mirror, and they turn to bats if the doors open on sunlight.",
                    Duration = 150f, Floors = new[] { C, L, Li, O, La, G, Bo, P },
                    Mix = Mix(commuter: 0.4f, plant: 0.16f, mirror: 0.19f, vampire: 0.25f), PatienceBase = 28f,
                    SpawnStart = 3.95f, SpawnEnd = 2.05f, WaitCapStart = 6, WaitCapEnd = 12,
                    Deck = Deck(0.22f, 0.44f, 0.1f, 0.1f, 0.14f), Lighting = "dusk", Music = "night", Beat = "thursday",
                    Stars = new[] { 20500, 26500, 34500 },
                },
                new ShiftDef
                {
                    Id = "friday", Day = "Friday", Title = "Special Delivery",
                    Note = "Floors have started checking out. Couriers have parcels for them. Hurry.",
                    NewKind = Kind.Courier,
                    NewText = "Some floors leave the building. Couriers must reach them first. Docking at a floor holds it in place.",
                    Duration = 150f, Floors = new[] { L, Bo, O, C, Li, G, P }, Offsite = new[] { La },
                    Mix = Mix(commuter: 0.4f, plant: 0.17f, mirror: 0.2f, vampire: 0.23f), PatienceBase = 28f,
                    SpawnStart = 3.9f, SpawnEnd = 2.2f, WaitCapStart = 6, WaitCapEnd = 12,
                    Deck = Deck(0.24f, 0.44f, 0.1f, 0.1f, 0.12f),
                    DepartPeriod = 20f, DepartStops = 6, CouriersPerDeparture = 1,
                    Lighting = "evening", Beat = "friday",
                    Stars = new[] { 23000, 31500, 39500 },
                },
                new ShiftDef
                {
                    Id = "saturday", Day = "Saturday", Title = "Surf's Up",
                    Note = "The building has been feeling adventurous. Bring a towel.",
                    NewKind = Kind.Swimmer, NewFloor = Oc,
                    NewText = "The Ocean drops in for a few stops. Get the swimmers to the Lobby before the tide goes out.",
                    Duration = 150f, Floors = new[] { L, G, O, Li, La, P, C, Bo }, Offsite = new[] { Oc },
                    Mix = Mix(commuter: 0.38f, plant: 0.15f, mirror: 0.15f, vampire: 0.17f, swimmer: 0.15f), PatienceBase = 28f,
                    SpawnStart = 4.8f, SpawnEnd = 2.8f, WaitCapStart = 6, WaitCapEnd = 12,
                    Deck = Deck(0.24f, 0.44f, 0.1f, 0.1f, 0.12f),
                    DepartPeriod = 24f, DepartStops = 6, CouriersPerDeparture = 1, OceanStay = 6, SwimmersOnArrival = 2,
                    Lighting = "beach", Beat = "saturday",
                    Stars = new[] { 26000, 33000, 40000 },
                },
                new ShiftDef
                {
                    Id = "sunday", Day = "Sunday", Title = "Family Day",
                    Note = "Bring-your-kid-to-work day. One of them has found the buttons.",
                    NewKind = Kind.Kid, NewFloor = D,
                    NewText = "Kids press every button: the car stops at each floor on the way, so watch for sunlight.",
                    Duration = 150f, Floors = new[] { L, D, O, G, Li, C, La, Bo, P }, Offsite = new[] { Oc },
                    Mix = Mix(commuter: 0.3f, plant: 0.12f, mirror: 0.12f, vampire: 0.14f, swimmer: 0.1f, kid: 0.22f), PatienceBase = 29f,
                    SpawnStart = 5.1f, SpawnEnd = 3.4f, WaitCapStart = 7, WaitCapEnd = 12,
                    Deck = Deck(0.22f, 0.44f, 0.1f, 0.1f, 0.14f),
                    DepartPeriod = 26f, DepartStops = 6, CouriersPerDeparture = 1, OceanStay = 5, SwimmersOnArrival = 1,
                    Lighting = "golden", Beat = "sunday",
                    Stars = new[] { 16500, 22500, 28000 },
                },
                new ShiftDef
                {
                    Id = "monday2", Day = "Monday Again", Title = "Board Meeting",
                    Note = "The owner is in today. He does not do small talk, and he does not do stops.",
                    NewKind = Kind.Tycoon,
                    NewText = "Tycoons go express: stop anywhere else first and you lose the tip. They pay very well.",
                    Duration = 150f, Floors = new[] { L, O, Li, G, D, La, C, Bo, P }, Offsite = new[] { Oc },
                    Mix = Mix(commuter: 0.28f, plant: 0.11f, mirror: 0.11f, vampire: 0.12f, swimmer: 0.08f, kid: 0.15f, tycoon: 0.15f),
                    PatienceBase = 28f, SpawnStart = 5.2f, SpawnEnd = 3.4f, WaitCapStart = 7, WaitCapEnd = 12,
                    Deck = Deck(0.22f, 0.42f, 0.1f, 0.1f, 0.16f),
                    DepartPeriod = 26f, DepartStops = 6, CouriersPerDeparture = 1, OceanStay = 5, SwimmersOnArrival = 1,
                    Lighting = "overcast", Beat = "monday2",
                    Stars = new[] { 19500, 27000, 33000 },
                },
                new ShiftDef
                {
                    Id = "graveyard", Day = "Friday the 13th", Title = "Graveyard Shift",
                    Note = "Everyone is checking in tonight. Everyone. Good luck.",
                    NewText = "Everything at once, and floors can now Flip.",
                    Duration = 180f, Floors = new[] { C, L, Li, O, G, D, La, Bo, P }, Offsite = new[] { Oc },
                    Mix = Mix(commuter: 0.22f, plant: 0.12f, mirror: 0.12f, vampire: 0.18f, swimmer: 0.08f, kid: 0.14f, tycoon: 0.14f),
                    PatienceBase = 29f, SpawnStart = 6.6f, SpawnEnd = 4.15f, WaitCapStart = 8, WaitCapEnd = 11,
                    Deck = Deck(0.2f, 0.38f, 0.1f, 0.1f, 0.14f, 0.08f),
                    DepartPeriod = 22f, DepartStops = 6, CouriersPerDeparture = 1, OceanStay = 5, SwimmersOnArrival = 1,
                    Lighting = "night", Music = "night", Beat = "graveyard",
                    Stars = new[] { 24500, 32500, 41000 },
                },
                new ShiftDef
                {
                    Id = "overtime", Day = "Overtime", Title = "One More Floor",
                    Note = "No clock. The building never sleeps, and now neither do you. Five complaints and you're done.",
                    NewText = "Endless. It keeps getting busier until five complaints end it.",
                    Duration = 0f, Endless = true, Floors = new[] { L, O, Li, G, D, La, C, Bo, P }, Offsite = new[] { Oc },
                    Mix = Mix(commuter: 0.24f, plant: 0.12f, mirror: 0.12f, vampire: 0.16f, swimmer: 0.08f, kid: 0.14f, tycoon: 0.14f),
                    PatienceBase = 27f, SpawnStart = 3.4f, SpawnEnd = 1.5f, WaitCapStart = 7, WaitCapEnd = 14,
                    Deck = Deck(0.2f, 0.38f, 0.1f, 0.1f, 0.14f, 0.08f),
                    DepartPeriod = 24f, DepartStops = 6, CouriersPerDeparture = 1, OceanStay = 5, SwimmersOnArrival = 1,
                    Lighting = "night", Music = "night", Beat = "overtime",
                    Stars = new[] { 9000, 16500, 28000 },
                },
            };
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list;
        }
    }
}

using System;

namespace OneMoreFloor.Core
{
    public enum FloorId { Lobby, Office, Library, Laundromat, Boiler, Greenhouse, Penthouse, Crypt, Ocean, Daycare }

    public enum Kind { Commuter, Houseplant, Mirror, Vampire, Courier, Swimmer, Kid, Tycoon }

    public enum PState { Waiting, Riding, Done }

    public enum Outcome { None, Delivered, StormedOff, Fumed, Poofed, SweptAway, PackageLost, WentWithFloor }

    public enum CarState { Docked, Closing, Moving, QuickStop, Opening }

    public enum BoardResult { Ok, NotDocked, NotHere, Full, Conflict }

    public sealed class FloorDef
    {
        public FloorId Id;
        public string Name;
        public uint Color;      // 0xRRGGBB signature hue
        public bool Sunny;
        public bool CanDepart;
    }

    public sealed class KindDef
    {
        public Kind Kind;
        public string Name;
        public int Size;
        public int Fare;
        public float PatienceMul;   // multiplier on the shift's base patience
        public float RideDrain;     // patience drain rate while riding (1 = same as waiting)
        public string Rule;         // one line, shown on the intro card
    }

    /// <summary>Static content tables for floors and passenger kinds.</summary>
    public static class Defs
    {
        public const int FloorCount = 10;
        public const int KindCount = 8;

        static readonly FloorDef[] floors =
        {
            new FloorDef { Id = FloorId.Lobby,      Name = "Lobby",       Color = 0xC8323F, CanDepart = false },
            new FloorDef { Id = FloorId.Office,     Name = "Office",      Color = 0xE0A526, CanDepart = true },
            new FloorDef { Id = FloorId.Library,    Name = "Library",     Color = 0x95603A, CanDepart = true },
            new FloorDef { Id = FloorId.Laundromat, Name = "Laundromat",  Color = 0x3FBFC0, CanDepart = true },
            new FloorDef { Id = FloorId.Boiler,     Name = "Boiler Room", Color = 0xE2622A, CanDepart = true },
            new FloorDef { Id = FloorId.Greenhouse, Name = "Greenhouse",  Color = 0x7CC242, CanDepart = true, Sunny = true },
            new FloorDef { Id = FloorId.Penthouse,  Name = "Penthouse",   Color = 0x7B4FC0, CanDepart = true },
            new FloorDef { Id = FloorId.Crypt,      Name = "Crypt",       Color = 0x56606E, CanDepart = true },
            new FloorDef { Id = FloorId.Ocean,      Name = "Ocean",       Color = 0x1F86D0, CanDepart = true, Sunny = true },
            new FloorDef { Id = FloorId.Daycare,    Name = "Daycare",     Color = 0xF06EAA, CanDepart = true },
        };

        static readonly KindDef[] kinds =
        {
            new KindDef { Kind = Kind.Commuter,   Name = "Commuter",     Size = 1, Fare = 100, PatienceMul = 1.00f, RideDrain = 0.4f,
                          Rule = "Just wants their floor." },
            new KindDef { Kind = Kind.Houseplant, Name = "Houseplant",   Size = 1, Fare = 150, PatienceMul = 1.10f, RideDrain = 1.2f,
                          Rule = "Won't get off until the doors have opened on a sunny floor." },
            new KindDef { Kind = Kind.Mirror,     Name = "Mirror Mover", Size = 2, Fare = 220, PatienceMul = 1.10f, RideDrain = 0.4f,
                          Rule = "Takes two spaces. Won't ride with a vampire." },
            new KindDef { Kind = Kind.Vampire,    Name = "Vampire",      Size = 1, Fare = 180, PatienceMul = 1.00f, RideDrain = 0.4f,
                          Rule = "Won't ride with a mirror. Open the doors on sunlight and it's bats." },
            new KindDef { Kind = Kind.Courier,    Name = "Courier",      Size = 1, Fare = 260, PatienceMul = 1.40f, RideDrain = 0.4f,
                          Rule = "The floor on the label is leaving. Get there first." },
            new KindDef { Kind = Kind.Swimmer,    Name = "Swimmer",      Size = 1, Fare = 160, PatienceMul = 1.25f, RideDrain = 0.4f,
                          Rule = "Rescue them before the tide goes out. \"Lobby, please.\"" },
            new KindDef { Kind = Kind.Kid,        Name = "Kid",          Size = 1, Fare = 120, PatienceMul = 0.95f, RideDrain = 0.5f,
                          Rule = "Pressed every button: you'll stop at every floor on the way." },
            new KindDef { Kind = Kind.Tycoon,     Name = "Tycoon",       Size = 1, Fare = 380, PatienceMul = 0.85f, RideDrain = 0.85f,
                          Rule = "Express only. Any other stop first and you lose the tip." },
        };

        public static FloorDef Floor(FloorId id) => floors[(int)id];
        public static KindDef Of(Kind k) => kinds[(int)k];
        public static bool Sunny(FloorId id) => floors[(int)id].Sunny;
    }

    /// <summary>Timings and scoring constants shared by the sim, the bot and the presentation.</summary>
    public static class Tuning
    {
        // Car motion (units: slots, seconds)
        public const float MaxSpeed = 5.2f;
        public const float Accel = 30f;
        public const float DoorOpen = 0.26f;
        public const float DoorClose = 0.2f;
        public const float QuickOpen = 0.14f;
        public const float QuickHold = 0.24f;
        public const float QuickClose = 0.12f;

        public const int Capacity = 4;
        public const int QueueCap = 5;
        public const int MaxComplaints = 5;
        public const float RushHourSeconds = 30f;
        public const float RushFareMul = 1.5f;
        public const float RushSpawnMul = 1.4f;

        public const float StreakStep = 0.05f;
        public const int StreakCap = 30;
        public const float GroupStep = 0.25f;
        public const int JustInTimeBonus = 100;

        /// <summary>Seconds for a trip of <paramref name="dist"/> slots with no stops (trapezoid profile).</summary>
        public static float TravelTime(float dist)
        {
            if (dist <= 0f) return 0f;
            float accelDist = MaxSpeed * MaxSpeed / (2f * Accel);
            if (dist <= 2f * accelDist) return 2f * (float)Math.Sqrt(dist / Accel);
            return 2f * MaxSpeed / Accel + (dist - 2f * accelDist) / MaxSpeed;
        }
    }
}

using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    public sealed class Passenger
    {
        public int Id;
        public Kind Kind;
        public FloorId Origin, Dest;
        public FloorId At;            // where they're waiting (valid while Waiting)
        public PState State;
        public Outcome Outcome;
        public float Patience, PatienceMax;
        public float SpawnTime;
        public bool Sunned;           // houseplant has had its sun
        public bool ExpressBroken;    // tycoon was made to stop somewhere else
        public bool Fuming;           // ran out of patience while riding; leaves at the next stop
        public int Variant;           // cosmetic (suit colour etc.)

        public int Size => Defs.Of(Kind).Size;
        public float PatienceFrac => PatienceMax <= 0 ? 0 : Patience / PatienceMax;
        public bool Active => State != PState.Done;
    }

    public sealed class Car
    {
        public float Pos, Vel;
        public CarState State = CarState.Docked;
        public float Timer;
        public int Phase;
        public int DockedSlot;
        public int QuickSlot = -1;
        public FloorId? Target;
        public int Capacity = Tuning.Capacity;
        public readonly List<Passenger> Riders = new List<Passenger>();

        public int Load
        {
            get { int n = 0; foreach (var r in Riders) n += r.Size; return n; }
        }

        public int Free => Capacity - Load;

        /// <summary>Doors open (or opening) at a destination you chose: boarding is allowed.</summary>
        public bool IsOpen => State == CarState.Docked || State == CarState.Opening;

        public bool Has(Kind k)
        {
            foreach (var r in Riders) if (r.Kind == k) return true;
            return false;
        }
    }

    public enum Ev
    {
        ShiftStarted, ClockStarted, Spawned, Boarded, BoardRefused, Dropped,
        Departing, Retarget, QuickStop, QuickStopEnd, Arrived, Shuffled, Jammed, Calm,
        LeavingScheduled, LeavingTick, DepartureJammed, FloorDeparted,
        Delivered, NeedsSun, Sunned, Poofed, ExpressBroken, Complaint, StormedOff, Fuming, FumedOut,
        PackageLost, SweptAway, WentWithFloor, ChangedMind, StreakBroken, RushHour, Fired, ShiftEnded, Beat,
    }

    [System.Flags]
    public enum DeliveryFlags { None = 0, Express = 1, JustInTime = 2, Sunkissed = 4, Rescued = 8, Rush = 16 }

    public struct SimEvent
    {
        public Ev Type;
        public int Pid;
        public FloorId Floor, Floor2;
        public int Slot;
        public int Value;
        public int Aux;
        public Card Card;
        public Move[] Moves;
        public string Text;

        public override string ToString() => $"{Type} p{Pid} {Floor}/{Floor2} s{Slot} v{Value} a{Aux} {Text}";
    }
}

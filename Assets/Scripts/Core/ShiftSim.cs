using System;
using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    /// <summary>
    /// The whole game rules for one shift. Deterministic for a given seed and command sequence, with no
    /// Unity dependency: the presentation, the bot and the tests all drive this same object.
    /// </summary>
    public sealed partial class ShiftSim
    {
        public readonly ShiftDef Def;
        public readonly Rng Rng;
        public readonly Building B;
        public readonly Car Car = new Car();
        public readonly List<Passenger> All = new List<Passenger>();
        public readonly List<Passenger>[] Waiting = new List<Passenger>[Defs.FloorCount];
        public readonly List<Card> Forecast = new List<Card>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public float Time;
        public float TimeLeft;
        public bool ClockRunning;
        public bool Ended, Fired, RushHour;
        public int Score, Streak, BestStreak, Complaints, DeliveredCount, Stops;
        /// <summary>Complaints by cause, indexed by <see cref="Outcome"/>.</summary>
        public readonly int[] ComplaintsBy = new int[System.Enum.GetValues(typeof(Outcome)).Length];

        int nextId = 1;
        readonly List<Card> history = new List<Card>();
        readonly List<Move> moveBuf = new List<Move>();
        FloorId? oceanDisplaced;
        readonly bool[] forceDepart = new bool[Defs.FloorCount];

        /// <summary>
        /// Relaxed shift (an assist): patience lasts <see cref="Tuning.RelaxedPatience"/>x longer and complaints never
        /// end the shift. Never applies to endless shifts, which only end on complaints.
        /// </summary>
        public readonly bool Relaxed;

        public ShiftSim(ShiftDef def, ulong seed, bool relaxed = false)
        {
            Def = def;
            Relaxed = relaxed && !def.Endless;
            Rng = new Rng(seed);
            B = new Building(def.Floors, def.Offsite);
            for (int i = 0; i < Waiting.Length; i++) Waiting[i] = new List<Passenger>();
            TimeLeft = def.Endless ? 0f : def.Duration;
            Car.DockedSlot = 0;
            Car.Pos = 0;
            Car.State = CarState.Docked;
            ClockRunning = true;
            StartDirector();
            RefillForecast();
            Emit(new SimEvent { Type = Ev.ShiftStarted });
        }

        // ------------------------------------------------------------------ queries

        public FloorId DockedFloor => B.At(Car.DockedSlot);
        public bool Docked => Car.IsOpen;
        public int WaitingCount { get { int n = 0; foreach (var w in Waiting) n += w.Count; return n; } }

        /// <summary>Patience left (0–1) of the most impatient guest waiting on a floor; 1 when nobody waits there.</summary>
        public float LowestWaitingPatience(FloorId f)
        {
            float low = 1f;
            foreach (var p in Waiting[(int)f]) low = Math.Min(low, p.PatienceFrac);
            return Math.Max(0f, low);
        }
        public int StarCount => Def.StarsFor(Score);
        public float Multiplier => 1f + Tuning.StreakStep * Math.Min(Streak, Tuning.StreakCap);

        public Passenger Find(int id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }

        /// <summary>Slot the car will physically stop at next (a kid's quick stop, or the target).</summary>
        public int NextStopSlot
        {
            get
            {
                if (!Car.Target.HasValue) return Car.DockedSlot;
                int target = B.SlotOf(Car.Target.Value);
                if (target < 0) return Car.DockedSlot;
                if (!Car.Has(Kind.Kid)) return target;
                float dir = Math.Sign(target - Car.Pos);
                if (dir == 0) return target;
                int next = dir > 0 ? (int)Math.Floor(Car.Pos + 1e-3f) + 1 : (int)Math.Ceiling(Car.Pos - 1e-3f) - 1;
                return (dir > 0 ? next < target : next > target) ? next : target;
            }
        }

        /// <summary>0..1 how close things are to going wrong; drives the music.</summary>
        public float Trouble
        {
            get
            {
                float worst = 0f;
                foreach (var p in All)
                    if (p.Active) worst = Math.Max(worst, 1f - p.PatienceFrac);
                float t = SmoothStep(0.45f, 0.95f, worst);
                t += 0.07f * Complaints;
                for (int f = 0; f < Defs.FloorCount; f++)
                    if (B.Leaving[f] >= 0 && B.Leaving[f] <= 1 && (FloorId)f != FloorId.Ocean) t += 0.1f;
                return Math.Min(1f, Math.Max(0f, t));
            }
        }

        // ------------------------------------------------------------------ commands

        public BoardResult Board(int pid)
        {
            if (Ended || !Car.IsOpen) return BoardResult.NotDocked;
            var p = Find(pid);
            var here = DockedFloor;
            if (p == null || p.State != PState.Waiting || p.At != here) return BoardResult.NotHere;
            if (Car.Load + p.Size > Car.Capacity)
            {
                Emit(new SimEvent { Type = Ev.BoardRefused, Pid = pid, Floor = here, Aux = (int)BoardResult.Full });
                return BoardResult.Full;
            }
            var other = ConflictWith(p);
            if (other != null)
            {
                Emit(new SimEvent { Type = Ev.BoardRefused, Pid = pid, Floor = here, Aux = (int)BoardResult.Conflict, Value = other.Id });
                return BoardResult.Conflict;
            }
            Waiting[(int)here].Remove(p);
            Car.Riders.Add(p);
            p.State = PState.Riding;
            Emit(new SimEvent { Type = Ev.Boarded, Pid = pid, Floor = here, Slot = Car.DockedSlot });
            return BoardResult.Ok;
        }

        public Passenger ConflictWith(Passenger p)
        {
            foreach (var r in Car.Riders)
            {
                if (p.Kind == Kind.Vampire && r.Kind == Kind.Mirror) return r;
                if (p.Kind == Kind.Mirror && r.Kind == Kind.Vampire) return r;
            }
            return null;
        }

        public bool CanBoard(Passenger p) => Car.IsOpen && p.State == PState.Waiting && p.At == DockedFloor
                                             && Car.Load + p.Size <= Car.Capacity && ConflictWith(p) == null;

        public int BoardAll()
        {
            if (!Car.IsOpen) return 0;
            int n = 0;
            var queue = new List<Passenger>(Waiting[(int)DockedFloor]);
            foreach (var p in queue)
            {
                if (Car.Free <= 0) break;
                if (Car.Load + p.Size > Car.Capacity || ConflictWith(p) != null) continue;
                if (Board(p.Id) == BoardResult.Ok) n++;
            }
            return n;
        }

        public bool DropHere(int pid)
        {
            if (Ended || !Car.IsOpen) return false;
            var p = Find(pid);
            if (p == null || p.State != PState.Riding) return false;
            var here = DockedFloor;
            Car.Riders.Remove(p);
            p.State = PState.Waiting;
            p.At = here;
            Waiting[(int)here].Add(p);
            Emit(new SimEvent { Type = Ev.Dropped, Pid = pid, Floor = here, Slot = Car.DockedSlot });
            return true;
        }

        public bool SendToFloor(FloorId f)
        {
            int s = B.SlotOf(f);
            return s >= 0 && SendTo(s);
        }

        public bool SendTo(int slot)
        {
            if (Ended || slot < 0 || slot >= B.Count) return false;
            var f = B.At(slot);
            switch (Car.State)
            {
                case CarState.Docked:
                case CarState.Opening:
                    if (slot == Car.DockedSlot) return false;
                    Car.Target = f;
                    Car.State = CarState.Closing;
                    Car.Timer = Tuning.DoorClose;
                    Emit(new SimEvent { Type = Ev.Departing, Floor = f, Slot = slot, Aux = Car.DockedSlot });
                    return true;
                case CarState.Closing:
                    if (slot == Car.DockedSlot)
                    {
                        Car.Target = null;
                        Car.State = CarState.Opening;
                        Car.Timer = Tuning.DoorOpen * 0.5f;
                        Emit(new SimEvent { Type = Ev.Retarget, Floor = f, Slot = slot });
                        return true;
                    }
                    Car.Target = f;
                    Emit(new SimEvent { Type = Ev.Retarget, Floor = f, Slot = slot });
                    return true;
                default:
                    if (Car.Target == f) return false;
                    Car.Target = f;
                    Emit(new SimEvent { Type = Ev.Retarget, Floor = f, Slot = slot });
                    return true;
            }
        }

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt)
        {
            if (Ended || dt <= 0f) return;
            Time += dt;
            if (ClockRunning && !Def.Endless)
            {
                TimeLeft -= dt;
                if (!RushHour && TimeLeft <= Tuning.RushHourSeconds)
                {
                    RushHour = true;
                    Emit(new SimEvent { Type = Ev.RushHour });
                }
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    End(false);
                    return;
                }
            }
            if (ClockRunning) TickPatience(dt);
            if (Ended) return;
            TickCar(dt);
            TickDirector(dt);
        }

        void TickPatience(float dt)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var p = All[i];
                if (p.State == PState.Done) continue;
                float rate;
                if (p.State == PState.Waiting) rate = 1f;
                else if (p.Kind == Kind.Houseplant) rate = p.Sunned ? 0.5f : Defs.Of(p.Kind).RideDrain;
                else rate = Defs.Of(p.Kind).RideDrain;
                if (p.Fuming) continue;
                p.Patience -= rate * dt;
                if (p.Patience > 0f) continue;
                p.Patience = 0f;
                if (p.State == PState.Waiting)
                {
                    Waiting[(int)p.At].Remove(p);
                    Finish(p, Outcome.StormedOff);
                    Emit(new SimEvent { Type = Ev.StormedOff, Pid = p.Id, Floor = p.At });
                    Complain(p, Outcome.StormedOff);
                }
                else
                {
                    p.Fuming = true;
                    Emit(new SimEvent { Type = Ev.Fuming, Pid = p.Id });
                    Complain(p, Outcome.Fumed);
                }
                if (Ended) return;
            }
        }

        void TickCar(float dt)
        {
            switch (Car.State)
            {
                case CarState.Docked:
                    break;
                case CarState.Opening:
                    Car.Timer -= dt;
                    if (Car.Timer <= 0f) Car.State = CarState.Docked;
                    break;
                case CarState.Closing:
                    Car.Timer -= dt;
                    if (Car.Timer <= 0f)
                    {
                        Car.State = CarState.Moving;
                        Car.Vel = 0f;
                    }
                    break;
                case CarState.Moving:
                    StepMotion(dt);
                    break;
                case CarState.QuickStop:
                    Car.Timer -= dt;
                    if (Car.Timer > 0f) break;
                    if (Car.Phase == 0)
                    {
                        Car.Phase = 1;
                        Car.Timer = Tuning.QuickHold;
                        StopEffects(B.At(Car.QuickSlot), Car.QuickSlot, false);
                    }
                    else if (Car.Phase == 1)
                    {
                        Car.Phase = 2;
                        Car.Timer = Tuning.QuickClose;
                    }
                    else
                    {
                        Car.State = CarState.Moving;
                        Emit(new SimEvent { Type = Ev.QuickStopEnd, Slot = Car.QuickSlot });
                        Car.QuickSlot = -1;
                    }
                    break;
            }
        }

        void StepMotion(float dt)
        {
            if (!Car.Target.HasValue || !B.Has(Car.Target.Value))
            {
                int nearest = Math.Max(0, Math.Min(B.Count - 1, (int)Math.Round(Car.Pos)));
                Car.Target = B.At(nearest);
            }
            int stop = NextStopSlot;
            float d = stop - Car.Pos;
            float dir = Math.Sign(d);
            float desired = dir * Math.Min(Tuning.MaxSpeed, (float)Math.Sqrt(2f * Tuning.Accel * Math.Abs(d)));
            float dv = desired - Car.Vel;
            float maxDv = Tuning.Accel * dt;
            if (dv > maxDv) dv = maxDv; else if (dv < -maxDv) dv = -maxDv;
            Car.Vel += dv;
            float prev = Car.Pos;
            Car.Pos += Car.Vel * dt;
            // the shaft has buffers at both ends: a hard reversal near the end can't overshoot them
            if (Car.Pos < 0f) { Car.Pos = 0f; Car.Vel = 0f; }
            else if (Car.Pos > B.Count - 1) { Car.Pos = B.Count - 1; Car.Vel = 0f; }
            bool crossed = (prev - stop) * (Car.Pos - stop) <= 0f && Math.Abs(prev - stop) > 1e-5f;
            if (Math.Abs(stop - Car.Pos) < 0.015f || crossed)
            {
                Car.Pos = stop;
                Car.Vel = 0f;
                int target = B.SlotOf(Car.Target.Value);
                if (stop == target) FullStop(stop);
                else BeginQuickStop(stop);
            }
        }

        void BeginQuickStop(int slot)
        {
            Car.State = CarState.QuickStop;
            Car.Phase = 0;
            Car.Timer = Tuning.QuickOpen;
            Car.QuickSlot = slot;
            Emit(new SimEvent { Type = Ev.QuickStop, Slot = slot, Floor = B.At(slot) });
        }

        void FullStop(int slot)
        {
            Stops++;
            Car.DockedSlot = slot;
            Car.Target = null;
            Car.State = CarState.Opening;
            Car.Timer = Tuning.DoorOpen;
            var here = B.At(slot);
            Emit(new SimEvent { Type = Ev.Arrived, Floor = here, Slot = slot, Value = Stops });

            TickDepartures(here);
            here = B.At(slot); // a scripted departure may have swapped the docked floor (the ocean)
            PlayCard(here);
            StopEffects(here, slot, true);
            DirectorOnStop(here);
        }

        void PlayCard(FloorId anchor)
        {
            if (Forecast.Count == 0) RefillForecast();
            var card = Forecast[0];
            Forecast.RemoveAt(0);
            history.Add(card);
            bool ok = B.Apply(card, anchor, moveBuf);
            if (!ok) Emit(new SimEvent { Type = Ev.Jammed, Card = card, Floor = anchor, Slot = Car.DockedSlot });
            else if (moveBuf.Count > 0) Emit(new SimEvent { Type = Ev.Shuffled, Card = card, Moves = moveBuf.ToArray() });
            else Emit(new SimEvent { Type = Ev.Calm, Card = card });
            RefillForecast();
        }

        void TickDepartures(FloorId here)
        {
            // Only floors already counting down when we stopped; one that arrives during this stop
            // (the ocean) starts counting at the next.
            var ticking = new bool[Defs.FloorCount];
            for (int f = 0; f < Defs.FloorCount; f++) ticking[f] = B.Leaving[f] >= 0 && B.Has((FloorId)f);
            for (int f = 0; f < Defs.FloorCount; f++)
            {
                var id = (FloorId)f;
                if (!ticking[f] || B.Leaving[f] < 0 || !B.Has(id)) continue;
                B.Leaving[f]--;
                if (B.Leaving[f] > 0)
                {
                    Emit(new SimEvent { Type = Ev.LeavingTick, Floor = id, Value = B.Leaving[f], Slot = B.SlotOf(id) });
                    continue;
                }
                B.Leaving[f] = 0;
                if (id == here && !forceDepart[f])
                {
                    Emit(new SimEvent { Type = Ev.DepartureJammed, Floor = id, Slot = B.SlotOf(id) });
                    continue;
                }
                Depart(id);
            }
        }

        void Depart(FloorId f)
        {
            int slot = B.SlotOf(f);
            FloorId? preferred = f == FloorId.Ocean ? oceanDisplaced : null;
            var incoming = B.Replace(f, preferred);
            forceDepart[(int)f] = false;
            if (incoming == f)
            {
                B.Leaving[(int)f] = -1;
                return;
            }
            Emit(new SimEvent { Type = Ev.FloorDeparted, Floor = f, Floor2 = incoming, Slot = slot });

            foreach (var p in Waiting[(int)f])
            {
                if (p.Kind == Kind.Swimmer)
                {
                    Finish(p, Outcome.SweptAway);
                    Emit(new SimEvent { Type = Ev.SweptAway, Pid = p.Id, Floor = f });
                    Complain(p, Outcome.SweptAway);
                }
                else
                {
                    Finish(p, Outcome.WentWithFloor);
                    Emit(new SimEvent { Type = Ev.WentWithFloor, Pid = p.Id, Floor = f });
                }
            }
            Waiting[(int)f].Clear();

            for (int i = 0; i < All.Count; i++)
            {
                var p = All[i];
                if (!p.Active || p.Dest != f) continue;
                if (p.Kind == Kind.Courier)
                {
                    if (p.State == PState.Riding) Car.Riders.Remove(p);
                    else Waiting[(int)p.At].Remove(p);
                    Finish(p, Outcome.PackageLost);
                    Emit(new SimEvent { Type = Ev.PackageLost, Pid = p.Id, Floor = f });
                    Complain(p, Outcome.PackageLost);
                }
                else
                {
                    var origin = p.State == PState.Riding ? DockedFloor : p.At;
                    var nd = PickDest(p.Kind, origin);
                    if (nd.HasValue)
                    {
                        p.Dest = nd.Value;
                        Emit(new SimEvent { Type = Ev.ChangedMind, Pid = p.Id, Floor = f, Floor2 = nd.Value });
                    }
                }
            }

            if (incoming == FloorId.Ocean)
            {
                oceanDisplaced = f;
                B.Leaving[(int)FloorId.Ocean] = Def.OceanStay;
                Emit(new SimEvent { Type = Ev.LeavingScheduled, Floor = FloorId.Ocean, Value = Def.OceanStay, Slot = slot });
                for (int i = 0; i < Def.SwimmersOnArrival; i++) Spawn(Kind.Swimmer, FloorId.Ocean, FloorId.Lobby);
            }
            if (Ended) return;
        }

        void StopEffects(FloorId here, int slot, bool full)
        {
            bool sunny = Defs.Sunny(here);
            if (sunny)
            {
                for (int i = Car.Riders.Count - 1; i >= 0; i--)
                {
                    var r = Car.Riders[i];
                    if (r.Kind != Kind.Vampire) continue;
                    Car.Riders.RemoveAt(i);
                    Finish(r, Outcome.Poofed);
                    Emit(new SimEvent { Type = Ev.Poofed, Pid = r.Id, Floor = here, Slot = slot });
                    Complain(r, Outcome.Poofed);
                    if (Ended) return;
                }
                foreach (var r in Car.Riders)
                {
                    if (r.Kind != Kind.Houseplant || r.Sunned) continue;
                    r.Sunned = true;
                    r.Patience = r.PatienceMax;
                    Emit(new SimEvent { Type = Ev.Sunned, Pid = r.Id, Floor = here });
                }
            }
            foreach (var r in Car.Riders)
            {
                if (r.Kind != Kind.Tycoon || r.ExpressBroken || r.Dest == here) continue;
                r.ExpressBroken = true;
                Emit(new SimEvent { Type = Ev.ExpressBroken, Pid = r.Id, Floor = here });
            }

            int group = 0;
            var riders = new List<Passenger>(Car.Riders);
            foreach (var r in riders)
            {
                if (r.Fuming)
                {
                    Car.Riders.Remove(r);
                    Finish(r, Outcome.Fumed);
                    Emit(new SimEvent { Type = Ev.FumedOut, Pid = r.Id, Floor = here, Slot = slot });
                    continue;
                }
                if (r.Dest != here) continue;
                if (r.Kind == Kind.Houseplant && !r.Sunned)
                {
                    Emit(new SimEvent { Type = Ev.NeedsSun, Pid = r.Id, Floor = here });
                    continue;
                }
                Deliver(r, group++, here, slot);
            }
        }

        void Deliver(Passenger p, int groupIndex, FloorId here, int slot)
        {
            var kd = Defs.Of(p.Kind);
            float frac = Math.Max(0f, Math.Min(1f, p.PatienceFrac));
            var flags = DeliveryFlags.None;
            float tip = kd.Fare * frac;
            if (p.Kind == Kind.Tycoon)
            {
                if (p.ExpressBroken) tip = 0f; else flags |= DeliveryFlags.Express;
            }
            if (p.Kind == Kind.Houseplant && p.Sunned) flags |= DeliveryFlags.Sunkissed;
            if (p.Kind == Kind.Swimmer) flags |= DeliveryFlags.Rescued;
            int bonus = 0;
            if (p.Kind == Kind.Courier && B.Leaving[(int)here] >= 0 && B.Leaving[(int)here] <= 1)
            {
                flags |= DeliveryFlags.JustInTime;
                bonus = Tuning.JustInTimeBonus;
            }
            float amount = (kd.Fare + tip) * Multiplier * (1f + Tuning.GroupStep * groupIndex);
            if (RushHour)
            {
                amount *= Tuning.RushFareMul;
                flags |= DeliveryFlags.Rush;
            }
            int total = (int)(Math.Round(amount / 5f) * 5f) + bonus;
            Score += total;
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            DeliveredCount++;
            Car.Riders.Remove(p);
            Finish(p, Outcome.Delivered);
            Emit(new SimEvent { Type = Ev.Delivered, Pid = p.Id, Floor = here, Slot = slot, Value = total, Aux = groupIndex, Text = ((int)flags).ToString() });
            if (!ClockRunning)
            {
                ClockRunning = true;
                Emit(new SimEvent { Type = Ev.ClockStarted });
            }
        }

        void Finish(Passenger p, Outcome o)
        {
            p.State = PState.Done;
            p.Outcome = o;
        }

        void Complain(Passenger p, Outcome why)
        {
            Complaints++;
            ComplaintsBy[(int)why]++;
            if (Streak > 0) Emit(new SimEvent { Type = Ev.StreakBroken, Value = Streak });
            Streak = 0;
            Emit(new SimEvent { Type = Ev.Complaint, Pid = p?.Id ?? 0, Value = Complaints, Aux = (int)why });
            if (Complaints >= Tuning.MaxComplaints && !Relaxed) End(true);
        }

        void End(bool fired)
        {
            if (Ended) return;
            Ended = true;
            Fired = fired;
            if (fired) Emit(new SimEvent { Type = Ev.Fired });
            Emit(new SimEvent { Type = Ev.ShiftEnded, Value = Score, Aux = fired ? 1 : 0 });
        }

        void Emit(SimEvent e) => Events.Add(e);

        static float SmoothStep(float a, float b, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Order-sensitive digest of the visible state, for determinism tests.</summary>
        public long StateHash()
        {
            unchecked
            {
                long h = 1469598103934665603L;
                void Mix(long v) { h ^= v; h *= 1099511628211L; }
                Mix(Score); Mix(Complaints); Mix(Stops); Mix(DeliveredCount); Mix(All.Count);
                Mix((long)(Car.Pos * 1000)); Mix((int)Car.State);
                foreach (var f in B.Slots) Mix((int)f);
                foreach (var p in All) { Mix(p.Id); Mix((int)p.State); Mix((int)p.Dest); Mix((long)(p.Patience * 100)); }
                return h;
            }
        }
    }
}

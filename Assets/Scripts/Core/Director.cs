using System;
using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    /// <summary>Spawning, building events, the shuffle deck and each shift's scripted opening.</summary>
    public sealed partial class ShiftSim
    {
        float spawnTimer;
        float departTimer;
        bool holdSpawns;
        readonly float[] weightBuf = new float[16];

        /// <summary>Elapsed fraction of the shift (endless shifts ramp over four minutes, then keep going).</summary>
        public float Progress => Def.Endless ? Math.Min(1f, Time / 240f) : Math.Min(1f, 1f - TimeLeft / Def.Duration);

        void StartDirector()
        {
            spawnTimer = 1.2f;
            departTimer = Def.DepartPeriod > 0 ? Def.DepartPeriod * 0.6f : 0f;

            switch (Def.Beat)
            {
                case "monday":
                    // One commuter, one obvious swap, and the clock waits for the first delivery.
                    ClockRunning = false;
                    holdSpawns = true;
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Office);
                    Forecast.Add(new Card { Type = CardType.Swap, A = FloorId.Library, B = FloorId.Boiler });
                    break;
                case "tuesday":
                    Spawn(Kind.Houseplant, FloorId.Lobby, FloorId.Library);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Office);
                    break;
                case "wednesday":
                    Spawn(Kind.Mirror, FloorId.Lobby, FloorId.Penthouse);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Office);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Library);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Greenhouse);
                    break;
                case "thursday":
                    Spawn(Kind.Vampire, FloorId.Crypt, FloorId.Office);
                    Spawn(Kind.Mirror, FloorId.Lobby, FloorId.Penthouse);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Library);
                    break;
                case "friday":
                    B.Leaving[(int)FloorId.Library] = 4;
                    Emit(new SimEvent { Type = Ev.LeavingScheduled, Floor = FloorId.Library, Value = 4, Slot = B.SlotOf(FloorId.Library) });
                    Spawn(Kind.Courier, FloorId.Lobby, FloorId.Library);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Office);
                    departTimer = Def.DepartPeriod * 1.2f;
                    break;
                case "saturday":
                    // The trailer moment: the Library leaves at the first stop, even if you're docked there,
                    // and the Ocean takes its place.
                    B.Leaving[(int)FloorId.Library] = 1;
                    forceDepart[(int)FloorId.Library] = true;
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Library);
                    holdSpawns = true;
                    departTimer = Def.DepartPeriod * 1.3f;
                    break;
                case "sunday":
                    Spawn(Kind.Kid, FloorId.Lobby, FloorId.Penthouse);
                    Spawn(Kind.Commuter, FloorId.Office, FloorId.Lobby);
                    break;
                case "monday2":
                    Spawn(Kind.Tycoon, FloorId.Lobby, FloorId.Penthouse);
                    Spawn(Kind.Commuter, FloorId.Lobby, FloorId.Office);
                    break;
                case "graveyard":
                    Forecast.Add(new Card { Type = CardType.Flip, From = 2, Len = 5 });
                    Spawn(Kind.Vampire, FloorId.Crypt, FloorId.Library);
                    Spawn(Kind.Houseplant, FloorId.Lobby, FloorId.Office);
                    Spawn(Kind.Kid, FloorId.Lobby, FloorId.Daycare);
                    break;
                default:
                    for (int i = 0; i < Def.InitialSpawns; i++) SpawnRandom();
                    break;
            }
            if (Def.Beat != "monday" && Def.Beat != "saturday" && Def.InitialSpawns > 0 && WaitingCount < 2) SpawnRandom();
            Emit(new SimEvent { Type = Ev.Beat, Text = Def.Beat + ":start" });
        }

        void DirectorOnStop(FloorId here)
        {
            if (Def.Beat == "saturday" && holdSpawns && B.Has(FloorId.Ocean))
            {
                holdSpawns = false;
                spawnTimer = 2.5f;
                Emit(new SimEvent { Type = Ev.Beat, Text = "saturday:ocean", Floor = FloorId.Ocean, Slot = B.SlotOf(FloorId.Ocean) });
            }
        }

        void TickDirector(float dt)
        {
            if (Def.Beat == "monday" && holdSpawns && DeliveredCount > 0)
            {
                holdSpawns = false;
                spawnTimer = 0.6f;
                Spawn(Kind.Commuter, PickOrigin(Kind.Commuter, FloorId.Lobby) ?? FloorId.Library, FloorId.Lobby);
            }
            if (holdSpawns || !ClockRunning) return;

            float prog = Progress;
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                float k = SmoothStep(0f, 1f, prog);
                float interval = Def.SpawnStart + (Def.SpawnEnd - Def.SpawnStart) * k;
                if (Def.Endless && Time > 240f) interval *= Math.Max(0.55f, 1f - (Time - 240f) / 600f);
                if (RushHour) interval /= Tuning.RushSpawnMul;
                spawnTimer = interval * Rng.Range(0.75f, 1.25f);
                int cap = (int)Math.Round(Def.WaitCapStart + (Def.WaitCapEnd - Def.WaitCapStart) * k);
                if (WaitingCount < cap) SpawnRandom();
            }

            if (Def.DepartPeriod > 0f)
            {
                departTimer -= dt;
                if (departTimer <= 0f)
                {
                    departTimer = Def.DepartPeriod * Rng.Range(0.8f, 1.2f);
                    TryScheduleDeparture();
                }
            }
        }

        void TryScheduleDeparture()
        {
            if (B.Offsite.Count == 0) return;
            for (int f = 0; f < Defs.FloorCount; f++)
                if (B.Leaving[f] >= 0 && (FloorId)f != FloorId.Ocean && B.Has((FloorId)f)) return;

            var cands = new List<FloorId>();
            var weights = new List<float>();
            var docked = Car.IsOpen ? DockedFloor : (FloorId?)null;
            foreach (var f in B.Slots)
            {
                var fd = Defs.Floor(f);
                if (!fd.CanDepart || f == FloorId.Ocean || f == docked || B.IsLeaving(f)) continue;
                if (Car.Target.HasValue && Car.Target.Value == f) continue;
                cands.Add(f);
                weights.Add(1f + 0.3f * Waiting[(int)f].Count);
            }
            int pick = Rng.PickWeighted(weights);
            if (pick < 0) return;
            var x = cands[pick];
            B.Leaving[(int)x] = Def.DepartStops;
            Emit(new SimEvent { Type = Ev.LeavingScheduled, Floor = x, Value = Def.DepartStops, Slot = B.SlotOf(x), Floor2 = B.Offsite[0] });
            for (int i = 0; i < Def.CouriersPerDeparture; i++)
            {
                var origin = PickOrigin(Kind.Courier, x);
                if (origin.HasValue) Spawn(Kind.Courier, origin.Value, x);
            }
        }

        void SpawnRandom()
        {
            for (int k = 0; k < Defs.KindCount; k++) weightBuf[k] = Def.Mix[k];
            weightBuf[(int)Kind.Courier] = 0f;
            if (!B.Has(FloorId.Ocean) || B.Leaving[(int)FloorId.Ocean] <= 2) weightBuf[(int)Kind.Swimmer] = 0f;
            int kidx = Rng.PickWeighted(new ArraySegment<float>(weightBuf, 0, Defs.KindCount));
            if (kidx < 0) return;
            var kind = (Kind)kidx;
            var origin = PickOrigin(kind);
            if (!origin.HasValue) return;
            var dest = PickDest(kind, origin.Value);
            if (!dest.HasValue) return;
            Spawn(kind, origin.Value, dest.Value);
        }

        Passenger Spawn(Kind kind, FloorId origin, FloorId dest)
        {
            if (!B.Has(origin) && origin != FloorId.Ocean) return null;
            var kd = Defs.Of(kind);
            float patience = Def.PatienceBase * kd.PatienceMul;
            if (Def.Endless && Time > 240f) patience *= Math.Max(0.75f, 1f - (Time - 240f) / 900f);
            var p = new Passenger
            {
                Id = nextId++, Kind = kind, Origin = origin, Dest = dest, At = origin, State = PState.Waiting,
                Patience = patience, PatienceMax = patience, SpawnTime = Time, Variant = Rng.Range(0, 4),
            };
            All.Add(p);
            Waiting[(int)origin].Add(p);
            Emit(new SimEvent { Type = Ev.Spawned, Pid = p.Id, Floor = origin, Floor2 = dest });
            return p;
        }

        float OriginWeight(Kind kind, FloorId f)
        {
            bool sunny = Defs.Sunny(f);
            switch (kind)
            {
                case Kind.Commuter: return f == FloorId.Lobby ? 3f : f == FloorId.Ocean ? 0f : 1f;
                case Kind.Houseplant: return sunny ? 0f : 1f;
                case Kind.Mirror: return f == FloorId.Lobby || f == FloorId.Penthouse ? 2f : f == FloorId.Library ? 1.5f : sunny ? 0f : 0.6f;
                case Kind.Vampire: return sunny ? 0f : f == FloorId.Crypt ? 4f : 1f;
                case Kind.Courier: return f == FloorId.Lobby ? 3f : f == FloorId.Ocean ? 0f : 1f;
                case Kind.Swimmer: return f == FloorId.Ocean ? 1f : 0f;
                case Kind.Kid: return f == FloorId.Daycare ? 4f : f == FloorId.Ocean ? 0f : 1f;
                case Kind.Tycoon: return f == FloorId.Lobby ? 3f : f == FloorId.Penthouse || f == FloorId.Office ? 2f : f == FloorId.Ocean ? 0f : 0.3f;
            }
            return 1f;
        }

        float DestWeight(Kind kind, FloorId f, FloorId origin)
        {
            bool sunny = Defs.Sunny(f);
            if (f == FloorId.Ocean && kind != Kind.Courier) return 0f;
            switch (kind)
            {
                case Kind.Commuter: return f == FloorId.Office || f == FloorId.Lobby ? 2.5f : 1f;
                case Kind.Houseplant: return sunny ? 0f : 1f;
                case Kind.Mirror: return f == FloorId.Penthouse ? 3f : f == FloorId.Lobby ? 1.5f : 1f;
                case Kind.Vampire: return sunny ? 0f : f == FloorId.Crypt ? 4f : 1f;
                case Kind.Swimmer: return f == FloorId.Lobby ? 1f : 0f;
                case Kind.Kid: return f == FloorId.Daycare ? 4f : 1f;
                case Kind.Tycoon: return f == FloorId.Penthouse ? 3f : f == FloorId.Office || f == FloorId.Lobby ? 2f : 0.3f;
            }
            return 1f;
        }

        FloorId? PickOrigin(Kind kind, FloorId? exclude = null)
        {
            var cands = new List<FloorId>();
            var weights = new List<float>();
            foreach (var f in B.Slots)
            {
                if (exclude.HasValue && f == exclude.Value) continue;
                if (Waiting[(int)f].Count >= Tuning.QueueCap) continue;
                int leaving = B.Leaving[(int)f];
                if (leaving >= 0 && leaving <= 2 && kind != Kind.Swimmer) continue;
                float w = OriginWeight(kind, f);
                if (w <= 0f) continue;
                cands.Add(f);
                weights.Add(w);
            }
            int i = Rng.PickWeighted(weights);
            return i < 0 ? (FloorId?)null : cands[i];
        }

        FloorId? PickDest(Kind kind, FloorId origin)
        {
            var cands = new List<FloorId>();
            var weights = new List<float>();
            foreach (var f in B.Slots)
            {
                if (f == origin || B.IsLeaving(f)) continue;
                float w = DestWeight(kind, f, origin);
                if (w <= 0f) continue;
                cands.Add(f);
                weights.Add(w);
            }
            int i = Rng.PickWeighted(weights);
            return i < 0 ? (FloorId?)null : cands[i];
        }

        // ------------------------------------------------------------------ the shuffle deck

        void RefillForecast()
        {
            while (Forecast.Count < 3)
            {
                var predicted = new Building(B);
                foreach (var c in Forecast) predicted.Apply(c, null, null);
                Forecast.Add(GenerateCard(predicted));
            }
        }

        Card GenerateCard(Building pred)
        {
            var w = new float[6];
            Array.Copy(Def.Deck, w, 6);
            var recent = new List<Card>(history);
            recent.AddRange(Forecast);
            int n = recent.Count;
            if (n >= 2 && recent[n - 1].Type == CardType.Calm && recent[n - 2].Type == CardType.Calm) w[(int)CardType.Calm] = 0f;
            if (pred.Count < 4) { w[(int)CardType.Roll] = 0f; w[(int)CardType.Flip] = 0f; }
            if (pred.Count < 5) w[(int)CardType.Flip] = 0f;
            int t = Rng.PickWeighted(w);
            if (t < 0) return Card.Calm();
            var type = (CardType)t;

            bool lobbyRecent = false;
            for (int i = Math.Max(0, n - 2); i < n; i++)
            {
                var c = recent[i];
                if ((c.Type == CardType.Swap && (c.A == FloorId.Lobby || c.B == FloorId.Lobby)) ||
                    ((c.Type == CardType.Rise || c.Type == CardType.Sink) && c.A == FloorId.Lobby)) lobbyRecent = true;
            }
            var pool = new List<FloorId>();
            foreach (var f in pred.Slots) if (!(lobbyRecent && f == FloorId.Lobby)) pool.Add(f);

            switch (type)
            {
                case CardType.Swap:
                {
                    if (pool.Count < 2) return Card.Calm();
                    Card last = n > 0 ? recent[n - 1] : Card.Calm();
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        var a = Rng.Pick(pool);
                        var b = Rng.Pick(pool);
                        if (a == b) continue;
                        if (last.Type == CardType.Swap && ((last.A == a && last.B == b) || (last.A == b && last.B == a))) continue;
                        // keep most swaps local enough to follow, with the odd long one
                        int dist = Math.Abs(pred.SlotOf(a) - pred.SlotOf(b));
                        if (dist > 4 && Rng.Chance(0.5f)) continue;
                        return new Card { Type = CardType.Swap, A = a, B = b };
                    }
                    return Card.Calm();
                }
                case CardType.Rise:
                {
                    var c = new List<FloorId>();
                    foreach (var f in pool) if (pred.SlotOf(f) < pred.Count - 1) c.Add(f);
                    return c.Count == 0 ? Card.Calm() : new Card { Type = CardType.Rise, A = Rng.Pick(c) };
                }
                case CardType.Sink:
                {
                    var c = new List<FloorId>();
                    foreach (var f in pool) if (pred.SlotOf(f) > 0) c.Add(f);
                    return c.Count == 0 ? Card.Calm() : new Card { Type = CardType.Sink, A = Rng.Pick(c) };
                }
                case CardType.Roll:
                    return new Card { Type = CardType.Roll, From = Rng.Range(0, pred.Count - 2), Len = 3, Up = Rng.Chance(0.5f) };
                case CardType.Flip:
                {
                    int len = Math.Min(pred.Count, Rng.Range(4, 6));
                    return new Card { Type = CardType.Flip, From = Rng.Range(0, pred.Count - len + 1), Len = len };
                }
            }
            return Card.Calm();
        }
    }
}

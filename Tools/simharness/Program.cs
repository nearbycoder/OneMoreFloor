using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreFloor.Core;

// Usage: sim [balance|trace <shift> <seed>|fuzz]
static class Program
{
    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "balance";
        if (mode == "trace") return Trace(int.Parse(args[1]), ulong.Parse(args.Length > 2 ? args[2] : "1"), args.Length > 3 ? args[3] : "decent");
        if (mode == "fuzz") return Fuzz();
        if (mode == "stars") return SuggestStars(args.Length > 1 ? int.Parse(args[1]) : 12);
        return Balance(args.Length > 1 ? int.Parse(args[1]) : 8);
    }

    static int Balance(int seeds)
    {
        Console.WriteLine($"{"shift",-10} {"bot",-7} {"mean",7} {"min",6} {"max",6} {"deliv",6} {"compl",6} {"fired",5} {"stops",6}  stars(now)");
        foreach (var def in ShiftCatalog.All)
        {
            foreach (var (name, make) in new (string, Func<ulong, Bot>)[] { ("novice", Bot.Novice), ("sloppy", Bot.Sloppy), ("decent", Bot.Decent), ("strong", Bot.Strong) })
            {
                var res = new List<ShiftSim>();
                for (ulong s = 1; s <= (ulong)seeds; s++) res.Add(Bot.PlayOut(def, s * 7919, make(s)));
                var sc = res.Select(r => r.Score).ToList();
                Console.WriteLine($"{def.Id,-10} {name,-7} {sc.Average(),7:0} {sc.Min(),6} {sc.Max(),6} {res.Average(r => r.DeliveredCount),6:0.0} {res.Average(r => r.Complaints),6:0.0} {res.Count(r => r.Fired),5} {res.Average(r => r.Stops),6:0}  {string.Join(",", res.Select(r => r.StarCount))}  t={res.Average(r => r.Time):0}");
            }
        }
        return 0;
    }

    static int Round(double v) => (int)(System.Math.Round(v / 500.0) * 500);

    // Suggests star thresholds from bot play: 1 star ~ half a sloppy run, 2 stars ~ a sloppy run,
    // 3 stars ~ a decent (quick, accurate) run.
    static int SuggestStars(int seeds)
    {
        foreach (var def in ShiftCatalog.All)
        {
            double Mean(Func<ulong, Bot> make)
            {
                double t = 0;
                for (ulong s = 1; s <= (ulong)seeds; s++) t += Bot.PlayOut(def, s * 7919, make(s)).Score;
                return t / seeds;
            }
            double novice = Mean(Bot.Novice), sloppy = Mean(Bot.Sloppy), decent = Mean(Bot.Decent);
            int one = Round(def.Id == "monday" ? novice * 0.6 : sloppy * 0.5);
            int two = Round(sloppy * 0.85);
            int three = Round(decent * 0.9);
            Console.WriteLine($"{def.Id,-10} novice {novice,7:0} sloppy {sloppy,7:0} decent {decent,7:0}  ->  Stars = {{ {one}, {two}, {three} }}");
        }
        return 0;
    }

    static int Trace(int shift, ulong seed, string botName)
    {
        // same seeding as Balance: sim seed = seed * 7919, bot seed = seed
        var def = ShiftCatalog.Get(shift);
        var sim = new ShiftSim(def, seed * 7919);
        var bot = botName == "novice" ? Bot.Novice(seed) : botName == "sloppy" ? Bot.Sloppy(seed) : botName == "strong" ? Bot.Strong(seed) : Bot.Decent(seed);
        float dt = 1f / 30f;
        while (!sim.Ended && sim.Time < 400)
        {
            var a = bot.Tick(sim, dt, out int arg);
            if (a == Bot.Action.Drop) Console.WriteLine($"{sim.Time,6:0.00} DROP p{arg}");
            if (a == Bot.Action.Send) Console.WriteLine($"{sim.Time,6:0.00} SEND slot {arg} ({sim.B.At(arg)}) riders=[{string.Join(",", sim.Car.Riders.Select(r => r.Kind + ">" + r.Dest))}]");
            sim.Tick(dt);
            foreach (var e in sim.Events)
            {
                if (e.Type == Ev.Shuffled) Console.WriteLine($"{sim.Time,6:0.00}   {e.Card} -> [{string.Join(" ", sim.B.Slots)}]");
                else if (e.Type != Ev.Retarget) Console.WriteLine($"{sim.Time,6:0.00}   {e}");
            }
            sim.Events.Clear();
        }
        Console.WriteLine($"score={sim.Score} delivered={sim.DeliveredCount} complaints={sim.Complaints} stops={sim.Stops} stars={sim.StarCount}");
        return 0;
    }

    static int Fuzz()
    {
        int failures = 0;
        foreach (var def in ShiftCatalog.All)
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var sim = new ShiftSim(def, seed);
                var rng = new Rng(seed * 31);
                float dt = 1f / 20f;
                try
                {
                    while (!sim.Ended && sim.Time < 300)
                    {
                        int r = rng.Range(0, 10);
                        if (r == 0) sim.SendTo(rng.Range(-1, sim.B.Count + 1));
                        else if (r == 1) sim.BoardAll();
                        else if (r == 2 && sim.All.Count > 0) sim.Board(sim.All[rng.Range(0, sim.All.Count)].Id);
                        else if (r == 3 && sim.Car.Riders.Count > 0 && rng.Chance(0.1f)) sim.DropHere(sim.Car.Riders[0].Id);
                        sim.Tick(dt);
                        sim.Events.Clear();
                        Check(sim);
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine($"FAIL {def.Id} seed {seed}: {ex.Message}\n{ex.StackTrace}");
                    break;
                }
            }
        }
        Console.WriteLine(failures == 0 ? "fuzz OK" : $"fuzz FAILURES {failures}");
        return failures == 0 ? 0 : 1;
    }

    static void Check(ShiftSim sim)
    {
        if (sim.Car.Load > sim.Car.Capacity) throw new Exception("over capacity");
        if (sim.B.Slots.Distinct().Count() != sim.B.Slots.Count) throw new Exception("duplicate floor in stack");
        if (sim.B.Slots.Count != sim.Def.Floors.Length) throw new Exception("stack size changed");
        foreach (var f in sim.B.Offsite) if (sim.B.Has(f)) throw new Exception("offsite floor in stack");
        int waiting = 0, riding = 0;
        foreach (var p in sim.All)
        {
            if (p.State == PState.Waiting) { waiting++; if (!sim.Waiting[(int)p.At].Contains(p)) throw new Exception("waiting bookkeeping"); if (!sim.B.Has(p.At)) throw new Exception($"waiting on missing floor {p.At}"); }
            if (p.State == PState.Riding) { riding++; if (!sim.Car.Riders.Contains(p)) throw new Exception("rider bookkeeping"); }
        }
        if (waiting != sim.WaitingCount) throw new Exception("waiting count mismatch");
        if (riding != sim.Car.Riders.Count) throw new Exception("rider count mismatch");
        if (sim.Car.Pos < -0.01f || sim.Car.Pos > sim.B.Count - 1 + 0.01f) throw new Exception($"car out of shaft {sim.Car.Pos} state={sim.Car.State} vel={sim.Car.Vel} target={sim.Car.Target} tslot={(sim.Car.Target.HasValue ? sim.B.SlotOf(sim.Car.Target.Value) : -9)} next={sim.NextStopSlot} quick={sim.Car.QuickSlot} docked={sim.Car.DockedSlot}");
        foreach (var r in sim.Car.Riders)
            foreach (var o in sim.Car.Riders)
                if (r.Kind == Kind.Vampire && o.Kind == Kind.Mirror) throw new Exception("vampire rode with mirror");
    }
}

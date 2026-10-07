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
        if (mode == "human") return Human(args.Length > 1 ? int.Parse(args[1]) : 16);
        if (mode == "pace") return Pace(args.Length > 1 ? int.Parse(args[1]) : 16);
        if (mode == "relaxed") return Relaxed(args.Length > 1 ? int.Parse(args[1]) : 16);
        if (mode == "causes") return Causes(args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0.5f, args.Length > 2 ? int.Parse(args[2]) : 16);
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

    // How modelled people of three skill levels fare against the star thresholds: mean score and the share
    // of runs reaching 1/2/3 stars. A first-timer should usually clear a shift in a try or two, a practised
    // player should be pushing for three.
    static int Human(int seeds)
    {
        var levels = new (string name, float skill)[] { ("new", 0.15f), ("average", 0.5f), ("practised", 0.85f) };
        Console.WriteLine($"{"shift",-10} {"stars",-20} " + string.Join(" ", levels.Select(l => $"{l.name + " mean  1/2/3*",-30}")));
        foreach (var def in ShiftCatalog.All)
        {
            if (def.Endless) continue;
            var cols = new List<string>();
            foreach (var (name, skill) in levels)
            {
                var res = new List<ShiftSim>();
                for (ulong s = 1; s <= (ulong)seeds; s++) res.Add(Bot.PlayOut(def, s * 7919, Bot.Human(s, skill)));
                double P(int k) => 100.0 * res.Count(r => r.StarCount >= k) / res.Count;
                cols.Add($"{res.Average(r => r.Score),7:0} {P(1),4:0}% {P(2),4:0}% {P(3),4:0}%      ");
            }
            Console.WriteLine($"{def.Id,-10} {string.Join("/", def.Stars),-20} " + string.Join(" ", cols));
        }
        return 0;
    }

    // For each shift: the smallest slow-down of the spawn pacing (both ends of the ramp) at which modelled
    // average players are rarely fired and new players usually survive, then star thresholds from human
    // score percentiles at that pacing.
    // Relaxed shifts against standard ones for modelled people: how often they're fired, mean complaints, and how
    // often they reach the 1-star score (a relaxed clear, which unlocks the next shift).
    static int Relaxed(int seeds)
    {
        var levels = new (string name, float skill)[] { ("lowest", 0f), ("new", 0.15f) };
        Console.WriteLine($"{"shift",-10} " + string.Join("   ", levels.Select(l => $"{l.name + ": standard fired/compl/1*  relaxed fired/compl/1*",-58}")));
        foreach (var def in ShiftCatalog.All)
        {
            if (def.Endless) continue;
            var cols = new List<string>();
            foreach (var (name, skill) in levels)
            {
                string Row(bool relaxed)
                {
                    var res = new List<ShiftSim>();
                    for (ulong s = 1; s <= (ulong)seeds; s++) res.Add(Bot.PlayOut(def, s * 7919, Bot.Human(s, skill), relaxed: relaxed));
                    return $"{res.Count(r => r.Fired) * 100 / seeds,3}% {res.Average(r => r.Complaints),4:0.0} {res.Count(r => r.Score >= def.Stars[0]) * 100 / seeds,3}%";
                }
                cols.Add($"{"",8}{Row(false),-20}  {"",6}{Row(true),-20}");
            }
            Console.WriteLine($"{def.Id,-10} " + string.Join("   ", cols));
        }
        return 0;
    }

    static int Pace(int seeds)
    {
        foreach (var def in ShiftCatalog.All)
        {
            float s0 = def.SpawnStart, e0 = def.SpawnEnd;
            List<ShiftSim> Run(float skill)
            {
                var res = new List<ShiftSim>();
                for (ulong s = 1; s <= (ulong)seeds; s++) res.Add(Bot.PlayOut(def, s * 7919, Bot.Human(s, skill)));
                return res;
            }
            float chosen = 1f;
            string line = "";
            for (float k = 1f; k <= 1.81f; k += 0.05f)
            {
                def.SpawnStart = s0 * k;
                def.SpawnEnd = e0 * k;
                var avg = Run(0.5f);
                var nw = Run(0.15f);
                double firedAvg = avg.Count(r => r.Fired) / (double)seeds, firedNew = nw.Count(r => r.Fired) / (double)seeds;
                double complAvg = avg.Average(r => r.Complaints);
                line = $"k={k:0.00} avg fired {firedAvg * 100,3:0}% compl {complAvg:0.0} | new fired {firedNew * 100,3:0}%";
                chosen = k;
                if (def.Endless || (firedAvg <= 0.1 && complAvg <= 2.2 && firedNew <= 0.2)) break;
            }
            var a = Run(0.5f).Select(r => (double)r.Score).OrderBy(x => x).ToList();
            var n = Run(0.15f).Select(r => (double)r.Score).OrderBy(x => x).ToList();
            var pr = Run(0.85f).Select(r => (double)r.Score).OrderBy(x => x).ToList();
            double Pct(List<double> v, double q) => v[Math.Clamp((int)Math.Round(q * (v.Count - 1)), 0, v.Count - 1)];
            int one = Round(def.Id == "monday" ? Pct(n, 0.1) : Pct(n, 0.5));
            int two = Round(Math.Max(Pct(a, 0.6), one + 1500));
            int three = Round(Math.Max(Pct(pr, 0.6), two + 1500));
            Console.WriteLine($"{def.Id,-10} {line}  -> SpawnStart = {s0 * chosen:0.00}f, SpawnEnd = {e0 * chosen:0.00}f, Stars = {{ {one}, {two}, {three} }}" +
                              $"  (1* new {n.Count(x => x >= one) * 100 / n.Count}% avg {a.Count(x => x >= one) * 100 / a.Count}% | 2* avg {a.Count(x => x >= two) * 100 / a.Count}% | 3* pract {pr.Count(x => x >= three) * 100 / pr.Count}%)");
            def.SpawnStart = s0;
            def.SpawnEnd = e0;
        }
        return 0;
    }

    // What the complaints are about, for a modelled player of the given skill (mean per run).
    static int Causes(float skill, int seeds)
    {
        var names = Enum.GetNames(typeof(Outcome));
        foreach (var def in ShiftCatalog.All)
        {
            if (def.Endless) continue;
            var tally = new int[names.Length];
            int fired = 0;
            for (ulong s = 1; s <= (ulong)seeds; s++)
            {
                var sim = new ShiftSim(def, s * 7919);
                var bot = Bot.Human(s, skill);
                float dt = 1f / 30f;
                while (!sim.Ended && sim.Time < 900)
                {
                    bot.Tick(sim, dt, out _);
                    sim.Tick(dt);
                    foreach (var e in sim.Events) if (e.Type == Ev.Complaint) tally[e.Aux]++;
                    sim.Events.Clear();
                }
                if (sim.Fired) fired++;
            }
            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++) if (tally[i] > 0) parts.Add($"{names[i]} {tally[i] / (double)seeds:0.00}");
            Console.WriteLine($"{def.Id,-10} fired {fired * 100 / seeds,3}%  " + string.Join("  ", parts));
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
        int failures = 0, previewed = 0;
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
                        // the floor labels' forecast: the building after a full stop is what PreviewStop predicted
                        Building pred = sim.Car.Target.HasValue ? sim.PreviewStop(sim.Car.Target.Value, out _) : null;
                        int stops = sim.Stops;
                        sim.Tick(dt);
                        if (pred != null && sim.Stops != stops && !pred.Slots.SequenceEqual(sim.B.Slots))
                            throw new Exception($"stop {sim.Stops}: preview {string.Join(",", pred.Slots)} but got {string.Join(",", sim.B.Slots)}");
                        if (pred != null && sim.Stops != stops) previewed++;
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
        Console.WriteLine(failures == 0 ? $"fuzz OK ({previewed} stops matched the forecast preview)" : $"fuzz FAILURES {failures}");
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

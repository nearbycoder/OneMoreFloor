using System;
using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    /// <summary>
    /// A greedy operator used for balancing, the "every shift is beatable" tests and the in-game autopilot.
    /// It only uses the public commands a player has, with a reaction delay between actions.
    /// </summary>
    public sealed class Bot
    {
        public readonly float Reaction;
        public readonly float Noise;
        /// <summary>
        /// Human-model bots only: a multiplier on pointer travel time (Fitts' law, a = 0.1 s, b = 0.15 s/bit,
        /// with target sizes and distances measured on the 1080p layout), plus a perception delay before the
        /// first action at each stop. 0 = the original instant-pointer bots.
        /// </summary>
        public readonly float Motor;
        readonly Rng rng;
        float cooldown;
        int lastDropped = -1;
        bool wasOpen;

        public Bot(ulong seed, float reaction = 0.45f, float noise = 0.05f, float motor = 0f)
        {
            rng = new Rng(seed ^ 0xB07B07UL);
            Reaction = reaction;
            Noise = noise;
            Motor = motor;
        }

        // Fitts' law movement times for the common targets at 1080p (guest ~55 px from ~300 px away,
        // panel button ~70 px from ~700 px away), in seconds before the skill multiplier
        static float Fitts(float distance, float width) => 0.1f + 0.15f * (float)Math.Log(distance / width + 1.0, 2.0);
        // sending is a mix of panel buttons and clicking the (large) floor itself
        static readonly float MoveToGuest = Fitts(300f, 55f), MoveToButton = 0.5f * (Fitts(700f, 70f) + Fitts(400f, 100f));
        // a key press (Space, 1-9) with the hand already on the keyboard
        const float KeyPress = 0.2f;

        /// <summary>
        /// A model of a person with a mouse, skill 0 (first time) .. 1 (practised): slower decisions, pointer
        /// travel for every click, a beat to take in each new stop, and more misjudged routes at low skill.
        /// </summary>
        public static Bot Human(ulong seed, float skill)
        {
            skill = Math.Clamp(skill, 0f, 1f);
            return new Bot(seed, Lerp(1.25f, 0.5f, skill), Lerp(0.32f, 0.04f, skill), Lerp(1.3f, 0.8f, skill));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static Bot Novice(ulong seed) => new Bot(seed, 1.7f, 0.4f);
        public static Bot Sloppy(ulong seed) => new Bot(seed, 1.0f, 0.3f);
        public static Bot Decent(ulong seed) => new Bot(seed, 0.6f, 0.1f);
        public static Bot Strong(ulong seed) => new Bot(seed, 0.32f, 0f);

        public enum Action { None, Board, Send, Drop }

        /// <summary>Advances the bot's clock; returns what it decided (the command is already issued).</summary>
        public Action Tick(ShiftSim sim, float dt, out int arg)
        {
            arg = -1;
            if (sim.Ended) return Action.None;
            cooldown -= dt;
            bool open = sim.Car.IsOpen;
            // a person needs a moment to take in the new stop (and where everyone is) before acting
            if (open && !wasOpen && Motor > 0f) cooldown = Math.Max(cooldown, Reaction * 0.5f + Motor * MoveToGuest);
            wasOpen = open;
            if (cooldown > 0f || !open) return Action.None;

            var pick = ChooseBoarding(sim);
            // people let the whole queue in with Space when everyone there is welcome
            if (pick != null && Motor > 0f && WholeQueueWelcome(sim))
            {
                int before = sim.Car.Riders.Count;
                sim.BoardAll();
                if (sim.Car.Riders.Count > before)
                {
                    cooldown = Reaction * 0.35f + Motor * KeyPress;
                    arg = pick.Id;
                    return Action.Board;
                }
            }
            if (pick != null)
            {
                sim.Board(pick.Id);
                cooldown = Reaction * 0.35f + Motor * MoveToGuest;
                arg = pick.Id;
                return Action.Board;
            }

            int slot = ChooseSlot(sim);
            if (slot >= 0 && slot != sim.Car.DockedSlot && sim.SendTo(slot))
            {
                cooldown = Reaction + Motor * MoveToButton;
                arg = slot;
                return Action.Send;
            }

            // Stuck: a vampire can't get anywhere useful without passing sunlight (a kid aboard makes
            // every floor on the way a stop). Let the vampire off here and carry on.
            if (slot < 0 && sim.Car.Has(Kind.Vampire) && sim.Car.Has(Kind.Kid))
            {
                var v = FindRider(sim, Kind.Vampire);
                if (v != null && sim.DropHere(v.Id))
                {
                    lastDropped = v.Id;
                    cooldown = Reaction * 0.5f + Motor * MoveToGuest;
                    arg = v.Id;
                    return Action.Drop;
                }
            }
            cooldown = 0.25f;
            return Action.None;
        }

        public Passenger ChooseBoarding(ShiftSim sim)
        {
            Passenger best = null;
            float bestScore = 0f;
            var tycoon = FindRider(sim, Kind.Tycoon);
            foreach (var p in sim.Waiting[(int)sim.DockedFloor])
            {
                if (!sim.CanBoard(p)) continue;
                float s = Defs.Of(p.Kind).Fare * (1.5f - 0.5f * p.PatienceFrac);
                // don't break a tycoon's express with someone going elsewhere
                if (tycoon != null && !tycoon.ExpressBroken && p.Dest != tycoon.Dest) s *= 0.2f;
                if (p.Kind == Kind.Tycoon && sim.Car.Riders.Exists(r => r.Dest != p.Dest)) s *= 0.6f;
                // a vampire with an unsunned plant aboard (or vice versa) is trouble
                if (p.Kind == Kind.Vampire && sim.Car.Riders.Exists(r => r.Kind == Kind.Houseplant && !r.Sunned)) s *= 0.4f;
                if ((p.Kind == Kind.Vampire && sim.Car.Has(Kind.Kid)) || (p.Kind == Kind.Kid && sim.Car.Has(Kind.Vampire))) continue;
                if (p.Id == lastDropped) continue;
                if (s > bestScore) { bestScore = s; best = p; }
            }
            return best;
        }

        /// <summary>Would boarding everyone waiting here (in queue order, as Space does) be what this bot wants?</summary>
        bool WholeQueueWelcome(ShiftSim sim)
        {
            var queue = sim.Waiting[(int)sim.DockedFloor];
            int size = 0;
            bool vamp = sim.Car.Has(Kind.Vampire), kid = sim.Car.Has(Kind.Kid), mirror = sim.Car.Has(Kind.Mirror), tycoon = sim.Car.Has(Kind.Tycoon);
            foreach (var p in queue)
            {
                size += p.Size;
                if (p.Id == lastDropped) return false;
                if (p.Kind == Kind.Vampire) { if (kid || mirror) return false; vamp = true; }
                if (p.Kind == Kind.Kid) { if (vamp) return false; kid = true; }
                if (p.Kind == Kind.Mirror) { if (vamp) return false; mirror = true; }
                if (p.Kind == Kind.Tycoon || tycoon) return false;
            }
            return size <= sim.Car.Free;
        }

        static Passenger FindRider(ShiftSim sim, Kind k)
        {
            foreach (var r in sim.Car.Riders) if (r.Kind == k) return r;
            return null;
        }

        static float Value(Passenger p)
        {
            float fare = Defs.Of(p.Kind).Fare;
            if (p.Kind == Kind.Tycoon && p.ExpressBroken) return fare;
            return fare * (1f + p.PatienceFrac);
        }

        static float Urgency(Passenger p) { float u = 1f - p.PatienceFrac; return u * u; }

        public int ChooseSlot(ShiftSim sim)
        {
            var b = sim.B;
            var car = sim.Car;
            int here = car.DockedSlot;
            bool kid = car.Has(Kind.Kid);
            bool vamp = car.Has(Kind.Vampire);
            var tycoon = FindRider(sim, Kind.Tycoon);

            var scores = new List<(int slot, float score)>();
            for (int s = 0; s < b.Count; s++)
            {
                if (s == here) continue;
                var f = b.At(s);
                int dist = Math.Abs(s - here);
                float time = Tuning.DoorClose + Tuning.DoorOpen + 0.6f;
                if (kid) time += dist * Tuning.TravelTime(1) + (dist - 1) * (Tuning.QuickOpen + Tuning.QuickHold + Tuning.QuickClose);
                else time += Tuning.TravelTime(dist);

                // floors the doors will open on: the destination, plus every floor on the way with a kid aboard
                var opened = new List<FloorId> { f };
                if (kid)
                {
                    int dir = Math.Sign(s - here);
                    for (int m = here + dir; m != s; m += dir) opened.Add(b.At(m));
                }
                bool sunOnPath = false;
                foreach (var of in opened) if (Defs.Sunny(of)) sunOnPath = true;
                if (vamp && sunOnPath) continue;

                float gain = 0f;
                int freed = 0;
                foreach (var r in car.Riders)
                {
                    bool sunnedBy = r.Kind == Kind.Houseplant && !r.Sunned && sunOnPath;
                    if (opened.Contains(r.Dest) && (r.Kind != Kind.Houseplant || r.Sunned || sunnedBy))
                    {
                        gain += Value(r) * (1f + 2f * Urgency(r));
                        freed += r.Size;
                    }
                    else if (r.Kind == Kind.Houseplant && !r.Sunned && sunOnPath) gain += 0.6f * Value(r);
                    if (r.Fuming) freed += r.Size;
                }
                if (tycoon != null && !tycoon.ExpressBroken)
                {
                    bool firstStopIsTycoons = kid ? b.At(here + Math.Sign(s - here)) == tycoon.Dest : f == tycoon.Dest;
                    if (!firstStopIsTycoons) gain -= Defs.Of(Kind.Tycoon).Fare * tycoon.PatienceFrac * 1.2f;
                }

                int free = car.Free + freed;
                var queue = new List<Passenger>(sim.Waiting[(int)f]);
                queue.Sort((x, y) => x.Patience.CompareTo(y.Patience));
                foreach (var p in queue)
                {
                    if (p.Size > free) continue;
                    if (p.Kind == Kind.Vampire && car.Has(Kind.Mirror)) continue;
                    if (p.Kind == Kind.Mirror && car.Has(Kind.Vampire)) continue;
                    float u = Urgency(p);
                    if (p.Kind == Kind.Courier && b.Leaving[(int)p.Dest] >= 0 && b.Leaving[(int)p.Dest] <= 3) u = 1f;
                    if (p.Kind == Kind.Swimmer && b.Leaving[(int)FloorId.Ocean] >= 0 && b.Leaving[(int)FloorId.Ocean] <= 2) u = 1f;
                    if (p.Patience < time + 1.5f) continue; // won't make it
                    gain += Value(p) * 0.55f * (1f + 1.5f * u);
                    free -= p.Size;
                }
                if (gain <= 0f) continue;
                scores.Add((s, gain / (time + 0.5f)));
            }
            if (scores.Count == 0) return -1;
            scores.Sort((x, y) => y.score.CompareTo(x.score));
            if (Noise > 0f && rng.Chance(Noise))
                return scores[rng.Range(0, Math.Min(3, scores.Count))].slot;
            return scores[0].slot;
        }

        /// <summary>Plays a whole shift at a fixed timestep; returns the finished sim.</summary>
        public static ShiftSim PlayOut(ShiftDef def, ulong seed, Bot bot, float dt = 1f / 30f, float maxSeconds = 900f)
        {
            var sim = new ShiftSim(def, seed);
            float t = 0f;
            while (!sim.Ended && t < maxSeconds)
            {
                bot.Tick(sim, dt, out _);
                sim.Tick(dt);
                sim.Events.Clear();
                t += dt;
            }
            return sim;
        }
    }
}

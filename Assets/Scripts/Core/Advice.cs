namespace OneMoreFloor.Core
{
    /// <summary>
    /// What the time card says about a shift's complaints: each cause in plain words, and one tip for the
    /// biggest one, in the same voice as the coach.
    /// </summary>
    public static class Advice
    {
        /// <summary>The outcomes that count as complaints, in the order the time card lists them.</summary>
        public static readonly Outcome[] Causes = { Outcome.StormedOff, Outcome.Fumed, Outcome.Poofed, Outcome.SweptAway, Outcome.PackageLost };

        public static bool IsComplaint(Outcome o) => System.Array.IndexOf(Causes, o) >= 0;

        public static string Label(Outcome o, int n)
        {
            switch (o)
            {
                case Outcome.StormedOff: return $"{n} took the stairs";
                case Outcome.Fumed: return $"{n} fumed in the car";
                case Outcome.Poofed: return n == 1 ? "1 vampire met the sun" : $"{n} vampires met the sun";
                case Outcome.SweptAway: return n == 1 ? "1 swimmer swept out to sea" : $"{n} swimmers swept out to sea";
                case Outcome.PackageLost: return n == 1 ? "1 parcel left with its floor" : $"{n} parcels left with their floors";
                default: return "";
            }
        }

        public static string Tip(Outcome o)
        {
            switch (o)
            {
                case Outcome.StormedOff: return "Waiting guests give up first. Visit the busiest floors early, and let everyone in at once.";
                case Outcome.Fumed: return "Riders run out of patience too. Drop off the nearest first, and skip long detours with a full car.";
                case Outcome.Poofed: return "Doors opening on sunlight turn vampires into bats. The trip preview marks every sunny stop.";
                case Outcome.SweptAway: return "The Ocean only stays a few stops. Fetch swimmers before it leaves, then head for the Lobby.";
                case Outcome.PackageLost: return "A courier's floor is leaving. Watch its countdown and deliver before it reaches zero.";
                default: return "";
            }
        }

        /// <summary>"2 took the stairs · 1 vampire met the sun", or "" for a clean shift.</summary>
        public static string Summary(int[] by)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var o in Causes)
            {
                int n = by[(int)o];
                if (n <= 0) continue;
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(Label(o, n));
            }
            return sb.ToString();
        }

        /// <summary>The cause with the most complaints (the first listed wins a tie), or null for a clean shift.</summary>
        public static Outcome? Biggest(int[] by)
        {
            Outcome? best = null;
            int most = 0;
            foreach (var o in Causes)
                if (by[(int)o] > most) { most = by[(int)o]; best = o; }
            return best;
        }
    }
}

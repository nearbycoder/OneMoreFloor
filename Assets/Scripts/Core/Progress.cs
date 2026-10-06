namespace OneMoreFloor.Core
{
    /// <summary>
    /// Which shifts are open. A star on a shift opens the next one. So does the late pass: after
    /// <see cref="LatePassAttempts"/> shifts clocked out (finished or fired) without a star, The Management lets it
    /// slide and opens the next shift anyway. Overtime is the exception: it still needs a star on the Graveyard Shift.
    /// </summary>
    public static class Progress
    {
        public const int LatePassAttempts = 3;

        /// <param name="stars">Best stars per shift index.</param>
        /// <param name="plays">Shifts clocked out per shift index.</param>
        public static bool Unlocked(int shift, int[] stars, int[] plays)
        {
            if (shift <= 0) return true;
            int prev = shift - 1;
            if (stars[prev] >= 1) return true;
            if (shift >= ShiftCatalog.All.Count || ShiftCatalog.Get(shift).Endless) return false;
            return plays[prev] >= LatePassAttempts && Unlocked(prev, stars, plays);
        }

        /// <summary>True when the shift is open only because of the late pass (no star on the one before it).</summary>
        public static bool ByLatePass(int shift, int[] stars, int[] plays)
            => shift > 0 && stars[shift - 1] == 0 && Unlocked(shift, stars, plays);

        /// <summary>
        /// How many more tries on the shift before this one would open <paramref name="shift"/> by the late pass, or
        /// -1 when the late pass doesn't apply (already open, Overtime, or the shift before is still locked).
        /// </summary>
        public static int TriesToLatePass(int shift, int[] stars, int[] plays)
        {
            if (shift <= 0 || Unlocked(shift, stars, plays)) return -1;
            if (shift >= ShiftCatalog.All.Count || ShiftCatalog.Get(shift).Endless) return -1;
            if (!Unlocked(shift - 1, stars, plays)) return -1;
            return LatePassAttempts - plays[shift - 1];
        }

        /// <summary>
        /// The shift the title screen offers: the first open shift without a star whose next shift isn't open yet
        /// (a late-passed shift doesn't keep pulling the player back), else the last open shift.
        /// </summary>
        public static int NextShift(int[] stars, int[] plays)
        {
            int n = ShiftCatalog.All.Count;
            for (int i = 0; i < n; i++)
                if (Unlocked(i, stars, plays) && stars[i] == 0 && !(i + 1 < n && Unlocked(i + 1, stars, plays))) return i;
            for (int i = n - 1; i >= 0; i--)
                if (Unlocked(i, stars, plays)) return i;
            return 0;
        }
    }
}

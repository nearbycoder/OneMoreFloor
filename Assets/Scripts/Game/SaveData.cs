using System;
using System.IO;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>Progress and settings, saved as JSON in persistentDataPath/save.json.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 1;
        public int[] Best = new int[16];
        public int[] Stars = new int[16];
        public int[] Plays = new int[16];
        /// <summary>Relaxed clears: the 1-star score reached in a Relaxed shift (opens the next shift like a star).</summary>
        public bool[] RelaxedClear = new bool[16];
        /// <summary>Standard shifts that ended in "You're fired!", per shift.</summary>
        public int[] FiredCount = new int[16];
        public bool EndingSeen;
        public string[] SeenHints = new string[0];
        public float Master = 1f, Music = 0.8f, Sfx = 0.9f;
        public bool ScreenShake = true;
        public bool Fullscreen;
        public bool ShowForecast = true;
        /// <summary>No camera drift, push-ins or shake, floors settle without overshoot, and the UI holds still.</summary>
        public bool ReducedMotion;
        /// <summary>Relaxed shifts: more patience, no firing, stars and bests not saved (an assist).</summary>
        public bool Relaxed;
        /// <summary>Camera zoom during play (0 = whole tower, 1 = close-up following the car).</summary>
        public float Zoom;

        static string PathName => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
        static SaveData current;

        public static SaveData Current => current ?? (current = Load());

        /// <summary>Use a throwaway save (autopilot/tests) so a player's progress is never touched.</summary>
        public static bool Ephemeral;

        static SaveData Load()
        {
            try
            {
                if (!Ephemeral && File.Exists(PathName))
                {
                    var s = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathName));
                    if (s != null)
                    {
                        if (s.Best == null || s.Best.Length < 16) Array.Resize(ref s.Best, 16);
                        if (s.Stars == null || s.Stars.Length < 16) Array.Resize(ref s.Stars, 16);
                        if (s.Plays == null || s.Plays.Length < 16) Array.Resize(ref s.Plays, 16);
                        if (s.RelaxedClear == null || s.RelaxedClear.Length < 16) Array.Resize(ref s.RelaxedClear, 16);
                        if (s.FiredCount == null || s.FiredCount.Length < 16) Array.Resize(ref s.FiredCount, 16);
                        if (s.SeenHints == null) s.SeenHints = new string[0];
                        return s;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Save] could not read save: " + ex.Message);
            }
            return new SaveData();
        }

        /// <summary>
        /// Writes persistentDataPath/save.json: the player's real save, shared by the editor and the built game. Tests
        /// and automation must set <see cref="Ephemeral"/> first (Record and MarkHint save too).
        /// </summary>
        public void Save()
        {
            if (Ephemeral) return;
            try { File.WriteAllText(PathName, JsonUtility.ToJson(this, true)); }
            catch (Exception ex) { Debug.LogWarning("[Save] could not write save: " + ex.Message); }
        }

        public bool Unlocked(int shift) => Progress.Unlocked(shift, Stars, Plays, RelaxedClear);

        /// <summary>Open only thanks to the late pass (three tries without a star on the shift before).</summary>
        public bool LatePassed(int shift) => Progress.ByLatePass(shift, Stars, Plays, RelaxedClear);

        public int NextShift() => Progress.NextShift(Stars, Plays, RelaxedClear);

        public int TotalStars()
        {
            int n = 0;
            foreach (var s in Stars) n += s;
            return n;
        }

        /// <summary>
        /// Records a finished shift. Returns true when it's a new best score. A relaxed shift counts as a try and can
        /// earn a relaxed clear, but never saves stars or a best score.
        /// </summary>
        public bool Record(int shift, int score, int stars, bool relaxed = false, bool fired = false)
        {
            Plays[shift]++;
            if (relaxed)
            {
                if (stars >= 1) RelaxedClear[shift] = true;
                Save();
                return false;
            }
            if (fired) FiredCount[shift]++;
            bool best = score > Best[shift];
            if (best) Best[shift] = score;
            if (stars > Stars[shift]) Stars[shift] = stars;
            Save();
            return best;
        }

        public bool HintSeen(string id) => Array.IndexOf(SeenHints, id) >= 0;

        public void MarkHint(string id)
        {
            if (HintSeen(id)) return;
            Array.Resize(ref SeenHints, SeenHints.Length + 1);
            SeenHints[SeenHints.Length - 1] = id;
            Save();
        }
    }
}

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
        public bool EndingSeen;
        public string[] SeenHints = new string[0];
        public float Master = 1f, Music = 0.8f, Sfx = 0.9f;
        public bool ScreenShake = true;
        public bool Fullscreen;
        public bool ShowForecast = true;

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

        public void Save()
        {
            if (Ephemeral) return;
            try { File.WriteAllText(PathName, JsonUtility.ToJson(this, true)); }
            catch (Exception ex) { Debug.LogWarning("[Save] could not write save: " + ex.Message); }
        }

        public bool Unlocked(int shift)
        {
            if (shift <= 0) return true;
            return Stars[shift - 1] >= 1;
        }

        public int NextShift()
        {
            var all = ShiftCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (Unlocked(i) && Stars[i] == 0) return i;
            for (int i = all.Count - 1; i >= 0; i--)
                if (Unlocked(i)) return i;
            return 0;
        }

        public int TotalStars()
        {
            int n = 0;
            foreach (var s in Stars) n += s;
            return n;
        }

        /// <summary>Records a finished shift. Returns true when it's a new best score.</summary>
        public bool Record(int shift, int score, int stars)
        {
            Plays[shift]++;
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

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
        /// <summary>MUTE IN BACKGROUND: the sound fades out while the window doesn't have focus.</summary>
        public bool MuteInBackground = true;
        public bool ScreenShake = true;
        public bool Fullscreen;
        public bool ShowForecast = true;
        /// <summary>No camera drift, push-ins or shake, floors settle without overshoot, and the UI holds still.</summary>
        public bool ReducedMotion;
        /// <summary>LARGER TEXT: small text is drawn with capitals at least 12 px high on any screen (see <see cref="TextFloor"/>).</summary>
        public bool LargeText;
        /// <summary>GRAPHICS FIDELITY: 0 LOW, 1 MEDIUM, 2 HIGH (the default), 3 ULTRA (see <see cref="GraphicsQuality"/>).</summary>
        public int Fidelity = GraphicsQuality.Default;
        /// <summary>The GRAPHICS setting before GRAPHICS FIDELITY (0 HIGH, 1 BALANCED, 2 LOW): read from older saves, and
        /// still written (ULTRA as HIGH) so an older build keeps the nearest level.</summary>
        public int Graphics;
        /// <summary>Daily Overtime: the date of the runs below ("2026-10-06"), today's best and tries, and the best day.</summary>
        public string DailyDate = "";
        public int DailyBest, DailyPlays, DailyRecord;
        public string DailyRecordDate = "";
        /// <summary>Relaxed shifts: more patience, no firing, stars and bests not saved (an assist).</summary>
        public bool Relaxed;
        /// <summary>Camera zoom during play (0 = whole tower, 1 = close-up following the car).</summary>
        public float Zoom;

        public const string FileName = "save.json";
        static SaveData current;

        public static SaveData Current => current ?? (current = Load());

        /// <summary>Use a throwaway save (autopilot/tests) so a player's progress is never touched.</summary>
        public static bool Ephemeral;

        static SaveData Load() => Ephemeral ? new SaveData() : LoadFrom(Application.persistentDataPath);

        /// <summary>
        /// Writes persistentDataPath/save.json: the player's real save, shared by the editor and the built game. Tests
        /// and automation must set <see cref="Ephemeral"/> first (Record and MarkHint save too), or call
        /// <see cref="SaveTo"/> with a throwaway folder.
        /// </summary>
        public void Save()
        {
            if (Ephemeral) return;
            SaveTo(Application.persistentDataPath);
            // in a browser the folder is in memory until it's pushed to IndexedDB (the page doesn't sync on its own)
            Web.SyncFileSystem();
        }

        /// <summary>
        /// Reads <paramref name="dir"/>/save.json, falling back to save.json.bak when the main file is missing or
        /// can't be read. An unreadable save.json is renamed to save.unreadable-&lt;time&gt;.json first, so the next
        /// save neither overwrites it nor pushes the good backup out. Values are clamped to sane ranges.
        /// </summary>
        public static SaveData LoadFrom(string dir)
        {
            string path = Path.Combine(dir, FileName), bak = path + ".bak";
            var s = TryRead(path, out bool unreadable);
            if (unreadable)
            {
                string aside = Path.Combine(dir, "save.unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                try
                {
                    File.Move(path, aside);
                    Debug.LogWarning("[Save] save.json couldn't be read; kept it as " + Path.GetFileName(aside));
                }
                catch (Exception ex) { Debug.LogWarning("[Save] couldn't move the unreadable save aside: " + ex.Message); }
            }
            if (s == null)
            {
                s = TryRead(bak, out _);
                if (s != null) Debug.LogWarning("[Save] loaded the backup, save.json.bak");
            }
            return s != null ? s.Sanitized() : new SaveData();
        }

        /// <summary>The parsed file, or null. <paramref name="unreadable"/> is set when the file exists but isn't a save.</summary>
        static SaveData TryRead(string path, out bool unreadable)
        {
            unreadable = false;
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path);
                var s = string.IsNullOrWhiteSpace(text) ? null : JsonUtility.FromJson<SaveData>(text);
                if (s == null) unreadable = true;
                // a save from before GRAPHICS FIDELITY keeps its GRAPHICS level
                else if (!text.Contains("\"Fidelity\"")) s.Fidelity = GraphicsQuality.FromLegacy(s.Graphics);
                return s;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Save] could not read " + Path.GetFileName(path) + ": " + ex.Message);
                unreadable = true;
                return null;
            }
        }

        /// <summary>
        /// Writes <paramref name="dir"/>/save.json without ever leaving a half-written file behind: the JSON goes to
        /// save.json.tmp (flushed to disk), which then replaces save.json in one step, keeping the old one as save.json.bak.
        /// </summary>
        public void SaveTo(string dir)
        {
            string path = Path.Combine(dir, FileName), tmp = path + ".tmp", bak = path + ".bak";
            try
            {
                Graphics = GraphicsQuality.ToLegacy(Fidelity);
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(this, true));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                if (!File.Exists(path)) { File.Move(tmp, path); return; }
                if (Web.IsWeb)
                {
                    // the browser's file system has no atomic replace (IndexedDB gets the result in one sync anyway)
                    File.Copy(path, bak, true);
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                    return;
                }
                try { File.Replace(tmp, path, bak); }
                catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException)
                {
                    // no atomic replace here: keep a backup first, then swap (a crash in between leaves .bak and .tmp)
                    File.Copy(path, bak, true);
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Save] could not write save: " + ex.Message); }
        }

        /// <summary>Fills missing arrays and clamps every value to its range (a hand-edited or damaged save).</summary>
        SaveData Sanitized()
        {
            if (Best == null || Best.Length < 16) Array.Resize(ref Best, 16);
            if (Stars == null || Stars.Length < 16) Array.Resize(ref Stars, 16);
            if (Plays == null || Plays.Length < 16) Array.Resize(ref Plays, 16);
            if (RelaxedClear == null || RelaxedClear.Length < 16) Array.Resize(ref RelaxedClear, 16);
            if (FiredCount == null || FiredCount.Length < 16) Array.Resize(ref FiredCount, 16);
            if (SeenHints == null) SeenHints = new string[0];
            if (DailyDate == null) DailyDate = "";
            if (DailyRecordDate == null) DailyRecordDate = "";
            for (int i = 0; i < Stars.Length; i++) Stars[i] = Mathf.Clamp(Stars[i], 0, 3);
            for (int i = 0; i < Best.Length; i++) Best[i] = Mathf.Max(0, Best[i]);
            for (int i = 0; i < Plays.Length; i++) Plays[i] = Mathf.Max(0, Plays[i]);
            for (int i = 0; i < FiredCount.Length; i++) FiredCount[i] = Mathf.Max(0, FiredCount[i]);
            DailyBest = Mathf.Max(0, DailyBest);
            DailyPlays = Mathf.Max(0, DailyPlays);
            DailyRecord = Mathf.Max(0, DailyRecord);
            Master = Unit(Master, 1f);
            Music = Unit(Music, 0.8f);
            Sfx = Unit(Sfx, 0.9f);
            Zoom = Unit(Zoom, 0f);
            if (Fidelity < GraphicsQuality.Low || Fidelity > GraphicsQuality.Ultra) Fidelity = GraphicsQuality.Default;
            Graphics = GraphicsQuality.ToLegacy(Fidelity);
            return this;
        }

        static float Unit(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp01(v);

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

        /// <summary>Records a daily Overtime run on <paramref name="date"/>. Returns true for a new best today.</summary>
        public bool RecordDaily(string date, int score)
        {
            if (DailyDate != date) { DailyDate = date; DailyBest = 0; DailyPlays = 0; }
            DailyPlays++;
            bool best = score > DailyBest;
            if (best) DailyBest = score;
            if (score > DailyRecord) { DailyRecord = score; DailyRecordDate = date; }
            Save();
            return best;
        }

        /// <summary>Today's best, or 0 when the stored day isn't <paramref name="date"/>.</summary>
        public int DailyBestOn(string date) => DailyDate == date ? DailyBest : 0;

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

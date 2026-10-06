using System;
using System.IO;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// A local playtest log: one JSON line per shift played (finished, restarted or quit) appended to
    /// persistentDataPath/playtests.jsonl. Nothing leaves the machine. It records the result, what the
    /// complaints were about, which input device was used, and how quickly the player acted: the delay from the
    /// doors opening to their first action at a stop, and the gap between consecutive actions. Those are the
    /// numbers the balance model (Bot.Human) assumes, so Tools/playtest_report.py can check the model against
    /// real people and suggest new star thresholds.
    /// </summary>
    public sealed class Telemetry
    {
        [Serializable]
        public sealed class Record
        {
            public int version = 1;
            public string when, shift, end, build;
            public ulong seed;
            public int score, stars, delivered, complaints, stops;
            public bool fired;
            public float seconds;
            public int stormedOff, fumed, poofed, packageLost, sweptAway;
            public int sends, boards, boardAlls, drops, refused;
            public int mouseActions, padActions, keyActions;
            public float firstActionMean, actionGapMean;
            public int firstActionSamples, actionGapSamples;
            public float zoomMean;
            public int plays;
            public bool relaxed;
        }

        public static bool Enabled = true;
        static string PathName => System.IO.Path.Combine(Application.persistentDataPath, "playtests.jsonl");

        Record r;
        ShiftSim sim;
        bool wasOpen;
        float openedAt = -1f, lastActionAt = -1f, zoomSum;
        int zoomFrames;
        bool actedThisStop;

        public void Begin(ShiftSim s, ulong seed)
        {
            sim = s;
            r = new Record { shift = s.Def.Id, seed = seed, when = DateTime.Now.ToString("s"), build = Application.version };
            wasOpen = false;
            openedAt = lastActionAt = -1f;
            zoomSum = 0f;
            zoomFrames = 0;
        }

        /// <summary>Called every frame while the shift runs (player-controlled shifts only).</summary>
        public void Tick(float zoom)
        {
            if (r == null || sim == null || sim.Ended) return;
            bool open = sim.Car.IsOpen;
            if (open && !wasOpen) { openedAt = sim.Time; actedThisStop = false; }
            wasOpen = open;
            zoomSum += zoom;
            zoomFrames++;
        }

        /// <summary>A player command (not the bot's): kind is send/board/boardall/drop.</summary>
        public void Action(string kind)
        {
            if (r == null || sim == null || sim.Ended) return;
            switch (kind)
            {
                case "send": r.sends++; break;
                case "board": r.boards++; break;
                case "boardall": r.boardAlls++; break;
                case "drop": r.drops++; break;
            }
            if (Controls.Pad) r.padActions++;
            else if (Controls.KeyNav || kind == "boardall" && !Controls.Pad) r.keyActions++;
            else r.mouseActions++;
            float now = sim.Time;
            if (sim.Car.IsOpen && openedAt >= 0f && !actedThisStop)
            {
                actedThisStop = true;
                r.firstActionMean += now - openedAt;
                r.firstActionSamples++;
            }
            if (lastActionAt >= 0f && now - lastActionAt < 6f)
            {
                r.actionGapMean += now - lastActionAt;
                r.actionGapSamples++;
            }
            lastActionAt = now;
        }

        public void SimEvent(SimEvent e)
        {
            if (r == null) return;
            if (e.Type == Ev.BoardRefused) r.refused++;
            if (e.Type != Ev.Complaint) return;
            switch ((Outcome)e.Aux)
            {
                case Outcome.StormedOff: r.stormedOff++; break;
                case Outcome.Fumed: r.fumed++; break;
                case Outcome.Poofed: r.poofed++; break;
                case Outcome.PackageLost: r.packageLost++; break;
                case Outcome.SweptAway: r.sweptAway++; break;
            }
        }

        /// <summary>Write the line. end = "finished" | "restarted" | "quit".</summary>
        public void Finish(string end)
        {
            if (r == null || sim == null) return;
            var rec = r;
            r = null;
            if (!Enabled || SaveData.Ephemeral) return;
            if (end != "finished" && sim.Time < 5f) return;   // backed straight out: not a play
            rec.end = end;
            rec.score = sim.Score;
            rec.stars = sim.StarCount;
            rec.delivered = sim.DeliveredCount;
            rec.complaints = sim.Complaints;
            rec.stops = sim.Stops;
            rec.fired = sim.Fired;
            rec.seconds = sim.Time;
            if (rec.firstActionSamples > 0) rec.firstActionMean /= rec.firstActionSamples;
            if (rec.actionGapSamples > 0) rec.actionGapMean /= rec.actionGapSamples;
            rec.zoomMean = zoomFrames > 0 ? zoomSum / zoomFrames : 0f;
            rec.plays = SaveData.Current.Plays[sim.Def.Index];
            rec.relaxed = sim.Relaxed;
            try { File.AppendAllText(PathName, JsonUtility.ToJson(rec) + "\n"); }
            catch (Exception ex) { Debug.LogWarning("[Telemetry] could not write playtest log: " + ex.Message); }
        }
    }
}

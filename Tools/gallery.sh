#!/usr/bin/env bash
# Capture every menu screen in one play session (needs Tools/editor.sh running).
# Uses a throwaway save with some sample progress, so the player's real save is never touched.
#   Tools/gallery.sh <out dir>
set -euo pipefail
PP="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:?out dir}"; mkdir -p "$OUT"
G='var g=OneMoreFloor.GameRoot.Instance;'
SAVE='OneMoreFloor.SaveData.Ephemeral=true; typeof(OneMoreFloor.SaveData).GetField("current",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,null);
var s=OneMoreFloor.SaveData.Current; int[] st={3,2,1}; int[] be={3420,2610,1880}; for(int i=0;i<3;i++){s.Stars[i]=st[i]; s.Best[i]=be[i];}'
"$PP/Tools/shot.sh" "$OUT/title.png" "$SAVE $G g.ShowTitle();" 3
u() { unity command eval --project-path "$PP" -- --code "$G $1 return \"ok\";" --timeout 30000 >/dev/null; }
cap() { unity command eval --project-path "$PP" -- --code "return OneMoreFloor.Shots.Capture(\"$OUT/$1.png\", 1920, 1080);" --timeout 30000 | tail -1; }
u 'g.ShowRoster();'; sleep 1.5; cap roster
u 'g.ShowIntro(3);'; sleep 1.5; cap intro
u 'g.BeginShift(3); g.Runner.AutoBot=OneMoreFloor.Core.Bot.Strong(5); g.Runner.TimeScale=4f;'; sleep 6; u 'g.Runner.AutoBot=null; g.Runner.TimeScale=1f;'; sleep 1; cap hud
u 'var r=g.Runner; r.InputEnabled=false; int pid=-1; foreach(var p in r.Sim.Car.Riders) pid=p.Id; if(pid<0) foreach(var w in r.Sim.Waiting) if(w.Count>0){pid=w[0].Id;break;} typeof(OneMoreFloor.ShiftRunner).GetProperty("HoverPid").SetValue(r,pid);'; sleep 0.5; cap tooltip
u 'g.Runner.InputEnabled=true;'
u 'g.Pause();'; sleep 1.5; cap pause
u 'g.ShowSettings((OneMoreFloor.UiScreen)typeof(OneMoreFloor.GameRoot).GetField("pause",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(g));'; sleep 1.5; cap settings
u 'g.Resume(); g.Runner.AutoBot=OneMoreFloor.Core.Bot.Strong(5); g.Runner.FastForward(400f);'; sleep 8; cap results

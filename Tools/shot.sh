#!/usr/bin/env bash
# Headless iteration loop against a resident batch-mode editor (see Tools/editor.sh):
# refresh + recompile, enter play mode, optionally run C# (eval) to set up a moment, then capture.
#   Tools/shot.sh <out.png> [setup C#] [wait seconds]
# Example: Tools/shot.sh /tmp/omf/a.png 'OneMoreFloor.GameRoot.Instance.StartShift(3,1);' 4
set -euo pipefail
PP="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:?out path}"; SETUP="${2:-}"; WAIT="${3:-3}"
u() { local c="$1"; shift; unity command "$c" --project-path "$PP" "$@"; }
ready() { for i in $(seq 1 90); do u eval 'return 1;' >/dev/null 2>&1 && return 0; sleep 1; done; echo "editor not responding" >&2; return 1; }
ready
u editor_stop >/dev/null 2>&1 || true
ready
u eval 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null
sleep 1; ready
r=$(unity recompile --project-path "$PP" --format json 2>&1 || true)
if echo "$r" | grep -q '"failed": true'; then echo "$r" | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; [print(e.get('file'),e.get('line'),e.get('message')) for e in d['errors']]"; exit 1; fi
ready
u eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity"); return "ok";' >/dev/null
u eval 'UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "OMF"); return "ok";' >/dev/null
u editor_play >/dev/null
sleep 2; ready
if [ -n "$SETUP" ]; then u eval -- --code "$SETUP return \"ok\";" --timeout 30000 | tail -1; fi
sleep "$WAIT"
u eval -- --code "return OneMoreFloor.Shots.Capture(\"$OUT\", 1920, 1080);" --timeout 30000 | tail -1
unity command console --project-path "$PP" --format json -- --tail 30 --level error | python3 -c "
import json,sys
txt=sys.stdin.read()
i=txt.find('{')
d=json.loads(txt[i:]) if i>=0 else {}
d=d.get('data',{}).get('result',d.get('data',{}))
if isinstance(d,str): d=json.loads(d)
for e in d.get('entries',[]): print('ERR', e['message'][:300]); print('   ', e.get('stackTrace','').split(chr(10))[0][:200])
" || true

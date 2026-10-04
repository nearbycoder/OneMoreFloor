#!/usr/bin/env bash
# Hit-test every visible button, slider and toggle on each menu: a pointer at the control's centre must land on that control.
# Needs Tools/editor.sh running and a play session (run Tools/gallery.sh first, or Tools/shot.sh).
#   Tools/uicheck.sh
set -euo pipefail
PP="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHECK='var es=UnityEngine.EventSystems.EventSystem.current; var cam=g.UiCam; var sb=new System.Text.StringBuilder(); int ok=0;
var targets=new System.Collections.Generic.List<UnityEngine.MonoBehaviour>();
targets.AddRange(UnityEngine.Object.FindObjectsByType<OneMoreFloor.UiButton>(UnityEngine.FindObjectsSortMode.None));
targets.AddRange(UnityEngine.Object.FindObjectsByType<OneMoreFloor.UiSlider>(UnityEngine.FindObjectsSortMode.None));
targets.AddRange(UnityEngine.Object.FindObjectsByType<OneMoreFloor.UiToggle>(UnityEngine.FindObjectsSortMode.None));
foreach(var b in targets){ if(!b.isActiveAndEnabled) continue; var cg=b.GetComponentInParent<UnityEngine.CanvasGroup>(); if(cg!=null && (!cg.blocksRaycasts || cg.alpha<0.5f)) continue;
 var rt=(UnityEngine.RectTransform)b.transform; var sp=UnityEngine.RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
 var pe=new UnityEngine.EventSystems.PointerEventData(es){position=sp}; var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(); es.RaycastAll(pe,hits);
 var top=hits.Count>0?hits[0].gameObject:null; bool good=top!=null && top.transform.IsChildOf(b.transform);
 if(good) ok++; else sb.Append("MISS "+b.GetType().Name+" "+b.name+" hit="+(top?top.name:"none")+"\n"); }
return ok+" ok\n"+sb;'
u() { unity command eval --project-path "$PP" -- --code "var g=OneMoreFloor.GameRoot.Instance; $1" --timeout 30000 | tail -1 | python3 -c "import json,sys; l=sys.stdin.read().split('\t'); print(json.loads(l[2]).get('result') if len(l)>2 else l)"; }
step() { unity command eval --project-path "$PP" -- --code "var g=OneMoreFloor.GameRoot.Instance; $1 return \"ok\";" --timeout 30000 >/dev/null; sleep 1.2; echo "== $2"; u "$CHECK"; }
step 'OneMoreFloor.SaveData.Ephemeral=true; typeof(OneMoreFloor.SaveData).GetField("current",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,null); g.ShowTitle();' title
step 'g.ShowRoster();' roster
step 'g.ShowIntro(0);' intro
step 'g.BeginShift(0); g.Pause();' pause
step 'g.ShowSettings((OneMoreFloor.UiScreen)typeof(OneMoreFloor.GameRoot).GetField("pause",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(g));' settings
step 'g.Resume(); g.Runner.AutoBot=OneMoreFloor.Core.Bot.Strong(5); g.Runner.FastForward(400f);' results
sleep 3; echo "== results (settled)"; u "$CHECK"

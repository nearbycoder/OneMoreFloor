#!/usr/bin/env bash
# Resident headless editor for fast iteration (drive it with `unity command`, see Tools/shot.sh).
#   Tools/editor.sh start | stop | status
set -euo pipefail
PP="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
case "${1:-status}" in
  start)
    export LD_LIBRARY_PATH="$PP/Tools/.libs${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
    mkdir -p "$PP/Logs"
    nohup "$UNITY" -batchmode -projectPath "$PP" -logFile "$PP/Logs/serve.log" >/dev/null 2>&1 &
    echo $! > "$PP/Logs/serve.pid"
    until unity command eval --project-path "$PP" 'return 1;' >/dev/null 2>&1; do sleep 2; done
    echo "editor ready (pid $(cat "$PP/Logs/serve.pid"))"
    ;;
  stop)
    unity command eval --project-path "$PP" 'UnityEditor.EditorApplication.Exit(0); return 0;' >/dev/null 2>&1 || true
    pid="$(cat "$PP/Logs/serve.pid" 2>/dev/null || true)"
    for i in $(seq 1 30); do [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null || break; sleep 1; done
    echo "editor stopped"
    ;;
  status)
    unity command eval --project-path "$PP" 'return UnityEngine.Application.unityVersion;' 2>&1 | tail -1
    ;;
esac

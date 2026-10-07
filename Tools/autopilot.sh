#!/usr/bin/env bash
# Self-test of the built game: walks the menus and plays every shift with the bot, saving
# screenshots to ${1:-Logs/autopilot}. Prints PASS/FAIL lines; exit code 0 on success.
#   Tools/autopilot.sh [outdir] [shiftIndex]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Logs/autopilot}"
# the player runs from its own folder, so a relative -logFile would land under Builds/Linux
OUT="$(realpath -m "$OUT")"
rm -rf "$OUT"; mkdir -p "$OUT"
extra=()
[ -n "${2:-}" ] && extra=(-omfAutopilotShift "$2")
timeout 1200 "$ROOT/Tools/play.sh" -screen-width 1920 -screen-height 1080 -logFile "$OUT/player.log" -omfAutopilot "$OUT" "${extra[@]}" > /dev/null 2>&1 || true
grep -E "\[AutoPilot\] (PASS|FAIL|done|perf)" "$OUT/player.log" || { echo "no autopilot output (see $OUT/player.log)"; exit 1; }
grep -q "\[AutoPilot\] done: PASS" "$OUT/player.log"

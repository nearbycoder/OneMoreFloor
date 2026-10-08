#!/usr/bin/env bash
# Self-test of the built game: walks the menus and plays every shift with the bot, saving
# screenshots to ${1:-Logs/autopilot}. Prints PASS/FAIL lines; exit code 0 on success.
#   Tools/autopilot.sh [outdir] [shiftIndex|pad|ui]   (the full run takes about 15 minutes)
# It runs in a private nested KWin (Tools/nested.sh) when KWin is available, so no window opens on the real
# desktop. OMF_NESTED=0 runs it on the real desktop instead. OMF_SIZE=WxH sets the window (default 1920x1080).
# OMF_LARGE_TEXT=1 plays it with the LARGER TEXT setting on.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Logs/autopilot}"
# the player runs from its own folder, so a relative -logFile would land under Builds/Linux
OUT="$(realpath -m "$OUT")"
rm -rf "$OUT"; mkdir -p "$OUT"
extra=()
[ -n "${2:-}" ] && extra=(-omfAutopilotShift "$2")
[ "${OMF_LARGE_TEXT:-0}" = 1 ] && extra+=(-omfLargeText)
SIZE="${OMF_SIZE:-1920x1080}"; W="${SIZE%x*}"; H="${SIZE#*x}"
args=(-screen-width "$W" -screen-height "$H" -logFile "$OUT/player.log" -omfAutopilot "$OUT" "${extra[@]}")
if [ "${OMF_NESTED:-1}" != 0 ] && command -v kwin_wayland > /dev/null && command -v dbus-run-session > /dev/null; then
  # the nested desktop leaves room for the window's title bar
  "$ROOT/Tools/nested.sh" --size "$((W + 100))x$((H + 100))" --timeout 2400 "${args[@]}" > "$OUT/nested.log" 2>&1 || true
else
  timeout 2400 "$ROOT/Tools/play.sh" "${args[@]}" > /dev/null 2>&1 || true
fi
grep -E "\[AutoPilot\] (PASS|FAIL|done|perf)" "$OUT/player.log" || { echo "no autopilot output (see $OUT/player.log)"; exit 1; }
grep -q "\[AutoPilot\] done: PASS" "$OUT/player.log"

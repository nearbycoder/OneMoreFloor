#!/usr/bin/env bash
# Runs the built game (Tools/play.sh, so any self-test) inside a private nested KWin: its own Wayland socket,
# D-Bus session and scratch config folder, all closed afterwards. The window can't appear on the real desktop
# or go fullscreen there, the real pointer and keyboard can't reach it, and it isn't throttled for being covered.
# Unity's prefs land in the scratch folder too, never in ~/.config/unity3d.
#
#   Tools/nested.sh [--size WxH] [--timeout SECONDS] [--keep] [play.sh args...]
#   Tools/nested.sh --size 1380x900 -screen-width 1280 -screen-height 800 -omfAutopilot "$PWD/Logs/ap" -omfAutopilotShift ui
#
# --size is the nested desktop (default 2020x1180, room for a 1920x1080 window and its title bar). --timeout stops
# the game after that many seconds (default: none). --keep leaves the scratch folder (Logs/nested-<pid>/, with
# KWin's log). The exit code is the game's. Only processes started here are stopped, by PID.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SIZE=2020x1180; KEEP=0; LIMIT=0
while [ $# -gt 0 ]; do
  case "$1" in
    --size) SIZE="$2"; shift 2 ;;
    --timeout) LIMIT="$2"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    *) break ;;
  esac
done
for tool in kwin_wayland dbus-run-session; do
  command -v "$tool" > /dev/null || { echo "nested.sh: needs $tool (KDE's KWin and D-Bus)" >&2; exit 2; }
done
[ -n "${XDG_RUNTIME_DIR:-}" ] || { echo "nested.sh: XDG_RUNTIME_DIR isn't set" >&2; exit 2; }

SCRATCH="$ROOT/Logs/nested-$$"
rm -rf "$SCRATCH"; mkdir -p "$SCRATCH/config"
export OMF_NESTED_SOCK="omf-nested-$$" OMF_NESTED_DIR="$SCRATCH" OMF_ROOT="$ROOT" OMF_LIMIT="$LIMIT"
export OMF_NESTED_W="${SIZE%x*}" OMF_NESTED_H="${SIZE#*x}"
# Everything inside (KWin's settings, Unity's prefs) reads and writes the scratch folder.
export XDG_CONFIG_HOME="$SCRATCH/config"

set +e
env -u DISPLAY dbus-run-session -- bash -c '
  kwin_wayland --virtual --socket "$OMF_NESTED_SOCK" --width "$OMF_NESTED_W" --height "$OMF_NESTED_H" \
    --no-lockscreen --no-global-shortcuts > "$OMF_NESTED_DIR/kwin.log" 2>&1 & KW=$!
  trap "kill \$KW 2>/dev/null; wait \$KW 2>/dev/null" EXIT
  for i in $(seq 40); do [ -S "$XDG_RUNTIME_DIR/$OMF_NESTED_SOCK" ] && break; sleep 0.25; done
  [ -S "$XDG_RUNTIME_DIR/$OMF_NESTED_SOCK" ] || { echo "nested.sh: KWin did not start (see $OMF_NESTED_DIR/kwin.log)" >&2; exit 3; }
  echo "[nested] kwin $KW on $OMF_NESTED_SOCK (${OMF_NESTED_W}x$OMF_NESTED_H)"
  limit=(); [ "$OMF_LIMIT" -gt 0 ] && limit=(timeout -k 10 "$OMF_LIMIT")
  WAYLAND_DISPLAY="$OMF_NESTED_SOCK" "${limit[@]}" "$OMF_ROOT/Tools/play.sh" "$@" & G=$!
  echo "[nested] game $G"
  wait $G; code=$?
  echo "[nested] game exited $code"
  exit $code
' nested "$@"
code=$?
set -e
# Nothing started here may outlive it.
left=$(pgrep -f "$OMF_NESTED_SOCK" || true)
[ -z "$left" ] || echo "[nested] warning: still running with this session's socket: $left" >&2
[ "$KEEP" = 1 ] || rm -rf "$SCRATCH"
exit $code

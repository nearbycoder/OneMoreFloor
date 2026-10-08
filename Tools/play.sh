#!/usr/bin/env bash
# Runs the built Linux player. On this machine the X11/XWayland path hangs at startup,
# so use Unity's native Wayland backend when a Wayland session is available.
#   Tools/play.sh [extra player args]
set -euo pipefail
# OMF_GAME runs another build (a baseline kept for before/after captures, say)
GAME="${OMF_GAME:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/OneMoreFloor.x86_64}"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
# Unity takes the first -screen-width it sees, so only add the default size when the caller didn't pass one
args=(-screen-fullscreen 0)
case " $* " in *" -screen-width "*) ;; *) args+=(-screen-width 1600 -screen-height 900) ;; esac
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
# Automation (autopilot, demo, trailer, ephemeral runs) keeps Unity's config, prefs and any save inside the repo
# (gitignored Logs/xdg), so it can never touch the player's ~/.config/unity3d. Set XDG_CONFIG_HOME to override.
case " $* " in
  *" -omfAutopilot "*|*" -omfDemo "*|*" -omfTrailer "*|*" -omfEphemeral "*|*" -omfPerf "*|*" -omfFidelityShots "*|*" -omfSkyShots "*)
    if [ -z "${XDG_CONFIG_HOME:-}" ] || [ "${XDG_CONFIG_HOME}" = "$HOME/.config" ]; then
      export XDG_CONFIG_HOME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Logs/xdg"
      mkdir -p "$XDG_CONFIG_HOME"
    fi ;;
esac
exec "$GAME" "${args[@]}" "$@"

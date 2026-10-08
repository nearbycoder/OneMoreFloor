#!/usr/bin/env bash
# GRAPHICS FIDELITY on the built game, inside the private nested KWin (Tools/nested.sh), on a throwaway save:
#   Tools/fidelity.sh shots [outdir] [shift]   one frozen moment of a shift at every step, LOW to ULTRA (whole tower and
#                                             close-up), as PNGs of the back buffer
#   Tools/fidelity.sh perf  [outdir] [shift]   frame times: 30 s of bot play per step, vsync off, steps interleaved over
#                                             OMF_ROUNDS rounds (default 2), plus HIGH without the city (-omfNoCity)
# OMF_SIZE=WxH sets the window (default 1920x1080). The shift defaults to the Graveyard Shift (8), the busiest.
# The load average is printed with every run: on a shared machine, compare runs taken at a similar load.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="${1:-perf}"
OUT="$(realpath -m "${2:-$ROOT/Logs/fidelity}")"
SHIFT="${3:-8}"
SIZE="${OMF_SIZE:-1920x1080}"; W="${SIZE%x*}"; H="${SIZE#*x}"
# the nested desktop is bigger than the window, so the game's window-size clamp leaves the window as asked
NEST="$((W * 5 / 4))x$((H * 5 / 4))"
mkdir -p "$OUT"
names=(LOW MEDIUM HIGH ULTRA)
load() { cut -d' ' -f1 /proc/loadavg; }

case "$MODE" in
  shots)
    "$ROOT/Tools/nested.sh" --size "$NEST" --timeout 180 -screen-width "$W" -screen-height "$H" \
      -logFile "$OUT/shots.log" -omfFidelityShots "$OUT" "$SHIFT" > "$OUT/nested-shots.log" 2>&1 || true
    grep -E "\[Fidelity\]" "$OUT/shots.log" || { echo "no fidelity output (see $OUT/shots.log)"; exit 1; }
    ;;
  perf)
    rounds="${OMF_ROUNDS:-2}"
    for r in $(seq 1 "$rounds"); do
      # alternate the order so a load change during the round doesn't always land on the same step
      order=(0 1 2 3 nocity); [ $((r % 2)) = 0 ] && order=(nocity 3 2 1 0)
      for lv in "${order[@]}"; do
        extra=(); name="$lv"
        if [ "$lv" = nocity ]; then extra=(-omfFidelity 2 -omfNoCity); name="HIGH-nocity"; else extra=(-omfFidelity "$lv"); name="${names[$lv]}"; fi
        before=$(load)
        "$ROOT/Tools/nested.sh" --size "$NEST" --timeout 120 -screen-width "$W" -screen-height "$H" \
          -logFile "$OUT/perf-$r-$name.log" -omfPerf "$SHIFT" -omfNoVsync "${extra[@]}" > "$OUT/nested-perf-$r-$name.log" 2>&1 || true
        line=$(grep -E "\[Perf\] shift" "$OUT/perf-$r-$name.log" || echo "no perf line")
        echo "round $r $name (load $before -> $(load)): ${line#*\[Perf\] }"
      done
    done
    ;;
  *) echo "usage: Tools/fidelity.sh shots|perf [outdir] [shift]" >&2; exit 2 ;;
esac

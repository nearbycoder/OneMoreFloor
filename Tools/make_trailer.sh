#!/usr/bin/env bash
# Builds the feature trailer and the README media from the built game (Builds/Linux):
#   1. a video pass: TrailerReel renders every scripted shot offline at a locked 30 fps (Captures/trailer/shots/*.mp4)
#      and saves the README stills (Captures/trailer/stills/*.png);
#   2. an audio pass: the same script in real time with the game's sound effects recorded (music muted);
#   3. Tools/trailer/build.py cuts the shots to the music, mixes the bed from the game's own stems with ducking,
#      and writes docs/media/ (trailer.mp4, trailer-poster.jpg, teaser.gif, screenshots).
#
#   Tools/make_trailer.sh            capture + assemble (about 15 minutes)
#   Tools/make_trailer.sh capture    only capture
#   Tools/make_trailer.sh assemble   re-assemble from an existing capture
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${WORK:-$ROOT/Captures/trailer}"

if [ "${1:-}" != "assemble" ]; then
  rm -rf "$WORK" && mkdir -p "$WORK"
  echo "video pass (offline render, ~6 min)..."
  timeout 1800 "$ROOT/Tools/play.sh" -logFile "$WORK/video.log" -omfTrailer "$WORK" -omfTrailerPass video > /dev/null 2>&1 || true
  grep -q "\[Trailer\] done" "$WORK/video.log" || { echo "video pass failed (see $WORK/video.log)"; exit 1; }
  echo "audio pass (real time, ~5 min)..."
  timeout 1800 "$ROOT/Tools/play.sh" -logFile "$WORK/audio.log" -omfTrailer "$WORK" -omfTrailerPass audio \
    -omfRecordAudio "$WORK/audio.wav" -omfRecordSeconds 900 > /dev/null 2>&1 || true
  [ -f "$WORK/audio.wav" ] || { echo "audio pass failed (see $WORK/audio.log)"; exit 1; }
  if grep -E "Exception" "$WORK/video.log" "$WORK/audio.log" | grep -qv "^ *at "; then
    echo "warning: exceptions in the capture logs"; grep -h "Exception" "$WORK/video.log" "$WORK/audio.log" | sort | uniq -c | head
  fi
fi

[ "${1:-}" = "capture" ] && exit 0
nice -n 10 python3 "$ROOT/Tools/trailer/build.py" "$WORK" "$ROOT/docs/media"

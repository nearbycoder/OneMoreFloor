#!/usr/bin/env bash
# Builds the feature trailer and the README media from the built game (Builds/Linux):
#   1. a video pass: TrailerReel renders every scripted shot offline at a locked 30 fps (Captures/trailer/shots/*.mp4)
#      and saves the README stills (Captures/trailer/stills/*.png);
#   2. an audio pass: the same script in real time with the game's sound effects recorded (music muted);
#   3. Tools/trailer/build.py cuts the shots to the music, mixes the bed from the game's own stems with ducking,
#      and writes docs/media/ (trailer.mp4, trailer-poster.jpg, teaser.gif, screenshots).
#
#   Tools/make_trailer.sh            capture + assemble (about 20 minutes)
#   Tools/make_trailer.sh capture    only capture (capture video / capture audio: one pass; the audio pass plays in
#                                    real time, so it can wait for a quiet machine; capture audio shot1,shot2 re-records
#                                    just those shots and splices them into the existing audio.wav)
#   Tools/make_trailer.sh assemble   re-assemble from an existing capture
#   Tools/make_trailer.sh stills     only the README screenshots: the video pass, then docs/media/screenshots
#                                    (the trailer, its poster and the teaser GIF are left as they are; ~6 minutes).
#
# Every pass runs in a 1920x1080 window inside a private nested KWin (Tools/nested.sh) when KWin is there, so no
# window opens on the desktop (OMF_NESTED=0 runs it on the desktop). OMF_FIDELITY picks the GRAPHICS FIDELITY step
# to record at (0 LOW .. 3 ULTRA, default 3; the capture is offline, so ULTRA costs time, not frame rate). The audio
# pass plays in real time, so its own output stream is muted on the sound server while it records the game's mix.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${WORK:-$ROOT/Captures/trailer}"
FIDELITY="${OMF_FIDELITY:-3}"

# run_game LOG ARGS...: the built game with the trailer's window size and fidelity, nested when possible
run_game() {
  local log="$1"; shift
  local args=(-screen-width 1920 -screen-height 1080 -omfFidelity "$FIDELITY" "$@")
  if [ "${OMF_NESTED:-1}" != 0 ] && command -v kwin_wayland > /dev/null && command -v dbus-run-session > /dev/null; then
    # 2200x1300 leaves room for the 1920x1080 window and its title bar (the game shrinks a window over 95% of the display)
    "$ROOT/Tools/nested.sh" --size 2200x1300 --timeout 1800 "${args[@]}" > "$log" 2>&1 || true
  else
    timeout 1800 "$ROOT/Tools/play.sh" "${args[@]}" > "$log" 2>&1 || true
  fi
}

# mute_stream LOG: wait for the game named in LOG ("[nested] game PID") to open its sound stream and mute that stream
# only, so the real-time audio pass isn't heard on this machine's speakers. AudioTap records the game's own mix before
# the sound server, so the recording is unaffected. The pass talks to PipeWire through ALSA under its own application
# name with state restore off (see AUDIO_ENV), so the mute can't be remembered for the player's game or other apps.
AUDIO_ENV=(PULSE_SERVER="unix:/nonexistent/omf-no-pulse"
           PIPEWIRE_PROPS='{ application.name = "One More Floor trailer capture" state.restore-props = false }')
mute_stream() {
  command -v pactl > /dev/null || return 0
  local log="$1" pid="" id=""
  for _ in $(seq 120); do
    pid=$(sed -n 's/^\[nested\] game \([0-9]*\)$/\1/p' "$log" 2> /dev/null | head -n 1)
    [ -n "$pid" ] && break; sleep 0.5
  done
  [ -n "$pid" ] || return 0
  for _ in $(seq 240); do
    id=$(pactl list sink-inputs 2> /dev/null | awk -v pid="$pid" '/^Sink Input #/ { id = substr($3, 2); name = "" }
      /application.name = "One More Floor trailer capture"/ { name = 1 }
      /application.process.id = / { gsub(/"/, "", $3); if ($3 == pid && name) print id }' | head -n 1)
    if [ -n "$id" ]; then
      pactl set-sink-input-mute "$id" 1
      echo "audio pass: game $pid, sink input $id, $(pactl list sink-inputs | awk -v id="$id" '$0 ~ "^Sink Input #" id "$" { on = 1 } on && /Mute:/ { print "Mute:", $2; exit }')"
      return 0
    fi
    kill -0 "$pid" 2> /dev/null || return 0
    sleep 0.25
  done
}

if [ "${1:-}" = "stills" ]; then
  STILLS="$ROOT/Captures/stills"
  rm -rf "$STILLS" && mkdir -p "$STILLS"
  echo "video pass for the stills (offline render, ~6 min)..."
  run_game "$STILLS/nested.log" -logFile "$STILLS/video.log" -omfTrailer "$STILLS" -omfTrailerPass video
  grep -q "\[Trailer\] done" "$STILLS/video.log" || { echo "video pass failed (see $STILLS/video.log)"; exit 1; }
  grep -h "\[Trailer\] GRAPHICS FIDELITY" "$STILLS/video.log" || true
  nice -n 10 python3 "$ROOT/Tools/trailer/build.py" "$STILLS" "$ROOT/docs/media" screenshots
  exit 0
fi

PASSES="video audio"
[ "${1:-}" = "capture" ] && [ -n "${2:-}" ] && PASSES="$2"
if [ "${1:-}" != "assemble" ] && [[ " $PASSES " == *" video "* ]]; then
  rm -rf "$WORK" && mkdir -p "$WORK"
  echo "video pass (offline render, ~10 min at ULTRA)..."
  run_game "$WORK/video-nested.log" -logFile "$WORK/video.log" -omfTrailer "$WORK" -omfTrailerPass video
  grep -q "\[Trailer\] done" "$WORK/video.log" || { echo "video pass failed (see $WORK/video.log)"; exit 1; }
  grep -h "\[Trailer\] GRAPHICS FIDELITY" "$WORK/video.log" || true
fi
if [ "${1:-}" = "capture" ] && [ "${2:-}" = audio ] && [ -n "${3:-}" ]; then
  # redo some shots of the audio pass: record them on their own, append them to audio.wav, point the manifest at them
  [ -f "$WORK/audio.wav" ] && [ -f "$WORK/audio_shots.txt" ] || { echo "no audio pass in $WORK to patch"; exit 1; }
  REDO="$WORK/redo"; rm -rf "$REDO" && mkdir -p "$REDO"
  echo "audio pass for $3 (real time; load $(cut -d' ' -f1-3 /proc/loadavg))..."
  mute_stream "$REDO/audio-nested.log" & MUTER=$!
  ( export "${AUDIO_ENV[@]}"
    run_game "$REDO/audio-nested.log" -logFile "$REDO/audio.log" -omfTrailer "$REDO" -omfTrailerPass audio -omfTrailerOnly "$3" \
      -omfRecordAudio "$REDO/audio.wav" -omfRecordSeconds 300 )
  wait "$MUTER" 2> /dev/null || true
  [ -f "$REDO/audio.wav" ] || { echo "audio redo failed (see $REDO/audio.log)"; exit 1; }
  python3 - "$WORK" <<'PY'
import os, sys, wave
work = sys.argv[1]; redo = os.path.join(work, "redo")
main, patch = wave.open(os.path.join(work, "audio.wav")), wave.open(os.path.join(redo, "audio.wav"))
assert (main.getframerate(), main.getnchannels()) == (patch.getframerate(), patch.getnchannels())
offset = main.getnframes() / main.getframerate()
frames = main.readframes(main.getnframes()) + patch.readframes(patch.getnframes())
params = main.getparams(); main.close(); patch.close()
out = wave.open(os.path.join(work, "audio.wav.new"), "wb"); out.setparams(params); out.writeframes(frames); out.close()
os.replace(os.path.join(work, "audio.wav.new"), os.path.join(work, "audio.wav"))
lines = {l.split()[0]: l.split() for l in open(os.path.join(work, "audio_shots.txt")) if l.strip()}
for l in open(os.path.join(redo, "audio_shots.txt")):
    p = l.split()
    if not p: continue
    kv = dict(x.split("=") for x in p[1:])
    kv["start"] = f"{float(kv['start']) + offset:.4f}"; kv["end"] = f"{float(kv['end']) + offset:.4f}"
    lines[p[0]] = [p[0]] + [f"{k}={v}" for k, v in kv.items()]
    print(f"  {p[0]}: re-recorded, now at {kv['start']} s")
open(os.path.join(work, "audio_shots.txt"), "w").write("".join(" ".join(v) + "\n" for v in lines.values()))
PY
  cat "$REDO/audio.log" >> "$WORK/audio.log"
  exit 0
fi
if [ "${1:-}" != "assemble" ] && [[ " $PASSES " == *" audio "* ]]; then
  [ -f "$WORK/video_shots.txt" ] || { echo "no video pass in $WORK yet"; exit 1; }
  rm -f "$WORK/audio.wav" "$WORK/audio_shots.txt" "$WORK/audio-nested.log"
  echo "audio pass (real time, ~5 min; load $(cut -d' ' -f1-3 /proc/loadavg))..."
  mute_stream "$WORK/audio-nested.log" & MUTER=$!
  ( export "${AUDIO_ENV[@]}"
    run_game "$WORK/audio-nested.log" -logFile "$WORK/audio.log" -omfTrailer "$WORK" -omfTrailerPass audio \
      -omfRecordAudio "$WORK/audio.wav" -omfRecordSeconds 900 )
  wait "$MUTER" 2> /dev/null || true
  [ -f "$WORK/audio.wav" ] || { echo "audio pass failed (see $WORK/audio.log)"; exit 1; }
  if grep -E "Exception" "$WORK/video.log" "$WORK/audio.log" | grep -qv "^ *at "; then
    echo "warning: exceptions in the capture logs"; grep -h "Exception" "$WORK/video.log" "$WORK/audio.log" | sort | uniq -c | head
  fi
fi

[ "${1:-}" = "capture" ] && exit 0
nice -n 10 python3 "$ROOT/Tools/trailer/build.py" "$WORK" "$ROOT/docs/media"

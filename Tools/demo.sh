#!/usr/bin/env bash
# Records the gameplay demo video from the built game (see DemoReel.cs): a 30 fps offline video
# pass, then a real-time audio pass of the same scripted run, muxed into one MP4.
#   Tools/demo.sh [out.mp4]    (default: Builds/Demo/OneMoreFloor-demo.mp4)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Builds/Demo/OneMoreFloor-demo.mp4}"
WORK="$(mktemp -d /tmp/omf-demo.XXXX)"
mkdir -p "$(dirname "$OUT")"

echo "video pass (offline, ~2 min)..."
timeout 900 "$ROOT/Tools/play.sh" -logFile "$WORK/video.log" -omfDemo "$WORK" -omfDemoPass video > /dev/null 2>&1 || true
grep -q "\[Demo\] wrote" "$WORK/video.log" || { echo "video pass failed (see $WORK/video.log)"; exit 1; }

echo "audio pass (real time, ~1.5 min)..."
timeout 300 "$ROOT/Tools/play.sh" -logFile "$WORK/audio.log" -omfDemo "$WORK" -omfDemoPass audio \
  -omfRecordAudio "$WORK/audio.wav" -omfRecordSeconds 140 > /dev/null 2>&1 || true
[ -f "$WORK/audio.wav" ] || { echo "audio pass failed (see $WORK/audio.log)"; exit 1; }

grep -h "\[Demo\] shift:" "$WORK/video.log" "$WORK/audio.log"
OFF="$(cat "$WORK/audio_offset.txt")"
LEN="$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$WORK/video.mp4")"
FADE="$(python3 -c "print(max(0.0, $LEN - 1.5))")"
ffmpeg -hide_banner -loglevel error -y -i "$WORK/video.mp4" -ss "$OFF" -t "$LEN" -i "$WORK/audio.wav" \
  -map 0:v -map 1:a -c:v copy \
  -af "loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=in:d=0.6,afade=t=out:st=$FADE:d=1.5" -ar 48000 -c:a aac -b:a 192k \
  -shortest -movflags +faststart "$OUT"
rm -rf "$WORK"
echo "wrote $OUT"

#!/usr/bin/env bash
# Rebuilds the browser version into Builds/Pages, the folder GitHub Pages serves at
# https://nearbycoder.github.io/OneMoreFloor/ (index.html at its root, .nojekyll beside it).
#
#   Tools/build-pages.sh            build, then print the site's size and its largest files
#
# The build log goes to Logs/build-webgl.log. Check the result as it will be served with
#   node Tools/check-pages.mjs --serve Builds/Pages
set -euo pipefail
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$PROJECT/Builds/Pages"
LOG="$PROJECT/Logs/build-webgl.log"
mkdir -p "$PROJECT/Logs"

rm -rf "$OUT"
status=0
nice -n 10 "$PROJECT/Tools/unity.sh" build-webgl > "$LOG" 2>&1 || status=$?
grep -E '\[BuildScript\]|error CS|Build Finished|Exiting batchmode' "$LOG" | tail -n 20 || true
if [ "$status" -ne 0 ] || [ ! -f "$OUT/index.html" ]; then
  echo "build-pages: the WebGL build failed (exit $status); see $LOG" >&2
  exit 1
fi

# GitHub Pages runs Jekyll unless told not to, and Jekyll skips some files
touch "$OUT/.nojekyll"

# Unity writes some files private to the user; a web server (and the Pages upload) needs them readable
chmod -R a+rX "$OUT"

# GitHub refuses files over 100 MB
big="$(find "$OUT" -type f -size +95M)"
if [ -n "$big" ]; then
  echo "build-pages: files too big for GitHub Pages:" >&2
  echo "$big" >&2
  exit 1
fi

echo "build-pages: $(du -sh "$OUT" | cut -f1) in $OUT"
find "$OUT" -type f -printf '%s\t%P\n' | sort -rn | head -n 6 | awk -F'\t' '{ printf "  %6.1f MB  %s\n", $1 / 1048576, $2 }'

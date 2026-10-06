#!/usr/bin/env bash
# Builds the Linux and macOS players one after the other and packages each one as a release zip with a .sha256:
#   <out>/OneMoreFloor-v<version>-linux-x86_64.zip
#   <out>/OneMoreFloor-v<version>-macos-universal.zip   (unsigned, un-notarized: see its README.txt)
# The version comes from ProjectSettings (bundleVersion). Nothing is uploaded, and existing zips are never overwritten.
#   Tools/release.sh                      build both, package into Builds/Release
#   OUT=/tmp/rel SKIP_BUILD=1 Tools/release.sh   package the builds already in Builds/Linux and Builds/Mac
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(grep -m1 'bundleVersion:' "$ROOT/ProjectSettings/ProjectSettings.asset" | awk '{print $2}')"
OUT="${OUT:-$ROOT/Builds/Release}"
mkdir -p "$OUT" "$ROOT/Logs"

LINUX="OneMoreFloor-v$VERSION-linux-x86_64"
MAC="OneMoreFloor-v$VERSION-macos-universal"
for n in "$LINUX" "$MAC"; do
  [ -e "$OUT/$n.zip" ] && { echo "$OUT/$n.zip already exists; move it away or bump the version" >&2; exit 1; }
done

if [ -z "${SKIP_BUILD:-}" ]; then
  # one heavy build at a time
  echo "building Linux..."
  nice -n 10 "$ROOT/Tools/unity.sh" build-linux > "$ROOT/Logs/release-linux.log" 2>&1 || { tail -20 "$ROOT/Logs/release-linux.log"; exit 1; }
  echo "building macOS..."
  nice -n 10 "$ROOT/Tools/unity.sh" build-mac > "$ROOT/Logs/release-mac.log" 2>&1 || { tail -20 "$ROOT/Logs/release-mac.log"; exit 1; }
fi
[ -x "$ROOT/Builds/Linux/OneMoreFloor.x86_64" ] || { echo "no Linux build" >&2; exit 1; }
[ -d "$ROOT/Builds/Mac/OneMoreFloor.app" ] || { echo "no macOS build" >&2; exit 1; }

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# ---- Linux: the player folder minus Unity's do-not-ship folders
mkdir -p "$STAGE/$LINUX"
( cd "$ROOT/Builds/Linux" && tar --exclude='*_DoNotShip' --exclude='*_ButDontShipItWithYourGame' -cf - . ) | ( cd "$STAGE/$LINUX" && tar -xf - )
cat > "$STAGE/$LINUX/README.txt" <<TXT
One More Floor $VERSION (Linux x86_64)
===================================

You run the elevator in The Shuffleton, a hotel that rearranges its floors every time you stop.

Run:   ./OneMoreFloor.x86_64
       (on a Wayland desktop, add -force-wayland if the window doesn't appear)

Controls: mouse and keyboard, or a gamepad. Click a waiting guest to let them in, click a floor
(or press 1-9) to send the car. Space lets everyone in. Esc pauses. The first shift teaches the rest.

Saves: ~/.config/unity3d/Nearby/One More Floor/

Source, trailer and full instructions: https://github.com/nearbycoder/OneMoreFloor
TXT

# ---- macOS: the app bundle (symlinks and executable bits kept)
mkdir -p "$STAGE/$MAC"
cp -a "$ROOT/Builds/Mac/OneMoreFloor.app" "$STAGE/$MAC/"
cat > "$STAGE/$MAC/README.txt" <<TXT
One More Floor $VERSION (macOS 12+, Intel and Apple silicon)
=====================================================

You run the elevator in The Shuffleton, a hotel that rearranges its floors every time you stop.

This build is NOT signed with an Apple Developer ID and NOT notarized, and it hasn't been tested
on a Mac yet. macOS will refuse to open it at first. To run it anyway:

  1. Move OneMoreFloor.app to Applications (or anywhere you like).
  2. Control-click (right-click) it and choose Open, then Open again.
     On macOS 15 and later, if there's no Open button: try to open it once, then go to
     System Settings > Privacy & Security and click "Open Anyway".
  Or, in Terminal:   xattr -dr com.apple.quarantine /path/to/OneMoreFloor.app

Controls: mouse and keyboard, or a gamepad. Click a waiting guest to let them in, click a floor
(or press 1-9) to send the car. Space lets everyone in. Esc pauses. The first shift teaches the rest.

Source, trailer and full instructions: https://github.com/nearbycoder/OneMoreFloor
TXT

# zip with Unix permissions and symlinks kept (Python's zipfile, so the zip tool isn't needed)
pack() {
  python3 - "$STAGE" "$1" "$OUT/$1.zip" <<'PY'
import os, stat, sys, time, zipfile
stage, name, dest = sys.argv[1:4]
with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(os.path.join(stage, name)):
        dirs.sort(); files.sort()
        for d in dirs + files:
            full = os.path.join(root, d)
            arc = os.path.relpath(full, stage)
            st = os.lstat(full)
            if stat.S_ISLNK(st.st_mode):
                zi = zipfile.ZipInfo(arc, time.localtime(st.st_mtime)[:6])
                zi.create_system = 3
                zi.external_attr = (stat.S_IFLNK | 0o777) << 16
                z.writestr(zi, os.readlink(full))
            elif stat.S_ISDIR(st.st_mode):
                zi = zipfile.ZipInfo(arc + "/", time.localtime(st.st_mtime)[:6])
                zi.create_system = 3
                zi.external_attr = (stat.S_IFDIR | 0o755) << 16 | 0x10
                z.writestr(zi, b"")
            else:
                z.write(full, arc)  # keeps the mode bits, including +x
PY
}

for n in "$LINUX" "$MAC"; do
  pack "$n"
  ( cd "$OUT" && sha256sum "$n.zip" > "$n.zip.sha256" )
  echo "$OUT/$n.zip  $(du -h "$OUT/$n.zip" | cut -f1)  $(cut -c1-16 "$OUT/$n.zip.sha256")..."
done

#!/usr/bin/env bash
# Runs the Unity 6.6 editor (6000.6.2f1) against this project.
#
# The editor links against libxml2.so.2, but CachyOS/Arch now ship libxml2.so.16, so it exits
# immediately with "libxml2.so.2: cannot open shared object file". The proper fix is
# `sudo pacman -S libxml2-legacy`; until then this points the loader at a local copy in
# Tools/.libs (gitignored). Copy one there with:
#   cp -L /path/to/libxml2.so.2 Tools/.libs/
#
#   Tools/unity.sh                 open the project in the editor (GUI)
#   Tools/unity.sh batch <Method>  run a static editor method in batch mode and quit
#   Tools/unity.sh build-linux     batch-build Builds/Linux/OneMoreFloor.x86_64
#   Tools/unity.sh build-mac       batch-build Builds/Mac/OneMoreFloor.app (universal, unsigned)
#   Tools/unity.sh build-windows   batch-build Builds/Windows/OneMoreFloor.exe (needs Windows Build Support)
#   Tools/unity.sh build-webgl     batch-build the browser build into Builds/Pages (Tools/build-pages.sh wraps it)
#   Tools/unity.sh test            run EditMode tests, results in Logs/test-results.xml
set -euo pipefail

UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export LD_LIBRARY_PATH="$PROJECT/Tools/.libs${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
mkdir -p "$PROJECT/Logs"

case "${1:-open}" in
  open)
    exec "$UNITY" -projectPath "$PROJECT"
    ;;
  batch)
    shift
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -executeMethod "$1" -logFile - "${@:2}"
    ;;
  build-linux)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod OneMoreFloor.EditorTools.BuildScript.BuildLinux -logFile -
    ;;
  build-mac)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod OneMoreFloor.EditorTools.BuildScript.BuildMac -logFile -
    ;;
  build-windows)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod OneMoreFloor.EditorTools.BuildScript.BuildWindows -logFile -
    ;;
  build-webgl)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget WebGL \
      -executeMethod OneMoreFloor.EditorTools.BuildScript.BuildWebGL -logFile -
    ;;
  test)
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
      -testResults "$PROJECT/Logs/test-results.xml" -logFile "$PROJECT/Logs/test-run.log" || true
    python3 - "$PROJECT/Logs/test-results.xml" <<'PY'
import sys, xml.etree.ElementTree as ET
try:
    root = ET.parse(sys.argv[1]).getroot()
except Exception as e:
    print("no test results:", e); sys.exit(1)
print(f"total={root.get('total')} passed={root.get('passed')} failed={root.get('failed')} skipped={root.get('skipped')}")
for tc in root.iter('test-case'):
    if tc.get('result') != 'Passed':
        msg = tc.find('.//message')
        print("FAIL", tc.get('fullname'), (msg.text or '').strip()[:400] if msg is not None else '')
sys.exit(0 if root.get('failed') == '0' else 1)
PY
    ;;
  *)
    echo "usage: $0 [open|batch <Method>|build-linux|build-mac|build-windows|build-webgl|test]" >&2
    exit 2
    ;;
esac

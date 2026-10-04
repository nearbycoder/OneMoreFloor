#!/usr/bin/env bash
# Builds and runs the rules harness (Assets/Scripts/Core + Tools/simharness) on the .NET SDK bundled
# with the Unity editor.   Tools/sim.sh [balance [seeds]|trace <shiftIndex> <seed>|fuzz]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Data/DotNetSdk/dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
"$DOTNET" build "$ROOT/Tools/simharness/SimHarness.csproj" -c Release -v q -nologo -clp:NoSummary 1>&2
exec "$DOTNET" "$ROOT/Temp/simharness/bin/Release/net8.0/SimHarness.dll" "$@"

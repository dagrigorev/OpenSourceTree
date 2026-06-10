#!/usr/bin/env bash
#
# Builds and publishes OpenSourceTree for Linux or macOS.
#
#   ./build.sh                 # auto-detect RID (linux-x64 / osx-x64 / osx-arm64) -> dist/<rid>
#   ./build.sh linux-arm64     # explicit RID
#   ./build.sh --build-only    # plain Debug build, no publish
#
set -euo pipefail
cd "$(dirname "$0")"

PROJECT="src/OpenSourceTree/OpenSourceTree.csproj"
CONFIGURATION="${CONFIGURATION:-Release}"

if [[ "${1:-}" == "--build-only" ]]; then
    dotnet build "$PROJECT" -c "$CONFIGURATION"
    exit $?
fi

RID="${1:-}"
if [[ -z "$RID" ]]; then
    case "$(uname -s)" in
        Linux)  RID="linux-x64" ;;
        Darwin) [[ "$(uname -m)" == "arm64" ]] && RID="osx-arm64" || RID="osx-x64" ;;
        *)      RID="win-x64" ;;
    esac
fi

OUT="dist/$RID"
echo "Publishing $CONFIGURATION / $RID (self-contained) -> $OUT"

dotnet publish "$PROJECT" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -o "$OUT"

echo "Done. Run: $OUT/OpenSourceTree"

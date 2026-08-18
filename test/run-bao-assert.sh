#!/usr/bin/env bash
# Load BAO-1 and run the bao1 assertions in the vanilla-only profile.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
# GS_WRAP: launch inside gamescope's nested compositor — immune to the desktop's
# display state (owner gaming via Proton, mode-list churn, XF86VidMode crashes).
GS=(gamescope -W 1600 -H 900 --)
SAVEDATA="$REPO/test/SaveData"
SCENARIO="${1:-bao1}"
RESULT="$SAVEDATA/test-results-$SCENARIO.json"
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$REPO/Source/BetterAttackOrders/BetterAttackOrders.csproj" -c Release
    dotnet build "$REPO/test/StagingMod/Source/BAOTestStaging.csproj" -c Release
fi
rm -f "$RESULT"
timeout --signal=TERM 15m "${GS[@]}" "$RIMWORLD" -savedatafolder="$SAVEDATA" \
    "-celoadsave=BAO-1-attack-order" "-ceassert=$SCENARIO" || true
if [[ -f "$RESULT" ]]; then
    echo "== results =="; cat "$RESULT"
else
    echo "NO RESULTS FILE" >&2; exit 1
fi

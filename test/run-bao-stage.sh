#!/usr/bin/env bash
# Build + regenerate BAO-1-attack-order in this repo's own VANILLA-ONLY profile.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
# GS_WRAP: launch inside gamescope's nested compositor — immune to the desktop's
# display state (owner gaming via Proton, mode-list churn, XF86VidMode crashes).
GS=(gamescope -W 1600 -H 900 --)
SAVEDATA="$REPO/test/SaveData"
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$REPO/Source/BetterAttackOrders/BetterAttackOrders.csproj" -c Release
    dotnet build "$REPO/test/StagingMod/Source/BAOTestStaging.csproj" -c Release
fi
mkdir -p "$SAVEDATA/Config" "$SAVEDATA/Saves"
for f in ModsConfig.xml Prefs.xml; do
    [[ -e "$SAVEDATA/Config/$f" ]] || cp "$REPO/test/Config/$f" "$SAVEDATA/Config/$f"
done
rm -f "$SAVEDATA/Saves"/BAO-*.rws
exec "${GS[@]}" "$RIMWORLD" -savedatafolder="$SAVEDATA" -quicktest -baostage

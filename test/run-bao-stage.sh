#!/usr/bin/env bash
# Build + regenerate BAO-1-attack-order in this repo's own VANILLA-ONLY profile.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
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
exec "$RIMWORLD" -savedatafolder="$SAVEDATA" -quicktest -baostage

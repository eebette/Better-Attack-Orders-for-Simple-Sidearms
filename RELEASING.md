# Releasing

Manual local builds (workshop-local SS reference, unlicensed upstream — no CI);
`Assemblies/BetterAttackOrders.dll` committed.

## Checklist

1. `dotnet build Source/BetterAttackOrders/BetterAttackOrders.csproj -c Release`
2. Automated pass: `./test/run-bao-stage.sh` then `./test/run-bao-assert.sh`
   (vanilla-only profile) — `test-results-bao1.json` must be `"passed": true`.
3. Composition sanity: one manual load in the CE+SS suite profile.
4. **Upstream first**: file the SS issue (`docs/UPSTREAM_ISSUE.md`) if not yet
   filed — on-record intent; this mod retires if SS ever fixes it.
5. Demo GIF (owner): load BAO-1-attack-order via `run-bao-stage.sh` profile,
   dev mode off. Scene: drafted pawn with revolver, right-click the distant
   raider — order appears, pawn draws the rifle and fires. Clip to `Media/`,
   embed in README + the description draft's slot.
6. Record the SS version tested against; tag `v1.0.0`; upload via in-game
   Mods → Upload.

## Save compatibility

This mod stores nothing in saves and patches one utility method: safe to add
and remove mid-save with zero footprint.

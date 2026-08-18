# Test plan — Better Attack Orders for Simple Sidearms

Automated end-to-end in this repo's own **vanilla-only profile**
(Core + Harmony + Simple Sidearms + this mod — no Combat Extended; that's the
mod's claim):

```
./test/run-bao-stage.sh          # regenerate BAO-1-attack-order (quit after letter)
./test/run-bao-assert.sh         # run the bao1 assertions
```

Result: `test/SaveData/test-results-bao1.json`. Green pass recorded 2026-08-18:

- **scenario-is-the-deadlock**: runner CONSTRUCTS the precondition (revolver
  25.9 in hand, bolt-action 36.9 in inventory — SS may have re-equipped on
  load, so it normalizes explicitly), parks the target at equipped-range+3
  (computed at runtime; hardcoded distances lose to vanilla def changes), and
  verifies the equipped verb genuinely cannot hit.
- **order-available-with-patch**: `FloatMenuUtility.GetRangedAttackAction`
  returns a real action where vanilla returns null.
- **swap-and-attack**: invoking the action equips the rifle and starts
  `AttackStatic` at the target.

Guard verified by design + first-run failure: the patch rescues ONLY
drafted-pawn + equipped-can't-hit failures — the initial version would have
force-enabled orders for undrafted pawns ("X is not drafted" was vanilla's
actual first refusal), caught by the harness.

Pending: composition run in the CE+SS suite profile (the fix calls SS's
`findBestRangedWeapon`, which the suite's core patch makes CE-aware — expected
free, verify once when convenient). Owner feel-pass.

## Harness ops note

Truncate `Player.log` (`: > "$LOG"`) before each launch a watcher greps — the
first polls otherwise read the PREVIOUS boot's log and match stale markers
(this run's staging was once falsely "detected" that way).

# Test plan — Better Attack Orders for Simple Sidearms

Automated end-to-end in this repo's own **vanilla-only profile**
(Core + Harmony + Simple Sidearms + this mod — no Combat Extended; that's the
mod's claim):

```
./test/run-bao-stage.sh          # regenerate BAO-1-attack-order (quit after letter)
./test/run-bao-assert.sh bao1    # the order fix
./test/run-bao-assert.sh bao2    # the idle auto-switch
```

Results: `test/SaveData/test-results-bao*.json`. Green passes recorded 2026-08-18:

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

**bao2 (idle auto-switch)** — same save, three phases: toggle OFF + drafted +
target at the rifle's edge → pawn must hold the revolver and stay idle for 1800
ticks (negative control); toggle ON → pawn swaps to the rifle unprompted and
enters warmup on the target.

Two scenario contaminations the harness surfaced (and the fixes):
- SS itself swaps weapons around drafting — its AutoUndrafter postfix re-equips
  by preference every 100 ticks on Wait_Combat, and its warmup auto-switch fires
  the moment ANY target enters equipped range. The scenario pins the target at
  the rifle's edge (never inside revolver range) and teleport-resets the
  charging raider each check.
- 1.6's bucketed `tickIntervalAction` deltas: an exact-tick `IsHashIntervalTick`
  gate inside a CheckForAutoAttack postfix almost never coincides with the
  invocation ticks — vanilla already throttles the call, so the patch carries no
  extra gate. (Diagnosed by decompiling the SHIPPED Assembly-CSharp with
  ilspycmd — the Krafs ref assembly is extern stubs.)

Pending: composition run in the CE+SS suite profile (the fix calls SS's
`findBestRangedWeapon`, which the suite's core patch makes CE-aware — expected
free, verify once when convenient). Owner feel-pass.

## Harness ops note

Truncate `Player.log` (`: > "$LOG"`) before each launch a watcher greps — the
first polls otherwise read the PREVIOUS boot's log and match stale markers
(this run's staging was once falsely "detected" that way).

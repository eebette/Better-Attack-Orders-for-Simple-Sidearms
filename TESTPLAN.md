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

Label annotation (added with the label patch): bao1 also asserts the rescued
option reads "Fire at X (using bolt-action rifle)" AND that an in-range order's
label stays pristine vanilla. Isolation note: bao1 disables the idle
auto-switch during setup — otherwise v1.1 fires on the draft tick itself (the
Wait job's init runs CheckForAutoAttack) and legitimately dissolves the
deadlock before the order path is exercised. That interference was v1.1
working as designed, caught by the label assertions.

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

## Adversarial review round (2026-09-02) — 6 MEDIUM fixed

Three attackers over the failure-doctrine retrofit + idle-redesign state found
six real MEDIUMs; all fixed. The mod looked converged and was not.

Guard-parity + forced-state cluster (the always-on order path was LESS guarded
than the toggle idle path — backwards):
- **M1 order fix ignored forced weapons.** `findBestRangedWeapon` does not
  consult `ForcedWeapon` (SS's forced-respect lives in
  `trySwapToMoreAccurateRangedWeapon`, which this path never calls), so a forced
  revolver got overridden on a rescued click. Fixed: `WouldRescue` bails on
  `IsCurrentWeaponForced(false)`.
- **M2 idle armed a FORCE-UNARMED pawn.** The gate checked the two armed forced
  fields, missing `ForcedUnarmed`/`ForcedUnarmedWhileDrafted`. Fixed by the same
  `IsCurrentWeaponForced` call (covers all forced states; also removes a mirror).
- **M3 order re-enabled non-range refusals.** Vanilla `GetRangedAttackAction`
  returns `OutOfRange` in an else-if BEFORE the incapable-of-violence branch, so
  an out-of-range refusal masks it; `WouldRescue` lacked the `Downed`/`Violent`
  guards the idle path has, so it armed pacifists. Fixed: added both guards.
- Pinned by **bao4**: `order-declines-when-weapon-forced` +
  `idle-leaves-force-unarmed-alone`. A/B'd — neuter both forced bails and the
  order offers the rifle to a forced pawn / the idle arms the unarmed pawn.

- **M4 `IsEligibleCarried` omitted `canUseSidearmInstance`.** The fallback could
  select a biocoded/bladelink/role-locked weapon SS's equip (`equipSpecificWeapon`
  line 128) then refuses, looping the idle switch every ~4 ticks. Fixed by adding
  SS's own `canUseSidearmInstance` (gated on `AllowBlockedWeaponUse`, matching
  `findBestRangedWeapon`).
- **M5 reach used raw `def.range`, not effective.** Under a weather `maxRangeCap`
  (vanilla-triggerable) or CE range-reducing ammo, two long carried guns
  flip-flopped (SS's window uses weather-capped `AdjustedRange`; BAO's raw range
  diverged). Fixed: `RescueLogic.WithinWindow`/`EffectiveRange` call vanilla's own
  `AdjustedRange` + `EffectiveMinRange` on the carried weapon's verb (the same
  methods SS's window uses — not a reproduction), which also closes the min-range
  LOW.
- **M6 missing failure-doctrine layer 3.** The Prepare guards proved only the
  RimWorld targets, not the SS members the bodies call (JIT-resolved on first
  compile). All three postfixes now split thin-outer / `[MethodImpl(NoInlining)]`
  inner in try/catch with `Log.ErrorOnce` (0x0BA0000x), matching the reference
  `F01_ReloadAbort`.

Also: L2 (idle now mirrors vanilla's `Wait_Combat` + `canUseRangedWeapon` gate),
L3 (the order closure re-validates via `WouldRescue` at CLICK time, not
menu-build time — the captured winner could have been hauled/equipped/destroyed).
bao1-4 all green; "Installed 3 patch class(es)", no guard errors.

**Convergence pass (2 attackers): no regression from the six fixes; one doctrine
gap closed (L4), one edge accepted rather than mirrored (M7).**
- **M7 (ideoligion-animal masking) — ACCEPTED LIMITATION, NOT fixed.** Vanilla's
  out-of-range else-if masks its ideoligion-animal refusals
  (`IsKillingInnocentAnimal` / `IsVeneratedAnimal`), so a rescued order can offer
  a shot at an out-of-range venerated/innocent animal the pawn's ideoligion
  forbids. A first fix re-checked those conditions in `WouldRescue` — but that
  is a 1:1 REPRODUCTION of vanilla's own refusal expression (there is no callable
  vanilla API for it), which violates the no-mirror rule, so it was REVERTED.
  Distinct from M3's `Violent`/`Downed`, which are pawn-CAPABILITY checks BAO
  makes on its own account (the idle path checks them too) — not a reproduction
  of GetRangedAttackAction's UI logic. The residual edge is narrow (Ideology DLC
  + a venerate/protect-animal precept + an out-of-range protected animal + a
  player click) and recoverable (a mood hit, no corruption). A clean fix would
  need to re-invoke vanilla with a reaching weapon (invasive) or transpile its
  range check (bigger architecture change) — deferred; candidate for the upstream
  issue draft. See [[rule-no-upstream-code-reuse]].
- **L4 (M6 completeness) — FIXED.** the order's click closure calls
  `equipSpecificWeaponFromInventory` OUTSIDE PostfixInner's try (it runs later, on
  click), so an SS rename of that one method would throw uncaught at click. The
  closure now carries its own try/`Log.ErrorOnce` (0x0BA00004).
- Both attackers CLEARED the six fixes against decompiles: the verb-on-carried
  range calls (WithinWindow/EffectiveRange) are NPE-safe (a CompEquippable verb's
  caster is always null; AdjustedRange/EffectiveMinRange never deref it) and are a
  line-for-line match of SS's own window; the three NoInlining splits are
  behaviour-preserving with distinct keys; IsCurrentWeaponForced(false) does not
  over-bail a default-preference pawn. Converged.

## Harness ops note

Truncate `Player.log` (`: > "$LOG"`) before each launch a watcher greps — the
first polls otherwise read the PREVIOUS boot's log and match stale markers
(this run's staging was once falsely "detected" that way).

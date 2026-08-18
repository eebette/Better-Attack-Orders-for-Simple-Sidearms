# Better Attack Orders for Simple Sidearms

**Status: BUILT and machine-verified 2026-08-18** — single Harmony postfix on
`FloatMenuUtility.GetRangedAttackAction`, green end-to-end pass on a
vanilla-only modlist (see TESTPLAN.md). Remaining: owner feel-pass, upstream
issue filing (draft in `docs/UPSTREAM_ISSUE.md`), demo GIF, publish.

## Objective

Fix a vanilla Simple Sidearms deadlock: SS's only in-combat weapon-swap trigger
runs during aim warmup, which requires an attack job *with the currently equipped
weapon* — so a pawn holding a shotgun with a sniper rifle in inventory **cannot
even be ordered** to attack a distant target. The float menu says "Out of range"
(computed against the equipped weapon), no job forms, no warmup happens, the swap
logic never runs. The player must manually switch via the SS gizmo first.

Fix: when building/validating a ranged attack order, consider **all carried
weapons**; if the order is only satisfiable by a different carried weapon, swap
(via SS's own preference machinery) before the attack job starts.

**Mechanism settled (owner, 2026-08-18): SINGLE-OPTION repair.** The existing
"Fire at X" order simply appears where it used to be missing and auto-swaps to
the capable weapon via SS's own selection — NO per-weapon "attack with <weapon>"
float-menu entries (new UI surface, a convention SS never uses; fails the
ownership test). The name stays "Better Attack Orders" because the player-visible
outcome is the existing order working, not a new order type. Explicit per-weapon
entries remain a possible later opt-in ADDITION if feel-testing shows auto-pick
choosing wrong.

## Scope and provenance

- Descoped 2026-08-18 from the CE+SS suite's Tactics module by the owner's seam
  test: the deadlock exists in **pure vanilla SS** — no Combat Extended
  involvement — so it is a standalone SS fix, not suite scope. History and the
  original spec live in the Tactics repo's README (feature 2).
- **Upstream first**: file a single-topic issue on
  https://github.com/PeteTimesSix/SimpleSidearms with the repro before/alongside
  building (SS is in maintenance mode; expect no reply, but intent goes on
  record). If SS ever fixes it upstream, this mod retires.
- Discovered during CE playtesting of the suite (CE's larger range spreads make
  the deadlock constant), but the fix itself must not reference CE.

## Design constraints

- Dependencies: **Harmony + Simple Sidearms only.** No CE, no suite mods.
- Free composition, zero coupling: the fix calls SS's own
  `GettersFilters.findBestRangedWeapon(pawn, target)` — when the CE+SS suite is
  installed, the core patch's Harmony patches make that call CE-aware (ammo,
  CE DPS) automatically. Do not special-case CE here.
- Swap only on an explicit player attack order — this changes when a pawn *can be
  ordered*, never who it targets (no target choice, no autonomous behavior).
- Respect SS state: forced-weapon settings, skip flags (manual-use/EMP/dangerous
  filtering) via SS's own selection call — never reimplement its filters.
- Licensing: SS has NO published license — behavioral reference only, never copy
  its code. This mod is MIT.

## Technical context

- SS internals: `GettersFilters.findBestRangedWeapon` (nullable target, returns
  `(weapon, dps, averageSpeed)` tuple), `WeaponAssingment` equip helpers,
  `CompSidearmMemory`. Source: https://github.com/PeteTimesSix/SimpleSidearms
  (1.6 branch `v1.6/`).
- **Main unknown (research first): RimWorld 1.6 float-menu architecture.** 1.6
  reworked float menus into option-provider classes — find where the ranged
  attack option's range validation lives (vanilla `FloatMenuOptionProvider` for
  drafted attack orders) before choosing the patch point. The patch likely
  either relaxes the range check to "any carried weapon reaches" and prepends a
  swap to the order's job chain, or adds a parallel option ("attack with
  <weapon>").
- Also cover the direct right-click-attack path used when the option is chosen,
  and keyboard/queued orders if they route differently.

## Build

Same pattern as the suite repos (SDK net48, `Krafs.Rimworld.Ref 1.6.*`,
`Lib.Harmony 2.3.3` ExcludeAssets=runtime, `Krafs.Publicizer` over the local
workshop DLL `~/.local/share/Steam/steamapps/workshop/content/294100/927155256/v1.6/Assemblies/SimpleSidearms.dll`).
No CI (workshop-local refs, unlicensed upstream). Committed DLL in `Assemblies/`
at release, per the suite's RELEASING.md conventions.

## Testing

Reuse the suite's harness pattern (CLI-arg-gated staging GameComponent +
assert runner writing JSON; see the compat patch repo's `test/`). Scenario:
pawn with short-range primary + long-range sidearm, target beyond primary's
range but within sidearm's; assert the order is issuable and the pawn swaps and
fires. Must ALSO pass with vanilla-only modlist (Harmony + SS + this) — that's
the point.

- packageId: `eebette.BetterAttackOrders`
- RimWorld 1.6. MIT license.

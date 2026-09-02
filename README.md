# Better Attack Orders for Simple Sidearms

[![Latest Release](https://img.shields.io/github/v/release/eebette/Better-Attack-Orders-for-Simple-Sidearms?label=Latest%20Release)](https://github.com/eebette/Better-Attack-Orders-for-Simple-Sidearms/releases)
<!-- Steam Workshop badge goes here at publish -->

![Better Attack Orders for Simple Sidearms](Media/Badge_BAO.png)

Your pawn is holding a revolver and carrying a sniper rifle — this mod makes the
attack order understand that.

<!-- DEMO GIF: out-of-range order swaps to the rifle and fires (Media/, at publish) -->

- [Features](#features)
- [Development](#development)
- [Building](#building)
- [Testing](#testing)
- [Thanks](#thanks)
- [License](#license)

NOTE: This mod stores nothing in your save — safe to add or remove at any time.

## Features

**The fix**

- Vanilla validates ranged attack orders against the *equipped* weapon only: a
  pawn with a short-range weapon in hand and a longer-ranged sidearm in
  inventory gets "Cannot fire: out of range" — and because no attack job can
  form, Simple Sidearms' auto-switch (which only runs during aim warmup) never
  gets its chance. The order is deadlocked.
- With this mod, the fire order considers **every weapon the pawn carries**. If
  only a carried sidearm can reach the target, issuing the order swaps to it
  and fires. No new buttons — the existing order just works.
- Honest labeling: the rescued order reads **"Fire at X (using bolt-action
  rifle)"** — you see which gun comes out before you click. Orders that work
  vanilla keep their untouched vanilla label; the annotation appears exactly
  and only where vanilla offered nothing at all.

**Idle auto-switch** *(toggleable, on by default)*

- The autonomous sibling of the order fix: a drafted pawn standing guard
  auto-attacks only what its *equipped* weapon can reach — same blindness, no
  right-click involved. With this on, a pawn with nothing in range of the
  equipped weapon draws a carried weapon that CAN reach a target, then engages
  normally.
- Pure idle rescue: never runs while anything is already in equipped range,
  never touches pawns with a forced weapon, respects hold fire, drafted pawns
  only — undrafted colonist AI is untouched.

**Guardrails**

- Weapon choice goes through Simple Sidearms' own selection: forced-weapon
  settings and its skip filters (manual-use / EMP / dangerous) are respected.
- Only the exact broken case is rescued — a *drafted* pawn whose *equipped*
  weapon can't hit. Every other refusal (not drafted, incapable of violence,
  nothing reaches) stands untouched.

**Combat Extended**

- No CE dependency; works identically with or without it. With the
  [CE+SS Compatibility Patch](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Patch)
  installed, weapon choice automatically becomes CE-aware (ammo state, CE
  ballistics) — zero configuration, zero coupling.

## Development

The deadlock exists in pure vanilla Simple Sidearms; this mod is a standalone
interim fix and retires if it ever lands upstream (single-topic issue draft:
[`docs/UPSTREAM_ISSUE.md`](docs/UPSTREAM_ISSUE.md)). Implementation is three
Harmony postfixes — the order fix on `FloatMenuUtility.GetRangedAttackAction`,
its honest label on `FloatMenuOptionProvider_DraftedAttack.GetOptionsFor`, and
the idle auto-switch on `JobDriver_Wait.CheckForAutoAttack` — all sharing one
selection path (`RescueLogic`); design decisions and provenance live in
[`docs/DESIGN.md`](docs/DESIGN.md). Simple Sidearms is a
build-time reference only — it ships no license, so no SS code is copied or
redistributed.

Releases are manual local builds with the DLL committed in `Assemblies/` — the
compile reference lives in the local Steam Workshop folder, so CI cannot build
this repo. Release checklist: [`RELEASING.md`](RELEASING.md).

## Building

Requires the .NET SDK and a Steam Workshop subscription to Simple Sidearms:

```bash
dotnet build Source/BetterAttackOrders/BetterAttackOrders.csproj -c Release
```

References the workshop DLL at
`~/.local/share/Steam/steamapps/workshop/content/294100/927155256/` (override
with `-p:RimWorldWorkshopDir=...`), compiles against
[Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref) 1.6,
and uses [Krafs.Publicizer](https://github.com/krafs/Publicizer) for Simple
Sidearms internals. Output lands in `Assemblies/`.

## Testing

Automated end-to-end tests run in this repo's own **vanilla-only profile**
(Core + Harmony + Simple Sidearms + this mod — no Combat Extended, which is the
point):

```bash
./test/run-bao-stage.sh     # build + stage the deadlock save; quit after the letter
./test/run-bao-assert.sh    # load it, assert the fix, write test-results-bao1.json
```

The runner constructs the deadlock (revolver equipped, bolt-action carried,
target parked between the two ranges), verifies the order exists where vanilla
returns null, and confirms the swap-and-attack. Details and recorded passes:
[`TESTPLAN.md`](TESTPLAN.md).

## Thanks

- **PeteTimesSix** for [Simple Sidearms](https://github.com/PeteTimesSix/SimpleSidearms).
- The **Combat Extended team** — this fix was found while building the
  [CE+SS compatibility suite](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Patch),
  where CE's larger range spreads make the deadlock constant.

## License

This mod's code is [MIT](LICENSE).

# Better Attack Orders for Simple Sidearms

[![Latest Release](https://img.shields.io/github/v/release/eebette/Better-Attack-Orders-for-Simple-Sidearms?label=Latest%20Release)](https://github.com/eebette/Better-Attack-Orders-for-Simple-Sidearms/releases)
<!-- Steam Workshop badge goes here at publish -->

![Better Attack Orders for Simple Sidearms](Media/Badge_BAO.png)

RimWorld mod that extends a pawn's target scanning to consider targets within
its [Simple Sidearms](https://github.com/PeteTimesSix/SimpleSidearms) sidearms' ranges.

<!-- DEMO GIF: out-of-range order swaps to the rifle and fires (Media/, at publish) -->

## Features

- Ranged attack orders consider remembered sidearms.
- Idle pawn target scanning considers remembered sidearms.

## Load order

> Harmony → Simple Sidearms → this mod.

## My other mods

### The CE + Simple Sidearms suite

![CE + Simple Sidearms Compatibility Patch](Media/Badge_Suite.png)

Mods that make Combat Extended and Simple Sidearms run together smoothly.

| Module                                                                                                                                               | What it does                                                         |
|------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| [![CE + Simple Sidearms Compatibility Patch](Media/Badge_Patch.png)](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Patch)   | Core compatibility patch for Combat Extended and Simple Sidearms.    |
| [![CE + Simple Sidearms Loadouts Module](Media/Badge_Loadouts.png)](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Loadouts) | Syncs loadouts between Combat Extended and Simple Sidearms.          |
| [![Compatibility Module - Tactics](Media/Badge_Tactics.png)](https://github.com/eebette/CombatExtended-SimpleSidearms-Compatibility-Tactics)         | Sensible tweaks to nonsense pawn behavior when CE + SS run together. |

### Standalone

| Mod                                                                                                                                     | What it does                                                                                                                    |
|-----------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|
| [![Pawns Optimize Weapon Quality](Media/Badge_POWQ.png)](https://github.com/eebette/Pawns-Optimize-Weapon-Quality)            | Pawns will upgrade their held guns when a higher-quality copy is available.                                                     |
| [![Universal Patch for More Materials](Media/Badge_UPMM.png)](https://github.com/eebette/Universal-Patch-for-More-Materials)            | Adds materials from [More Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=3055040889) to non-vanilla recipes. |

## FAQ

**CE compatible?**

Yup!

**Can I add or remove it mid-save?**

Yep.

**Does it change balance?**

Simplifies/automates aspects of combat management.

**AI?**

This mod was engineered with the help of an AI Coding Assistant (Claude Code, Fable 5, Max effort). The amount of
researching and deep-diving the compatibility interfaces of mods that it patches would have been insurmountable without
it.

Development followed a standard process driven and scrutinized by me (the real human person writing this):
explore, design, build, test, fix, review, scrutinize, test again over many rounds.

I have manually reviewed and verified all code in this mod.

I ask that if you have unconstructive feedback regarding the usage of AI while developing this mod, that it remains
outside of this community space. Thank you.

## Building

Requires the .NET SDK and a Steam Workshop subscription to Simple Sidearms:

```bash
dotnet build Source/BetterAttackOrders/BetterAttackOrders.csproj -c Release
```

References the versioned SimpleSidearms 1.6 DLL at
`~/.local/share/Steam/steamapps/workshop/content/294100/927155256/v1.6/Assemblies/SimpleSidearms.dll` (override the
workshop root with `-p:RimWorldWorkshopDir=...`), compiles against
[Krafs.Rimworld.Ref](https://www.nuget.org/packages/Krafs.Rimworld.Ref) 1.6, and
uses [Krafs.Publicizer](https://github.com/krafs/Publicizer) for Simple Sidearms internals. Output lands in
`Assemblies/`.

## Testing

Automated end-to-end tests run in this repo's own **vanilla-only profile**
(Core + Harmony + Simple Sidearms + this mod - no Combat Extended, which is the point):

```bash
./test/run-bao-stage.sh          # build + stage the test saves; quit after the letter
./test/run-bao-assert.sh bao1    # load + assert one scenario, write test-results-bao1.json
```

Four scenarios (pass the name to `run-bao-assert.sh`):

- **bao1** - the order fix: revolver equipped, bolt-action carried, target parked between the two ranges; the order
  exists where vanilla returns null, and swaps-and-fires.
- **bao2** - the idle auto-switch stays off when its toggle is off.
- **bao3** - the idle switch draws Simple Sidearms' higher-DPS pick, not merely the longest-range gun.
- **bao4** - a force-unarmed pawn is left alone.

Details and recorded passes: [`TESTPLAN.md`](TESTPLAN.md).

## Thanks

- **PeteTimesSix** for [Simple Sidearms](https://github.com/PeteTimesSix/SimpleSidearms).

## License

This mod's code is [MIT](LICENSE).

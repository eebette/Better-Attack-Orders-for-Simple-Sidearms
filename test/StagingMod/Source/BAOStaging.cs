using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PeteTimesSix.SimpleSidearms;
using RimWorld;
using SimpleSidearms.rimworld;
using Verse;
using Verse.AI;

namespace BAOTestStaging
{
    /// <summary>
    /// Stages BAO-1-attack-order (-quicktest -baostage): colonist "Rangey" with a
    /// short-range autopistol EQUIPPED and a long-range bolt-action rifle in
    /// inventory; a disarmed hostile parked between the two ranges. Runs on a
    /// VANILLA-ONLY modlist — that is the point of this mod.
    /// </summary>
    public class BAOStagingComponent : GameComponent
    {
        public BAOStagingComponent(Game game)
        {
        }

        public override void StartedNewGame()
        {
            if (!GenCommandLine.CommandLineArgPassed("baostage"))
            {
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    Stage();
                }
                catch (Exception e)
                {
                    Log.Error("[BAOStaging] Staging failed: " + e);
                }
            });
        }

        private void Stage()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[BAOStaging] No map; launch with -quicktest -baostage.");
                return;
            }
            IntVec3 anchor = ComputeAnchor(map);

            var request = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                          PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                          canGeneratePawnRelations: false, colonistRelationChanceFactor: 0f);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.Name = new NameTriple("Test", "Rangey", "BAO");
            pawn.equipment?.DestroyAllEquipment();
            pawn.inventory?.DestroyAll();
            SkillRecord shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            if (shooting != null)
            {
                shooting.Level = 10;
            }
            GenSpawn.Spawn(pawn, FindCell(map, anchor), map);

            var pistol = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Revolver"));
            pawn.equipment.AddEquipment(pistol);
            var rifle = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
            pawn.inventory.innerContainer.TryAdd(rifle, true);
            CompSidearmMemory.GetMemoryCompForPawn(pawn)?.InformOfAddedSidearm(rifle);

            Faction pirates = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate);
            if (pirates != null)
            {
                var rr = new PawnGenerationRequest(
                    DefDatabase<PawnKindDef>.GetNamedSilentFail("Pirate_Gunner") ?? PawnKindDefOf.Drifter,
                    pirates, PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false);
                Pawn raider = PawnGenerator.GeneratePawn(rr);
                raider.equipment?.DestroyAllEquipment();
                GenSpawn.Spawn(raider, FindCell(map, anchor + new IntVec3(35, 0, 0)), map);
                Verse.AI.Group.LordMaker.MakeNewLord(pirates,
                    new LordJob_AssaultColony(pirates, canKidnap: false, canTimeoutOrFlee: false,
                        sappers: false, useAvoidGridSmart: false, canSteal: false), map,
                    new List<Pawn> { raider });
            }

            GameDataSaveLoader.SaveGame("BAO-1-attack-order");
            Find.TickManager.Pause();
            Log.Message("[BAOStaging] BAO save created.");
            Find.LetterStack.ReceiveLetter("BAO save created", "BAO-1-attack-order written.", LetterDefOf.PositiveEvent);
        }

        private static IntVec3 ComputeAnchor(Map map)
        {
            bool Valid(IntVec3 c) => c.Standable(map) && !c.Fogged(map);
            if (CellFinder.TryFindRandomCellNear(map.Center, map, 30, Valid, out IntVec3 cell))
            {
                return cell;
            }
            CellFinderLoose.TryGetRandomCellWith(Valid, map, 1000, out cell);
            return cell.IsValid ? cell : map.Center;
        }

        private static IntVec3 FindCell(Map map, IntVec3 near)
        {
            IntVec3 root = near.ClampInsideMap(map);
            if (CellFinder.TryFindRandomCellNear(root, map, 15, c => c.Standable(map) && !c.Fogged(map), out IntVec3 cell))
            {
                return cell;
            }
            return map.Center;
        }
    }

    /// <summary>Assert runner: -celoadsave=BAO-1-attack-order -ceassert=bao1.
    /// Owns the "bao" scenario prefix.</summary>
    [StaticConstructorOnStartup]
    public static class BAOTestBoot
    {
        static BAOTestBoot()
        {
            Log.Message("[BAOStaging] assembly loaded.");
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out string scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("bao"))
            {
                return;
            }
            if (GenCommandLine.TryGetCommandLineArg("celoadsave", out string save) && !save.NullOrEmpty())
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Log.Message($"[BAOTest] Auto-loading save '{save}'.");
                    GameDataSaveLoader.LoadGame(save);
                });
            }
        }
    }

    public class BAOTestRunnerComponent : GameComponent
    {
        private bool active;
        private bool done;
        private int startTick;
        private int phase;
        private string scenario;
        private Pawn rangey;
        private Pawn raider;
        private Action orderAction;
        private readonly List<string> results = new List<string>();
        private bool failed;

        public BAOTestRunnerComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            if (!GenCommandLine.TryGetCommandLineArg("ceassert", out scenario)
                || scenario.NullOrEmpty() || !scenario.StartsWith("bao"))
            {
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                rangey = Find.CurrentMap.mapPawns.FreeColonistsSpawned
                    .FirstOrDefault(p => p.Name is NameTriple nt && nt.Nick == "Rangey");
                raider = Find.CurrentMap.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(p => p.HostileTo(Faction.OfPlayer) && !p.Dead);
                if (rangey == null || raider == null)
                {
                    Fail("staging pawns missing");
                    Finish();
                    return;
                }
                active = true;
                startTick = Find.TickManager.TicksGame;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                Log.Message($"[BAOTest] {scenario} started.");
            });
        }

        private void Check(string name, bool pass, string detail)
        {
            results.Add($"{{\"name\": \"{name}\", \"passed\": {(pass ? "true" : "false")}, \"detail\": \"{detail.Replace("\"", "'")}\"}}");
            if (!pass)
            {
                failed = true;
            }
            Log.Message($"[BAOTest] {name}: {(pass ? "PASS" : "FAIL")} - {detail}");
        }

        private void Fail(string reason)
        {
            Check("setup", false, reason);
        }

        public override void GameComponentTick()
        {
            if (!active || done)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (Find.TickManager.Paused || Find.TickManager.CurTimeSpeed != TimeSpeed.Superfast)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
            }
            if (tick % 30 != 0)
            {
                return;
            }

            if (scenario == "bao2")
            {
                TickBao2(tick);
                return;
            }

            if (phase == 0)
            {
                // bao1 tests the ORDER fix in isolation — the v1.1 idle auto-switch
                // (default ON) otherwise fires the instant the pawn is drafted (the
                // Wait job's init runs CheckForAutoAttack) and dissolves the deadlock
                // before the order path is ever exercised.
                BetterAttackOrders.BAOMod.Settings.autoSwitchWhenIdle = false;
                // CONSTRUCT the precondition instead of assuming it (SS's own logic may
                // have re-equipped the rifle on load): revolver in hand, rifle in
                // inventory, raider parked between the two ranges (computed at runtime —
                // hardcoded distances lose to vanilla def changes).
                ThingWithComps Find(string defName) =>
                    rangey.GetCarriedWeapons(includeEquipped: true, includeTools: true)
                        .FirstOrDefault(t => t.def.defName == defName);
                ThingWithComps revolver = Find("Gun_Revolver");
                ThingWithComps rifle = Find("Gun_BoltActionRifle");
                if (revolver == null || rifle == null)
                {
                    Check("setup", false, $"weapons missing: revolver={(revolver != null)} rifle={(rifle != null)}");
                    Finish();
                    return;
                }
                if (rangey.equipment.Primary != revolver)
                {
                    PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment
                        .equipSpecificWeaponFromInventory(rangey, revolver, dropCurrent: false, intentionalDrop: false);
                }
                float equippedRange = rangey.equipment.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 0f;
                float rifleRange = rifle.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                int park = (int)Math.Min(equippedRange + 3f, rifleRange - 2f);
                if (park < 5)
                {
                    Check("setup", false, $"degenerate ranges: equipped={equippedRange:F1} rifle={rifleRange:F1}");
                    Finish();
                    return;
                }
                IntVec3 cell = (rangey.Position + new IntVec3(park, 0, 0)).ClampInsideMap(rangey.Map);
                if (!cell.Standable(rangey.Map))
                {
                    CellFinder.TryFindRandomCellNear(cell, rangey.Map, 8, c => c.Standable(rangey.Map), out cell);
                }
                raider.Position = cell;
                raider.Notify_Teleported();

                float distance = raider.Position.DistanceTo(rangey.Position);
                Verb equipped = rangey.equipment.PrimaryEq?.PrimaryVerb;
                bool pistolBlocked = equipped != null && !equipped.CanHitTarget(raider);
                Check("scenario-is-the-deadlock", pistolBlocked,
                    $"dist={distance:F1} equippedRange={equipped?.verbProps?.range:F1} rifleRange={rifleRange:F1} canHit={!pistolBlocked}");

                rangey.drafter.Drafted = true; // the order API requires draft
                orderAction = FloatMenuUtility.GetRangedAttackAction(rangey, new LocalTargetInfo(raider), out string failStr);
                Check("order-available-with-patch", orderAction != null,
                    $"action={(orderAction != null ? "present" : "NULL")} failStr='{failStr}'");

                // Label annotation: rescued option must name the weapon; a non-rescued
                // (in-range) option must keep the untouched vanilla label.
                var options = RimWorld.FloatMenuMakerMap.GetOptions(
                    new List<Pawn> { rangey }, raider.DrawPos, out _);
                string fireLabel = options.FirstOrDefault(o =>
                    o?.Label != null && o.Label.StartsWith("FireAt".Translate(raider.Label, raider)))?.Label;
                bool annotated = fireLabel != null && fireLabel.Contains("using")
                                 && fireLabel.ToLowerInvariant().Contains("bolt-action");
                Check("rescued-label-names-weapon", annotated, $"label='{fireLabel ?? "none"}'");

                IntVec3 closeCell = (rangey.Position + new IntVec3(8, 0, 0)).ClampInsideMap(rangey.Map);
                if (!closeCell.Standable(rangey.Map))
                {
                    CellFinder.TryFindRandomCellNear(closeCell, rangey.Map, 6, c => c.Standable(rangey.Map), out closeCell);
                }
                IntVec3 farCell = raider.Position;
                raider.Position = closeCell;
                raider.Notify_Teleported();
                var closeOptions = RimWorld.FloatMenuMakerMap.GetOptions(
                    new List<Pawn> { rangey }, raider.DrawPos, out _);
                string closeLabel = closeOptions.FirstOrDefault(o =>
                    o?.Label != null && o.Label.StartsWith("FireAt".Translate(raider.Label, raider)))?.Label;
                bool vanillaClean = closeLabel != null && !closeLabel.Contains("using");
                Check("in-range-label-untouched", vanillaClean, $"label='{closeLabel ?? "none"}'");
                raider.Position = farCell;
                raider.Notify_Teleported();
                if (orderAction == null)
                {
                    Finish();
                    return;
                }
                orderAction();
                phase = 1;
                startTick = tick;
                return;
            }

            if (phase == 1)
            {
                bool rifleEquipped = rangey.equipment.Primary?.def?.defName == "Gun_BoltActionRifle";
                bool attacking = rangey.CurJobDef == JobDefOf.AttackStatic;
                if (rifleEquipped && attacking)
                {
                    Check("swap-and-attack", true, $"primary={rangey.equipment.Primary?.def?.defName} job={rangey.CurJobDef?.defName}");
                    Finish();
                    return;
                }
                if (tick - startTick > 3000)
                {
                    Check("swap-and-attack", false, $"primary={rangey.equipment.Primary?.def?.defName} job={rangey.CurJobDef?.defName}");
                    Finish();
                }
            }
        }

        /// <summary>Normalize: revolver equipped, rifle carried, raider parked
        /// between the two ranges. Returns false (and finishes) on setup failure.</summary>
        private IntVec3 parkCell = IntVec3.Invalid;

        private bool ConstructDeadlock(out float rifleRange, bool parkAtRifleEdge = false)
        {
            rifleRange = 0f;
            ThingWithComps Find(string defName) =>
                rangey.GetCarriedWeapons(includeEquipped: true, includeTools: true)
                    .FirstOrDefault(t => t.def.defName == defName);
            ThingWithComps revolver = Find("Gun_Revolver");
            ThingWithComps rifle = Find("Gun_BoltActionRifle");
            if (revolver == null || rifle == null)
            {
                Check("setup", false, $"weapons missing: revolver={(revolver != null)} rifle={(rifle != null)}");
                return false;
            }
            if (rangey.equipment.Primary != revolver)
            {
                PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment
                    .equipSpecificWeaponFromInventory(rangey, revolver, dropCurrent: false, intentionalDrop: false);
            }
            // SS swaps to its PREFERRED ranged weapon on draft (BySkill would pick the
            // rifle and dissolve the deadlock) — make the revolver the stated
            // preference so the deadlock survives drafting, exactly like a player
            // who set their short gun as default.
            CompSidearmMemory memory = CompSidearmMemory.GetMemoryCompForPawn(rangey);
            memory.primaryWeaponMode = PeteTimesSix.SimpleSidearms.Utilities.Enums.PrimaryWeaponMode.Ranged;
            memory.SetRangedWeaponTypeAsDefault(revolver.toThingDefStuffDefPair());
            float equippedRange = rangey.equipment.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 0f;
            rifleRange = rifle.def.Verbs?.FirstOrDefault()?.range ?? 0f;
            // parkAtRifleEdge: idle scenarios park deep — the raider charges, and if
            // he ever dips inside the equipped weapon's range, vanilla auto-attack
            // starts a warmup and SS's OWN warmup auto-switch fires (it swapped the
            // rifle in during an early run and contaminated the negative control).
            int park = parkAtRifleEdge
                ? (int)(rifleRange - 3f)
                : (int)Math.Min(equippedRange + 3f, rifleRange - 2f);
            if (park < 5 || park <= equippedRange)
            {
                Check("setup", false, $"degenerate ranges: equipped={equippedRange:F1} rifle={rifleRange:F1} park={park}");
                return false;
            }
            IntVec3 cell = (rangey.Position + new IntVec3(park, 0, 0)).ClampInsideMap(rangey.Map);
            if (!cell.Standable(rangey.Map))
            {
                CellFinder.TryFindRandomCellNear(cell, rangey.Map, 8, c => c.Standable(rangey.Map), out cell);
            }
            parkCell = cell;
            raider.Position = cell;
            raider.Notify_Teleported();
            return true;
        }

        /// <summary>The raider melee-charges (assault lord); shove him back to his
        /// post whenever he strays so he can never enter the equipped weapon's range.</summary>
        private void KeepRaiderParked()
        {
            if (raider == null || raider.Dead || raider.Downed || !parkCell.IsValid)
            {
                return;
            }
            if (raider.Position.DistanceTo(parkCell) > 2f)
            {
                raider.Position = parkCell;
                raider.Notify_Teleported();
            }
        }

        private void TickBao2(int tick)
        {
            if (phase == 0)
            {
                // Negative control: toggle OFF, drafted, no order — must stay idle
                // on the revolver.
                BetterAttackOrders.BAOMod.Settings.autoSwitchWhenIdle = false;
                if (!ConstructDeadlock(out _, parkAtRifleEdge: true))
                {
                    Finish();
                    return;
                }
                rangey.drafter.Drafted = true;
                Check("post-setup-state", true,
                    $"equipped={rangey.equipment.Primary?.def?.defName} mode={CompSidearmMemory.GetMemoryCompForPawn(rangey).primaryWeaponMode} default={CompSidearmMemory.GetMemoryCompForPawn(rangey).DefaultRangedWeapon?.thing?.defName}");
                phase = 1;
                startTick = tick;
                return;
            }
            if (phase == 1)
            {
                KeepRaiderParked();
                bool swapped = rangey.equipment.Primary?.def?.defName == "Gun_BoltActionRifle";
                bool attacking = rangey.CurJobDef == JobDefOf.AttackStatic || rangey.stances.curStance is Stance_Warmup;
                if (swapped || attacking)
                {
                    Check("off-stays-idle", false,
                        $"acted with toggle OFF: primary={rangey.equipment.Primary?.def?.defName} job={rangey.CurJobDef?.defName} patchSwaps={BetterAttackOrders.JobDriver_Wait_CheckForAutoAttack_Patch.SwapCount} toggle={BetterAttackOrders.BAOMod.Settings.autoSwitchWhenIdle}");
                    Finish();
                    return;
                }
                if (tick - startTick > 1800)
                {
                    Check("off-stays-idle", true, $"held revolver, idle for 1800 ticks");
                    BetterAttackOrders.BAOMod.Settings.autoSwitchWhenIdle = true;
                    phase = 2;
                    startTick = tick;
                }
                return;
            }
            if (phase == 2)
            {
                KeepRaiderParked();
                bool swapped = rangey.equipment.Primary?.def?.defName == "Gun_BoltActionRifle";
                bool engaging = rangey.stances.curStance is Stance_Warmup
                                || rangey.CurJobDef == JobDefOf.AttackStatic
                                || (raider.Dead || raider.Downed);
                if (swapped && engaging)
                {
                    Check("on-swaps-and-engages", true, $"primary={rangey.equipment.Primary?.def?.defName} stance={rangey.stances.curStance?.GetType().Name} raiderDead={raider.Dead}");
                    Finish();
                    return;
                }
                if (tick - startTick > 4000)
                {
                    Check("on-swaps-and-engages", false, $"primary={rangey.equipment.Primary?.def?.defName} stance={rangey.stances.curStance?.GetType().Name} job={rangey.CurJobDef?.defName}");
                    Finish();
                }
            }
        }

        private void Finish()
        {
            done = true;
            var sb = new StringBuilder();
            sb.Append($"{{\n  \"scenario\": \"{scenario}\",\n");
            sb.Append($"  \"passed\": {(!failed ? "true" : "false")},\n");
            sb.Append("  \"checks\": [\n    ");
            sb.Append(string.Join(",\n    ", results));
            sb.Append("\n  ]\n}\n");
            string path = Path.Combine(GenFilePaths.SaveDataFolderPath, $"test-results-{scenario}.json");
            File.WriteAllText(path, sb.ToString());
            Log.Message("[BAOTest] Results written; shutting down.");
            Root.Shutdown();
        }
    }
}

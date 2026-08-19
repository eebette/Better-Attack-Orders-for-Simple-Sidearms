using System.Linq;
using HarmonyLib;
using PeteTimesSix.SimpleSidearms;
using PeteTimesSix.SimpleSidearms.Utilities;
using RimWorld;
using SimpleSidearms.rimworld;
using Verse;
using Verse.AI;

namespace BetterAttackOrders
{
    /// <summary>
    /// The autonomous sibling of the order fix: a drafted pawn standing on wait
    /// duty auto-attacks only what the EQUIPPED verb can hit
    /// (JobDriver_Wait.CheckForAutoAttack → BestShootTargetFromCurrentPosition
    /// with the current verb) — with a longer-ranged sidearm carried, it just
    /// stands there. This postfix runs after vanilla found nothing: if a carried
    /// weapon could engage a target from where the pawn stands, swap to it; the
    /// next auto-attack check engages normally.
    ///
    /// Consent guards: toggleable (default on); drafted pawns on wait jobs only
    /// (undrafted AI untouched); hold fire respected; pawns with a forced weapon
    /// are never second-guessed; only fires when NOTHING is in equipped range —
    /// pure idle rescue, never overriding an active fight. Throttled to one scan
    /// per pawn per 60 ticks.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class JobDriver_Wait_CheckForAutoAttack_Patch
    {
        public static int SwapCount; // test forensics

        [HarmonyPostfix]
        public static void Postfix(JobDriver_Wait __instance)
        {
            if (!BAOMod.Settings.autoSwitchWhenIdle)
            {
                return;
            }
            Pawn pawn = __instance.pawn;
            // No extra throttle: vanilla already rate-limits CheckForAutoAttack to a
            // ~4-tick hash interval, and 1.6's bucketed tickIntervalAction deltas make
            // exact-tick IsHashIntervalTick gates here miss almost every invocation.
            if (pawn == null || !pawn.Drafted || pawn.Downed
                || !(pawn.drafter?.FireAtWill ?? false)
                || pawn.stances.curStance is Stance_Busy
                || pawn.equipment == null || pawn.inventory == null
                || !pawn.IsValidSidearmsCarrierRightNow()
                || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }
            CompSidearmMemory memory = CompSidearmMemory.GetMemoryCompForPawn(pawn, fillExistingIfCreating: false);
            if (memory?.ForcedWeapon != null || memory?.ForcedWeaponWhileDrafted != null)
            {
                return; // explicit player weapon choice — never second-guess it
            }

            // Vanilla just ran with the equipped verb; if it found something the
            // pawn is already engaging (Stance_Busy above) or will. Re-check
            // cheaply: anything in equipped range → not our case.
            Verb equippedVerb = pawn.equipment.PrimaryEq?.PrimaryVerb;
            if (equippedVerb != null && equippedVerb.Available()
                && (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(
                    pawn, TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable) != null)
            {
                return;
            }

            // Longest-reaching carried candidate that could see a target.
            ThingWithComps best = null;
            float bestRange = equippedVerb?.verbProps?.range ?? 0f;
            var carried = pawn.GetCarriedWeapons(includeEquipped: false, includeTools: false);
            for (int i = 0; i < carried.Count; i++)
            {
                ThingWithComps weapon = carried[i];
                if (!weapon.def.IsRangedWeapon
                    || GettersFilters.isManualUse(weapon)
                    || GettersFilters.isDangerousWeapon(weapon)
                    || GettersFilters.isEMPWeapon(weapon))
                {
                    continue;
                }
                var verbs = weapon.def.Verbs;
                float range = (verbs != null && verbs.Count > 0) ? verbs[0].range : 0f;
                if (range > bestRange)
                {
                    bestRange = range;
                    best = weapon;
                }
            }
            if (best == null)
            {
                return;
            }
            Thing target = (Thing)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                maxDist: bestRange);
            if (target == null)
            {
                return; // nothing the longer gun could fight either — stay put
            }

            SwapCount++;
            WeaponAssingment.equipSpecificWeaponFromInventory(pawn, best, dropCurrent: false, intentionalDrop: false);
            // Next CheckForAutoAttack tick engages with the new verb naturally.
        }
    }
}

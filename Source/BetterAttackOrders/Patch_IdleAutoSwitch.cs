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
    /// stands there. This postfix runs after vanilla found nothing and, if a
    /// carried weapon could engage a target from where the pawn stands, swaps to
    /// it; the next auto-attack check engages normally.
    ///
    /// Two steps, so the idle path picks the same weapon the right-click order
    /// would (they share RescueLogic):
    ///   1. DETECT — is there any target within the pawn's LONGEST carried reach?
    ///      That is the "worth switching at all" gate, computed on the maximum
    ///      possible range so nothing engageable is missed.
    ///   2. SELECT — hand that target to RescueLogic.FindReachingWeapon: Simple
    ///      Sidearms' own findBestRangedWeapon pick (CE-aware ammo/ballistics when
    ///      the compat patch is present), with a longest-reach fallback, filtered
    ///      to a carried weapon that actually reaches. So the pawn draws the BEST
    ///      reaching gun, not merely the longest.
    ///
    /// Consent guards: toggleable (default on); drafted pawns on wait jobs only
    /// (undrafted AI untouched); hold fire respected; pawns with a forced weapon
    /// are never second-guessed; only fires when NOTHING is in equipped range —
    /// pure idle rescue, never overriding an active fight. No extra throttle:
    /// vanilla already rate-limits CheckForAutoAttack.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class JobDriver_Wait_CheckForAutoAttack_Patch
    {
        public static int SwapCount;             // test forensics
        public static ThingDef FirstDrawnDef;    // test forensics: the FIRST gun this path
                                                 // selected. Only the first draw reflects the
                                                 // selection on the pristine idle state — later
                                                 // firings re-pick as the equipped gun changes,
                                                 // and SS's warmup auto-switch re-picks the
                                                 // final weapon independently.

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
            // The two scans below run only after every cheap guard has passed.
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

            // DETECTION: the longest reach among eligible carried ranged weapons.
            // Same eligibility filter RescueLogic uses (ranged, not manual/dangerous/
            // EMP), so a weapon that sets maxReach is always one selection can return.
            float maxReach = 0f;
            foreach (ThingWithComps weapon in pawn.GetCarriedWeapons(includeEquipped: false, includeTools: false))
            {
                if (!weapon.def.IsRangedWeapon
                    || GettersFilters.isManualUse(weapon)
                    || GettersFilters.isDangerousWeapon(weapon)
                    || GettersFilters.isEMPWeapon(weapon))
                {
                    continue;
                }
                float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                if (range > maxReach)
                {
                    maxReach = range;
                }
            }
            if (maxReach <= 0f)
            {
                return; // no carried ranged weapon to switch to
            }

            // Is any target reachable at all if the pawn switched to its longest
            // gun? Nothing sits in equipped range (checked above), so a hit here is
            // strictly a target a carried weapon could newly engage.
            Thing target = (Thing)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                maxDist: maxReach);
            if (target == null)
            {
                return; // candidates == 0 — stay put
            }

            // SELECTION: the shared, SS/CE-aware, reach-checked pick — identical to
            // the right-click order. Non-null whenever a target was found within
            // maxReach (the longest gun reaches it, so the fallback always has one).
            ThingWithComps winner = RescueLogic.FindReachingWeapon(pawn, new LocalTargetInfo(target));
            if (winner == null || winner == pawn.equipment.Primary)
            {
                return;
            }

            SwapCount++;
            if (FirstDrawnDef == null)
            {
                FirstDrawnDef = winner.def;
            }
            WeaponAssingment.equipSpecificWeaponFromInventory(pawn, winner, dropCurrent: false, intentionalDrop: false);
            // Next CheckForAutoAttack tick engages with the new verb naturally.
        }
    }
}

using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Patches vanilla's idle auto-attack (JobDriver_Wait.CheckForAutoAttack) to swap a drafted
    /// guarding pawn to the carried weapon SS scores best for the target when its equipped weapon
    /// can hit nothing but a carried one can.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Wait), "CheckForAutoAttack")]
    public static class JobDriver_Wait_CheckForAutoAttack_Patch
    {
        public static bool Prepare() => BAOGuard.Require(typeof(JobDriver_Wait), "CheckForAutoAttack",
            Type.EmptyTypes,
            "the idle auto-switch is inactive; a guarding pawn will stand still with a longer-ranged sidearm carried.");

        public static int SwapCount;             // test forensics
        public static ThingDef FirstDrawnDef;    // test forensics: the FIRST gun  selected.

        [HarmonyPostfix]
        public static void Postfix(JobDriver_Wait __instance)
        {
            try
            {
                PostfixInner(__instance);
            }
            catch (Exception e)
            {
                Log.ErrorOnce(BAOGuard.LogPrefix + "Idle auto-switch failed; the pawn is left as vanilla placed it. " + e, 0x0BA00002);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostfixInner(JobDriver_Wait __instance)
        {
            if (!BAOMod.Settings.autoSwitchWhenIdle)
            {
                return;
            }
            Pawn pawn = __instance.pawn;
            // The two scans below run only after every cheap guard has passed.
            if (pawn == null || !pawn.Drafted || pawn.Downed
                || !(pawn.drafter?.FireAtWill ?? false)
                || __instance.job?.def != JobDefOf.Wait_Combat
                || !(__instance.job?.canUseRangedWeapon ?? false)
                || pawn.stances.curStance is Stance_Busy
                || pawn.equipment == null || pawn.inventory == null
                || !pawn.IsValidSidearmsCarrierRightNow()
                || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return;
            }
            CompSidearmMemory memory = CompSidearmMemory.GetMemoryCompForPawn(pawn, fillExistingIfCreating: false);
            if (memory?.IsCurrentWeaponForced(alsoCountPreferredOrDefault: false) ?? false)
            {
                return; // forced weapon OR forced-unarmed - the player's explicit choice
            }

            // Bail if the equipped weapon can already hit a target from here.
            Verb equippedVerb = pawn.equipment.PrimaryEq?.PrimaryVerb;
            if (equippedVerb != null && equippedVerb.Available()
                && (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(
                    pawn, TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable) != null)
            {
                return;
            }

            // The longest EFFECTIVE reach among eligible carried ranged weapons.
            float maxReach = 0f;
            foreach (ThingWithComps weapon in pawn.GetCarriedWeapons(includeEquipped: false, includeTools: false))
            {
                if (!RescueLogic.IsEligibleCarried(pawn, weapon))
                {
                    continue;
                }
                float range = RescueLogic.EffectiveRange(pawn, weapon);
                if (range > maxReach)
                {
                    maxReach = range;
                }
            }
            if (maxReach <= 0f)
            {
                return; // no carried ranged weapon to switch to
            }

            // Is any target reachable at all if the pawn switched to its longest gun?
            Thing target = (Thing)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                maxDist: maxReach);
            if (target == null)
            {
                return; // candidates == 0 - stay put
            }

            // The shared, SS/CE-aware, reach-checked pick (same as the right-click order).
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

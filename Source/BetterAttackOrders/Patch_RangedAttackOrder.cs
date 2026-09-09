using System;
using System.Collections.Generic;
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
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        public const string HarmonyId = "eebette.BetterAttackOrders";

        static Bootstrap()
        {
            var harmony = new Harmony(HarmonyId);
            int applied = 0;
            var failures = new List<string>();
            foreach (Type type in typeof(Bootstrap).Assembly.GetTypes())
            {
                try
                {

                    if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length == 0)
                    {
                        continue;
                    }
                    // A Prepare-false class returns no patched methods and is SKIPPED.
                    var patched = harmony.CreateClassProcessor(type).Patch();
                    if (patched != null && patched.Count > 0)
                    {
                        applied++;
                    }
                }
                catch (Exception e)
                {
                    failures.Add(type.Name);
                    Log.Error($"{BAOGuard.LogPrefix}Patch class {type.Name} could not be applied - "
                              + $"that one feature is inactive, the others still work. {e}");
                }
            }
            if (failures.Count > 0)
            {
                Log.Warning($"{BAOGuard.LogPrefix}Installed {applied} patch class(es); "
                            + $"{failures.Count} failed ({string.Join(", ", failures)}).");
            }
            else
            {
                Log.Message($"{BAOGuard.LogPrefix}Installed {applied} patch class(es).");
            }
        }
    }

    /// <summary>Shared failure-doctrine guard: every patch's Prepare() proves its
    /// target still exists.</summary>
    internal static class BAOGuard
    {
        internal const string LogPrefix = "[Better Attack Orders] ";

        internal static bool Require(Type type, string method, Type[] args, string consequence)
        {
            if (AccessTools.Method(type, method, args) != null)
            {
                return true;
            }
            Log.Error($"{LogPrefix}{type.Name}.{method} not found - {consequence} "
                      + "RimWorld or Simple Sidearms probably moved it.");
            return false;
        }
    }

    /// <summary>
    /// Patches vanilla's ranged-attack order (FloatMenuUtility.GetRangedAttackAction) to re-enable
    /// it via a carried sidearm when the equipped weapon can't reach the target but a carried one can.
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetRangedAttackAction),
                  new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(string) },
                  new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    public static class FloatMenuUtility_GetRangedAttackAction_Patch
    {
        public static bool Prepare() => BAOGuard.Require(typeof(FloatMenuUtility), "GetRangedAttackAction",
            new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(string).MakeByRefType() },
            "an out-of-range attack order will not consider carried sidearms (the deadlock this mod fixes returns).");

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, LocalTargetInfo target, ref string failStr, ref Action __result)
        {
            try
            {
                PostfixInner(pawn, target, ref failStr, ref __result);
            }
            catch (Exception e)
            {
                Log.ErrorOnce(BAOGuard.LogPrefix + "Attack-order rescue failed; the vanilla order stands. " + e, 0x0BA00001);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostfixInner(Pawn pawn, LocalTargetInfo target, ref string failStr, ref Action __result)
        {
            if (__result != null || pawn == null || !target.IsValid)
            {
                return;
            }
            // Rescue ONLY the failure this mod fixes: a drafted pawn whose EQUIPPED
            // weapon can't hit the target.
            if (!RescueLogic.WouldRescue(pawn, target, out ThingWithComps winner))
            {
                return;
            }

            failStr = null;
            __result = () =>
            {
                try
                {
                    // Re-validate at CLICK time.
                    if (RescueLogic.WouldRescue(pawn, target, out ThingWithComps freshWinner))
                    {
                        WeaponAssingment.equipSpecificWeaponFromInventory(pawn, freshWinner, dropCurrent: false, intentionalDrop: false);
                    }
                }
                catch (Exception e)
                {
                    Log.ErrorOnce(BAOGuard.LogPrefix + "Attack-order weapon swap failed; firing with the equipped weapon. " + e, 0x0BA00004);
                }
                // The ordered attack issues regardless - the player asked to fire here.
                Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };
        }
    }

    /// <summary>Shared between the action patch and the label patch so the two can
    /// never disagree about when a rescue happens or which weapon it uses.</summary>
    public static class RescueLogic
    {
        /// <summary>True when this order would be OUR rescued order: drafted pawn,
        /// equipped weapon can't hit, and a carried weapon can.</summary>
        public static bool WouldRescue(Pawn pawn, LocalTargetInfo target, out ThingWithComps winner)
        {
            winner = null;
            if (pawn == null || !target.IsValid || !pawn.Drafted || pawn.Downed
                || pawn.WorkTagIsDisabled(WorkTags.Violent)
                || pawn.equipment == null || pawn.inventory == null
                || !pawn.IsValidSidearmsCarrierRightNow())
            {
                return false;
            }
            if (CompSidearmMemory.GetMemoryCompForPawn(pawn, fillExistingIfCreating: false)
                    ?.IsCurrentWeaponForced(alsoCountPreferredOrDefault: false) ?? false)
            {
                return false; // forced weapon / forced-unarmed
            }
            Verb equippedVerb = pawn.equipment.PrimaryEq?.PrimaryVerb;
            if (equippedVerb != null && equippedVerb.CanHitTarget(target))
            {
                return false;
            }
            winner = FindReachingWeapon(pawn, target);
            return winner != null;
        }

        /// <summary>
        /// The carried weapon SS picks for this target.
        /// </summary>
        public static ThingWithComps FindReachingWeapon(Pawn pawn, LocalTargetInfo target)
        {
            var (best, _, _) = GettersFilters.findBestRangedWeapon(pawn, target);
            return best != null && best != pawn.equipment.Primary && WithinWindow(pawn, best, target)
                ? best
                : null;
        }

        /// <summary>One eligibility rule for the whole mod (the order fix's fallback
        /// and the idle switch's detection): a ranged carried weapon that is not the
        /// equipped gun and one Simple Sidearms would actually let the pawn wield -
        /// its skip flags (manual-use, EMP, dangerous) AND usability (canUseSidearmInstance:
        /// bladelink/biocode/role, unless AllowBlockedWeaponUse).</summary>
        public static bool IsEligibleCarried(Pawn pawn, ThingWithComps weapon)
        {
            return weapon.def.IsRangedWeapon
                   && weapon != pawn.equipment?.Primary
                   && !GettersFilters.isManualUse(weapon)
                   && !GettersFilters.isDangerousWeapon(weapon)
                   && !GettersFilters.isEMPWeapon(weapon)
                   && (PeteTimesSix.SimpleSidearms.SimpleSidearms.Settings.AllowBlockedWeaponUse
                       || StatCalculator.canUseSidearmInstance(weapon, pawn, out _));
        }

        /// <summary>A carried weapon's MAX engage range.</summary>
        public static float EffectiveRange(Pawn pawn, ThingWithComps weapon)
        {
            Verb verb = weapon.TryGetComp<CompEquippable>()?.PrimaryVerb;
            VerbProperties props = verb?.verbProps ?? weapon.def.Verbs?.FirstOrDefault();
            if (props == null)
            {
                return 0f;
            }
            return verb != null ? props.AdjustedRange(verb, pawn) : props.range;
        }

        /// <summary>SS's two-sided range window for this carried weapon against this
        /// target, plus the line of sight the pawn needs from where it stands.</summary>
        public static bool WithinWindow(Pawn pawn, ThingWithComps weapon, LocalTargetInfo target)
        {
            Verb verb = weapon.TryGetComp<CompEquippable>()?.PrimaryVerb;
            VerbProperties props = verb?.verbProps ?? weapon.def.Verbs?.FirstOrDefault();
            if (props == null)
            {
                return false;
            }
            float distance = target.Cell.DistanceTo(pawn.Position);
            float max = verb != null ? props.AdjustedRange(verb, pawn) : props.range;
            float min = props.EffectiveMinRange(target, pawn);
            return distance >= min && distance <= max
                   && GenSight.LineOfSight(pawn.Position, target.Cell, pawn.Map, skipFirstCell: true);
        }
    }
}

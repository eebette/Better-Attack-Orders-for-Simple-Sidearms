using System;
using System.Collections.Generic;
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
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        public const string HarmonyId = "eebette.BetterAttackOrders";

        static Bootstrap()
        {
            // Per class, not PatchAll: an upstream member moving (RimWorld or Simple
            // Sidearms renames a method or a parameter — Harmony binds parameters by
            // name, invisible to a Prepare guard) costs THAT one patch with a named,
            // player-readable error, not the whole mod half-applied mid-assembly.
            var harmony = new Harmony(HarmonyId);
            int applied = 0;
            var failures = new List<string>();
            foreach (Type type in typeof(Bootstrap).Assembly.GetTypes())
            {
                try
                {
                    // Attribute probe INSIDE the try: decoding [HarmonyPatch] resolves
                    // its typeof() args, so upstream type-level drift there also costs
                    // one class, not the loop.
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
                    Log.Error($"{BAOGuard.LogPrefix}Patch class {type.Name} could not be applied — "
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
    /// target still exists and, if not, logs a named player-readable consequence and
    /// returns false so the class is skipped (inert) while the others still apply.</summary>
    internal static class BAOGuard
    {
        internal const string LogPrefix = "[Better Attack Orders] ";

        internal static bool Require(Type type, string method, Type[] args, string consequence)
        {
            if (AccessTools.Method(type, method, args) != null)
            {
                return true;
            }
            Log.Error($"{LogPrefix}{type.Name}.{method} not found — {consequence} "
                      + "RimWorld or Simple Sidearms probably moved it.");
            return false;
        }
    }

    /// <summary>
    /// The vanilla ranged-attack order is validated against the EQUIPPED weapon
    /// only: FloatMenuUtility.GetRangedAttackAction returns null (and the float
    /// menu shows "Cannot fire: out of range") even when a carried sidearm could
    /// reach the target — and because no attack job can form, Simple Sidearms'
    /// warmup auto-switch never gets a chance to run. Deadlock.
    ///
    /// This postfix runs only when vanilla found NO action: if a carried weapon
    /// (chosen by SS's own selection, so forced-weapon settings and skip flags are
    /// respected) can reach the target from where the pawn stands, the order
    /// becomes available again — clicking it swaps to that weapon and issues the
    /// exact same attack job vanilla would have. Single-option repair: no new
    /// menu entries, the existing order just works.
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
            if (__result != null || pawn == null || !target.IsValid)
            {
                return;
            }
            // Rescue ONLY the failure this mod fixes: a drafted pawn whose EQUIPPED
            // weapon can't hit the target. Every other vanilla refusal (not drafted,
            // downed, incapable of violence, ...) must stand untouched.
            if (!RescueLogic.WouldRescue(pawn, target, out ThingWithComps winner))
            {
                return;
            }

            failStr = null;
            __result = () =>
            {
                WeaponAssingment.equipSpecificWeaponFromInventory(pawn, winner, dropCurrent: false, intentionalDrop: false);
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
        /// equipped weapon can't hit, and a carried weapon can. Outputs the weapon.</summary>
        public static bool WouldRescue(Pawn pawn, LocalTargetInfo target, out ThingWithComps winner)
        {
            winner = null;
            if (pawn == null || !target.IsValid || !pawn.Drafted
                || pawn.equipment == null || pawn.inventory == null
                || !pawn.IsValidSidearmsCarrierRightNow())
            {
                return false;
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
        /// The carried weapon SS itself would pick for this target, provided it can
        /// actually reach from the pawn's current position. Falls back to the
        /// longest-reaching eligible carried weapon when SS's pick can't reach.
        /// </summary>
        public static ThingWithComps FindReachingWeapon(Pawn pawn, LocalTargetInfo target)
        {
            float distance = target.Cell.DistanceTo(pawn.Position);

            bool Reaches(ThingWithComps weapon)
            {
                float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                return range >= distance
                       && GenSight.LineOfSight(pawn.Position, target.Cell, pawn.Map, skipFirstCell: true);
            }

            // SS's own choice first — respects forced weapons, preferences, and
            // (when the CE+SS compat suite is present) its CE-corrected scoring.
            var (best, _, _) = GettersFilters.findBestRangedWeapon(pawn, target);
            if (best != null && best != pawn.equipment.Primary && Reaches(best))
            {
                return best;
            }

            return pawn.GetCarriedWeapons(includeEquipped: false, includeTools: false)
                .Where(w => IsEligibleCarried(pawn, w))
                .Where(Reaches)
                .OrderByDescending(w => w.def.Verbs?.FirstOrDefault()?.range ?? 0f)
                .FirstOrDefault();
        }

        /// <summary>One eligibility rule for the whole mod (the order fix's fallback
        /// and the idle switch's detection): a ranged carried weapon that is not the
        /// equipped gun and not one Simple Sidearms itself skips (manual-use, EMP,
        /// dangerous). Selection and scoring stay SS's — this is only the gate.</summary>
        public static bool IsEligibleCarried(Pawn pawn, ThingWithComps weapon)
        {
            return weapon.def.IsRangedWeapon
                   && weapon != pawn.equipment?.Primary
                   && !GettersFilters.isManualUse(weapon)
                   && !GettersFilters.isDangerousWeapon(weapon)
                   && !GettersFilters.isEMPWeapon(weapon);
        }
    }
}

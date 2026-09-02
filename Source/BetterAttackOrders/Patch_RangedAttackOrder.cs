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

        // Thin outer / NoInlining inner (failure-doctrine layer 3): the inner body
        // references SS members the JIT resolves only when it first compiles — an SS
        // rename would throw from inside the hook, uncatchable by the Prepare guard;
        // the try here turns that into a one-time error, original order intact.
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
            // weapon can't hit the target. Every other vanilla refusal (not drafted,
            // downed, incapable of violence, forced weapon, ...) stands untouched —
            // WouldRescue bails on each.
            if (!RescueLogic.WouldRescue(pawn, target, out ThingWithComps winner))
            {
                return;
            }

            failStr = null;
            __result = () =>
            {
                // This closure runs LATER, on click — outside PostfixInner's try — and
                // is the one place equipSpecificWeaponFromInventory is reached, so it
                // carries its own failure-doctrine guard (an SS rename would otherwise
                // throw uncaught at click time).
                try
                {
                    // Re-validate at CLICK time, not menu-build time: the float menu does
                    // not pause the game, so the captured winner could have been hauled,
                    // equipped, or destroyed in the interim. Re-running WouldRescue picks
                    // a fresh reaching weapon (or none).
                    if (RescueLogic.WouldRescue(pawn, target, out ThingWithComps freshWinner))
                    {
                        WeaponAssingment.equipSpecificWeaponFromInventory(pawn, freshWinner, dropCurrent: false, intentionalDrop: false);
                    }
                }
                catch (Exception e)
                {
                    Log.ErrorOnce(BAOGuard.LogPrefix + "Attack-order weapon swap failed; firing with the equipped weapon. " + e, 0x0BA00004);
                }
                // The ordered attack issues regardless — the player asked to fire here.
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
        /// equipped weapon can't hit, and a carried weapon can. Outputs the weapon.
        ///
        /// Guards match the idle path's: vanilla returns "out of range" in an
        /// else-if chain BEFORE it checks incapable-of-violence, so an out-of-range
        /// refusal masks those reasons — without the Downed/Violent bail here, the
        /// rescue would re-enable an attack vanilla refuses for a NON-range reason
        /// and swap the weapon of a pawn that cannot fight. And a forced weapon (or
        /// forced-unarmed) is the player's explicit choice: SS's own auto-swap bails
        /// on IsCurrentWeaponForced, and so does this — findBestRangedWeapon does NOT
        /// consult the forced flags, so the check must live here.</summary>
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
                return false; // forced weapon / forced-unarmed — the player's call, not ours
            }
            // The out-of-range refusal is an else-if that ALSO masks vanilla's
            // ideoligion-animal refusals (an out-of-range venerated/innocent animal
            // reads "OutOfRange", not "IdeoligionForbids"). The mask means we cannot
            // read the reason from failStr, so re-check the same conditions vanilla
            // checks after its range branch, or the rescue would re-enable a shot the
            // pawn's ideoligion forbids. (Same masking class as Downed/Violent above;
            // self/same-faction are handled upstream by the provider's CanTarget.)
            if (target.Thing is Pawn victim)
            {
                if (HistoryEventUtility.IsKillingInnocentAnimal(pawn, victim)
                    && !new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal,
                        pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    return false;
                }
                if (pawn.Ideo != null && pawn.Ideo.IsVeneratedAnimal(victim)
                    && !new HistoryEvent(HistoryEventDefOf.HuntedVeneratedAnimal,
                        pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    return false;
                }
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
        /// The carried weapon SS itself would pick for this target (CE-corrected
        /// scoring when the compat suite is present), provided it can actually reach
        /// from the pawn's current position. Falls back to the longest-reaching
        /// eligible carried weapon — by EFFECTIVE range — when SS's pick can't reach.
        /// </summary>
        public static ThingWithComps FindReachingWeapon(Pawn pawn, LocalTargetInfo target)
        {
            // SS's own choice first — preferences and CE scoring.
            var (best, _, _) = GettersFilters.findBestRangedWeapon(pawn, target);
            if (best != null && best != pawn.equipment.Primary && WithinWindow(pawn, best, target))
            {
                return best;
            }

            return pawn.GetCarriedWeapons(includeEquipped: false, includeTools: false)
                .Where(w => IsEligibleCarried(pawn, w))
                .Where(w => WithinWindow(pawn, w, target))
                .OrderByDescending(w => EffectiveRange(pawn, w))
                .FirstOrDefault();
        }

        /// <summary>One eligibility rule for the whole mod (the order fix's fallback
        /// and the idle switch's detection): a ranged carried weapon that is not the
        /// equipped gun and one Simple Sidearms would actually let the pawn wield —
        /// its skip flags (manual-use, EMP, dangerous) AND usability
        /// (canUseSidearmInstance: bladelink/biocode/role, unless AllowBlockedWeaponUse).
        /// Without the usability check the fallback could pick a weapon SS's own
        /// equip then refuses, looping the switch. Selection/scoring stay SS's.</summary>
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

        /// <summary>A carried weapon's MAX engage range, computed the way vanilla and
        /// SS compute it — VerbProperties.AdjustedRange, which applies the weather
        /// max-range cap the raw def range ignores. Using the raw range diverges from
        /// SS's own selection window and, under a range cap (blizzard weather, CE
        /// range-reducing ammo), flip-flops the idle switch between two long guns.</summary>
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

        /// <summary>SS's own two-sided range window for this carried weapon against
        /// this target (EffectiveMinRange..AdjustedRange), plus the line of sight the
        /// pawn needs from where it stands. Calls the same vanilla methods SS's
        /// findBestRangedWeapon uses — not a reproduction of them.</summary>
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

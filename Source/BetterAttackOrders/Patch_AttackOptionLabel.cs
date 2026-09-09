using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterAttackOrders
{
    /// <summary>
    /// Honest labeling for the rescued order.
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedAttack), nameof(FloatMenuOptionProvider_DraftedAttack.GetOptionsFor),
                  new[] { typeof(Thing), typeof(FloatMenuContext) })]
    public static class FloatMenuOptionProvider_DraftedAttack_GetOptionsFor_Patch
    {
        public static bool Prepare() => BAOGuard.Require(typeof(FloatMenuOptionProvider_DraftedAttack), "GetOptionsFor",
            new[] { typeof(Thing), typeof(FloatMenuContext) },
            "the rescued attack order will still work but will not name the weapon it draws.");

        [HarmonyPostfix]
        public static void Postfix(Thing clickedThing, FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            try
            {
                PostfixInner(clickedThing, context, ref __result);
            }
            catch (Exception e)
            {
                Log.ErrorOnce(BAOGuard.LogPrefix + "Attack-order label annotation failed; the order works, unlabeled. " + e, 0x0BA00003);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PostfixInner(Thing clickedThing, FloatMenuContext context, ref IEnumerable<FloatMenuOption> __result)
        {
            if (context == null || clickedThing == null || context.IsMultiselect)
            {
                return;
            }
            Pawn pawn = context.FirstSelectedPawn;
            if (!RescueLogic.WouldRescue(pawn, new LocalTargetInfo(clickedThing), out ThingWithComps winner))
            {
                return;
            }
            string vanillaLabel = "FireAt".Translate(clickedThing.Label, clickedThing);
            // def label, not instance label - "bolt-action rifle", no quality/stuff
            string suffix = "BAO_UsingWeapon".Translate(winner.def.label);
            __result = Annotate(__result, vanillaLabel, suffix);
        }

        private static IEnumerable<FloatMenuOption> Annotate(IEnumerable<FloatMenuOption> options, string vanillaLabel, string suffix)
        {
            foreach (FloatMenuOption option in options)
            {
                if (option != null && option.action != null && option.Label == vanillaLabel)
                {
                    option.Label = vanillaLabel + suffix;
                }
                yield return option;
            }
        }
    }
}

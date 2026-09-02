using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterAttackOrders
{
    /// <summary>
    /// Honest labeling for the rescued order: the float-menu text names the weapon
    /// the click will draw — "Fire at raider (using bolt-action rifle)" — exactly
    /// and only when the order is ours (vanilla orders keep their untouched vanilla
    /// labels; the rescue only exists where vanilla offered nothing). The label is
    /// composed in FloatMenuOptionProvider_DraftedAttack, not in the utility the
    /// action patch hooks, hence the second patch point; RescueLogic keeps the two
    /// from ever disagreeing.
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
            // def label, not instance label — "bolt-action rifle", no quality/stuff noise
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

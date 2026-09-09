using UnityEngine;
using Verse;

namespace BetterAttackOrders
{
    public class BAOSettings : ModSettings
    {
        // ON by default.
        public bool autoSwitchWhenIdle = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoSwitchWhenIdle, "autoSwitchWhenIdle", true);
        }
    }

    public class BAOMod : Mod
    {
        public static BAOSettings Settings { get; private set; }

        public BAOMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BAOSettings>();
        }

        public override string SettingsCategory()
        {
            return "Better Attack Orders";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("Auto-switch when standing idle", ref Settings.autoSwitchWhenIdle,
                "An idle drafted pawn will automatically switch to a longer range sidearm if a target is in range when ENABLED.");
            listing.End();
        }
    }
}

using UnityEngine;
using Verse;

namespace BetterAttackOrders
{
    public class BAOSettings : ModSettings
    {
        // Install-as-consent: ON by default. The pawn only acts where it would
        // otherwise stand idle, and vanilla hold-fire remains the real
        // don't-shoot control.
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
                "A drafted pawn with nothing in range of the equipped weapon draws a carried weapon that CAN reach a target, then engages normally. Respects hold fire, never touches pawns with a forced weapon, and never runs while a target is already in range. The right-click order fix is always on.");
            listing.End();
        }
    }
}

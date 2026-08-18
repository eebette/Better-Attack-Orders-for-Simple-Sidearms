# Draft: SS upstream issue (single-topic, owner files)

**Title:** Ranged attack orders ignore carried sidearms — out-of-range deadlock

**Body:**

A pawn holding a short-range weapon with a longer-ranged sidearm in inventory
cannot be ordered to attack a target between the two ranges:

1. Colonist equips a revolver (range 25.9), carries a bolt-action rifle
   (range 36.9) as a remembered sidearm.
2. Draft, right-click a hostile ~30 cells away.
3. Float menu: "Cannot fire: out of range" — validated against the equipped
   weapon only. No attack job can form, so the aim-warmup auto-switch (the only
   in-combat swap trigger) never gets a chance to run. The player must manually
   swap via the gizmo first.

Reproduces in vanilla + Harmony + Simple Sidearms only (no other mods).
RimWorld 1.6, SS <version>.

Suggested shape: when `FloatMenuUtility.GetRangedAttackAction` fails on range
for a drafted pawn, check carried weapons; if one reaches, offer the order and
swap before the attack job (respecting forced-weapon settings).

I've published a standalone interim fix
(https://github.com/eebette/Better-Attack-Orders-for-Simple-Sidearms, MIT) and
would be happy to retire it if this lands upstream in any form.

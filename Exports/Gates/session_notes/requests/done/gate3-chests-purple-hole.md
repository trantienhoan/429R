# Request from gate 3: GateRun's chests and the purple hole (shared code)

**What:** a chest (or its seed) still waiting when the player dies in a gate is eaten by the purple hole if it lies within
14 m of the shield: GateRun drops chests under Currently Spawned Stuffs with names like
`Treasure_Chest_and_Seed_Bracer(Clone)` / `PG_Treasure_Chest_and_Seed_Chest(Clone)`, and `PurpleHole.Keeps` only spares
names (on the thing or a parent) containing Fairy/Knight (start) or Boss/_Base/Skydome/Valley/Island.

**Why:** contract #8/#9: after a revive mid-gate the round's gear chest is gone; GateRun goes on after 25 s, and only
GateExit's GearGuard sets the gear bool at the end (with its warning). Gate 3's valley is spared (its name), but the
chests are not under it.

**The exact change (smallest, pick one):**
1. `GateRun.Chest()`: name the dropped chest `"Gate Chest " + i + " " + prefab.name` and add `"Gate Chest"` to
   `PurpleHole.keepNamesContaining` (one string; re-check the scene's/prefab's serialized list if the field is saved), or
2. `GateRun.Holder()`: drop chests under the GateRun's own object (each gate's stage root) and let each gate's root name
   be kept (gate 3's "Ball_Valley" already is).

**Test:** a gate with a chest waiting, kill the player (coins revive): the chest and its seed are still there.


**Coordinator, 2026-11-23 01:12: DONE (both ideas, safer form).** New `Game.Rooms.KeepFromPurpleHole` marker: the purple hole never eats it or anything under it; Gate Run adds it to every chest it drops. While a gate runs (`GateRun.Current != null`) the hole also keeps names starting `Seed_` / `The_Seed`. Main game unchanged.

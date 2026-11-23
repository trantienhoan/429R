# Request from gate 2: GateFinalesMenu's gate 2 part is now obsolete (2026-11-23)

**What:** gate 2 is on GateRun. Its giant pumpkin is `GateRun.finaleBoss` on `Prefabs/Bowling/Bowling_Arena` (set by
`Tools > 429 Game > Bowling > Gate 2: Use Gate Run`; gate 2's own variant `Prefabs/Bowling/Pumpkin_Ball_Boss_Bowling`
wins when it exists). BowlingStage no longer reads `pumpkinFinale`, `finaleBoss`, `strikesAddUp`... except
`strikesAddUp` (still used), and has no rules sign any more (the GateRun card).

**Why:** `GateFinalesMenu.Set()` still edits `Game.Bowling.BowlingStage` on the arena (pumpkinFinale, finaleBoss,
strikesAddUp, rulesText). I kept those fields (hidden) so the menu doesn't throw, but writing them does nothing now;
"Gate Finales: As Before" can no longer turn gate 2's giant pumpkin off.

**The change (your file, when convenient):** in `Assets/Scripts/Gates/Editor/GateFinalesMenu.cs`, drop the
`Edit(ArenaPath, "Game.Bowling.BowlingStage", ...)` block (or point it at the arena's `Game.Gates.GateRun` and only set
`finaleBoss` there with the same `SetBoss` rule). After that I can remove the 3 hidden fields from BowlingStage.
No hurry: nothing breaks meanwhile.


**Coordinator, 2026-11-23 01:12: DONE.** GateFinalesMenu now skips a gate that has Gate Run (it only sets Gate Run's finaleBoss with the keep-a-variant rule and finaleSeed; "As Before" leaves it alone); the old fields are written null-safely only for a gate without Gate Run. You can remove the hidden BowlingStage fields.

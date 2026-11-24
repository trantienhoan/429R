# Gates review, 2026-11-23 evening: what changed, what to fix

A read-only review of every gate change from the user's baseline `b568870` (11-22 23:39) to `3a5765e` (11-23 17:15,
including the K: PC day). Four reviewers read the code (one per gate, one for the shared flow and the ending); the main
findings were checked again in the code. No Play test, nothing changed. **Fix later, smallest first.**

IDs: G1/G2/G3 = a gate, S = shared. **Bug** = breaks or does the wrong thing; **Risk** = could, or feels bad;
**Polish** = look, text, clean-up. Confidence in brackets. Plan and contract: `GATES_PLAN.md` (§2); hand-overs: `gateN/NOTES.md`.

## 1. Where things stand

- Gates 1 and 2 run on the shared `GateRun`: card -> start chest -> 3 timed rounds, each ending in a gear chest -> the
  gate's own giant pumpkin -> The_Seed -> rooftop.
- Gate 3 left `GateRun` on 11-23 (the user's redesign on the K: PC): 15 balls, one 300 s song while the field shrinks,
  only your own pockets count, then the Black Pumpkin Ball, a chest with The_Seed; no gear chests (GateExit's GearGuard
  sets the 4 gear bools). See `session_notes/requests/gate3-new-flow-info.md`.
- **The ending holds for all 3 routes** (static check of GAMESTAGES): Path N ON -> GateExit -> Rooftop_ON PG -> Whack 6/7
  -> lid -> BOSS -> Thank For Playing -> new run with every flag reset. Today's scene change only moved the *house* route's
  bonus to the new Whack (Bonus Round PR -> Whack_Round), not the gates' path.
- Gate 1's Doom change (the broken-pot boss ends only the gate) was play-tested on the K: PC: PASS (BOSS stayed false,
  Whack 6 reached).
- The 4 deleted `Slime_Showdown_*` prefabs: nothing references them any more.
- **Not done yet:** the end-to-end run (E6) after gate 3's redesign; any headset run.

## 2. Fix first

| # | ID | What breaks | Kind |
|---|---|---|---|
| 1 | G1-1 | A broken pot doesn't stop gate 1's rounds: chests, hat and a second giant pumpkin keep coming | Bug (high) |
| 2 | G3-1 | Gate 3's push off the holes is a wall all song, and late it bounces you between two holes | Bug (high) |
| 3 | G3-2 | Late in gate 3, every fall puts you back next to a hole: a death loop even after a win | Risk (med) |
| 4 | S-1 | Gates 1+2: eating the seed while picking up the 42 candies ends the gate, the candy is cleared | Bug (med) |
| 5 | G3-3 | Gate 3's 90 s seed fallback skips the seed's heal, candies and Green Seed | Bug (high) |
| 6 | G1-2 | A late start seed in gate 1 pops a second cauldron (smoke, push 2.8 m) | Bug (med) |
| 7 | G1-7, G2-1, G2-2, G3-8 | Setup menus that overwrite your hand edits when re-run | Bug (high) |
| 8 | G3-5 | Gate 3 rebuilds the field's meshes and colliders almost every frame | Risk (med) |

## 3. Gate 1: PlayGround (cook and defend)

What changed: on GateRun through `PlayGroundGame` (card, cauldron chest mid-field, 3 rounds: hat, fill the pot, fire,
defend, slime mini bosses, gear chest; the pot bursts into `Pumpkin_Ball_Boss_Stew`); `CauldronDefence` got a round mode,
monster gates at the corners, pot-biters, spare log/hat/pumpkin; the kit (41 prefabs, 60 looks-only pieces), fog lifted,
ambient x2; 27 house models placed (13 breakables via `SpawnPropHere`, one is the rooftop pumpkin vine); 68 looks-only
`House_Pieces` (4 used). `HurryPumpkinBoss` (rooms): the saved door skull no longer outlives a Game Over: checked, right.

- **G1-1 Bug (high): a broken pot doesn't stop the gate flow.** `PlayGroundGame.cs:156-165`, `CauldronDefence.cs:1063`
  (Doom), `GateRun.cs:249-254`. Nothing stops GateRun when the pot breaks: the round clock runs on, `EndRoundNow` returns
  early, the gear chest still drops, rounds 2-3 hide the hat again and drop their chests; then GateRun's fallback drops the
  stew pumpkin with no arena set-up next to the Doom boss, and once it's out, killing the Doom boss no longer ends the gate.
  Fix: in `Doom()`, stop GateRun the way `EndGateByChaosBoss` does (`run.StopAllCoroutines()`), keep the pot-broken line.
- **G1-2 Bug (med): a late start seed pops a second cauldron.** `PlayGroundGame.cs:171-179, 226-235`;
  `Seed_Bracer_Cauldron.prefab`. Not eaten while the start chest waits: the fallback cauldron pops in the field's middle,
  on the start chest's spot. When that seed is eaten later (or auto-given after 300 s, or by GateExit's GearGuard), a
  second cauldron pops with smoke and a push of up to 2.8 m, then `KeepOneCauldron` removes it. Fix: a second
  `CauldronDefence` removes itself in `Awake` before it pops; put the fallback cauldron at least 2 m from the chest.
- **G1-3 Risk (med): tree pumpkins outside the floor break the pumpkin rules.** `PlayGroundBedroomModelsMenu.cs:76`,
  `CauldronDefence.cs:524, 538-546`. About 20 vine pumpkins lie off the floor: the pot's spare pumpkin never comes (they
  count within 30 m); over 12 tree pumpkins lets a field pumpkin turn into a rolling bomb, even the one the recipe needs;
  the 42 cap breaks the oldest. Fix: `TurnsIntoBowler.Hold/Release` in `PlayGroundGame.OnEnable/OnDisable`; count only
  pumpkins on the NavMesh, or switch the vine's Pumpkin_Spawn FSM off.
- **G1-4 Risk (med): a round chest can hang in the air.** `PlayGroundGame.cs:249-273`. The floor ray misses past the walls
  (the floor ends 0.8-3.9 m out), `Floor()` keeps head height, the chest is kinematic: out of reach, its candy and EXP lost.
  Fix: keep the spot inside the field; fall back to the floor's height.
- **G1-5 Risk (low-med): the round clock runs through the boss fights; the pot never heals.** `GateRun.cs:272-279`,
  `CauldronDefence.cs:888-900`. Round 3 (new hat spot, 5 ingredients, 75 s cook, 2 big slimes) must fit in 180 s; at
  time-up the bosses vanish with no reward. One 150-health pot for all 3 rounds makes the doom likelier late. Fix: round 3
  about 240 s, or hold the clock while bosses are out; give back some pot health each round.
- **G1-6 Risk (high, not today):** the finale hand-off races GateRun's 3 s fallback; see S-5.
- **G1-7 Bug (high): menus undo hand work.** `PlayGroundGateMenu.cs:184-208` deletes inactive cauldron/hat copies on every
  run (backup only the first time); `PlayGroundKitMenu.cs:272-276` rewrites the stew pumpkin's model, radius, offset and
  turn on every Build; `CauldronDefenceMenu.cs:99-130` "Use Cooking Rounds" switches Cauldron Defence off under a live
  GateRun (rounds then only end on time). Fix: write only the first time or into empty fields; warn, or switch GateRun off too.
- **G1-8 Polish (high): open monster gates aren't shown.** `CauldronDefence.cs:297, 1354`: nothing listens to `GatesMoved`;
  all 4 kit gates always glow. Fix: a small component per gate that switches its mist on `GatesMoved`.
- **G1-9 Polish (med): text.** "NO ESCAPE!" is a big red pop-up (`CauldronDefence.cs:144, 265`); the pot's panel says
  "DEFEND THE CAULDRON!" while the HUD says "DEFEND THE POT", plus two long cauldron warnings
  (`CauldronDefence.cs:1216-1227`, `CauldronHealth.cs:46-47`). Fix: blank `groundBreakText`; one name, short lines.
- **G1-10 Polish (med): look and speed.** Still much darker than the concept (0.13-0.18 vs 0.27, even with ambient x2);
  the toys stand outside the walls; `CookingRewards.cs:84-94, 124-126` runs 3 `FindAnyObjectByType` every frame through
  the finale and exit; pole lights + vine add 5 point lights.
- **G1-11 Polish (high): dead code.** The old single-dish path ("MINI BOSS!"), PlayGroundRounds, SpecialStage3Trigger
  still on the cauldron, the old rounds / candy pot / wild slimes, `Cauldron_Pos` switched off twice, 9 `_off` kit pieces,
  64 unused House_Pieces. Delete after your OK.

## 4. Gate 2: Slime Bowl (bowl and fight)

What changed: on GateRun (card "BOWL AND FIGHT", Bracer chest, three 120 s rounds: 1 slime / 2 slimes / 4 small, each
ending in a gear chest, then `Pumpkin_Ball_Boss_Bowling`, 100 HP, 42 candies, ball hits add 4 damage, 8 while dizzy); its
own exit, chest drops, signs, phase 3 and the 4-slime showdown removed; the SlimeBowl kit (24 pieces, one palette, mesh
flames, vine cups, violet fog); quicker racks; one `Bowling_Lane` prefab (4 lanes), Hand Set Up guard, Fit Fall Edges and
Import Kit Pieces menus, strike bell and miss tone. No user-rule breaks; all 101 references in the arena resolve; the
round flow, death clean-up and statics hold.

- **G2-1 Bug (high): Import Kit Pieces resets the palette material.** `SlimeBowlKitMenu.cs:144-163` (from
  `BowlingHandSetUpMenu.cs:279`): base colour white, smoothness 1, emission x1.4 come back on every import, silently.
  Fix: set those only when the material is first made.
- **G2-2 Bug (high): two old builder menus skip the Hand Set Up guard.** `SlimePinSetupMenu.cs:151-234`,
  `BowlingBallSetupMenu.cs:46-55` (Set Up Bowling Look too): a re-run rebuilds Slime_Pin's sizes and the ball's roll
  numbers. Fix: one `if (BowlingHandSetUpMenu.Refuse(...)) return;` line in each.
- **G2-3 Bug (med): the miss tone plays for any knocked pin.** `BowlingStage.cs:691`, `PinSet.cs:346-369`: a rack bumped
  by a hatchling or a weapon sounds "miss"; a ball that reaches the rack and knocks nothing stays silent. Fix: track that a
  ball arrived (e.g. `BallArrived`) and test that.
- **G2-4 Risk (med): the HUD line hides behind the cup balls.** `Bowling_Arena.prefab:1664`: the line is 1.8 m ahead,
  0.55 m under the eyes; cup balls stand 1.1 m out at 0.8-1.25 m. Fix: this gate's `hudPlace.y` about 0.3.
- **G2-5 Risk (med): the giant pumpkin's candies fall off the island.** `BowlingStage.cs:912, 1369-1374`: the fountain
  starts about 1.6 m out at 0.4-1.4 m/s sideways; past 2.5 m the Fall Edges destroy candy. Fix: side speed (0.2, 0.7).
- **G2-6 Risk (med): lanes never go dark.** `BowlingStage.cs:291, 1032`: from round 2 to the exit, 4 deck spot lights, the
  dirt light, 4 sparkle sets and 8 flames stay on. Fix: switch a lane off after about 10 s with no rack.
- **G2-7 Polish (high): the first 4 vines pop up on arrival, not with the start seed.** `Bowling_Arena.prefab:2803`
  (`openSpots: 4`), `KitSprout.cs:42`: empty cups through the card. Fix: `openSpots: 0`.
- **G2-8 Polish (high): hand set-up only covers the lanes.** `BowlingStage.cs:42, 68, 1192`, `BowlingBallSupply.cs:46`:
  island size 2.5/2.6, chest keep 1.9 and the cup ring are fixed numbers; a bigger island or moved cups leave the chests,
  seed, candies and foul line behind. Fix: read the Dirt collider, as Fit Fall Edges does.
- **G2-9 Polish (high): look.** Much darker than the concept (0.08 vs 0.30); lanes read as thin planks; the portals you
  dropped still stand at every lane end (`Bowling_Lane.prefab` nests `SlimeBowl_Kit_Pumpkin_Arch`); round start / round won
  are silent (`Bowling_Arena.prefab:1667-1668`); balls break at 22.6 m while still over visible deck stone.
- **G2-10 Polish (high): dead code.** `GiantSlimeSetupMenu.cs:27, 49-80, 283-367` (the showdown), `GiantFlyingSlime.cs:416,
  468, 512, 736, 814` (no callers), `BowlingStage.cs:252` `strikes` (never read), the unused `Ended` event, inactive Fire
  Columns in every lane, VineGrow hidden but running, unused Slime_Pin and Rune_Ring kit prefabs. Delete after the headset pass.

## 5. Gate 3: Ball Valley (run and snooker)

What changed: morning: on GateRun with its kit; 3a5765e (the user's redesign): GateRun removed, `BallValleyGate`
rewritten: card -> 15 balls -> a 300 s song while the field shrinks 1 -> 0.1 (ease 2.2), music pitch 0.92 -> 1.3, hunters
1 -> 3; only your pockets count (3 candies each); all 15 in -> Black Pumpkin Ball (r 1.2, 3x mass, 150 HP) -> 30 EXP,
42-candy rain, the main game's Treasure_Chest_and_Seed, The_Seed (90 s fallback) -> GateExit; song runs out -> Collapse
(Daruma-style kill, no revive); candy trees, boulders, 3 jello volcanoes; kit remodelled on the house texture.
Checked, fine: Game Over clears the valley and resets every static; a second visit starts clean; the 300 s kill can't fire
after the Black ball dies, after the seed, or twice; the purple hole keeps the valley, the chest and the seed.

- **G3-1 Bug (high): the push off the holes is a wall all song, and late it ping-pongs.** `BallValleyPlayer.cs:626-649`,
  `BallValleyStage.cs:819`, `BallValleyGate.cs:269-275, 289-294`. The shrink is continuous, so `shrinking` is true from
  about 20 s: walking into a hole is never a fall (its comment says it should be). From about 286 s two holes' rims are
  close enough that the push throws you from one to the other every frame (jumps up to ~0.85 m). `StopClock` leaves
  `drivePending` stale, so it goes on after a late win. Fix: skip the push when its target is within Shrink Push From Rim
  of another hole; `stage.drivePending = -1` in `StopClock`; decide if walking in should still be a fall.
- **G3-2 Risk (med): late in the song a fall puts you back next to a hole.** `BallValleyPlayer.cs:660, 696-710, 752`.
  After ~285 s no kept spot is 1.5 m clear of a hole, so a fall puts you in the middle; from ~291 s the middle itself is
  by a hole: about a fall a second at 55 HP, coin flips, revives into the hole; a win after ~291 s can still end in Game
  Over. Fix: start Collapse (or forgive falls) once the middle is closer than Keep From Rim to a hole.
- **G3-3 Bug (high): the 90 s seed fallback skips the seed's rewards.** `BallValleyGate.cs:462-472`: it only sets
  The_Seed, so the seed's heal, candies and Green Seed (the gate-unlock currency) never come. Also `ChestSpot()`
  (`:580-600`) ignores props: the chest can land inside a boulder or volcano. Fix: eat the seed the way GateExit does
  (send `next` to The_Seed in "Rotate and Wait"); keep the chest clear of props.
- **G3-4 Risk (med): the Black Pumpkin Ball backs off 10 s after every hit on you.** `BallValleyPlayer.cs:319`
  (`restAfterHit` 10 applies to it too); no dizzy bonus; 150 HP at whole-number punches is dozens of hits on a shrinking
  field. If it comes late, it's pocket it or lose. Fix: skip `ForgetHuman` for the Black ball; lower `blackHealth`.
- **G3-5 Risk (med): the shrink rebuilds the field almost every frame.** `BallValleyGate.cs:269-273`, `Valley.cs:135-151`:
  lawn and rim rebuilt, floor and teleport MeshColliders re-cooked 10 times a second each (that throttle was for 4 s
  steps); plus 15 balls x up to 8 tails, 40 loose balls, uncapped candy. NOTES: editor frame 4-6 ms -> 12.3 ms. Fix: move
  `driveScale` in steps (at least 0.005, or every 0.25 s).
- **G3-6 Polish (med): the Black ball's laugh and break sound are almost silent.** `BallValleyGate.cs:367, 425`:
  `PlayClipAtPoint` fades from 1 m and the ball is metres away. Fix: a half-3D source with min distance 8 (as `PurpleHole.Play`).
- **G3-7 Polish (low): Collapse's busy check is weaker than Daruma's.** `BallValleyGate.cs:532, 548`: the 12 s cap counts
  real time (a pause held past 300 s gets the kill under the pause menu); no health-0 or black-view check. Fix: Daruma's
  PlayerDown and fade checks; don't count paused time.
- **G3-8 Bug (med): re-running the menus overwrites hand edits.** `BallValleyKitMenu.cs:151, 157, 168, 345, 373`:
  Build Ball Valley Kit resets kitDecor, blackPumpkinLook and the volcano/tree/rock lists, rebuilds every Kit prefab and
  the boss variant (only Ball_Valley is backed up), and still writes `GateRun.finaleBoss`; `BallValleyMenu.cs:113, 217,
  220`: Set Up overwrites candyPrefabs and fallSounds and makes the shared Pumpkin_Ball_Boss if missing. Fix: fill empty
  fields only; back up the pieces too.
- **G3-9 Polish (low): new big moments use the BALLS `Gfx.Puff`** (`BallValleyGate.cs:365, 434, 457`), not the shared
  Smoke_Flash_Small_Puff (already in `voiceFrom.puff`).
- **G3-10 Polish (med): look.** The field (stone rim, glowing pit rings, jelly trees) is close to the concept, but the BALLS
  pale-lavender mountains dominate every view; the concept's dark misty hills, moon and lantern glow are missing (NOTES M1-M3).
- **G3-11 Polish (high): dead code.** `Boss => null` (`BallValleyGate.cs:138`) and the giant-pumpkin branches it feeds in
  `BallValleyPlayer` (`:94, 267, 525, 682, 712, 752`); `BallValleyStage.cs:144, 984-1031` (ShrinkToPhase/Last, LastPhaseReached,
  AtLastPhase); `Sfx.Shrink` (no caller); GateRun branches in both menus.

## 6. Shared flow and the ending

What changed: `GateRun` (new; on Play_Ground_Z and Bowling_Arena, not Ball_Valley); `GateBossFinale` drops the seed if the
boss vanishes, exits once; base `Pumpkin_Ball_Boss` breakCandies 14 -> 42 (no variant overrides it); `PurpleHole` +
`KeepFromPurpleHole`; `ScriptedMoveVignette` (launch vignette, only in gates); `DeathCameraFall` no longer moves the hands
while closing (the Play-exit "DeadCam" error; not re-tested); `PcControls` editor-only now; `SaveManager.whackScores`
(old saves load fine); scene: 3 small changes, GAMESTAGES otherwise identical.

| Step | Gate 1 PlayGround | Gate 2 Slime Bowl | Gate 3 Ball Valley |
|---|---|---|---|
| Card | GateRun "COOK AND DEFEND" | GateRun "BOWL AND FIGHT" | own copy "RUN AND SNOOKER" |
| Start | PlayGround cauldron chest, mid-field | house Bracer chest | none (15 balls) |
| HUD | ROUND n/3 + task n/m | ROUND n/3 STRIKES n/m | BALLS n/15 + seconds |
| Timing | 150/165/180 s, time-up = round won | 120 s x3, time-up = round won | 300 s song, time-up = death |
| Gear | 3 chests, 25 s waits | same | 4 red seed balls: your pocket spits a gear seed (since 11-23 evening); GearGuard eats any left |
| Boss | stew pumpkin, 150 HP | bowling pumpkin, 150 HP (was 100 until 11-23 evening) | Black Ball, 150 HP |
| Reward | 8/12/16 + 4 seeds + 42 candy + 30 EXP | same | 3 per pocket + field + 42 candy + 30 EXP |
| Seed | floating, 20 s | floating, 20 s | in a chest, 90 s |
| Death | coins; broken pot -> Doom boss ends the gate | coins; a fall puts you back mid-island | coins; song end kills |
| Exit | GateExit; PlayGround sinks | GateExit; the arena rides along | GateExit on the valley |

- **S-1 Bug (med): gates 1+2 can lose most of the giant pumpkin's 42 candies.** `GateExit.cs:35, 94`,
  `GateBossFinale.cs:85-97`, `PumpkinBallBoss.cs:693`: The_Seed floats 1 m above the spot where the candy fountain starts;
  stepping in for candy can eat it (or 20 s pass); ~1.9 s later the view goes black and Destroy PlayGroundZ's Clear Spawned
  Stuffs removes every candy still lying there. Fix: `seedSeconds` 20 -> 60, and the seed 2-3 m toward the player (the
  `seedSpot` of `GateRun.Finale`).
- **S-2 Your call (high): gate 3 reaches the rooftop weaker.** No gear seeds (each: a heal, a 6-candy health bonus, a
  Yellow Seed) and no chest fountains (8/12/16 candies); GearGuard only sets the bools. Contract #3, #7 and #8 are off for
  gate 3 by the user's design. **Decided 2026-11-23 evening (user):** "make some balls bigger a little and have red aura,
  push that ball into hole and the hole will spit out a seed": built (4 seed balls, one per gear seed; see §7), not
  play-tested. The chest candy is still missing.
- **S-3 Polish (high): no hint while a chest waits.** `GateRun.cs:418-434`: the HUD line is blank; gate 1's start chest lands
  mid-field, maybe behind you; after 25 s the gate goes on and the unopened chest is cleared at the exit. Fix: "OPEN THE
  CHEST" while waiting. (Gate 3 copies GateRun's card/HUD code, so fixes like this won't reach it.)
- **S-4 Polish (high): gate 2's start chest looks different:** the house `Treasure_Chest_and_Seed_Bracer` (GateRun's
  default), while its round chests and gate 1's are PlayGround wood. Fix: a PlayGround-wood Bracer chest without the cauldron seed.
- **S-5 Risk (high, not today): the finale hand-off races a fixed 3 s.** `GateRun.cs:249`: gate 1's pot calls Finale after
  Burst Delay (2.5 s); at 3 s or more the generic pumpkin drops 6 m ahead with no arena limits. Fix: raise the wait to 8 s,
  and/or let gate 1 call `run.Finale` at once (`CauldronDefence.cs:922-929`).
- **S-6 Risk (low): a player "down" without dying freezes GateExit in the black.** `GateExit.cs:136, 146-155`: line 136
  waits with no timeout or Game Over check; the 20 s counter only runs while not down. Gate 3's session saw "health 0, no
  Game Over" once. Fix: a Game Over check and a timeout there.
- **S-7 Polish (low impact): the win path leaks one empty holder per won run.** GAMESTAGES "recreate" and "Recreate" both
  make a CurrentlySpawnedStuffs; anything spawned in the 1.3 s between is never cleared (older than today). Fix: switch off
  the create action in "recreate".
- **Gate 3's GateExit timeout request** (`session_notes/requests/gate3-gateexit-timeout.md`): **don't apply as written.**
  The stuck case can't happen in a normal run (the bools are always set, the waits are every frame, Rooftop_ON PG always
  makes a new rooftop, Game Over is caught). Its `SetState("Rooftop_ON PG")` skips Destroy PlayGroundZ (gate 1's lifted fog
  and light and `GateRun.Current` would stay through the ending); its step 3 (`Running = false`) makes gate 3 destroy its
  valley under the held player; its test doesn't reproduce the stall. Smaller and safer: at the 20 s warning call
  `GearGuard()` and set The_Seed true again; a hard fallback only as `SetState("Destroy PlayGroundZ")` from Path N ON or
  Enemy 4; plus S-6.

## 7. Your calls

1. ~~Gate 3's arrival power (S-2)~~ **Done 2026-11-23 evening (your words: "make some balls bigger a little and have red
   aura, push that ball into hole and the hole will spit out a seed"):** 4 of the 15 balls (picked at random) are 1.25x
   bigger, with a red aura: the game's red Chaos_Aura (as the pumpkin boss wears it), a louder copy per ball (wisps
   more solid and thicker, its glow smaller and lifted so it doesn't cut a line on the lawn), plus a red glow pulse and
   sparks on the ball. Your own pocket of one spits its gear seed (Bracer, Chest, Head, Shield, in that order) out of the
   hole, in a red streak, onto the lawn half way to the middle; the seed moves in if the holes come near it; Gate Exit
   eats any still waiting. Code: `BallValleyGate.cs` (Seed Balls), `BallPlayer.cs` (`sizeTimes`, `aura`, `auraLook`),
   `BallValleyStage.DropBalls` (returns the balls); `Ball_Valley.prefab` has the 4 seeds and Chaos_Aura; Set Up Ball
   Valley fills them if empty. **Editor-tested 2026-11-23 evening** (`_claude_test`): seed balls r 1.0-1.1 m (others
   0.83); each pocket spat its seed 4-12 m clear of the holes; eating each set its gear bool (all 4 true); a seed ball
   knocked in by the others drops back still big and red; 0 errors. Pictures: `Documents/429R_claude_backups/2026-11-23/
   seed_balls/` (second PC). Not tried in the headset. Test hook: BallValleyGate's "Test: Pocket A Seed Ball (Mine)".
2. ~~Gate 2's giant pumpkin: 100 HP~~ **Done 2026-11-23 evening: 150 HP** (`Bowling_Arena.prefab` finaleHealth, and the
   code default). Editor-tested: it came in with 150.
3. **REMINDER (you asked 2026-11-23): add something to gate 2's game.** You haven't decided what yet.
4. Gate 2's round start / round won sounds (your clips): silent now.
5. Gate 1's round 3 length (G1-5).
6. The dead code in G1-11, G2-10 and G3-11: delete after the headset pass?
7. Gate 3's GateExit request: take the smaller fix above?

## 8. Only a Play or headset test can tell

- **E6 again:** gate 3's new flow and gate 1's Doom path through the whole ending (E6 ran before both changed).
- **Gate 1:** does the vine keep growing, can its pumpkins be far-grabbed from the edge; the stew pumpkin among the 3
  solid toys; entering within ~35 s of a run start (Lift Light saves the opening's 0.42 ambient); round 3 vs 180 s; the
  HUD and the pot's panel together; whether the start chest under a fallback cauldron still opens.
- **Gate 2:** balls staying in the tiny cradle spheres when bumped; steered bowls' angle into the pins; full-grown pins
  apart; a charge hit throwing you ~6 m (onto the far lane or a Fall Edge?); lane counters among the kit; the violet night.
- **Gate 3:** how G3-1/G3-2 feel; real punch damage on the Black ball; a coin flip + revive against the 12 s cap; the
  Black ball's sounds; the song at pitch 1.3; the seed's damage to the Lid on the rooftop.
- **All:** Quest frame time (gate 1's extra lights, gate 2's round 3 lights, gate 3's shrink); the launch vignette; the
  DeathCameraFall fix (needs a headset death). House route, for the Whack session: the new Whack waits for the chest and
  both mallets with no timer (`WhackRound.cs:280, 322`).

## 9. Docs now out of date

- `GATES_PLAN.md`: the status line, §0, §1 and E1 still put gate 3 on GateRun with a giant pumpkin; 150 s rounds (gate 1:
  150/165/180, gate 2: 120); "14 candies" (now 42); 20 s chest waits (25 s); "one notice" (WORKING SITE + the card show
  every visit); the Doom question (decided and done); the gate 2 test list names menu items that are gone.
- `gate1/NOTES.md`: §3.1 "not play-tested" (K: PC: PASS); §2a "a seed eaten later doesn't make a second one" (it does:
  G1-2); checklist 1 "the start chest lands in front of you" (field middle); E2; §1 "Dress PlayGround With Kit is
  re-runnable" (builds once now, 9 pieces off, solid toys inside the field).
- `gate2/NOTES.md`: §1/§2/§4 "re-apply the kit, keep every value" (refused while Hand Set Up is on); §7 "Import Kit Pieces:
  nothing placed or moved" (it resets the palette: G2-1); §3 pumpkinFinale / rulesText (removed); comments in
  `BowlingPlayMenu.cs:13-14`, `KitSprout.cs`, `BowlingStage.cs:31`, `BowlingLane.cs:13`.
- `gate3/NOTES.md`: §3b-3d still describe GateRun, 120 s rounds, hunters 1/1/2, SetUpBoss; §3e drop-in 4 s (prefab: 3 s);
  stale code comments (`shrinkPushFromRim` tooltip, `dashBossDamage`, the BallValleyStage summary, GatePieces, DropBlack's
  "all out"); `gate3-gateexit-timeout.md` cites waits gate 3 no longer has.

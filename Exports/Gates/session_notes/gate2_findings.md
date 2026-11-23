# Gate 2 (Slime Bowling): code facts and weak spots (2026-11-22)

Read from the working copy by a read-only helper; spot-checked by the planning session (gate 2's own exit copy and the
Shield gear forced at the win are confirmed). Line numbers drift: re-check before editing.
BS = Assets/Scripts/Bowling/BowlingStage.cs, PS = PinSet.cs, RA = Scripts/PlayGround/BowlingRollAssist.cs,
PBB = Scripts/Gates/PumpkinBallBoss.cs, ASM = Bowling/Editor/BowlingArenaSetupMenu.cs, Arena = Prefabs/Bowling/Bowling_Arena.prefab.

## 1. Flow today
- Scene in: Stage 1 next2 -> Clean for Path 2 -> Demo? Path 2 -> Path 2 Holder -> Path 2 ON (fade, teleport to (0,0,0)
  yaw 45, spawn the arena, WORKING SITE notice, BoolAllTrue 4 gears + The_Seed -> next1).
- Start (BS 302-332): thumbstick off, re-forced off every 0.5 s (338); a fall returns the player to the centre; fog 14-38 m.
- Opening (BS 392-447): Bracer chest after 1 s; the stage waits (no timeout) for its seed; eating it grows vines + balls;
  4 s later the rules sign + the slime (6 s descent, 2 laps x 5 s).
- Pooping (Think, 450-472): least-used free lane, never the same twice in a row; the 7th turn drops 6 divers and counts
  as the hit (459, 581-619).
- Strikes (PS 361-397): the first ball past 19.8 m opens the rack's only window; strike = every pin down, none touched
  before the window. Strikes add up (BS 646); 3 strikes -> Hit (662) -> split.
- Phase end (740-779): racks melt, a chest (Chest, then Head), 5 s breather.
- After phase 2 (PumpkinFinale, 790-820): slimes fly off, wave killed, boss via `PumpkinBallBoss.Spawn` (NOT
  GateBossFinale) on a random lane end with lane hooks (canBeAt, chargeAlong, hopTo); Broken -> Win.
- Exit (ToTheRooftop, 1488-1613): gate 2's OWN copy of the GateExit logic (not Gates/GateExit.cs): arena unparents,
  GearGuard (1617-1637) force-sets missing gear bools (logs a warning), seed at the player's feet, auto-eaten after 20 s,
  black, wait for the new Rooftop_Base, player 4 m from the PumpkinTree. Fixes in GateExit don't reach gate 2.
- Scene out: Enemy 4 - Get the Seed (switches on "-EnemyManager 4", its Bee Swarm) -> Tween PlayGroundZ -> Destroy
  PlayGroundZ -> Rooftop_ON PG -> Prepare_Whack_6.
- Old paths kept: pumpkinFinale off or finaleBoss empty -> phase 3 (wing loss) + the 4-slime SpiderBrain showdown
  (754-759, 866-1014); bossAfterPhase and strikesAddUp switchable; menus "Gate Finales: As Before" and "Gate 2 Back To
  PlayGround" undo; the unused "Gate 3 Pick" state still points to Clean for Path 2; no bee code left.

## 2. Tunables (Arena prefab = code defaults: 2983-3069, supply 5549-5555)
| | Phase 1 | Phase 2 | Phase 3 |
|---|---|---|---|
| Slime size | 7 | 5.5 | 4.5 |
| Fly speed (m/s) | 6 | 6.5 | 7 |
| Poop every (s) | 12 | 14 | 18 |
| Pin grow (s) | 24 | 22 | 20 |
| Flyer cap | 5 | 8 | 8 |
- Rules: 3 strikes to hit, dive after 6 racks, 6 divers; rules sign 25 s; seed auto-eat 20 s.
- Boss: radius 1.3, health 100 (prefab 150); PBB wind-up 1.6 s, charge 13 m/s, dizzy 3.5 s x2, 0.25 s per fist, 14 candies.
- Balls: 2 on the pad, +1 per seed; sprouts open 4/+2/+1/+1; grow 3 s + 1 s; at most 9; a stalled ball pops after 8 s.
- Rolling (Bowling_Ball.prefab 307-316): a release <= 0.9 m up rolls at 5-9 m/s, steered 35% toward the pins within 25 deg;
  throws capped at 1.2 m/s.
- Pins 0.66x0.24 m -> 0.36x0.40 m; strike help 1; flyers live 22 s; divers 10 s.
- Pace: first rack ~30 s after arriving; phase 1 40-100 s, phase 2 55-120 s, boss 35-70 s; typical total 4-5 min
  (~3 best, 6.5 worst; the dives force progress).

## 3. Weak spots and bugs
- Re-run trap: "Set Up Bowling Arena" rebuilds the Arena from scratch (ASM 142-269) and never sets the finale fields:
  finaleBoss empties and the gate silently goes back to phase 3 + showdown (BS 747); the sign text reverts to "in a row"
  (ASM 60) while strikes add up. "Gate Finales" must be re-run after it.
- The Shield chest never drops on this path: GearGuard sets Shield_Gear and logs a warning every win (confirmed). The
  player loses that seed's item, heal and ball sprout. The Head chest drops ~1.5 s before the boss.
- Waits with no timeout: the start chest (397; only Seed_Bracer's FSM setting Bracer_Gear frees it, 1259-1270); seed
  auto-eat depends on the seed FSM's "Rotate and Wait" + "next" (297, 1531); the rooftop wait only warns (1554-1575);
  old showdown only: the drop-order wait (935) and the win needing every wave enemy dead (372).
- Boss candies probably lost (play check): the fountain starts at the boss (PBB 693), >= 3.8 m out or on a lane; they fly
  2-5 m/s and land past the Fall Edges (which keep only Seed/Coin); the thumbstick stays locked (only unlocked in the old
  showdown, 978).
- Boss home point: arenaCentre = the first lane end, not updated after hops (810); PBB's "outside -> go to arenaCentre"
  (531-535) could drag it over the void. Low risk.
- Bowling rules: one window per rack (a weak first ball wastes the rack and its pins hatch); RA changes only the roll's
  direction, not its side offset (136-142): balls bowled off a lane's 2 m mouth roll off the 5 m dirt; no approach marks.
- Cost: 8 linked Danger_Flame x 164 max + sparkles ~1,450 particles max vs ASM's 650 budget (62) (notes: ~660 alive);
  5 spot lights; lanes never go dark once lit; `FindObjectsByType<ContinuousMoveProvider>` every 0.5 s (1715).
- Statics fine: cleared in Restore (1760-1763); Game Over resets the 5 bools.
- Untested (NOTES 487, 750): headset feel, the full rounds, real punches/balls on the boss; lane mode not re-tested since
  PBB's 2026-11-20 aim-lock change (NOTES 282-283).

## 4. Fun and pacing
- ~30 s before there is anything to bowl at; the 2 entrance laps are 10 s of idling.
- Rules sign 25 s and only over lane 0; the lane counter still says "n/3" like a streak (1127).
- A strike shows only as small pops 20 m away + the floor counter; a miss is silent.
- Pins ~1 deg across at 20 m with ~30% fog: aimable, results hard to read.
- Balls grow on the floor 1.1 m from the centre: the player bends down for every ball.
- Easy: rails, a 5 m/s minimum, steering and strike help; a player who never strikes still finishes, ~2x slower.
- Boss loop ~9 s: 5.5 s waiting, 3.5 s punching at the dirt edge. Whether a rolled ball hurts it depends on
  EnemyHitDamage.Judge counting it as the player's (PBB 593): untested.

## 5. Looks
- All placeholder: lanes and rails are cube primitives with copied PlayGround materials; the dirt is a code-built slab;
  vines code-built; the deck a quad with Lana's Circle06; fire = Fire_Woods.fbx + Danger_Flame; pins and the giant =
  Flying_Slime.fbx; balls = pumpkin_bowling.fbx; signs bare text; a black void with a fog band.
- The concept in Exports/Bowling_Concept/PROMPTS.md (floating rock island, wooden lanes, stone rune decks, braziers, a
  ball-return cauldron, clouds) isn't started; REMODEL_LIST's side-path section (289-336) all unchecked.

## 6. Shared-file risk
- Giant pumpkin: PBB + Prefabs/Gates/Pumpkin_Ball_Boss shared with gates 1 and 3; gate 2 is the only user of lane mode
  (hopTo, chargeAlong).
- RA: its static AimFinder is set by BS; runtime gate-2 only (pumpkin_bowling.prefab is used nowhere).
- Main-game code: EnemyWave, EnemyLimit; SlimeBomber (Daruma stage, Rainbow slime, PlayGroundField); FlyingSlime and
  SlimeKit (Paint + Rainbow slimes); SpiderBrain + SlimeBody (old showdown only); FallEdge + FallOutOfWorld statics;
  GameStages, HeadsetFade, PauseMenu, PlayerRevive, Checkpoints, PlayerLaunch, PlayerInventory, EffectPool, CandyFountain.
- Combat: EnemyHitDamage.Judge + HitSettings decide the boss's damage; the ThrowAssist opt-out; the name "Bowling_Ball"
  is matched in BlastSkills.KeepNames and ThrowWeightMenu; EnemyHitDamage, HitSettings, WeaponPower, GentleFarGrab have
  uncommitted edits.
- Prefab variants: Flying_Slime is the base of Giant_Flying_Slime and Slime_Pin_Pop (anything added to Flying_Slime flows
  into the giant); Slime_Bomber is the base of Slime_Pin_Flyer (also Daruma_Bomber, Rainbow_Slime); Daruma_Bomber (the
  divers) is also in Special_Stage_3; Slime_Big/_Red/_Blue are the bases of Slime_Showdown_* and gate 1's mini bosses;
  Danger_Flame linked 8 times (also Boss_Area, Gate_Torch_Purple).
- Shared items: Treasure_Chest_and_Seed_Bracer + Seed_Bracer (also Pumpkin_0); the PG chests (gate 1); The_Seed;
  beehive_pin.physicMaterial (the pins).
- GAMESTAGES names relied on: the 5 bools, "Game Over", the globals CurrentlySpawnedStuffs / Rooftop_Base / Lid, the
  PumpkinTree tag, the exit-chain states shared with gates 1/3 and the main game.
- Menus that write Path 2 or the Arena: Gate2BowlingMenu, GatePathsMenu (cloned Path 2 for Path 3), SteamDemoMenu,
  PlayGroundNoticeSetup, Gate3PickMenu, GateFinalesMenu (edits the Arena AND Fire_Woods_n_Cauldron).

## 7. Test hooks
- BowlingStage right-click in Play (1317-1448): Eat The Seed, Strike The First Rack, Hit Slime 0, Next Phase, Showdown Now
  (with the pumpkin finale on: drops the Shield chest and goes straight to the giant pumpkin), Win Now. Public:
  DebugStrike(lane), DebugHit(i), StreakOf, PoopsOf, SetsOn, FinalePumpkin.
- PinSet right-click: Debug Knock All (with/without the window), Force Strike, Hatch Now; StrikeHelp property.
- Statics: BowlingStage.Running, BowlingStage.Ended (no listeners), BowlingRollAssist.AimFinder.
- No "Play Bowling" menu (unlike Ball Valley).
- Fastest: Play on `_claude_test`, `GameStages.Fsm().SetState("Clean for Path 2")`, dismiss WORKING SITE, Eat The Seed ->
  Showdown Now (~2 s to the boss) -> Win Now. The arena also runs alone in an empty scene (seed -> Yellow_Seed
  inventory path, no rooftop).

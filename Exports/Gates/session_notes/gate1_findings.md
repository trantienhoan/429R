# Gate 1 (PlayGround, "Defend the Cauldron"): code facts and weak spots (2026-11-22)

Read from the working copy by a read-only helper; spot-checked by the planning session (GAMESTAGES' global "Win" ->
"wait" -> Thank For Playing is confirmed, so a killed Doom boss ends the run as a win; CauldronDefenceMenu does re-point
Path 1 ON to "Tween PlayGroundZ To Boss"). Line numbers drift: re-check before editing.
Bare `X.cs:n` = Assets/Scripts/PlayGround/X.cs; "prefab" = Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab.

## 1. Flow today
- GAMESTAGES: Stage 1 next1 -> Clean for Path 1 -> Demo? Path 1 -> Re-Create Stuffs Hodler -> Path 1 ON (spawns
  Play_Ground_Z at -LevelManger = world origin, WORKING SITE notice pauses; waits BoolAllTrue(4 gears, The_Seed) -> next1
  -> Tween PlayGroundZ -> Destroy PlayGroundZ -> Rooftop_ON PG -> Prepare_Whack_6).
- PlayGround (Play_Ground_Z.prefab, Phase_1 only): 2 trees, skull, table, 3 pipes, 3 barrels, 3 candy baskets, the hat,
  the Bracer chest. FirstFit fits the roam area (~11x12 m) at 5.5 s (PlayGroundField.cs:177).
- The chest waits on a collision (no timeout). Its seed (touch, or 300 s auto) sets Bracer_Gear and spawns the cauldron
  at the seed's spot.
- CauldronDefence.Awake (:272-292): one dish = 4 candies (at most 3 kinds) + 1 Pumpkin_On_Tree, 300 s cook. The pop
  pushes a player within 3 m back (:366).
- Hat hit -> recipe shown. Wrong recipe -> a Boss-class Slime_Big (Cauldron.cs:1363). Right recipe -> "LIGHT THE FIRE!";
  one spare log after 45 s (:381-400).
- Defence (:404-574): walls down (GO), 4 wave phases, near 7 m / far 8.5 m targeting, mini bosses at 25/50/75/90%, each
  dropping a chest.
- Win (:584-632): Shield_Sphere on (now the Purple Hole), KillAll, ClearAll, GearGuard; 2.5 s later the pot bursts
  (CookingRewards; cauldron destroyed 3 s after, Cauldron.cs:1257-1308); GateBossFinale: Pumpkin_Ball_Boss (radius 1.5,
  150 hp, 42 candies) -> The_Seed -> rooftop.
- Loss (:691-784): pot wrecked, endless waves, teleport breaks, pumpkin_boss_Z_Doom (always chaos).
- Kept / dead: PlayGroundRounds disabled (prefab:16800) but the rounds, candy pot and laughing pumpkins stay in
  Cauldron.cs; Daruma only if Pumpkin Finale is unticked or Finale Boss empty (:598-602); PlayGroundField phases /
  scatter / HideHat only after a rounds seed (PlayGroundField.cs:198-239); Phase_2/_3 never built (moot); GAMESTAGES
  "Tween PlayGroundZ To Boss" / "Destroy PlayGroundZ To Boss" / "Boss PG" have no incoming transition; "Gate 3 Pick"
  unused; inactive nested copies of the cauldron and hat inside Play_Ground_Z (prefab lines 13108-13177, 5642-5706).

## 2. Tunables (prefab:16894-17025)
- Cook 300 s; each log +1 heat (cap 3, fades 0.05/s); cook speed x(1+heat): 75-300 s in practice.
- Waves: Spider_Small max 4 alive every 2.5-4 s -> +Spider_Normal 5, 2-3.5 s -> Spider_Normal + Cockroach_Small 6,
  1.8-3 s -> Cockroach_Small/Normal 7, 1.5-2.5 s. Spawns >= 5 m from the player.
- Mini bosses (health x random size 0.75-1.33): Blue 85-95, Red 125-140, Yellow 130-145, Slime_Big 55-65; at
  1:15/2:30/3:45/4:30 (prefab labels still say 2:30...9:00).
- Cauldron 150 hp, bites 1/2/4; bosses never sent at it (:480).
- Trees 5-6 hp, 1-4 logs; they still release white slimes every 20-80 s.

## 3. Weak spots
- Menu trap: "Use Defend The Cauldron" re-points Path 1 ON -> "Tween PlayGroundZ To Boss" (CauldronDefenceMenu.cs:466):
  a re-run silently drops the rooftop tree defence. Its doc comment (:14-28) is stale.
- Doom boss ends the game: killing it sends GAMESTAGES the global "Win" (PumpkinBossBrain.cs:2135-2151) -> wait ->
  fairy reward -> Thank For Playing -> new run (matches the 11-07 rule "killing the real boss = a win"; confirm with
  the user it should hold for the Doom boss). Waves stop only on `over` (:422); teleport comes back only in OnDestroy (:310).
- Purple Hole (added 11-19, after the last gate-1 test on 11-18): the shield and the PlayGround both sit at world origin,
  reach 14 m (Rooms/PurpleHole.cs:41). At the win it eats props, hat, logs, unopened chests before the boss (untested in
  gate 1). On a death + coin revive (not verified) it could eat the Bracer chest, hat, trees or table -> possible
  softlock; mini bosses it eats drop their gear chest for free (:535-549). Its "Boss" keep rule is case-sensitive (:60),
  so pumpkin_boss_Z_Doom doesn't match it.
- The CookingRewards notice (pauses the game) shows ~0.3 s after the giant pumpkin lands (CookingRewards.cs:76-104).
- Minor: only one spare log ever (:389); an EnemyWave entry leaks after a loss; FindObjectsOfTypeAll at the win (:675)
  may hitch; GateFinalesMenu forces cook = 300 on every run (Gates/Editor/GateFinalesMenu.cs:55); "10 minutes" docs stale
  (:25, CauldronHealth.cs:25). No TODO/FIXME.
- Untested (notes 9b): the headset; a full real run; every balance change since 11-18.

## 4. Fun / pacing
- No guidance before the cook: nothing says what to do until the countdown appears.
- The hat is no search: always ~2 m from the cauldron.
- Tree pumpkins come only from broken table or skull pieces after a wait.
- Every wrong guess spawns a boss slime.
- Logs make the cook swing a lot: stacked logs cut it to ~75 s and bunch the 4 mini bosses together.
- Little pressure on the cauldron: > 8.5 m away only exists on the far side; a loss is either unwinnable or ends the game.
- Waves are samey: random puffs at the edge, no gates.
- Finale cramped: a 13 m/s charge crosses the field in ~0.7 s; its 6 m throw happens with the walls down.
- Dark: gate 1 never lifts the house fog.

## 5. Models
- All 40 side-path prefabs unchecked in Exports/Remodel/REMODEL_LIST.md:289-342 (Play_Ground_Z.fbx, trees, skull, pot,
  slimes, hat...).
- Nothing from Exports/PlayGround_Concept/BUILD_LIST.md built (50x50 m rings, 4 monster gates, swing/slide/sandbox...,
  the fog lift). The user places the hats (notes 8).

## 6. Shared-file risk
- SpiderBrain (edited 11-16 for gate 1: GoFor :1706, the IBiteTarget bite :5199) runs every bug incl. Spider_Tree.
- Shared code: EnemyWave/EnemyLimit (every EnemyManager); SlimeBody (gate 2); PumpkinBossBrain (the Doom boss is a
  variant of the final boss); Fist, HeldThings, PlayerLaunch, EnemyHitDamage; GameStages, EffectPool, WorldUI;
  TutorialNotice; DarumaStage; GateBossFinale, PumpkinBallBoss, GateExit (gates 1-3); PurpleHole.
- PlayGround-folder files used by the MAIN game: RunAwayPumpkin, LaughingPumpkin, NearGrabOnly, PumpkinBoom (room
  pumpkins), CookingIngredient (every Candy_N), Hideout.
- Shared prefabs: Pumpkin_Ball_Boss (all 3), The_Seed; the PG gear chests are variants of the main-game gear chests;
  Slime_Big/Blue/Red are the bases of gate 2's Slime_Showdown slimes; Spider/Cockroach Small/Normal (rooms),
  Pumpkin_On_Tree, *_Drop pieces, smokes.
- GAMESTAGES: the exit chain is shared with gates 2/3; gear bools + The_Seed shared with Path 2/3 ON; global "Win" goes
  to the ending.
- Menus: Use Defend The Cauldron / Use Cooking Rounds, Gate Paths, Gate Finales, Set Up Pumpkin Ball Boss, WIP Notice,
  Purple Hole, Gate 3 Picks, old PlayGround setups.

## 7. Test hooks
- CauldronDefence: "Test: Break The Cauldron" (:861), TestSkipTo (:869), static Current + events (:207-218, no code
  subscribers).
- Cauldron: "Test: Put The Right Recipe In" (:634), TestSetProgress, TestSpeed (reset every Play), LightFire.
- Others: CauldronHealth.TakeDamage, MagicHat.ShowRecipe, PumpkinBossBrain.ForceNextChaos.
- No jump-to-gate-1 menu. Fastest: in Play `GameStages.Fsm().Fsm.SetState("Clean for Path 1")`, close the notice, send
  FINISHED to the start chest, then the Test context menus + TestSpeed / TestSkipTo (profile `_claude_test`).

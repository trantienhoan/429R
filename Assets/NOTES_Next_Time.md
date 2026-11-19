# Next time: where we stopped (2026-11-11 night)

Two PCs: the **K: PC** (has K:/WORK_2026/08_04/_claude/ with the boss round-3 files) and the **second PC** (no K:). Claude's memory, backups and test scripts stay on each PC, so everything needed to go on is written here.

## RESUME HERE

**SMOOTHNESS + PURPLE HOLE + BEDROOM KIT + LIVING ROOM KIT (second PC, 2026-11-19 evening/night). Scene SAVED 20:09 (user OK); BOTH kit room swaps after it are NOT saved yet; nothing committed.**
- **Smoothness:**
  - **How it was measured:** an editor profile of the whole bedroom flow, with a bot playing on the throwaway save. Tools: Documents/429R_claude_backups/2026-11-19/smooth (prof_tick, bot_tick, smooth_run.py).
  - **Steady cost:** small, 2-3 ms of game work a frame.
  - **The freezes were room furniture sets.** Every spawn point creates its object the moment it's switched on, all in one frame: 97-162 ms.
  - **Fix:** StaggeredSpawn on the 10 room sets (Tools > 429 Game > Performance > Spread Room Set Spawns). Their simple spawn points are kept OFF in the scene and switched on 2 a frame. Restore Room Set Spawns undoes it. Result: mini room 162 -> 32 ms, bedroom 103 -> 7 ms.
  - **Sounds:** Preload Short Sounds set 182 clips under 1 MB to preload and 7 bigger ones to load in the background (189 .meta files changed).
  - **Not done, to suggest:**
    - Windows IL2CPP: the PC build is Mono, and first-time code costs 20+ ms; the module isn't installed.
    - SaveManager writes on the main thread (7.7 ms).
    - Enemy and piece spawn bursts (10-17 ms): pooling.
    - Smoke puffs at the head (GPU).
    - No headset check yet.
- **Purple hole** (user: "suck in everything else", candy stays):
  - **What it is:** PurpleHole on the Fairy Shield (Shield_Sphere), made by Tools > 429 Game > Effects > Purple Hole along with Prefabs/Effects/Purple_Hole.prefab, using the wall portal's materials. Sounds: whoosh.wav, om_nom_nom.wav.
  - **Where it opens:** all 9 shield uses: 8 GAMESTAGES clean-ups and PlayerDeath DEAD. It opens 5.5 m above the shield's centre.
  - **What it eats:** loose or breakable stage things within 14 m. They fly in as copies of their looks; the originals are removed at once.
  - **What it keeps:** candy, the player and what they hold, Fairy, Knight, bosses, anything named *_Base*/Skydome/Valley/Island, and anything bigger than 6 m.
  - **The shield** still sweeps, unseen, from 2.6 s. Smoke_Blast_Brown/Green are switched off while it eats.
  - **Tested:** the mini room clean-up ate 102 of 110 things, the room and candy stayed. Pictures in .../2026-11-19/purple_hole/pics.
- **Bedroom kit:**
  - **Files:** Exports/Remodel/BEDROOM_KIT_PLAN.md (analysis + swap steps) and Exports/Remodel/Bedroom_Kit.blend, made by tools/bedroom_kit.py (no overwrite without --force).
  - **What it is:** 3 m pieces with today's wallpaper and wood textures, sample rooms at 7.5 x 7.5 m.
  - **Window:** the user picked the old room's Wall02 window (86 faces), now in the kit window piece. Done live in their open Bedroom_Kit.blend, which THEY save; the script rebuilds the same piece from Bedroom_Walls.blend.
  - **Doors** (user: "2 kinds of door, 1 secret door as a short cut, 1 normal locked door"):
    - **BR_Kit_Door_Wide:** a doorway with the user's Wall05 frame.
    - **The two leaves**, separate objects that snap into it: BR_Kit_Door_Secret (Bedroom_Base_Z_Door, horseshoe sign, the small room's breakable shortcut door) and BR_Kit_Door_Locked (Bedroom_Base_Z_Door_2, W/C). Both whole and unchanged.
    - **BR_Kit_Door** stays as the open passage.
    - Done live (the user saves).
  - **Clean mesh** (user: "I want a clean mesh, not a messy kit"): the pieces were rebuilt the user's way (see the plan): one closed 0.345 m slab per wall with the fewest quads round openings, plus box baseboards, inner corner posts, and the user's door frame and window. No bevels, no crown, all quads, closed. Done live; the user saves.
  - **User edits:** removed the chain from BR_Kit_Door_Secret ("so it looks more breakable"), saved. The file is dirty again (roof, rim and lamp pieces added live, material slots repaired): THE USER SAVES Bedroom_Kit.blend. Export: `blender -b Exports/Remodel/Bedroom_Kit.blend --python Exports/Remodel/tools/export_bedroom_kit.py -- <repo>/Assets/Models/Rooms/Bedroom_Kit.fbx` (13 meshes).
- **Bedroom kit ROOM (user: "Yes, build it", same evening): BUILT, SWAPPED IN, editor-tested. The scene change is NOT SAVED yet (the user saves it).**
  - **Menus:** Tools > 429 Game > Rooms > Build Bedroom Kit Room (makes Prefabs/Bases/Bedroom_Kit_Base.prefab + Models/Animator/Bedroom_Kit_Animator.controller), Use Bedroom Kit Room (Bedroom_Base_Pos spawns it, and fits the furniture), Use Old Bedroom Room.
  - **How it moves:** RoomKit (Scripts/Rooms/RoomKit.cs) does the old clips in code: walls unfold from flat + grow with overshoot, the roof flies in last; going, the roof flies off first and the walls hop, fold and shrink. The floor never moves. The Animator keeps the old state names (RoomKitState on each), so GAMESTAGES, the light path and DropWallHungProps need no change. Hidden sets are invisible + not solid but stay ON, so the doors' FSMs run from the start like the old room.
  - **Doors:** the old doors' own parts (health, damage, wobble, break pieces, coin toss) on the kit leaves. The secret door hangs on the old hinge, stands ajar 14.4° and slams shut when the player is on the floor (its trigger now watches the kit's Floor Trigger; Close Door turns it in its own space). Its break pieces are Door_Break_Drop_No_Chain (a variant with the chain + padlock pieces unseen; they still turn into pumpkins).
  - **Layout** (from the old come clips): mini room secret door on the north wall, window on the south; big room W/C door on the west wall, chained door on the north wall, windows on the east (z -0.75) and south (x 0.75) walls.
  - **Furniture:** the mini room was 8.1 x 7.7 m, so 97 furniture spawn points in the 6 bedroom sets moved in (up to 0.37 m) to fit 7.5 x 7.5. Touching pieces move as a group (stacks stay neat), falling Chaos pieces start under the 3 m ceiling. Hung props now sit flush on the walls.
  - **Tested** (bot, throwaway save, 4 runs): both rooms come and go on time, light on/off, the hole, the furniture fit (0 pieces in the walls), the secret door's hits, wobble, break and coin (skull both times: the pumpkin boss folds the room). Pictures: Documents/429R_claude_backups/2026-11-19/bedroom_kit/build/play*. Not tried: the clover shortcut, the headset.
  - **Chained door (user: "Yes, add it"):** new kit piece BR_Kit_Door_Chained = the horseshoe door WITH chain + padlock (the old big room's second locked door), added live to the user's open Bedroom_Kit.blend (THEY SAVE it) and exported (14 meshes). In the big room's north wall, centred x -0.75 (old -1.4; the 1.5 m grid), with the old door's Health/Damage FSMs (300 health, tough furniture hits, regular break pieces with the chain). The welcome mat spots (Bedroom_Matt_2_Pos in --Bedroom-- and --Bedroom_Chaos--) follow it (+0.69 m). Tested: it breaks and the doorway opens.
  - **Not in the kit yet:** the bunting and curtains (part of the old wall mesh).
  - **Old room:** Bedroom_Base_Z.fbx/prefab stay (the living room is the same model).
- **Living room kit ROOM (user: "Yes, build it", same night): BUILT + SWAPPED IN (scene NOT saved).**
  - **Menus:** Tools > 429 Game > Rooms > Build Living Room Kit Room / Use Living Room Kit Room / Use Old Living Room Room (LivingroomKitSetupMenu.cs).
  - **Files:** prefab Prefabs/Bases/Livingroom_Kit_Base, controller Models/Animator/Livingroom_Kit_Animator, kit Models/Rooms/Livingroom_Kit.fbx (export: tools/export_livingroom_kit.py).
  - **Sets:**
    - **Living** (9 m): X door north, big double door east, the shortcut horseshoe door south with the old door's parts.
    - **Play:** the cross at 1x; the game still scales the room 1.5x. The 4 old brick walls (+ portals) close the arms, the old breakable ceiling is its roof, and its arm floors stay after GO4.
    - **Round-1 fall edges:** they watch a "Livingroom_Floor" marker under the Play floor, so they switch off when the playroom comes, as before.
  - **Furniture:** 36 spawn points moved. Living room pictures go out 0.21 m to the new walls, the playroom candles go in to the arm ends, and the 4 Jenga gates (2.33 m, wider than the 2.25 m arms) now stand in front of the arm openings.
  - **To redo the fits after a change:** reload the scene, then run BOTH Use … Kit Room menus once.
  - **Tested** (bot, throwaway save, 7 runs; save deleted after): the living room comes on spawn and goes at EM2 (light off); the playroom comes after the 1.5x scale and goes at GO4; the brick wall breaks; hung props sit flush (gap 0.01); the playroom fit is fine.
  - **Shortcut door tested** (user: "Yes, test it"): 75 health, two 60 hits break it, its chained pieces drop, the coin tosses (50/50) and the door is gone; the stage goes on. That run's coin was a skull (hurries the pumpkin boss); a clover broadcasts DOOR_SHORTCUT like the old door (GAMESTAGES global -> Door Shortcut).
  - **Game Over at the end of the playroom (2 test runs): SOLVED, a test artifact, not the kit.** A big-hit logger caught it: two Fat_Pumpkin_Enemy stomping the standing-still bot player (DamageHeavy 40-50 each, ~1 s apart). They are tree pumpkins woken by the candy from the bot's kills; the tree belongs to the starting rooftop, which the real game removes in "2 - The BR" (DestroyObject Rooftop_Base). The test jumps from Move to Fairy straight to CP LivingRoom / CP Scale LR, so that rooftop stays under the playroom. The bot can't kill fat pumpkins and only heals below 60, so two quick stomps kill (Game Over without revive coins).
  - **Test tools:** .../2026-11-19/livingroom_kit: lr_kit_run5.py + lr_kit_watch5.cs.txt (the kit watcher + player FSM snapshots at Rooftop_ON / Game Over + the shortcut door's states + a big-hit logger listing what's near the player). Gotchas: read prefab FSMs only after Fsm.Init, or LoadActions gives zeros and nulls (and console errors); for a clean test from a CP jump, destroy the Rooftop_Base var's object first.
- **Living room + playroom kit (user, same night: "check the next stage, analyze them, check their animation, then create a kit of livingroom and playroom"): KIT MADE.**
  - **Plan + analysis:** Exports/Remodel/LIVINGROOM_KIT_PLAN.md (rooms, doors, clips, flow, what the swap needs).
  - **Kit:** Exports/Remodel/Livingroom_Kit.blend (new file), made by tools/livingroom_kit.py. Renders in livingroom_kit_renders/.
  - **Pieces:** walls 3 / 1.5 m; LR_Kit_Door (1.40 m doorway + the user's frame) and LR_Kit_Door_Big (2.47 m); leaves X (chained), Double (big door) and Horseshoe (shortcut door, chained); floor; the old roof slab and lamp. Slabs 0.31 m, the wallpaper once up each 3 m wall, no baseboards (the panelling is painted).
  - **Sample rooms:** living room 9 x 9 m; the playroom cross at 1x (square 7.5 m, arms 1.5 x 3.3 m; the game scales it 1.5x).
  - **Waiting on the user:** check the kit and answer the sizes / 3 m height / reuse-the-breakable-ceiling-and-brick-walls questions, then "build it".
  - **Analysis tools:** Documents/429R_claude_backups/2026-11-19/livingroom_kit (lr_watch.cs.txt + lr_run.py: bot from CP LivingRoom; lr_walls.py: wall faces from the baked mesh). Gotchas: SkinnedMeshRenderer doesn't re-skin between renders in one editor call (bake it). After a CP jump from the rooftop start, the rooftop castle stays under the living room (test artifact).

**RUSH MOMENTS (user picked 2026-11-19, from their 10-22 wish "more enemies = more chaos"): BUILT + Editor-tested in both rooms (second PC, bot on a throwaway save; real saves untouched). Menu run in the open scene; the scene still needed SAVING at hand-off (check it has a Wave Rush on -EnemyManager 1 and 3). Not tried in the headset; nothing committed.** New WaveRush (Scripts/Enemies/WaveRush.cs) on -EnemyManager 1 (Bedroom) and -EnemyManager 3 (Playroom) via Tools > 429 Game > Enemies > Set Up Rush Moments. Once 6 of the room's 12 are down, 12 small bugs (Spider_Small / Cockroach_Small) pour in from the room's own spawn points (SpiderIdleSpawns / BeeSpawns), up to 8 at once (Enemy Limit still caps), with a laugh / roach screech + ShakeBigger; they join the manager's wave (and its CurrentlySpawnedStuffs holder, so Game Over clears them), so its own spawns and its boss wait; beat them all = 8 random candies burst where the last fell + a bell (give-up 45 s: reward by the player). Once per wave; re-armed on a new wave/loop (the manager's Init sets numberOfEnemies 0). All numbers on the Wave Rush component.
- Test (both rooms the same): rush started on the 6th kill; 8 alive at once, 12 in all; the manager sat in Wait for Slot while they were out; candies burst at the last kill; then the rest of the wave and the boss (Spider_Boss_PumpkinButt / Call Boss). A player standing still with 9 bugs on them died in ~20 s.
- The 11-09 balance list was already DONE 2026-11-10 (only a headset test left).

**00. Hit feel (K: PC, 2026-11-19, user picked "fixes 1-3"): built, compile-checked, Editor-tested with shots at a Spider_Normal (bot profile, real saves untouched). Not tried in the headset; nothing committed.**
- **Each fist or thing has its own hit window** (EnemyHitDamage, Fat Pumpkin): before, one shared 0.3 s wait per enemy silently ate a left-right combo's second punch and 2 of a Triple Punch's 3 fists. The same thing hitting again within 0.3 s is still ignored. Every Hit Counts now only means "a soft hit still does 1". Tested: two shots landing in the same frame both hurt; the same shot again 0.12 s later did not, 0.6 s later did.
- **SMASH, a third step** (Hit Settings > Smash): power 16 or more (twice a hard hit) does x3 (a throw x4, a flying jab +2), with a 1.6x pop, a crunch (rock_hit_1-3, swap them in Hit Settings), knock-back x1.5 farther / x1.4 higher and 0.3 s more stun. Tested: 6 damage from a weapon with Damage 2 (hard 4); knocked 2.25 m vs 1.50 m.
- **Hit freeze, the enemy only** (new HitFreeze.cs; Hit Settings > Hit Freeze): the player's hit that hurts stops the enemy dead (animation + walking): 0.04 s normal, 0.07 hard, 0.11 smash, +0.05 on a kill; no new freeze within 0.12 s after one (kills always). The knock-back waits for it (Knockback.Hold). Tested: the animator held at 0 for exactly those times and its speed came back, and the body stood still and then flew. Not on the Fat Pumpkin or the pumpkin ball boss (they get the smash crunch only). Not tried on pumpkin_boss_Z.
- **One stat, one move (fix 4, same day):** Fist Size now also adds to the closed punch: +1 damage every 3 points (Player Level > Punch Damage Every; a bare fist still taps for 1). Throw Power adds to thrown damage: +1 every 2 points (Throw Damage Every), on top of each thing's Thrown Bonus, so a hard throw triples it. Candy points count. The Level Up menu shows "punch +N" and "+N damage". Tested: punch 2/3/4 at Fist Size 3/6/9; at Throw Power 10 a thrown book did 7, and 21 when thrown hard (it did 2 / 6 before).
- **Red hit blink** (new HitBlink.cs; Hit Settings > Hit Blink): an enemy a hit hurts turns red (paint towards red + a little red glow), stays full for a third of the time, then fades: 0.14 s normal, 0.18 hard, 0.24 smash. It uses property blocks like the bee-boss bomb flash; each part's own block is put back afterwards (tested: cleared, so it batches again). Works on every EnemyHitDamage enemy (bugs, bosses, slimes, pumpkin_boss_Z) and the Fat Pumpkin. Not on furniture or things with Hide Hit Effect. The runner pumpkins and the pumpkin ball boss already had their own red flash and keep it. The chase pumpkins (Pumpkin_3/5/7, PlayMaker only) don't blink. Tested on a spider and a giant slime.
- Feel audit numbers (enemy health/toughness, damage per stat, hits to kill): Claude's memory 429r-feel-audit-2026-11-19 (K: PC).
- **EnemyManager 2 spawn points moved onto the NavMesh (scene SAVED by Claude, 2026-11-19; backup Documents/429R_claude_backups/2026-11-19/2 Game Scene_before_em2_spawns.unity).**
  - **What was wrong:** in the Living Room, 7 of the 8 points were off the NavMesh: 0.3-1.3 m past its edge (±3.67 m when the room first appears, ±4.25 after LivingRoom_GO), or under furniture.
  - **Why it rarely showed:** three things saved them. The FSM snaps each spawn to the NavMesh within 10 m, LandOnNavMesh catches any enemy that still appears off the mesh, and SpiderBrain re-places one that's been stuck off it for 3 s.
  - **The new spots** are good with both furniture sets (Stuffs and Chaos), at least 0.4 m from the NavMesh edge, with a full path to the player:
    - IdleSpawn (0.5, 3.25)
    - (1) (-0.75, -3.75)
    - (2) (-3.75, -1.75)
    - (4) (-0.5, 3.75)
    - (5) (0.25, -3.75)
    - (6) (3.5, -3.75)
    - (7) (-3.75, 2)
  - **Tested:** CP LivingRoom → LR - Stuffs → Enemy2 - Wait. Each roach appeared within 0.33 m of its point (only the FSM's ±0.3 m jitter), and all moved to the player.
  - **Door_Break_Drop_Pos_3 (the Cockroach_Boss) was left by the east door:** the nearest clear floor is 4.5 m away. LandOnNavMesh puts the boss on the floor 0.5-0.7 m away, and it walks fine.
  - **Side effects of that save:** the scene also got the two new Player Level values and SkyFlyers' current field list. The old `wingspanDegrees` was already unused, so no change in game.
  - **Also seen:** breaking a Living Room furniture piece drops Spider_Small spiders at the piece's height (y 1.1-1.3, once at z -6.1, outside the floor). LandOnNavMesh puts them on the floor.
- **The other EnemyManagers' spawn points (same day, scene SAVED; backup Documents/429R_claude_backups/2026-11-19/2 Game Scene_before_em0_em1_spawns.unity).** Measured with every furniture set, before and after each room's GO animation. A point counts as good when it is on the NavMesh, at least 0.4 m from its edge, with a full path to the player.
  - **EM0 (Mini Bedroom):** all 3 points were 0.4-1.0 m off the NavMesh. Moved:
    - (-3.09, 3.93) → (-3, 2.75)
    - (4.47, 0.63) → (3.5, 0.75)
    - (-3.28, -3.97) → (-3.25, -3.5)
  - **EM1 (Bedroom):** 5 of 6 points were 0.2-1.7 m off (IdleSpawn (4) at x 5.75 was outside the wall). Moved:
    - IdleSpawn (-1.62, 2.94) → (-2.75, 2)
    - (2) → (1.5, -3.5)
    - (3) → (3.5, 3.5)
    - (4) → (3.5, -0.5)
    - (5) → (3.5, -1.75)
    - (1) was fine, left as it was.
  - **EM3 (Living Room ×1.5) and EM4 (rooftop bee points):** already fine, unchanged. EM4's GhostSpawns aren't used any more.
  - **Tested:** the real waves (Mini Enemy, Enemy1 - Wait). Every spider appeared within 0.35 m of its point and walked to the player.
- **Mini boss chaos rebirth (user, 2026-11-19), built + Editor-tested:**
  - **When:** a mini boss (Spider/Cockroach/Bee Boss and their PumpkinButt versions) that the player kills without it ever hurting them comes back at once, 75% of the time, as a chaos one: ×2 health, ×1.5 damage, ×2.5 EXP, with the red Chaos_Aura (the chaos pumpkins' aura). It comes back once only.
  - **What counts as hurting the player:** a bite, a fist-blocked bite, a charge, a bomb, or a web ball or sting that lands.
  - **When it doesn't come back:** a boss the game kills (a fall, a crush, a stage clean-up).
  - **Handover:** the chaos one takes over the first one's death flags, waves, FlagWhenAllGone, and its name (the rooftop swarm counts bosses by name). So the stage only goes on when the chaos one is dead too.
  - **Where:** settings are in Assets/Resources/Mini Boss Chaos.asset (Tools > 429 Game > Enemies > Set Up Mini Boss Chaos Rebirth). Code: SpiderBrain (OnEnemyDied, BecomeChaos, LandOnPlayer), EnemyHitDamage.KilledByPlayer, WebBall, FlagWhenAllGone.Replace. Test hook: SpiderBrain.ForceNextChaosRebirth.
  - **The big Gate 1 slimes (also Boss size) are left out:** they split and get their colour from the cauldron.
  - **Tested:**
    - Spider: 76 → chaos 134 health, EXP 13, aura.
    - Bee PumpkinButt: 282 health, EXP 38.
    - Cockroach: 172 health.
    - Chaos bites 58 and 14, above a normal boss's maximums of 50 and 9.
    - It doesn't come back after biting, after a game kill, or when already chaos.
    - WhiteSpiderDown is set only when the chaos one dies.
  - **Not tried in the headset.**
- **Magic hat table smoke fixed (user: "scaling down at root, not puff out"):**
  - **Cause:** Door_smoke_black (a Smoke_Puff_Loop copy) on both hat tables and 9 treasure chests has its own FSM: Wait 4.2 s → TweenScale itself to 0.1 over 2 s → Deactivate Self. With the 3D puffs (Hierarchy scaling, Local space) that squeezed every puff into a column at its root.
  - **Fix:** new PuffOutWhenShrunk on the shared Prefabs/Effects/Smoke/Smoke_Puff_Loop (all copies get it). When the shrink starts, it stops that FSM, keeps the size, stops new puffs, lets the last ones drift up, grow and fade, then switches the smoke off and re-arms the FSM. Smokes that never shrink aren't touched.
  - **Tested on the LR hat table:** puffs stayed about 2× size and rose 0.02 → 0.65 m; the smoke was off 3.4 s after the shrink began.
  - **Treasure chests:** the user asked for the chest smoke too; it was already covered. All 9 Treasure_Chest_* and Bonus_Chest_Pumpkin have it (same FSM). Play-tested on Treasure_Chest_Seed with the same result.
  - The other Smoke_Puff_Loop copies (rooftop smoke_black, Pumpkin_Tree, the Bus/Elon Boong and Elon_Bullet smoke) are never shrunk, so it does nothing there. EffectPool turns copies off and on around a re-scale, which resets it.
  - **Rooftop smoke_black (user asked, same day):** never shrunk, but it vanished in one frame. The rooftop's Info "End" deletes the 4 *_Gate_Spawn 0.5 s after it starts, and a game over switches them off. New **Fade When Removed** option, on for those 4 copies only (Rooftop_Base_Z overrides; backup Documents/429R_claude_backups/2026-11-19/Rooftop_Base_Z_before_smoke_fade.prefab). It keeps a copy of the live puffs each frame, because a switched-off particle system has already dropped them by OnDisable. On removal those puffs go on in a loose copy where the smoke stood, with no new ones, deleted when the last one's life is over.
  - **Tested:** deleted (End) → 18 puffs faded out in 2.3 s; switched off (game over) → same spot and size, gone in 3.9 s. No errors.
  - **Pumpkin_Tree smoke (user asked, same day):** its Door_smoke_black loops 15-17 s with a sound, then its FSM's DestroySelf removed it in one frame; it also goes with the tree. Fade When Removed is now on for it too (Pumpkin_Tree override; backup .../Pumpkin_Tree_before_smoke_fade.prefab). Tested: on Destroy Self the 23 puffs went on in the same spot and size and were gone 2.3 s later.
  - Backup: Documents/429R_claude_backups/2026-11-19/Smoke_Puff_Loop_before_puff_out.prefab.
- **Music players' "Smoke_Spray" fixed too (user, same day).**
  - **What it is:** a copy of Prefabs/Effects/Music_Notes_Loop (the flat music notes). Its own FSM takes a 2 s break every 300 s: TweenScale to 0 in 0.1 s, Wait 2, back to Init.
  - **Fix:** PuffOutWhenShrunk with **Come Back** ticked on Music_Notes_Loop. At the break the notes stop coming and the last ones fade at full size; then the FSM restarts from Init and so do the notes.
  - **Covers:** all 6 copies (the Bedroom, Living Room, JACK and PlayGround music players, Play_Ground_Z, and Special Stage 3's Song Notes).
  - **Tested:** break forced in Play. Notes held 0.5 size, gone in 2 s, back to 6-7 notes.
  - Backup: Documents/429R_claude_backups/2026-11-19/Music_Notes_Loop_before_puff_out.prefab.

**0. NEXT (user, 2026-11-18 night: "Make the game run smoother and play fun first", before the remodel; build size left alone):** pick one first: (a) **smoothness**: profile a play session (bedroom bug fights, the 180-pumpkin rooms, bonus round, bosses: frame time, GC per frame, physics/animation/particles), fix the worst spikes, fade 3D smoke puffs right at the head; a code scan found no costly per-frame lookups, so it needs measuring (MCP server on, or Unity closed for a hidden run); (b) **fun**: the 15-point balance list (memory balance-pass-todo; the user died 3 times in the 11-18 recording, so check which points still stand). Also: Gloves.blend has unsaved changes in the user's Blender (thumb fix + chubby; the FBX already has them); nothing from 11-18 is committed.

**1. Pumpkin boss ROUND 3 (K: PC): prepared + compile-checked, NOT applied, NOT tested.** Everything is in K:/WORK_2026/08_04/_claude/boss_slide_2026-11-11/staged3/ (PumpkinBossBrain.cs, Editor/PumpkinBossSlideMenu.cs, PumpkinBossFx.cs, LiveNavMesh.cs; base3/ + base3_md5.txt = the repo files it was made from; they still matched at 17:05). What it does (all user decisions):
- **Cracks removed** (user: "not really good, remove it for now"; may model cracking parts later): brain `drawCracks` off; the menu takes the crack overlay off the renderer. The 1/5 damage on the pumpkin and the break at 20% stay. *Meanwhile* the cracks are hidden in the game by Boss_Butt_Cracks.mat `_Collapse = 1` (set 17:07; round 3 makes it proper).
- **Arms swing on hops:** each butt hop plays the user's `jump_slam` retimed (arms back on the crouch, up on take-off, forward-down on landing) with an Animator "HopSpeed" speed parameter; a "Face" override layer keeps his normal face (idle_true) while hopping; half the clip's hip turn reaches the waist.
- **Break and regrow instead of sinking** (user: the sinking up and down looked really bad): going down for punches/slam/clap/spin/dizzy, the pumpkin bursts (light burst, no candy) and his upper body drops 0.35 s to the old sunk height; going back up, the pumpkin regrows in 0.6 s with an overshoot. The transform grows the pumpkin out at 1.2 s. The real break (20%) still never regrows.
- **Heavier ground smoke:** wake puffs on both sides while sliding (thicker in the charge), a skid burst, a dust ring at hop take-off and a ground blast on every hop landing (boom/shake only near the player). Estimated peaks ~69 slide / ~93 charge / ~95 hop chain (limit ~98): measure.
- **Stone blocks (Stone_Cube_Dice 1/2/3, immovable):** hop arcs/landings checked against the pumpkin's real width; LiveNavMesh treats immovable bodies as never-walkable (all enemies path round them).
- **To resume:** (1) write gen_pairs3.py (copy of gen_pairs2.py: base3 → staged3 → pairs3), re-check the repo md5s against base3_md5.txt; (2) apply outside Play (fixlist_2026-11-11/rep.py), recompile, run Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup (log should say the crack overlay was taken off); (3) run test/run_case3.sh cases (no_overlay, hop_arms, drop_regrow, trail_particles, block_hop, block_behind, bugs_path, pics before/after) + round-2 regressions (momentum, charge_hit, bounce_hit, butt, pose, hops, crush) + test3/bk_test.cs a2_hopPast and b3_headOn; (4) show the user pictures of the break-and-regrow, the hop arm swing and the smoke.
- **MERGE FIRST (2026-11-11 night):** the second PC changed `PumpkinBossBrain.cs` and `pumpkin_boss_Z.prefab` for the hop damage (item 2), so those two no longer match base3's md5. Build round 3's pairs from base3 → staged3 as planned, but apply them to the CURRENT files. The hop change touches only three places: (a) the hop fields after `hopStretch` (tooltips, `hopHitReach` 2, `hopHitEvent` "DamageHeavy", new `[Range(1f, 3f)] hopNearShare = 2.5f`); (b) the top of `HitPlayer(string healthEvent, Vector2 launch, Vector3 from, float share = 1f)` (`if (IsChaos) share *= chaosDamage;`, then `PlayerDamageShare.Next(share)` before the event and `Take()` after, only when share isn't 1); (c) the end of `HopLands()` (`float outside = hopHitReach > 0f ? Mathf.Clamp01((near - BodyRadius) / hopHitReach) : 0f;` and `HitPlayer(hopHitEvent, hopLaunch, transform.position, Mathf.Lerp(hopNearShare, 1f, outside));`). If a round-3 pair hits one of those spots, apply round 3, then redo the hop change from this description. The prefab only got the 3 hop values (hopHitReach 2, hopHitEvent DamageHeavy, hopNearShare 2.5).

**2. Done 2026-11-11 night (second PC), all compile-checked and Editor-tested** (save profile `_claude_test`, deleted after; the user's slots untouched):
- **Hop landing damage (user: the closer the player, the more damage; x2.5 right next to him):** a hop landing within 2 m of his pumpkin's edge sends **DamageHeavy** (40-50 before Toughness) times **x2.5 right next to him (100-125)**, falling off to x1 at 2 m; nothing further out. A chaos boss adds its x1.3 (all shares are capped at x3). Tested (a chaos boss): 0 m 135, 0.2 m 141, 1 m 91, 1.8 m 66, 2.4 m no hit, all in the expected ranges.
- **NEW PUNCH after dying in a stage (user: "Yeah"):** a punch (or exploding throws) bought inside a special stage, then a game over there: the notice now shows 3 s into the next run, once the player is up (never over the death). PlayerLevel.cs ("next run news"). Tested for real: Fist Size bought in a stage, death with no coins left, the restart about 4.5 s later, NEW PUNCH! (Jab) 3 s after that.
- **Wingless bees never sting:** any flier that has lost its wings now only bites (before, only the kamikaze bees stopped). SpiderBrain.TryStartWebShot. Tested on Bee_Boss_PumpkinButt with the sting cooldown forced off: with wings 9 stings in 8 s, without wings 0 in 12 s (chase and bite only).
- **Candy vacuum: it was already fixed** by the afternoon boss rework (the bug report was from the morning test, before it). Tested: 8 candies dropped round the boss were all eaten within 1 s (first form) and 2 s (true form on the giant pumpkin).
- **Fairy lock tower: now fully tested.** On release it moves to the Fairy's parent and the sound plays to its end (1.4 s) although the Fairy is destroyed 0.4 s in; then it removes itself. The brightness looks right in a picture (second PC: Documents/429R_claude_backups/2026-11-11/pc2_fixes/pics/tower_steady.png).
- **Git noise:** `.gitignore` now ignores `*.spp.meta` and `*.unitypackage.meta` (Unity makes them for the ignored Substance and package files). **Commit the deleted `Assets/Models/TEST/Build_Square_Slot.unitypackage.meta` once:** its package isn't in git, so Unity deletes the meta on every start.
- Backups of the changed files (second PC): Documents/429R_claude_backups/2026-11-11/pc2_fixes/originals; the test scripts are next to them (pt_*.py).

**2b. Done later 2026-11-11 night (second PC), compile-checked and Editor-tested unless said:**
- **Steam demo App ID 5604260** is in `Assets/Resources/Demo Settings.asset` (new: commit it). The demo has no store page of its own (user: leave it unticked). Next: Tools > 429 Game > Steam Demo > Build Steam Demo (Windows) → `E:/429_Demo`, then SteamPipe-upload to the demo's depot (usually 5604261; Claude can write the upload script).
- **Game icon = the new v6 icon:** `Assets/Images/429_icon.png` (the default icon, used by Windows) and the 4 Android icons (legacy, round, adaptive fg/bg). Old ones: Documents/429R_claude_backups/2026-11-11/game_icon/ (second PC).
- **Reddit banner:** `Exports/Steam_Art/renders_v6/Promo_Reddit_Banner_1920x640.png` + a 305 KB `.jpg` (Reddit's limit is 500 KB); the shot is in `v6/render_user_v6.py` (only=Reddit).
- **Box bombs calmer (user: "so dangerous"):** Paper_Box_1-5 chance 30% → **15%**, rest between attacks 3 → **7 s**, face hit 20-25 → **12-15** (faceShare 0.3). Not played in the headset.
- **Pumpkin rooms x4 (user):** bedroom 45 → **180**, mini bedroom 35 → **140**, Pumpkin_On_Tree limit 140 → 560, rain 6 s; refill stays **12 more below 12** (user). **No rolling pumpkin bombs while a pumpkin room runs** (user; TurnsIntoBowler.Hold/Release). Each dropped pumpkin can't be hurt for its first 2.5 s (a third broke landing on the pile), and the room's pumpkins and its stage pumpkin take **only the player's hits** (EnemyHitDamage.PlayerHitsOnly): one broken pumpkin's flying pieces broke its neighbours in a chain (474 re-dropped in 2.5 min). That is also the answer to "why does Pumpkin_1 get hit by a normal pumpkin break": the pieces hit the mini bedroom's stage pumpkin. Tested: 180/180 stay, the pile sleeps, ~160 fps in the Editor (empty room ~257, rain 136-187); breaking 3 removes exactly 3. **The scene values were saved by the user at 20:21.**
- **Flying punch 7 damage at Fist Size 1 (user asked why): not a bug, the bonuses stack.** A flying punch does (1 + Fist Size) x Elon Boong (x2) + 1 hard hit + 1 red-hot hands; eaten red candies count as Fist Size points (~0.6 each at 1 point; the user's 2026-11-10 rule). E.g. Elon + one red candy: 6, hard 7. The user may still want candies to give size only, or a cap: ask.
- **Fat Pumpkin + Elon Boong:** the Elon punch's small blast landed a moment before the punch and started the fat pumpkin's 0.2 s hit pause, so the punch was ignored (1 damage a punch). Blasts no longer start that pause (FatPumpkin.Hit). Not tried with a real punch.
- **Pumpkin health (user, 2026-11-11 late):** runners x3 (Pumpkin_0/1/2/4/6 = 12/15/18/24/24; Chaos 1/2/4/6 = 24/27/30/36), Mob Pumpkin_7 5, chase Pumpkin_5/3/7 = 3/4/5, Pumpkin_Tree 100. **Fat Pumpkin:** starts at 6, +2 health a candy (at once), giant 150 (plus what won fights saved up; a won fight no longer doubles a small one). BalanceEnemiesMenu matches. Prefab values; nothing in the scene overrides them (only Pumpkin_Big, a copy of Pumpkin_10, overrides its health to 50).
- **Only the player's hits hurt pumpkins (user):** EnemyHitDamage got a saved **Player Hits Only** setting, on for Pumpkin_0/00/1/2/4/6 (+ Chaos), 6 1, 6 2, 51, On_Tree, Big, StartGamePumpkin (and the bonus round's copies) and pumpkin_Z; furniture the player holds or just threw still counts as theirs. Fat, Laughing, Mob and chase pumpkins already took only the player's hits.
- **Fairy Shield pops Fat Pumpkins, giant too (user):** it bursts as usual with no candy and no EXP (FatPumpkin.shieldTag). Tested: a small and a 150-health giant both popped.
- **TO DO later (user):** Laughing pumpkins (Pumpkin_8, 9, 9 1, 10) have 1 health; the user wants "at least 500 stamina". Their stamina today is METRES they can run before they're tired and stunned (3/4/5 m by round; hits take Metres Per Damage off it), so 500 would mean they never tire: agree with the user what it should mean first.
- **Candy (user: leave it):** a candy adds 0.5 point (x1 + 0.2 per own point) up to 5, then 0.1 up to 10; Weapon Efficiency and Fist Size points (candy ones too) are +1 damage each, which is why damage climbs fast with candy.
- **Fat Pumpkin damage numbers:** DamageNumbers.Show(target, damage, big, kill) (new) for health kept outside a Health FSM; FatPumpkin calls it. Tested: two hits showed "2", "2".
- **Pumpkin_00 still moving in the hand:** HeldStill paused Look At only in the FSM's current state, so the Health FSM's state change (9 s after it appears, then every ~78 s) turned it ~18° and the hand dragged it back. Now Look At, Smooth Look At and Tween Punch are switched off in every state while held. Tested: held 14 s past the change, 0 turns (before: 14 turns, 88°). Runner pumpkins too.
- Backups (second PC): Documents/429R_claude_backups/2026-11-11/ (box_bombs, pumpkin_rooms, fat_pumpkin, held_still).

**3. User decisions (2026-11-11 night):**
- His forearms in the roar slide pose stay ~9% bigger ("Bigger = Cooler"). No change.
- Graffiti on the boss (user: remove it, or keep it simple). **Nothing like it exists in the project on the second PC:** no decal projector, no graffiti object, material or texture in any prefab or the scene (an old Graffiti material and textures were deleted in 2025). On the rooftop the yellow marks are the lid's carved pumpkin face and the pink skulls are the stone dice. If the K: PC still shows graffiti on the boss, it's only there: show Claude a picture of it there.
- The pumpkin's damage look: the user may MODEL cracking parts later (e.g. chunks Butt_Chunk_1-3 that burst off at 33/66/100%, or 2-3 damage-stage models); the code already tracks the damage 0-1 (ButtCracked).

**How a punch is bought (the user asked):** level up, then make two fists (grip + trigger on both hands) and bump them together twice to open the Level Up menu. Each point in **Fist Size** makes the fist bigger; the punches unlock at Jab 1 point, Swollen Hand 6, Triple Punch 10 (Swing and Uppercut are locked for now). NEW PUNCH! shows when the menu closes.

**4. Fairy bone cage → purple "lock tower" particles (K: PC): APPLIED and fully tested.** Menu Tools > 429 Game > Fairy > Fairy Lock Tower (run; re-runnable). Fairy.prefab: The_Cage keeps only its trigger box + kinematic body + Start FSM (the lock still breaks when the StartGamePumpkin drops / the Fairy Shield clears it); its bone mesh, the bone drop (The_Cage_Drop_Pos FSM) and the 3 Skull_Candle_Purple (they were EditorOnly anyway) are gone; a nested Fairy_Lock_Tower (Prefabs/Effects/Fairy_Lock_Tower.prefab; materials Data/Effects/Fairy Lock *.mat; script Scripts/VFX/FairyLockTower.cs) shows a turning rune circle, 8 circling purple bars, a top ring and spiralling sparks (~30 particles, no lights) and on release fades, sweeps a ring, bursts sparks and plays glass_break_2. **No scene change** (GAMESTAGES' Fairy_Pos spawns the Fairy). Backup: K: PC Documents/429R_claude_backups/2026-11-11/fairy_lock/Fairy.prefab. Now unused: Prefabs/Items/Broken_Stuffs/The_Cage_Drop.prefab, The_Cage.fbx (left for the user to delete; PumpkinLaughSoundMenu still lists the drop prefab).

**5. Not tried live yet:** Right Ctrl (video) and Right Shift (photo) work ONLY while the 429R Editor is the focused window (Scripts/Cam/CaptureKeys.cs).

**6. GLOVES (user, 2026-11-12, K: PC): APPLIED + Editor play-tested (all tests PASS), not yet tried in the headset, not committed.** Files: K:/WORK_2026/08_04/_claude/gloves_2026-11-12/ (staged/, apply.py, tests/TEST_PLAN.txt, tests/gl_log.txt, tests/pics/). The Set Up Gloves menu has been run (twice). Originals of the changed scripts: Documents/429R_claude_backups/2026-11-12/gloves/scripts_before.
- Fixed after the play test (WeaponSkills.cs): Bomber Man no longer blows up a thing you just let go of above the floor (only the throw's own speed counts, not falling); Impact / Weapon Master no longer miss real swings (the weapon's fastest speed over the last few physics steps counts). Re-tested: drops of 28/50 cm and a thing let go lying still = no blast; a 2 m/s toss = Bomber blast, 1.2 m/s = none; held-weapon test 5 of 5 runs PASS.
- Looks to check in the headset: the glove sits ~3.7 cm toward the pinky (looked fine in pictures); the skill blasts' flash is a white starburst that gets cut where it meets a wall (minor).
- Each round the Tip Knight has ONE random pair of gloves hanging from his boong (Cay_boong in Knight.fbx), never the same kind twice in a row; touch to take them; kept until Game Over (a revive keeps them). Kinds: **Boxing** (+3 Fist Size; glove points DO unlock flying punches for the round), **Throwing** (+3 Throw Power; throws explode from the start), **Soldier** (was Warrior; +3 Weapon Efficiency). Round-only points like candies (capped with candies at 10).
- **No gloves = grip + trigger does nothing** (no punching). With gloves, grip + trigger pops a glove onto the hand (the hand no longer swells); the glove is the punching collider, grows with Fist Size, and flying punches are glove copies. Placeholder gloves (red/green/blue mitts) until the user models the real ones (swap the "Model" child in Prefabs/Player/Gloves/Glove_<Kind>_<L|R>).
- Saved with the round (continuing from a checkpoint keeps them). Older saves without glove fields: no gloves until the next round (user: strict).
- Fixed on the way: the Level Up fist bump works with bare squeezed hands (it needed fists: without gloves nobody could spend points); the start pumpkin's "MAKE A FIST" tip page hides without gloves; the radio skip also takes a tap with a held thing. The rooftop "Wait For the Punch" isn't a player punch (it's the boss's roof smash), so nothing gets stuck. Kept (user): bare squeezed hands still shatter pop-ups; a once-per-slot "GLOVES!" tip (pauses); NEW PUNCH! waits for gloves; gloves punch in special stages (their points drop there).
- The old "Lock Swing And Uppercut" tick is gone (round 4 unlock tree below). A punch (or exploding throws) unlocked by gloves gets its NEW PUNCH! card ~3 s after the pickup, once per save slot.
- **Flying punches are harder without Boxing gloves** (user, 2026-11-12): Boxing keeps today's conditions (jab 2.6 m/s, perfect 5 m/s, 0.4 s, straightness 0.75, aim 60°; swing 3.2 / uppercut 2.8); Throwing/Soldier (any kind without "Easy Flying Punches" in Glove Kinds) use a harder set: 3.2 m/s, 6 m/s, 0.32 s, 0.85, 40°, swing 3.8 / uppercut 3.4. Both sets are fields on each controller's Fist (tune in the headset). It only matters for players with their own Fist Size points (non-Boxing gloves don't unlock the Jab).
- **Unlock trees (user, 2026-11-12; staged with the gloves):** only points the player SPENDS on level-ups (+ the gloves' +3) unlock; candy only raises the basic numbers. Fist Size: Jab 1, Swing 3, Uppercut 5, Swollen 6, Triple 10 (the old Swing/Uppercut lock is gone). Throw Power: Explosive Objects 1/4/7 (EVERY hard throw explodes; reach 0.6/0.9/1.2 m, damage 2/4/6), Bomber Man 10 (every throw explodes at impact and again 0.5 s later, 1.4 m, 6+6). Weapon Efficiency: Impact 1/4/7 (a held weapon knocks a thing into another thing: a blast hurts both, 0.5/0.8/1.1 m, 2/4/6), Weapon Master 10 (every weapon hit explodes, 0.9 m, 8; furniture too). Gloves: Boxing = Jab+Swing+Uppercut at once, Throwing = Explosive Objects 1, **Soldier** (was Warrior) = Impact 1; all +3 points towards the next levels. The old bonus stat "Weapon Master" now shows as **"Strong Grip"**. Own thrown blasts still bite within 0.5 m; Impact/Weapon Master never hurt the player. Never explode: candy, lollipops, seeds, coins, chests, cooking things, firewood, puzzle pieces, Jenga, bowling balls, magic hats, boongs, candles, Legendary gear. Numbers in Assets/Resources/Blast Skills.asset (made by the menu).
- **Notices fly out as cards (user):** unlock notices AND level-up notices pop out ~1.25 m in front of the player, never pause the game, break on any hit (a squeezed bare hand too), fade after 7-8 s; up to 3 at once. Only the once-per-slot GLOVES! tip still pauses.
- **Done:** applied, menu run, tests passed (above). Left: the user tries it in the headset (glove on the hand, hang spot on the Knight's boong, flying-punch feel with and without Boxing gloves) and models the real gloves (swap the "Model" child in Prefabs/Player/Gloves/Glove_<Kind>_<L|R>). Boss round 3 (item 1) changes different files, so it can still be applied on top.

**7. GATES (user, 2026-11-12): no gate 3 stage for now.** "We are doing too much and spread too thin": the PlayGround (gate 1) and the bowling stage (gate 2) are not in a playable state yet and need work first. Gate 3 now sends the player into gate 1's or gate 2's stage, half and half (GAMESTAGES: "Stage 1 - The Start" next3 -> new state "Gate 3 Pick" = Send Random Event next1/next2 -> Clean for Path 1 / Clean for Path 2). Made by Tools > 429 Game > Paths > Gate 3 Picks Gate 1 Or 2 (Scripts/Paths/Editor/Gate3PickMenu.cs; "Gate 3 Back To Gate 1" undoes it). **The scene must be SAVED to keep it** (2 Game Scene; run the menu again on the other PC after pulling, or pull the saved scene). Gate prices unchanged (50 Yellow / 5 Green / 1 Boss Seed each); a bought gate stays open for good (Unlocks).

**7b. GATE 3 = BALLS (user, 2026-11-16): being built (step 1 of 6).** Gate 3 becomes the user's web game BALLS (K:/Repository/BALLS/Balls) in VR: the player is a ball collider (head never rolls), last man standing in the BALLS valley (lawn inside the rock border, 6 holes, shrinking valley). Punch balls; squeeze both gloved fists = charge special move, dash where you LOOK, hitting everything on the path. Killing slimes HEALS the player; the player never grows; bots still grow baby balls. Teleport only onto the lawn inside the rock border. User answers: falling into a hole or 0 health = Game Over chain (coin toss may continue); the player always has gloves by then; 4 rounds, more bots and more aggressive bots each round; player keeps level stats; dash aim = where you look; keep the BALLS valley art + a NEW bluer sky sphere with a few clouds. Seeds like gate 2: start chest = Bracer, round wins = Chest/Head/Shield, final win = The_Seed, then GAMESTAGES goes to the rooftop/tree/Pumpkin_Boss_Z. Steps: 1 valley + bots (player watches; Quest frame rate), 2 player ball + rolling + lawn teleport + holes, 3 punches + special move + being hit, 4 slimes heal + shrink + last man standing, 5 rounds + seeds + GAMESTAGES Path 3 (replaces Gate 3 Pick), 6 headset tuning. Code goes to Assets/Scripts/BallValley (namespace Balls, 429R edits marked "// 429R:"), art to Assets/BallValley. Gate 3 Pick (random gate 1 or 2) stays until step 5.
- **Step 1 DONE + Editor-tested 2026-11-16** (bot profile): Tools > 429 Game > Ball Valley > Set Up Ball Valley run (prefab Assets/Prefabs/BallValley/Ball_Valley.prefab), Watch Ball Valley (Play mode) shows it. All checks pass, 0 errors; a round ends (winner) and round 2 starts with 6 bots; ~23k triangles, ~120 renderers, ~35 rigidbodies. Fixed on the way: bots kept BALLS' full-size hole distances (barely shot, the last phase stalled) → scaled, and in the Moment of Truth bots get angrier over 15 s; the opening cutscene's end put the house fog back over the valley → the valley keeps its own look; the sky now draws after the valley (cheaper on Quest). The game files ported from BALLS are rebuilt by tools/port.py: change them THERE. Quest frame rate: the user's headset check.

**7c. GATE 3 SKY (user, 2026-11-16: "more 429R style, halloween vibe"):** the valley uses its OWN sky (an inside-out ball that follows the head, unlit, no fog), showing a painted Halloween dusk: deep blue top, violet, orange glow at the horizon, a big harvest moon with bats, a few dark clouds lit orange underneath, stars. Picture: Assets/BallValley/Sky/BallValley_Sky.png (drawn by K:/WORK_2026/08_04/_claude/ballvalley_2026-11-16/tools/halloween_sky.py; paint over it or swap it freely, it's a normal 360 picture: left edge = +Z, top = straight up). The valley light now comes from the moon (Ball Valley Stage: Sun Angles 32/20/0, warm orange, violet ambient). **2026-11-16 later (user: "use this sky for main game"): Tools > 429 Game > Look > Sky: Halloween** makes Assets/BallValley/Sky/Halloween_Skybox_Mat (Skybox/Panoramic, same picture, rotation 270 so the moon is where it is in the valley) the scene skybox and the head camera clears to it (was a flat fog colour); "Sky: Fog Colour" undoes it. Ambient (Flat) and reflections (Custom, none) unchanged. **Menu run; the user must SAVE the scene.** Sky picture mipmaps OFF (no seam). The gate 3 King crown is removed (user). **Flying slimes instead of painted bats (user: "reuse the slime"):** the sky picture has no bats now; the menu also adds a scene object "Sky Flyers" (Scripts/Lighting/SkyFlyers.cs): 6 copies of the Flying_Slime model (no AI/colliders/shadows), nearly black, looping round the moon with beating wings, faces turned a bit toward the player, now and then one swoops far across the sky; 27-33 m from the head (inside the house's 40 m far plane; past the 22 m fog they're black silhouettes, in gate 3 their eyes glow). Tune on the Sky Flyers object (size, loops, swoops, colour). Before that: the OLD Skydome (Prefabs/Bases/Skydome, used from Walk Tutorial on) was NOT visible in the main game: its material Base Color is black, it is 75 m away while the camera only sees 40 m, and the house fog (6-22 m, near-black) covers everything past 22 m. So the main game "sky" is the dark fog colour. Left as is.

**8. GATE 1 REDO = "Defend the Cauldron" (user, 2026-11-16): APPLIED + Editor play-tested (bot profile); the user must SAVE the scene.** Menu Tools > 429 Game > PlayGround > Use Defend The Cauldron (run; "Use Cooking Rounds (Old Gate 1)" undoes). Changed: Cauldron.cs, SpiderBrain.cs, PlayGroundField.cs, DarumaStage.cs; new: CauldronDefence.cs, CauldronHealth.cs, IBiteTarget.cs, Editor/CauldronDefenceMenu.cs; prefab Fire_Woods_n_Cauldron (backup in Documents/429R_claude_backups/2026-11-16/gate1_defend), new Prefabs/Enemies/pumpkin_boss_Z_Doom; GAMESTAGES Path 1 ON next1 -> Tween PlayGroundZ To Boss -> Destroy PlayGroundZ To Boss -> Boss PG -> clean it -> BOSS. Numbers: near 7 m / far 8.5 m, cauldron 150 hp (bites 1/2/4), mini bosses Slime_Big_Blue/Red/Yellow at 25/50/75% -> Chest/Head/Shield seed chests, Daruma win 8 of each candy (48) auto-fed. Tests: K:/WORK_2026/08_04/_claude/gate1_redo_2026-11-16/tests (TEST_PLAN.txt). Less cooking, more fighting: chest seed (Bracer) pops the cauldron out (big smoke, player pushed back); the fire is small until firewood is thrown in, then big; find the hat (the user places/spawns the hats) for the recipe; ONE right recipe starts a 10-minute cook: walls drop, monsters keep coming and attack the player when the player is near the cauldron, the cauldron otherwise (cauldron has health like the Pumpkin Tree). Mini bosses at time stages drop the gear seeds (Chest/Head/Shield). Cauldron broken = Chaos Pumpkin_Boss_Z + endless monsters + teleport ground broken (sure death). Success -> the DARUMA game (Special Stage 3) -> a Daruma win -> straight to the FINAL BOSS (no tree defence; user changed this the same day). Daruma win gives a big candy reward (the cauldron fight gives few candies) so the player meets Pumpkin_Boss_Z stronger. The old 3 rounds, laughing pumpkins, white slimes and the Daruma stage leave gate 1 (code kept). Files: K:/WORK_2026/08_04/_claude/gate1_redo_2026-11-16/. Also 2026-11-16: the sky slimes are smaller (Sky Flyers wingspan 2.5-3.8 degrees; scene not saved yet).

**8b. GATE 1 ANSWERS (user, 2026-11-16) - done:** firewood: each log makes the fire roar for a while and the countdown faster (kept as built). A 4th mini boss: Slime_Big at 9:00 (90%) with a plain reward chest (PG_Treasure_Chest_Drop, no gear) - "more = fun". Daruma prize: 42 candies, each a random kind (DarumaStage Prize Candies; fed + topped up so all count; about +3.5 a stat). Laughing pumpkins / white slimes stay off. The Path 1 ON notice title is now "WORKING SITE" (PlayGroundNoticeSetup too). **"Quit boss = Re-dream" (every gate and the main game):** Checkpoints.NoResume = {"CP Boss"}: a save left at the boss fight loads as a new run (Re-Dream: candies/items/level kept, no checkpoint, all revives, no gloves), and the pause menu hides Get a Rest there (Checkpoints.AtBoss). Scene must be SAVED (notice text + sky + flyers + gate 1 GAMESTAGES).

**9b. 2026-11-18 later: MENUS RUN + EDITOR PLAY-TESTED (no headset). Scene SAVED 19:34 (user OK'd; GAMESTAGES gate paths checked in the file).** User rules: **fall into a hole = a normal fall** (view drops, black, put back where you stood 2 s before, FallDamage 55; 0 health = the usual death) and **gate 1's cook = 5 minutes** (prefab + CauldronDefenceMenu). Tested through GAMESTAGES on save profile _claude_test (deleted after):
- Gate 3: Path 3 ON -> valley, rules notice, 15 balls, candies (Candy_N, grabbable, hover 0.7 m), all in -> "ALL IN!" -> shrink 6 s -> boss 2 s later -> chase / wind up / charge / dizzy at the edge -> broken -> The_Seed where it broke + gear bools -> eaten -> Enemy 4 -> Rooftop_ON PG -> Whack 6, player 3.5 m south of the roof's middle facing it, valley look given back (fog, far 40). 0 errors.
- Fixed from the test: bots killed a standing player in a minute -> **1 hunter at a time (Max Hunters), a bot that hits the player leaves them alone 10 s, plain crashes only shove (hurt from 12 m/s or a special move, at most every 1.5 s)**: a standing player now loses ~30 hp in 40 s (was ~90); the shrinking border left a player outside the field -> it sweeps them in (not in a hole's gap: the hole may swallow them = a fall); the start spot avoided trees/rocks but counted candies; a candy destroyed by the game (candy limit) broke the bots' food search -> off the list on destroy; the boss pushed 3 s into a corner when the player was beyond the edge -> skips that charge.
- Gate 2: sky = the Halloween skybox (moon, stars, sky flyers): fine; its fog (14-38 m, near-black) leaves a dark band at the horizon (offered: tint it). Pumpkin finale (forced): drops on a lane end (21 m), charges down the lane, dizzy at the dirt's edge (3.9 m out), leaps to another lane; broken -> Won -> seed -> rooftop -> Whack 6. 0 errors.
- Gate 1: cook 300 s, the win -> no Daruma, the pumpkin drops where the pot burst; the PlayGround roam area is only ~11 x 12 m -> **gate 1's pumpkin radius 1.5** (Finale Radius); it landed 1 m up (the floor ray hit the pot) -> fixed; broken -> seed -> PlayGround sinks 11 m while the player is held in the black (no fall) -> rooftop -> Whack 6. 0 errors.
- The rooftop's Pumpkin Tree only appears at Roof_Start (the boss), so the exits stand the player south of the roof's middle during Whack 6/7.
- Not yet: the headset (feel of shoves, the dash, fists-together charge), a real punch on the boss and balls (only Special Hit was called), gates 1/2 played through their whole rounds.

**9. GATES END LIKE GATE 3 (user, 2026-11-18, second PC): CODE DONE + compile-checked, NOT play-tested yet (Unity MCP was off), menus NOT run yet.** User: gate 3 flow = run around, survive, eat candy, push balls into holes; 15 balls; all 15 in OR the last phase -> the field at its final small stage -> a giant pumpkin-head ball boss drops (bigger than the holes, too heavy to push out, only attacks hurt it) -> break it -> The_Seed -> defend the tree like the main game. "Gate 2 and 1 learn from gate 3 ... make it fun, make it simple." Also: "in gate 3 change all the berry on the field to random candy".
- **Gate 3 (Ball Valley, Scripts/BallValley):** Ball Valley Gate (15 balls, pocketed = gone, first shrink 40 s, last phase at ~190 s, smallest 0.5 = 25 x 12.5 m, finale shrink 6 s, boss 2 s later; rules notice; a lazy line under the view: BALLS n / FIELD SHRINKS! / BOSS hp / TAKE THE SEED!). Ball Valley Player (the player on foot: an invisible trigger ball the bots hunt; a ball crashing in throws the player (0.32 m per m/s x sqrt(weight), max 5 m) and hurts from 7 m/s or a special move ("Damage"); walking into a ball pushes it; punches knock balls (Fist.Push + Knock); punched slimes pop into candy; SPECIAL MOVE = both fists made and held within 0.3 m for 1 s, pull apart = 12 m/s dash for 0.35 s where you look, stops at a hole's rim, knocks balls at 16 m/s, 10 damage to the boss, 3 s cooldown; head over a pit = fall: view drops + black, put on the lawn, then the usual death (DamageToPlayer; coins may revive); teleport only onto the lawn (Valley.MakeTeleportFloor). Berries = random Candy_1/2/4/5/7/8 (Pellet.SetupCandy: hover 0.7 m, touch nothing, bots eat by rolling over; grabbed = a normal candy). BallPlayer.vrBody (follows the player, no own doings).
- **Giant pumpkin (Scripts/Gates/PumpkinBallBoss.cs, Prefabs/Gates/Pumpkin_Ball_Boss):** the Bowling Pumpkin's model/laughs/roll warning/roll/pieces grown to radius 2.6, Fat Pumpkin's stomp + yelps, health 150 (gate 2: 100, radius 1.3), mass = Push Weight Immovable, keeps its height (never falls), stays in the arena; drops in, chases at 3.2 m/s, every 3-5 s winds up 1.6 s (laugh + choo-choo) and charges at 13 m/s; a charge stopped by a wall/rock/arena edge = DIZZY 3.5 s (stars, hits x2); charge into the player = DamageHeavy + 6 m throw, rolling bump = Damage + 2.5 m; only the player's hits (Enemy Hit Damage's rule), blasts, the special move; breaks into 14 candies (gate 1: 42) + 30 EXP, then **Gate Exit** (Scripts/Gates/GateExit.cs: The_Seed where it broke, gear bools guarded, black, player held in place, rooftop, put 3.5 m in front of the Pumpkin Tree).
- **Gate 1:** Cauldron Defence Pumpkin Finale: after the pot bursts the giant pumpkin drops where it stood (roam area = its arena) instead of the Daruma game (Daruma code kept: untick Pumpkin Finale). Cook time still 10 min (user's number; suggested shorter).
- **Gate 2:** after phase 2's chest the slimes fly off and the giant pumpkin (radius 1.3) drops on a lane's far end, bowls itself down the lane, stops dizzy at the dirt's edge, leaps to another lane (Bowling Stage Pumpkin Finale; phase 3 + showdown kept behind the tick). Strikes now ADD UP (3 strikes hurt a slime, a miss doesn't reset). Exit spot = in front of the tree.
- **GAMESTAGES:** every gate -> "Rooftop_ON PG" -> **Prepare_Whack_6** (Whack 6/7 = the tree defence, then Lid, BOSS); gate 1's Path 1 ON next1 -> Tween PlayGroundZ; gate 3 -> new Clean for Path 3 / Demo? Path 3 / Path 3 Holder / Path 3 ON (spawns Ball_Valley, waits for 4 gears + The_Seed).
- **RUN IN THIS ORDER (out of Play), then SAVE the scene:** Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss; Tools > 429 Game > Ball Valley > Set Up Ball Valley; Tools > 429 Game > Gates > Gate Finales: Giant Pumpkin In Gates 1 And 2; Tools > 429 Game > Gates > Gate Paths: Ball Valley + Tree Defence (scene). Undo: Gate Finales: As Before / Gate Paths: Back As Before. Test gate 3 alone: Play, then Tools > 429 Game > Ball Valley > Play Ball Valley (Play mode).
- **Not tested at all yet:** everything above (no editor run, no headset). Things to watch: candy pellets grabbable while kinematic + touching nothing; the bots' hunting of the VR body; shove distances; the dash's feel (comfort); the boss's charges in the shrunk valley and on the lanes; GAMESTAGES' rooftop after Path 3; FistBump double-bump while charging the special move (unlikely: a bump needs both hands moving at each other).

**10. NEW GLOVE MODEL (user's concept, 2026-11-18): DONE, in the 6 glove prefabs, not tried in the headset, not committed.** Red Boxing, green Throwing, blue Soldier (weapon handling). New file **Assets/Blender/Gloves.blend** (Export: Glove_R/Glove_L; Colour Previews; Lights; Camera), Assets/Models/Gloves/Gloves.fbx (1,320 tris a glove, same size and pivot as the placeholder), textures Assets/M_Textures/Gloves/Glove_*.png (1024). Materials Data/Gloves/Glove_*.mat now show the texture (base colour white; emission kept for Hot Hands); each prefab's Model = one "Glove" (Mitt + Cuff gone), the Knight's hanging preview too. Pictures: Exports/Gloves_Concept/model_v1_*.png, unity_v1_*.png. Rebuild/tweak: Documents/429R_claude_backups/2026-11-18/gloves_model/stage_full.py (second PC). Backups: Documents/429R_claude_backups/2026-11-18/gloves/. From the player's eyes the thumb and cuff show most; the pumpkin emblem stays on the outer side, as on the concept (user: keep it). **Later the same day: the USER's remodel** (fatter mitt, thumb pulled out of the mitt, "not 2 separated mesh"): Claude rebuilt the thumb inside its seam as clean rings (one piece with the mitt), softened the folds next to it, mirrored Glove_L to Glove_R, re-unwrapped, repainted (new contact shadows, stitches along the thumb seam) and re-exported; 1,374 tris a glove; Unity checked (unity_v2_*.png). The user's old thumb shell is still in the file, hidden (Glove_L.001, not exported). **Save Gloves.blend in Blender** to keep it. Never re-run stage_full.py "full" (it rebuilds the old shape over the user's remodel); repaint = paint_gloves.py. **Then (user): a smoke puff whenever a glove pops on** = Glove Kinds' new Pop Puff (Smoke_Flash_Small_Puff) at **Pop Puff Size 0.08** (0.3 hid the view; comparison Exports/Gloves_Concept/ingame_v3_pop_sizes.png), played from Fist.PuffGlove at the knuckles, grows with Fist Size, at most every 0.15 s a hand (the pickup's pop puffs too); **punch shape fitted to the fatter mitt** on all 6 prefabs: capsule r 0.058 -> 0.072, 0.16 -> 0.176 long, centre y -0.02 -> -0.026 (GlovesSetupMenu's numbers match). Play-tested hidden in the game scene (profile _claude_test, deleted): pickup pops both gloves (new mesh) with a puff, a later pop puffs again, the fists' Glove Shape = the new capsule; 0 errors. Not tried in the headset. **Then "chubby" (user: "make the fist look rounder ... chubby feeling"):** the glove reshaped smoothly (gloves_model/chubby.py: mitt 86% as long, middle 7.5% fatter, corners rounded, cuff 72% as long and 3.5% fuller; 23 -> 19 cm long), repainted (emblem/gold fist redrawn round on the new shape, the rest rides along), FBX re-exported; punch shape now centre y -0.028, **r 0.078, 0.156 long** (almost a ball). Mesh backup before it: Documents/429R_claude_backups/2026-11-18/gloves/Gloves_meshes_before_chubby.blend. **Damage rule (user 2026-11-18: "weapon damage only counts when the object is still in hand of Player, throw damage only counts when objects is off hand"):** WeaponPower.DamageOf: Weapon Efficiency (+ Weapon Master) only while held; a thrown thing gets only its Thrown Bonus (before, Weapon Efficiency was added to throws too and a hard throw tripled it); a thing lying about its own; a gun's shot keeps Weapon Efficiency. Everything else already followed the rule (Impact/Weapon Master/two hands = held; explosive throws, hard-throw x3, Hot Hands = thrown). **Bare fists (user: "a little damage when we make a fist without calling out gloves"):** Fist.bareFists (on): without gloves a fist taps for 1 (never hard, no Hot Hands bonus, no flying punches, no blasts, no Elon double, no uppercut knock-out), still shoves/stuns (Fist.IsBareFist + EnemyHitDamage.BareTap). Compile-checked only (Unity was open). **Recording 2026-11-18 21-58-54 (reviewed frame by frame; damage_video/ in the backups folder):** every number of 33+ was a thrown thing under the old rule ((1 + Weapon Efficiency 10-11 + 1) x 3 for a hard throw = 33-39, +Hot Hands 40-41), driven by "+WPN" candies (and the Soldier gloves' +3) inside a run, not by level-ups; held-weapon hits 22-26 and gloved flying punches 10-11 are by design; deaths at 07:12, 15:03, 28:34. Now a hard throw does about 6-9. The six **999s** were the game's own kill of a 666-health thing (no hit): DamageNumbers now shows no number for kills of 999+. Not changed (ask): overkill shown in full on tiny pumpkins; candy growth speed. **Midnight sky (user 2026-11-18: "change the sky in game to mid night sky, dark blue color"):** new picture Assets/BallValley/Sky/Midnight_Sky.png (2048x1024, painted by Documents/429R_claude_backups/2026-11-18/midnight_sky/midnight_sky.py: navy overhead, lighter blue horizon, a silver moon at the old moon's spot (lon 200, 32 up, so the Sky Flyers still loop it), moonlit clouds, small stars); the main game's Halloween_Skybox_Mat now uses it (one guid changed; backup in that folder), HalloweenSkyMenu's picture too. Gate 3's valley keeps BallValley_Sky.png. Done with Unity open (file edits, no scene change); not looked at in Unity yet. **Sky Flyers (the flying slimes round the moon), user 2026-11-18 "smaller and rarer, not all the time":** SkyFlyers.cs: wingspan 2.5-3.8 -> **1.4-2.2 degrees** (field renamed `wingspanDegrees` -> `wingspan` so the scene's saved value is dropped; no scene edit), and **visits**: the sky is empty, first visit after 15-40 s, then every 35-80 s 1-3 slimes fly in from far off (3 s), loop the moon 12-22 s and fly off. Compile-checked (Unity's Roslyn, 0 errors); NOT play-tested (the user had Unity open). Save the scene once to drop the old field. **Old Skydome: its Twinkling Stars removed (user)** (Prefabs/Bases/Skydome; backup Skydome_before_chubby.prefab in the gloves backup folder; Tools > 429 Game > VFX > Add Twinkling Stars To Skydome would put them back).

**Boss rework so far (APPLIED + Editor play-tested, 2 story fights won):** giant pumpkin butt (user's model Assets/Blender/Pumpkin_Boss_Z2.blend; butt slot split on import), snowman SLIDE in the user's roar pose (`transform` at 1.40 s, face from idle_true; slide_pose until < 0.6 m/s), SLIDE CHARGE (skids at the floor's end, bonks on props/stone blocks, 1 s recovery), BUTT BOUNCE, BUTT HOPS when the player is far (also over low walls/gaps), CRUSHING things in his path (not candy/Seed/Coin/held things/the player/bosses), throws now really fly (they pass through his own colliders), STUN GUARD 17 s (chained spells 3 → 1.8 → 1.5 s), pumpkin hits take 1/5, the pumpkin breaks at 20% (146 full damage) → 5 s dizzy → fights from the floor, pumpkin hidden in phase 2 and while sunk, candy vacuum fixed, 10% bigger. Details below and in Claude's memory.

**Crack painting tools (unused now, kept):** paint setup script + how-to in K:/WORK_2026/08_04/_claude/boss_slide_2026-11-11/cracks/ (boss_cracks_paint_setup.py, HOWTO_paint_cracks.txt); the user's painting Assets/M_Textures/Pumpkin_Boss/Boss_Butt_Cracks_Paint.png; menu Tools > 429 Game > Enemies > Rebuild Boss Cracks.

## 2026-11-11 (Claude, K: PC)

(Everything in this section was done on the K: PC; "this PC" below means the K: PC.)

**Pull from the other PC (Steam demo):** merged and checked state by state (the scene has both sides). GitHub Desktop "stashed" the local work during the pull; it was restored. The stash is still in GitHub Desktop (and backed up as git ref refs/claude/stash-backup-2026-11-11): drop it once you're happy.

**Steam demo (other PC's work), facts:**
- A build is the demo when the DEMO_BUILD define is set, or in the Editor with EditorPrefs 429_PlayAsDemo on (off on this PC).
- Demo checkpoints: CP Bedroom, CP 2.5 - BR. GAMESTAGES: Demo? → Demo? Door → Demo? Path 1/2 → Demo End (notice) → Demo Store (opens the Steam store page, restarts the demo).
- Demo saves go to the <SteamID>_Demo profile, apart from the real game's saves.
- Menus: Tools > 429 Game > Steam Demo > Set Up Demo Ending (already run + scene saved), Demo Settings (the App ID is still 0: set it before a real demo build), Play As Demo, Build Steam Demo (Windows) → E:/429_Demo.

**The 2026-11-10 open items: all fixed and Editor-tested** (except "boss over max", which is by design):
1. Candy Box broken at the face: its candies can't be eaten for 1.0 s (SpilledCandyMouthDelay; 0.5 s still let 4 of 42 in).
2. Box bomb beep only plays when the box really will fly.
3. No "+TOPH" etc. at a stat's candy max (no CandyEaten event when the points didn't go up).
4. Max Health candies give whole-number caps (127.5 → 128).
5. Daruma: a snap turn no longer counts as moving (the rig spot under the head is compared, not the rig origin).
6. A poison tick in the same frame as a hit no longer skips the red lines / hurt slow (PlayerBoosts.HealthSet).
7. SpawnPreview: no more error burst after a Play exit (tested). The burst on opening the Editor got its own fix (it waits until PlayMaker has given each FSM state its FSM); that one shows on the next Editor start.
8. Pumpkin boss health above max: **by design (user)**. capAtStartHealth stays 0.
9. First bee dive no longer lifts the player (StayOnGround ignores a flying bee's body).
10. Wall portals and Fat Pumpkin turning pause inside special stages (they shoved the player in Paint Bees).
- **Paint Bees falls: cause found, and the user said LET THEM BE (2026-11-11: stray Fat Pumpkins that follow you into a special stage may do whatever they want). Don't pause or remove them.** Fat Pumpkins born in the Living Room (Enemy 3 Stuffs) follow the player into Paint Bees: nothing pauses them there. They eat the round's candy (33 and 48 candies, health 272 and 438), one turns giant, and its stomp throws the player about 3.4 m towards the nearest Fall Edge. The Living Room's Fall Edges are still on around the field, so the player falls (-55) about every 7 s.

**New today (user):**
- **Pumpkin boss candy heal = like the player's:** a 1-7 roll per candy, full up to its start health, then the same reduced shares as the player's candy heal. It can go above max (by design). CandyVacuum.cs.
- **Fat Pumpkin gets tankier by eating:** each candy adds 2 x (1-7) health, no cap. Giant still at least 300, a duel winner still x2. FatPumpkin.cs.
- **Kamikaze bees** (Bee_Normal, Bee_Small): they mostly stay up and shoot poison stings (new Bee_Poison_Sting_Small). About 1 attack in 6 is a suicide dive: beep + flash, then it explodes on you like a box bomb (about 20-25 damage + poison) and dies with no EXP. Swat it during the dive: it dies normally, with EXP. In a Boss Rush they fire stings instead of diving. Bee bosses unchanged except below.
- **Wingless bees are a menace:** losing their wings makes them tougher (normal and small bees x2; Bee_Boss_PumpkinButt now loses its wings at 40% health, x3) and they walk at 80% speed (was 50%). Bee_Boss keeps its wings.
- Menu: Tools > 429 Game > Enemies > Set Up Kamikaze Bees (already run; prefab backups in Documents/429R_claude_backups/2026-11-11/suicide_bees). Test toggle in Play: "Kamikaze Bees: Every Attack Is A Dive (test)". No scene change.
- **Editor play test (11:35-12:30): all PASS.** Dive hits 20-25 + poison 2/s x5, shove ~0.55 m, bee gone, no EXP; swat = normal death + EXP; a 3 m sidestep dodges (1.8 m doesn't); 8 s timeout blows up harmlessly. Dives were 20 of 158 attacks (about 1 in 8; the roll is 1 in 6). Wingless: toughness 1.5→3 / 1→2 / PumpkinButt 2→6, speed x0.8; Bee_Boss keeps its wings. Bee bosses as yesterday (stings now hit 9-10 of 10-11). Boss candy heal: full roll below 729, half up to 1.5x, then ~10%, no cap. Fat Pumpkin: +2..+12 per candy seen (20 → 128 after 16), giant 300, duel x2.
- Seen in the tests, not changed: a wingless PumpkinButt fired 1 sting from the ground right after losing its wings, then only bit (26-49). When 6-8 candies drop around the pumpkin boss only 1-2 get vacuumed; the rest hover inside its body until removed (old, not from today's change). **Both handled 2026-11-11 night (RESUME HERE item 2):** wingless bees no longer sting; the vacuum was already fixed by the boss rework.

**Pumpkin boss rework (user, 2026-11-11 afternoon): giant pumpkin butt, no more walking.**
- New model: the user's Assets/Blender/Pumpkin_Boss_Z2.blend (legs removed, a giant pumpkin weighted 100% to Hips), exported to Models/Enemies/Pumpkin_Boss_Z.fbx. The old Pumpkin_Boss_Z.blend is untouched.
- The butt's own material slot is made on IMPORT (Scripts/Enemies/Editor/PumpkinBossButtSplit.cs: triangles moving only with Hips → submesh 1). No Blender slot needed; it redoes itself on every re-export.
- The "notice" and "laugh_true" clips (opening cutscene) aren't in either .blend: they're kept as Models/Enemies/Pumpkin_Boss_Z_notice.anim / _laugh_true.anim (taken from the last FBX that had them) and Opening_Boss.controller uses them. OpeningSetupMenu falls back to them.
- New behaviour (PumpkinBossBrain): the true form SLIDES on its pumpkin (momentum, lean, dust, grind sound), a SLIDE CHARGE (wind-up, straight 9 m/s, heavy hit; skids at the floor's edge; 1 s recovery after every charge), a BUTT BOUNCE instead of the leg jump, a STUN GUARD for every dizzy (17 s after each spell, user; chained spells 3 → 1.8 → 1.5 s), a CRACKABLE BUTT (hits on it take only 1/5 off his health but count in full towards breaking it; breaks at 20% of max health in full hits (user), then he fights from the floor; the crack LOOK was removed, see RESUME HERE), the candy vacuum aims at his mouth and no longer jams, and he's 10% bigger (1.87 / 1.87 / 2.31).
- Setup menu: Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup (already run; re-run after a model re-export: it rebuilds the pumpkin collider + waist sphere from the mesh). Backups in Documents/429R_claude_backups/2026-11-11/boss_slide/.
- No walls in any boss fight (user): rooms are broken by the boss, the rooftop by the other pumpkins when the tree breaks.
- Editor play tests: round 1 (14:50) and round 2 (16:00) all passed; later decisions and fixes are in RESUME HERE at the top.

**Editor freeze (11-11 10:37):** the 429R Editor froze for good in Play while the video recorder was recording; it had to be ended. The recorder used to start on Right Ctrl pressed in ANY app; since 17:00 the keys only work while the 429R Editor is focused.

## TO DO NEXT SESSION (user, 2026-11-09): balance pass, the game is too easy at level 10

At level 10 the user can "close my eyes and still win".

**2026-11-10 (Claude, this PC): CODE DONE + ALL MENUS RUN + PLAY-TESTED IN THE EDITOR (no headset).** All 15 items plus the user's later "6 candies" changes are in C# and setup menus. Everything compiles in Unity.

**2026-11-10 evening: the Editor play test (bot profile `_claude_bot`, real saves untouched).**
- Big test: 116 checks passed, 0 still failing, 5 partial, 12 need a headset. The testers fixed on the way: Max Health candy eaten inside a special stage no longer heals on exit; Level Up menu's Fist Size row counts candy; Candy Box spill no longer launches candies (push capped 1 m/s) and no longer cracks its own candies; a blocked box bomb no longer stops the others waking (BoxBomb skips it for 1 s).
- Then 5 more fixes, all tested and passing, plus a full bot run twice through:
  1. Bee stings and web balls aim at a standing player again. They used to lead by the old Character Controller speed after a shove (0/11 hits, now 11/11). Lead only while the thumbstick or arm swing is walking you, never in a knock-back flight (SpiderBrain.PlayerWalking).
  2. A game over inside Daruma / nesting / Paint Bees no longer heals the dead player or shows the Level Up notice over the death (PlayerLevel.Resume(quiet), PlayBonusRound waits while the player is down).
  3. A poison tick merged with a real hit now pops RED (PlayerPoison.HealthTaken, PlayerHealthHUD).
  4. No more "linear velocity of a kinematic body" warning on PlayGround chest drops.
  5. Anomaly Chance comments say 0.003 (about 1 in 333).
- Not tested (headset): arm-swing walking lead, a real triple punch after a game over, Re-Dream's scene reload inside a stage.
- **Answered 2026-11-11 (user: yes) and done:** after buying a punch inside a stage and dying there, the NEW PUNCH notice now shows 3 s into the next run.
- **Open items found (ALL HANDLED 2026-11-11, see the top section):** pumpkin boss health can go slightly above max (1094 → 1101, cause unknown); a Candy Box broken at the face feeds 1-2 candies; a box bomb from behind shoves you into its loot; its "about to blow" beep plays even if it then calms down; "+TOPH" etc. shows at a stat's candy max; Max Health candies make the cap a fraction (127.5); Daruma counts a snap turn as moving when the head is ~10 cm+ off the play-area centre; first dive sting of a run can lift the player ~1.3 m; Paint Bees: something shoves a standing player north over Fall Edge North every 6-7 s; a poison tick in the same frame as a hit skips that hit's red lines and hurt slow (DamageVignette/HurtSlow).
- Editor-only noise (old, FIXED 2026-11-11): SpawnPreview.cs:146 made PlayMaker log a burst of "Fsm not initialized" errors after each Play exit.
- Test scripts and results: K:/WORK_2026/08_04/_claude/playtest_2026-11-10/ and playtest_fixes_2026-11-10/.

**Menus already run on this PC** (Tools > 429 Game > Balance 2026-11-10; re-runnable, backups in Documents/429R_claude_backups/2026-11-10/):
- **Enemies**, **Bee Bosses Stay High**, **PlayGround And Portal FX**
- **Six Candies And Stats**: it also runs **Candy Colours** and Player > **Give Candies Their Buffs**.

**The scene must be SAVED:** Six Candies And Stats changed the Player Level object in 2 Game Scene (push 0.04, fist size 0.05, last point worth 1).

**On the OTHER PC after pulling:** the prefabs are fine once committed. Without the saved scene, run Six Candies And Stats there (it only changes Player Level in the scene).

**Still to do:**
- Headset test (Editor play test done 2026-11-10, see above).
- Do NOT re-run _claude/unity_mcp/gate_fx_build.cs: it turns the portal Wisps back on.

**6 candies (user, 2026-11-10 later):** one candy per level-up stat. Candy_3 and Candy_6 are RETIRED: they're out of every spawn list (41 prefabs); their prefabs stay.

| Candy | Name | Stat | Slime colour (in Candy_Colours.asset) | Dropped by |
|---|---|---|---|---|
| Candy_1 | Pumpkin Candy | Max Health | orange | Slime_Big |
| Candy_2 | | Weapon Efficiency | blue | Slime_Big_Blue |
| Candy_4 | | Move Speed | yellow | Slime_Big_Yellow |
| Candy_5 | Marshmallow | Toughness | pink | NEW pink slime skin |
| Candy_7 | | Throw Power | green | NEW green slime skin |
| Candy_8 | | Fist Size | red | Slime_Big_Red |

- The list lives in code: Scripts/Inventory/CandyKinds.cs. Every candy setup menu reads it.
- **The candies' look is the user's job** (user, 2026-11-10: "revert what you did with my candy's material, I will do it myself"). Their materials, glow rings, recipe pictures and the basket deco candies are back to the committed originals, and the Candy Colours menu no longer touches candies. After repainting a candy, set its row's colour in Data/Inventory/Candy_Colours.asset to match, then re-run Candy Colours so the slimes match. My flat materials were moved out to Documents/429R_claude_backups/2026-11-10/candies/removed_candy_materials.
- **Wrong recipe:** the cauldron spits Slime_Big in the colour of a candy the recipe still needs (Slime_Pink_Mat / Slime_Green_Mat for the 2 new colours). Its splits drop that candy.

**Later 2026-11-10 (user): only the current 6 candies.**
- The user deleted the Candy_3 and Candy_6 prefabs.
- Their leftovers are gone: stale refs in the 3 Random_*Candy spawners, the Candy_3/6 recipe icons (moved to Documents/429R_claude_backups/2026-11-10/gone_candies), and the Candy_3-model deco in both candy baskets.
- Still in the project, unused: Models/Bedroom_Stuffs/Candy_3.fbx and Candy_6.fbx.

**The candies are being remodelled by the user:**
- 1 candy corn, 2 teal ball, 4 reversed candy corn, 5 pink marshmallow, 7 olive-iris eyeball, 8 red-iris eyeball.
- The recipe icons in Data/Cooking/Icons are out of date (user's job).
- The particles (glow ring, ground disc, appear puff) were recoloured to match each candy. Only the candy prefabs changed; backups are in .../2026-11-10/candy_particle_colours.

**Shop (user):**
- Every restock offers 2 random candies of the 6, at 6 wraps each. They are extra pink rows; the panel grows at runtime, so there is no scene change.
- The Candy Box costs 26 wraps and is on the normal list. Broken, it spills one of each candy. It is a copy of Paper_Box_0 with a pink glow.
- Menu: Tools > 429 Game > Balance 2026-11-10 > Shop Candies And Candy Box (already run). "(Remake Icons)" redraws the shop icons in Data/Inventory/Icons after a remodel; the eyeballs are drawn turned 180.
- Like Paper_Box_0, a Candy Box swept by the Fairy Shield pops with no candies.

**Recipe pictures:** Data/Cooking/Icons/Candy_N.png were redrawn from the new candy models. "Shop Candies And Candy Box (Remake Icons)" now redraws both the shop icons and the recipe pictures (backups first).

**Flying Explosive Box (box bombs, user 2026-11-10):** menu Tools > 429 Game > Enemies > Set Up Box Bombs. Already run: BoxBomb is on Paper_Box_1-5.
- Each box rolls 30% once when it spawns; this applies to any Paper_Box_1-5 anywhere, including boxes that fall out of other boxes. Paper_Box_0 and Candy_Box are paid items and are left out (one line in BoxBombSetupMenu.cs).
- When the player comes near, it shakes (rattle, beep, orange pulse) for 1.0 s, or 1.4 s if it is out of view. Then it flies straight at the face: 20-25 damage, plus a shove.
- Any grab defuses it for good. A punch or a throw while it is awake breaks it with its normal drop and no bang. Backing off past 3.2 m calms it down.
- 1 awake at a time, with a 3 s rest after each attack.
- Test menus (Play mode): Box Bombs: Arm Every New Box (test), Box Bombs: Wake Nearest (test).
- Play-tested in the Editor 2026-11-10 (bot playthroughs pass with box bombs on). Fixed then: a box with its line to the player blocked is skipped for 1 s, so it can't stop the boxes behind it waking.

**Slime colours = candy colours (user, 2026-11-10, from the slime picture):**

| Candy | Slime colour | Slime |
|---|---|---|
| Candy_1 | orange #F0A030 | Slime_Big |
| Candy_2 | teal #3FA39A | Slime_Big_Blue |
| Candy_4 | red-orange #D2541C | Slime_Big_Yellow |
| Candy_5 | coral #E8857A | wrong-recipe slime only |
| Candy_7 | olive #A39A2A | wrong-recipe slime only |
| Candy_8 | red #D0282C | Slime_Big_Red |

- Each colour has its own skin, Materials/Flying_Slime/Slime_Candy_N_Mat, made and recoloured by the Candy Colours menu (already run).
- The bowling showdown slimes keep their old look as overrides.
- Rainbow_Slime now wears Rainbow_Slime_Lit_Mat (Rainbow on), so it is rainbow in the Editor too.
- Set Up PlayGround Slimes now runs Candy Colours at its end, so a rebuild keeps these colours.
- The Paint Bees' yellow paint now drops Candy_7 (olive is the nearest hue).

**Level-up stat changes (user):**
- Toughness push -4% a point (was -8%).
- Weapon Efficiency +1 weapon damage EVERY point (was every 5).
- Fist Size +5% bigger a point (was 10%).
- The 10th point counts as 1 (was 7), so a full stat = 10.

**Candies changed:**
- A candy's points x (1 + 0.2 x your own points in that stat): 0 points 0.5 a candy, 5 points 1.0, 10 points 1.5. The slow part after +5 grows the same way.
- Candy_8 (Fist Size candy) makes the fist and flying punches bigger and the jab harder, but never unlocks a punch. The closed fist no longer gets candy damage.
- Max Health candy raises the health cap for the run.
- The HUD shows +HP / +TOPH / +WPN / +FIST / +THROW / +SPD.

**Facts to know:**
- Fist.JabReach is renamed to Fist.ComboReach.
- The bee poison writes health through PlayerBoosts.SetHealth, so it causes no red flash and no hurt slow.
- Path 2 (Bowling) goes through "Enemy 4 - Get the Seed" and "Tween PlayGroundZ" on purpose. $PlayGround_Z is empty there, so the tween ends at once and the arena handles its own exit.

**User decisions:**
- Item 6: the flying JAB starts at the hand and flies only as far as the fist's own size (a 15 cm fist flies 15 cm).
- Item 8: special stages drop the stats and the health cap to level 0, but the player KEEPS their current health.
- Item 12: one FLAT colour per candy.
- Item 2: the giant Fat Pumpkin (300 health) shrugs off hits.

Prefab changes come from the menus under **Tools > 429 Game > Balance 2026-11-10**, which are re-runnable and make backups in Documents/429R_claude_backups/2026-11-10/.

Before re-running **Aggressive Bosses And White Bugs**, check its health table is fixed. It still had the old, lower mini boss health.

1. **Rainbow Slime:** lower its spawn chance. Its reward is too good and it's too easy to catch.
2. **Fat Pumpkin:** must be tankier.
   - Raise its base health and its toughness.
   - When it turns giant and starts hunting the player, its health goes up to 300 (not x2/x3).
3. **Stun resistance:** every mini boss, and Pumpkin_Boss_Z, gets harder to stun once below 40% health (lower stun chance).
4. **Bee bosses (Bee_Boss, Bee_Boss_PumpkinButt):** both are too weak today.
   - They stay high, out of the player's reach, most of the time, shooting stings that poison (health lost over time).
   - Only rarely do they dive down and sting by themselves.
5. **Cockroach and Spider mini bosses:** +30% MOVE SPEED (scarier).
6. **Flying Jab range:** its reach = the Fist Size (today it reaches further).
7. **Candy healing:**
   - Above full health, a candy heals +50% (as now).
   - Once health is 1.5x max health, each candy heals only +10%.
8. **Special stages:** inside every special stage, temporarily remove all buff stats from level-up points and candies, so the player plays with default stats. Restore them after.
9. **Pumpkin_Boss_Z push:** x2.5 push power.
10. **Chaos Pumpkin_Boss_Z:** a 33% chance to spawn as a chaos version with more health, more damage, more EXP and a red glow aura (like the Chaos_Aura on the runner pumpkins).
12. **Candy colours:** redo the candy system so each candy kind is ONE colour, all different. Today at least 3 kinds are orange.
13. **Slime drops match their colour:** red slime → red candy, blue slime → blue candy, and so on. The White Slime is the special one: it drops a random candy.
14. **Cauldron recipe hidden until the hat:** hitting the PlayGround cauldron shows the recipe as `??? x ??? x ??? x` until the black hat is found and hit. Only then does the recipe show.
15. **Recipe hat aura:** add an aura particle to the Recipe_Hat so it's easier to spot. Keep it light: the user dislikes overdone particles.
11. **Defend the Tree stage:** the portal that spawns the spider-lings still uses the old smoke particles. Swap them for the shared 3D smoke (Smoke_Flash_Small_Puff; never Lana's Smoke_Blast).

## Steam page assets (current: 2026-11-10/11, made on the second PC, all in git under Exports/)

- **Store art v6:** the knight raises his glowing sword at the giant Pumpkin Boss over the back wall (moon, Tree of Light, violet night). The USER posed the knight in `Exports/Steam_Art/Steam_Art_v6_pose.blend`: that file is the source, never re-pose him, and renders must look like the user's own F12 render. The whole Steam set (header, main, small, vertical and library capsules, library hero and logo, page background) plus promo key art, portrait and the YouTube banner: `Exports/Steam_Art/renders_v6/`. Scripts: `Exports/Steam_Art/v6/` (render_user_v6.py, compose_v6.py).
- **Steam icon = the new v6 icon** (user: "use the new icon for Steam"): `renders_v6/icon_new/` (512 PNG, 184 JPG, 1024 master; the sword stays inside a 4% margin). The painted badge and the 2026-11-09 3D icon remake (`assets/icon/Icon_Scene.blend`) are retired; never re-run `icon_scene_build.py` (it wipes the user's hand edits).
- **Profile picture** (the knight alone, blurry background): `renders_v6/profile/Knight_Profile_2048.png`, an 800 PNG and an 800 JPG under 500 KB.
- **Screenshots:** 14 in `Exports/Steam_Screenshots/2026-11-09/`.
- **Trailer 3** (rough cut) and its vertical Short, with the v6 logo end card and the "A VR ADVENTURE GAME" line under the logo (made in Blender: `Exports/Steam_Art/assets/tagline/Tagline.png`): D:/Movie/StreamingHere/Trailer/Trailer3/ on the second PC; build scripts in Documents/429R_claude_backups/2026-11-10/trailer3/ there.
- **Shorts** 35 (the last pumpkin) and 36 (one HP left): D:/Movie/StreamingHere/Shorts/ on the second PC.
- **The user's job:** upload the v6 set, the icon, the screenshots and the trailer in Steamworks; set up the demo (see "Steam demo" above: the App ID is still 0).
- Older rounds (renders v4/v5, the README's v4-v8 icon notes) stay in `Exports/Steam_Art/` for reference.

**Done from 2026-11-08:** `2 Game Scene` was saved with Use Bowling For Gate 2. The Bowling_Arena prefab is in the scene in commit da31bc37.

# Earlier: 2026-11-07 / 11-08

## 2026-11-08: Gate 2 = Slime Bowling (replaces the bee version), built and play-tested in the editor (not in the headset)

**How it plays:**
- **The layout:** a 5 x 5 m dirt island in the middle of an X of 4 long dark lanes. The head pin sits 18.3 m from the dirt, like real bowling.
- **The look:** lights only over the dirt and over each lit pin deck. Each lit lane gets 2 fire columns and sparkles. The fire columns are the Bonus Area's **Danger_Flame** (x1.5, linked to its prefab, its point light off for Quest): about 660 particles alive with all 4 lanes lit.
- **Start:** you appear on the dirt with Gate 1's first chest (Bracer). Eating its Yellow Seed grows vines, and bowling pumpkins grow on the vine tips.
- **The Giant Flying Slime** comes down from 30 m up, its wing sound getting louder, and circles your head 2 laps.
  - It lights a lane and poops a rack of 6 slime pins (1-2-3).
  - It never poops on the same lane twice in a row; when no lane is free, it circles your head.
- **Pins:** they grow from thin and tall (0.66 m) to fat and short over 24 / 22 / 20 s, then burst into small flying slimes that attack.
- **Strikes and hits:**
  - A strike = one ball knocks all 6 down.
  - A hit = 3 strikes in a row on that slime's racks.
  - The 7th poop without a hit = 6 divers onto your head, and it counts as a hit.
- **Phases:**
  1. 1 green slime; a hit splits it into red and blue.
  2. A hit splits each one: red becomes red + yellow, blue becomes blue + green.
  3. A hit takes a wing.
- **Chests:** each phase end drops a chest (Chest, then Head, then Shield gear) in front of you. Its seed grows more vines, so more balls.
- **Showdown:** when all 4 have lost a wing, each flies to a lane, loses its other wing and drops as a ground mini boss. They wait until the last has landed, then all set off together; their own speeds bring them one by one (tested: Green, Red, Blue, Yellow about 1.5-2.5 s apart). You can walk again in the showdown.

  | Slime | Speed | Trait | Health |
  |---|---|---|---|
  | Green "Quick" | 3.6 m/s | rapid little bites, smallest | 45-55 |
  | Blue "Sticky" | 2.4 m/s | slowing globs + sticky bite, Giant Web | 70-80 |
  | Red "Bull" | 1.6 m/s | Bull Charge from 7 m at any health (pushes you) | 100-110 |
  | Yellow "Bomb" | 1.1 m/s | biggest, heavy slam, Ticking Bomb at low health | 90-100 |

  The numbers are in `GiantSlimeSetupMenu.cs` (Showdowns table); re-run Set Up Giant Slime And Showdown after changing them.
- **Win:** beat all 4 → The_Seed → rooftop at the Lid → boss. Any gear bool still missing is set at the win, so the run never stalls.

**Files:**
- Code: `Scripts/Bowling/` (BowlingStage, BowlingLayout, BowlingLane, GiantFlyingSlime, SlimePin, PinSet, BowlingBallSupply, VineGrow).
- Prefabs: `Prefabs/Bowling/`.
- The bee version is backed up in `Documents/429R_claude_backups/bowling_v1_2026-11-07/`.

**Menus** (Tools > 429 Game > Bowling, run in this order):
1. Set Up Bowling Look
2. Set Up Slime Pins
3. Set Up Giant Slime And Showdown
4. Set Up Bowling Arena
5. Use Bowling For Gate 2

Test buttons are on the arena's BowlingStage component (right-click it in play mode): Eat The Seed, Strike The First Rack, Hit Slime 0, Next Phase, Showdown Now, Win Now.

**To do (user):**
- **SAVE THE SCENE.** Use Bowling For Gate 2 edited GAMESTAGES (the Path 2 states, and Path 2 ON now waits for the 4 gears + The_Seed like Path 1). The scene on disk still sends Gate 2 to the PlayGround.
- Play it in the headset: strikes, pin sizes, the slime sizes, how hard the showdown is.
- Screenshots: `Documents/429R_claude_backups/2026-11-08/bowling_shots/`.

## 2026-11-07: PlayGround redesign (3 cooking rounds, then Daruma), built and play-tested in the editor

**The flow:**
1. The Bracer chest seed makes the cauldron.
2. Each round:
   - The black hat gives the recipe.
   - A wrong recipe boils 2.5 s, then spits out a **Slime_Big**. No wave starts, and the pot works again at once.
   - A right recipe cooks 30 / 45 / 60 s. The bubble tower rises, the walls play GO / GO2 / GO3, and minions come non-stop.
   - When the cook is done, the pot spits the laughing pumpkin and the round's mini boss comes out: **Slime_Big_Blue** (round 1), **_Red** (round 2), **_Yellow** (round 3).
   - Catching the pumpkin stops the minions at once and drops the chest.
   - Eating the seed clears everything except the cauldron, hat and candies. The walls then play COME_2 / COME3 and the field grows (14, then 23, then 32 m wide).
3. After round 3, the pot shakes and rings the bell, and white slimes come out on a timer. 8 candies shut it: a 3 s cook, a big puff, then the **Daruma game**.
4. Daruma:
   - Win: The_Seed appears on the slime's platform. Eating it fades to black, the PlayGround goes, and the player stands 4 m from the Lid, facing it. COOKING MASTER! shows then.
   - Loss: death and a new run. The coins can't save it.

**The rules in the code:**
- **Slimes** (`Scripts/Enemies/SlimeBody.cs`, on SpiderBrain):
  - A Big slime killed by the player splits into exactly 3 Small ones, which never split. A Small one drops 0–2 candies.
  - Clearing the field, the Fairy Shield and wave kills never split a slime.
  - Blue: sticky globs and Giant Web. Red: Bulldozer and Bull Charge. Yellow: Ticking Bomb, and it splits out of its own blast.
  - Menu: **Tools > 429 Game > PlayGround > Set Up PlayGround Slimes**.
- **Cauldron:**
  - Push-back: a head within 0.55 m of the pot is pushed about 0.6 m, at most every 2 s, never while holding food.
  - A punch or weapon hit rings a bell clip and lets out a white slime (1 per 3 s, 3 at most, no EXP).
  - Menu: **Set Up Cauldron**.
- **Laughing pumpkin:**
  - It runs 3 / 4 / 5 m, then is stunned for 4 / 3.5 / 3 s with stars, silent and catchable. Otherwise it laughs non-stop.
  - It hides only near, inside the arena, and is leashed back if it leaves.
  - After 300 s it turns into the real pumpkin_boss_Z: the minions stop, and there is no chest.
  - Menu: **Set Up Laughing Pumpkins**.
- **Walls and phases (PlayGroundField):**
  - The C# drives the Animator states; the old PlayGround_Manager chain is cut (Phase 1 → State 1).
  - It turns on the child **PlayGround_Phase_2 / _3** after COME_2 / COME3. While those don't exist, it uses the old random layout.
  - Menu: **Set Up PlayGround Walls**.
- Other menus: **Set Up PlayGround Rounds** and **Set Up Cooking Rewards**. **Bonus > Hook Special Stage 3 To Cauldron** does NOT rebuild Special_Stage_3.

**To do (user):**
- Build `PlayGround_Phase_2` / `_3` as inactive children next to Phase_1, with props only. Include props that drop tree pumpkins. Leave out Cauldron_Pos and Recipe_Hat_Pos.
- In GAMESTAGES `Path 1 ON`, remove the "STILL WORKING ON THIS!" ShowNotice when ready, then save the scene.

**Not tested yet:** the whole stage in the headset, and real fists/weapons on the pot (tests raised the punch event in code).
Backups: `Documents/429R_claude_backups/2026-11-07/before/`.

# 2026-11-06

For the user and for Claude on the other PC. This PC's Claude memory and test scripts (K:/WORK_2026/08_04/_claude/)
are NOT in git, so everything needed to continue is written here.

## Finished and Play-tested today

- **Main game wrap-up**
  - Paid dice fly away like a rocket (RocketAway).
  - After the cooking-gate Seed the run skips whacks 6/7 and goes straight to the lid (GAMESTAGES 'Rooftop_ON PG').
  - The music players have their music notes back (Music_Notes_Loop) and are tougher.
  - Rooftop gates got purple torches and a spawn circle; closed gates block the NavMesh, so Fat Pumpkins can't walk through.
- **Playground**
  - Ghost balloons were replaced by **PG_White_Slime** (drops 1-2 normal candies).
  - The **Slime Orchard** repaint covers bushes, grass, bark, ground and concrete.
  - Lollipops were removed and replaced by **PG_Candy_Basket**.
  - Playground-only wood and metal: **PG_Wood_*** and **PG_Metal_Mat**, also on the broken pieces.
- **Cooking rewards**
  - Each round chest bursts out candies and EXP (8/12/16/20 candies, 40/60/80/120 EXP).
  - The first time a save slot clears the mystery pot, it gets +1 permanent stat point (CookingRewards, PlayerLevel.giftPoints).

## In progress: Special Stage Type 3 ("red light, green light" with the Giant Slime)

**What it is:**
- A reward after the PlayGround's **2nd pumpkin** is caught with its whole wave beaten.
- The player is taken to a narrow bridge with no way back and no teleport. The Giant Slime sings with its back turned, then turns and looks.
- Moving while it looks = caught: it spits bomber slimes that knock you off the bridge.
- A fall = back to the start + fall damage, and the 60 s clock keeps running.
- Touch the slime in time = win: 200 EXP + 30 candies + fireworks, then back to the Playground.
- The song speed is random and gets trickier the closer you get (slow, then suddenly very fast).

**Files (all new):**
- `Scripts/Bonus/DarumaStage.cs`: the game itself.
- `Scripts/PlayGround/SpecialStage3Trigger.cs`: on `Prefabs/PlayGround/Fire_Woods_n_Cauldron`.
- `Scripts/Bonus/Editor/DarumaStageSetupMenu.cs`: menu **Tools > 429 Game > Bonus > Set Up Special Stage 3**. It rebuilds `Prefabs/Bonus/Special_Stage_3`, `Prefabs/Enemies/Daruma_Bomber` and `Materials/Special_Stage_3`.
- `Audio/SFX/Special_Stage_3/`: song notes 1-9, look, gotcha.
- Small hooks added to existing scripts:
  - SlimeBomber: `FaceHit` event.
  - FallOutOfWorld: `ReturnSpot` / `ReturnYaw` / `Fell`.
  - PlayGroundRounds: `LastWaveCleared`.
  - GiantSlime: `TurnSpeed`.

**Play-tested OK:**
- The stage starts, the player is moved there, teleport turns off and the fog is set.
- The song plays at varied speeds, the slime turns and looks, and standing still is not caught.
- Time-out: "TIME'S UP", then back to the Playground with teleport, fog and the fall spot all restored.

**Tested on the other PC (2026-11-06, hidden Unity batch runs, save profile `_claude_bot`, deleted after):**
- Caught: moving while it looks → caught, 14 bomber slimes out, pushed off the bridge, 1 fall (health 240 → 177), back exactly on the start spot.
- Win: touching the slime → won, then back exactly where the player came from; the stage is gone.
- Note: the 200 EXP win reward can level the player up; the level-up notice pauses the game until it's punched away, and the stage goes on after that.
- NOT tested yet: the real trigger (2nd pumpkin + wave beaten) in a full Playground run, and the whole stage in the headset.

**Done on the other PC (2026-11-06), all play-tested:**
1. **Knocked down** (health 0, lucky coins, getting up from a revive; the same check as Player Launch): moving while it looks doesn't count, and getting up isn't moving. No bombers or pushes until the player is up; the forced-push wait starts again once they're up. Tested: knocked down and moved 0.6 m during a look → not caught.
2. **Cleared away mid-game:** OnDestroy now puts a player still within 60 m of the stage back on their return spot. Tested: the stage destroyed mid-song → the player was 0.08 m from where they came from, fog restored.
3. **Host colour:** DarumaStage.HostColour sets `_BaseMap` = Flying_Slime_Paint (white where it takes the colour, alpha = where) and `_BaseColor` through a MaterialPropertyBlock: **green while it sings, red while it looks (and laughs at a time-out), gold** at the start and when it's over. The Paint Bees giant keeps its brown. New fields: hostPaint, singColour, lookColour, restColour. Not made bigger (still x3.5): say so if it should be x5.
4. **Rules sign:** now at (-2.1, 1.7, 2.6), about 3.2 m away, turned to face the start (-41°), text 3 (was 2.2) and 3.2 x 2.7. Both the prefab and the setup menu.
5. Balancing fact kept as is: a fall still costs the normal 55 health.

## Notes for Claude

- Only `2 Game Scene` ships; GAMESTAGES (PlayMaker) runs the flow.
- Never edit .cs files during Play mode: Unity recompiles mid-play and breaks the run.
- Play tests use their own save folder (`SaveManager.Profile = "_claude_bot"`); delete `Saves/_claude_bot` afterwards and never touch the user's 3 slots.
- Don't save the user's scenes or .blend files.

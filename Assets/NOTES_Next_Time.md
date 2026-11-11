# Next time: where we stopped (2026-11-11 night)

Two PCs: the **K: PC** (has K:/WORK_2026/08_04/_claude/ with the boss round-3 files) and the **second PC** (no K:). Claude's memory, backups and test scripts stay on each PC, so everything needed to go on is written here.

## RESUME HERE

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

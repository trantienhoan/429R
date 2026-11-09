# Next time: where we stopped (2026-11-09)

## 2026-11-09 (late): the 3D Steam icon. Paused ("okay for now"), NOT uploaded.

**Status:**
- The uploaded Steam icon is still the user's painted badge (`Exports/Steam_Art/renders/`).
- The 3D remake is `Exports/Steam_Art/assets/icon/Icon_Scene.blend`, which the USER HAND-EDITED:
  - arms removed, so the paws peek over the digits;
  - Subdivision on;
  - digits tilted like the badge.
- **NEVER re-run `icon_scene_build.py`**: it rebuilds the scene and would wipe those edits. To render, run:
  `blender -b assets/icon/Icon_Scene.blend --python assets/icon/icon_render.py -- full`
- The colours, the eyes, the subtle weed leaf in the knight's LEFT eye and the gold come from `grade_icon.py`, applied after the render. They don't show in Blender.

**Files:**
- Latest render: `assets/icon/icon_render_1024.png`.
- Steam sizes: `assets/icon/3d/` (512 PNG, 184 JPG, 800 YouTube).
- Side-by-side with the badge: `story_tests/icon_colour_final.png`.

**Reviews:** v8 got 6.5 and 7 from independent reviewers. The last v9 review was cancelled when the user paused.

**Open ideas** (not done):
- give the paws 3 toe pads;
- fix the 2 and the 9 crossing rims;
- level the head (it's tilted about 6 degrees);
- bigger ears.

Details are in the README, sections "v4" to "v7-v8".

## 2026-11-09 (other PC, afternoon): Steam store art v5 "the knight guards the Tree of Light" (RENDERED)

**Where it lives:** `Exports/Steam_Art/`. `README.md` there has the v5 section, every switch and the Steam upload list.

**Pipeline:**
1. `knight_pass.py`
2. `story_pass.py` (NEW)
3. `lighting_pass.py`
4. `render_all.py`

Notes on running it:
- The scripts now find their own folder, so they run on both PCs.
- `fix_paths.py` (new) points texture paths of the other PC at this checkout.
- Blender is at `E:/Steam/...` on one PC and `G:/steam/...` on the other.

**User decisions 2026-11-09:**
- Title "429" only (the Steam page will be named "429").
- Keep the current fiery logo.
- **The Tree of Light** is the game's Pumpkin_Tree (the rooftop tree), **as in the game**: orange maple leaves, pumpkins on it. The Tree-of-Light POT in Knight.blend is NOT used in the game any more.
- **The knight:** right fist pushed toward the viewer, sword across his body pointing to HIS left, the blade's flat side facing the camera, shield on. Smiley eyes kept.

**In the art:**
- The Pumpkin Boss looms over the back wall with raised fists (its transform animation, frame 100), face glowing.
- The menace pumpkins turn toward the knight.

**Steam rule fixes:**
- The Library Hero puts the knight in the 860x380 safe area. Put the library logo at "centered top" in Steamworks.
- The Small capsule is mostly logo.
- The Page Background has no characters and is darker.
- The Library Logo is trimmed to the logo (about 1214x720).
- New icons: 512x512 PNG shortcut and 184x184 JPG app icon, of the knight's helmet.

**Renders:** `Exports/Steam_Art/renders/` (11 files). The previous round is in `variants/renders_v4/`.
- **Later (same day):** the user edited the .blend: the tree turned and the boss moved back. Those edits are copied into `story_pass.py`.
- **The rooftop is now the game's current one** from Unity: the cursed-stone floor with glowing cracks, the new walls and the rebuilt Lid. It is re-rendered.
- The renders before that update are in `variants/renders_v5a/`.
- **Evening:**
  - The user posed the knight by hand. `knight_pass.py` is now `POSE = "keep"`, so it never overwrites that pose.
  - The pumpkin moved next to the knight.
  - **2 promo pictures for Reddit:** `Promo_KeyArt_1920x1080` and `Promo_Portrait_1080x1350`, in `Exports/Steam_Art/renders/`.
  - If the .blend is open in Blender while the scripts run, saving from Blender overwrites the scripts' work. Close it first, or rerun `story_pass.py` + `lighting_pass.py`, which is safe.
  - **Night:**
    - 4 spiders in the user's chosen poses stand in front of the boss (smaller, as asked).
    - The big pumpkin glows.
    - The user's knight pose is kept in `Exports/Steam_Art/knight_pose_user.json`. `story_pass.py` restores it every run; if the knight is re-posed by hand, update that file.
  - **Late:** the big pumpkin now has the user's ORIGINAL game face (`pumpkin_0`), with black charred eye and mouth rims and fire burning deep inside.

**Still to do:**
- The user reviews the renders.
- At least 5 real gameplay screenshots (1920x1080+, 16:9; 4 marked all-ages).

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

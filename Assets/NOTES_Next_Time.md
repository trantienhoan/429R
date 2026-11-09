# Next time: where we stopped (2026-11-09)

## 2026-11-09: Steam store art in Blender (PAUSED here: the user stopped all work to move)

**Where it lives:** `Exports/Steam_Art/` (outside Assets). `README.md` there explains everything.
- **Pipeline:**
  1. `build_steam_art.py` (start over only)
  2. `assemble_v2.py`
  3. `knight_pass.py`
  4. `lighting_pass.py`
  5. `render_all.py` (all 9 Steam sizes into `renders/`; `-- preview` for quick ones)
- **Blender:** `E:/Steam/steamapps/common/Blender/blender.exe -b <file> --python <script>`. The user's own `Assets/Blender/Knight_429.blend` is never edited.
- **Assets:** `Exports/Steam_Art/assets/`, with their build scripts and test renders.
  - `Logo_429.blend` (`logo_build.py`): custom "429" digits, faceted gold, cracked green enamel, green fire. The stem on the 9 is removed (`stem=1` brings it back).
  - `Menace_Pumpkin.blend` (`menace_pumpkin_build.py`): the user's own pumpkin_0 with only the eyes and mouth reshaped to look menacing (texture copies in `menace_pumpkin/`).
  - `Background.blend` (`background_build.py`): castle, graveyard, dead trees, mountains, clouds, moon.
  - `Hero_Pumpkin.blend`: a sculpted pumpkin the user REJECTED ("can't be recognised as part of my game"). Not used.
- **Current renders** in `Exports/Steam_Art/renders/` are round 3: plain 429 logo, menace pumpkin, knight with smiley eyes and the OLD broken stretched wave.
- **Knight arm FIXED but NOT rendered yet:** the new `knight_pass.py` gives a clean wave (no stretch, upper body leaning 12 deg to his left, head tilted 15 deg, helmet follows the head). Switches are at the top: `POSE = "wave"/"normal"`, `LEAN_DEGREES`, `HEAD_TILT_DEGREES`. Tests are in `assets/knight/tests/fix_*.png`. To use it, run `knight_pass.py` → `lighting_pass.py` → `render_all.py` on `Steam_Art.blend`.

**NEXT (user decisions 2026-11-09, not started; the workflow was stopped before it changed anything):**
- **Title:** "429" ONLY.
- **"The green":** CLOVER only (no cannabis leaf).
- **Story = the knight GUARDS THE GREEN:**
  - The knight stands in front of the game's glowing green Tree of Light (Knight.blend Tree_Of_Light or Pumpkin_Tree_Drop.blend).
  - Menace pumpkins close in from the dark, and the Pumpkin Boss (Pumpkin_Boss_Z.blend) looms in the back.
- **Logo story idea:** 4-2-9 = Four-Twenty-Knight, so the 9 becomes the knight's helmet (eye slit, glowing eyes, clover), with a sword worked into the lettering.
- **Steam rules (checked online):**
  - Every capsule needs a readable logo or name.
  - The Library Hero has no text.
  - No calls to action, awards or review scores.
  - It must read at the Small capsule size, even about 231x87.
- The stopped workflow script (story version) can be re-run: it is saved in the session folder as `steam-art-story-wf_7a7d41a5-211.js`.

**Still pending from 2026-11-08:** SAVE `2 Game Scene` after **Use Bowling For Gate 2**. The Gate 2 GAMESTAGES states exist only in the open editor.

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

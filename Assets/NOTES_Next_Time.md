# Next time: where we stopped (2026-11-06)

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

**NOT tested yet:**
- Being caught (bomber stream, push off the bridge), falling back to the start, and winning by touching the slime.
- The real trigger (2nd pumpkin + wave beaten) in a full Playground run.
- Why the catch wasn't tested: in the Editor with no headset, the player's body starts slightly inside the floor, so the bot's first "step" only popped it up. Test it with the headset, or make the bot step twice.

**To do next (planned, not done):**
1. **Player knocked down during the stage** (health 0, lucky coins or revive): skip the "moved while it looks" check and stop the pushes until they're back up.
2. **If the stage is cleared away mid-game** (e.g. by a Game Over clean-up), DarumaStage.OnDestroy already gives teleport/fog/creatures back. But the player stays in the empty void if nothing else moves them. Fix: in OnDestroy, if the player is still within ~60 m of the stage, move them back to their return spot.
3. **The host looks like a brown pumpkin.** The Giant Slime material is unpainted white. Planned: tint it through a MaterialPropertyBlock (`_BaseColor` on 429/Slime Lit), **green while it sings, red while it looks**, as a clear cue. It might also be made bigger (x3.5 now, about 1.6 m; maybe x5).
4. The rules sign next to the start fills the left of the view; move it a little further away or angle it.
5. A fact for balancing: a fall costs the normal 55 health. At a low level (60 max health), 2 falls knock the player down.

## Notes for Claude

- Only `2 Game Scene` ships; GAMESTAGES (PlayMaker) runs the flow.
- Never edit .cs files during Play mode: Unity recompiles mid-play and breaks the run.
- Play tests use their own save folder (`SaveManager.Profile = "_claude_bot"`); delete `Saves/_claude_bot` afterwards and never touch the user's 3 slots.
- Don't save the user's scenes or .blend files.

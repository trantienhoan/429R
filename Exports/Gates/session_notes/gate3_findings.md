# Gate 3 (Ball Valley): code facts and weak spots (2026-11-22)

Read from the working copy (uncommitted edits included) by a read-only helper; spot-checked by the planning session
(the Awake tuning override and the Boss step with no fallback are confirmed). Line numbers drift: re-check before editing.
S/G/P = Assets/Scripts/BallValley/BallValleyStage / BallValleyGate / BallValleyPlayer.cs.

## 1. Flow today
- GAMESTAGES: Stage 1 next3 -> Clean for Path 3 -> Demo? Path 3 -> Path 3 Holder -> Path 3 ON (fade, teleport, spawn
  Ball_Valley at the origin, WORKING SITE notice; BoolAllTrue 4 gears + The_Seed -> next1 -> Enemy 4 - Get the Seed).
- Gate Awake overwrites the stage's tuning (G:89-100): first shrink 40 s, span 150 s, 5 phases, smallest 0.5, no gifts,
  no crumble. The prefab's own Tuning values (30 / 120 / 0.35 / gift 1) never apply: editing them does nothing.
- Start: valley look (own sun, fog off, far plane 240, sky ball; S:1756-1786); world + 15 bots, 0 revives, drop in after
  0.3-2 s (S:808-851); player near (0,0,-4) with an invisible 0.45 m trigger ball + lawn-only teleport (P:158-204);
  rules notice 1.5 s in, after any other notice, holds time (G:137-141).
- Shrinks at 40/90/140/190 s, 4 s each, 1.0 -> 0.5 of the 50x25 m field (S:673-689).
- Finale when no bots are left ("ALL IN!", G:148) or at the last phase (~194 s, G:187-190). The last phase also runs
  BALLS' Moment of Truth: trees/rocks crumble into slimes, bot aggression climbs to 1 over 15 s (S:906-912, Brains.cs:60).
- Boss: 6 s shrink to the smallest field, boss 2 s later on the far half from the player, inside the border minus its
  radius (G:193-273).
- Win: boss breaks -> seed >= 2.5 m from pits -> GateExit (G:293-309): stage detached, 4 gear bools set, The_Seed
  (auto-eaten after 20 s), rooftop, stage destroyed (restores the look; S:256-268, 1846-1858).
- Old "4 rounds, last ball standing": dead in gate mode (botsPerRound / aggressionPerRound / NextRound, RoundStarted /
  RoundEnded with no subscribers, the last-ball block S:754, gifts S:591, revives, Fireworks still added S:229, points
  and kill feed console-only). Still live: King of the Valley + Rainbow Slime (S:596; catcher ignores Knock 7 s,
  BallPlayer.cs:843), bot special moves, the Watch menu's rounds. Never built: 7b's rounds, per-round gear chests,
  slime heals.

## 2. Tunables
- Field 50x25 m; pit radius ~2.56 m corner / 2.36 m middle (Tuning.cs:12-13, 71-73).
- Bots: 15, temper 0.5-0.75, at most 1 hunting the player, a bot that hits you leaves you 10 s (G:31-36, P:60); speed
  9 m/s, up to +70% when big; up to 8 tail balls each.
- Hits on the player (P:47-82): shove from 2.5 m/s, 0.22 m per m/s x sqrt(mass), max 3.5 m; "Damage" at >= 12 m/s or a
  bot special, at most every 1.5 s; head 0.25 m past a pit rim -> FallDamage 55.
- Special move (P:91-101): both made fists <= 0.3 m apart for 1 s; release = 12 m/s x 0.35 s dash along the gaze;
  3 s cooldown; knocks balls at 16 m/s; boss 10 damage (x2 dizzy).
- Punch on a ball: Fist.Push, up to min(1.5 x hand speed, 14) m/s (Combat/Fist.cs:1048) + 0.35 s knock.
- Candy: 30 on the field at full size, +1 every 0.16 s, cap shrinks with the field (S:513-519, 1255-1260); slimes 4->6;
  a punched slime = 1-3 candies (P:320).
- Boss (prefab): radius 2.6, 150 hp, charge every 3-5 s, 1.6 s wind-up, 13 m/s, dizzy 3.5 s at x2; charge hit =
  DamageHeavy + 6 m throw.
- Run length (estimate): ~194 s clock + boss 1-3 min + seed <= 20 s -> 5-7 min.

## 3. Weak spots
- Ball phase can't softlock (the clock ends it); balls can't wedge on a rim (pits cut from the floor, dead pocket-back,
  ball dies at y < -2.5; ValleyShrink.cs:108-140, BallPlayer.cs:610).
- Waits with no exit: the Boss step waits forever if the boss object disappears without Broken (G:155-157, confirmed);
  GateExit's rooftop wait has no hard timeout (Gates/GateExit.cs:143-163, shared file); a player hugging a wall makes
  the boss skip charges (Gates/PumpkinBallBoss.cs:485-489) so it may never go dizzy (rare in the small arena).
- Null/cleanup gaps: loose-ball loops without null checks (S:1446, 1708-1710); if P is disabled (not destroyed)
  mid-fall, the character controller and mover stay off and the view stays black (P:148-156, 508-532).
- Falls: leaning 0.25 m over a rim costs 55 hp (P:227-233).
- Special-move gesture: 0.3 m between knuckles is about a boxing guard, and any parting after a full charge dashes
  (P:350-366); looking down gives an unstable dash direction (P:401-403); two fist bumps within 1.2 s open the Level
  Up menu (Combat/FistBump.cs:113-119).
- Balls slower than 2.5 m/s pass through the player until 0.65 m (P:261-288).
- Watch menu disables the gate after its Awake already rewrote the tuning (BallValley/Editor/BallValleyMenu.cs:279-287).
- Look restore is sound (re-applied every LateUpdate, S:1794-1831); lingers 0.2 s after GateExit's fade-in.
- No TODO/FIXME/HACK. Stale: BallValleyMenu.cs:19, "later step" comments, the numbers in NOTES:490.
- Untested (NOTES:283, 487, 496): headset feel of the dash, shoves, fist charge; real punches on balls and the boss;
  FistBump while charging; Quest frame rate.
- Performance: candy prefabs (3 FSMs + particles) spawned up to ~6/s; up to 15 heads + 120 tail balls + 40 loose balls,
  all continuous collision with contact modification; floor + teleport MeshColliders re-cooked every frame while
  shrinking (Valley.cs:215-226); BuildWorld in one frame; logFeed on (Debug.Log in builds).

## 4. Fun and pacing
- The goal doesn't drive progress: the boss comes at ~194 s anyway; the player's own pockets aren't marked or rewarded
  (G:173-179 ignores who pushed); bots pocket each other.
- Herding is slow: bots steer away within ~9 m of hole mouths (Brains.cs:419) and take control back below 5 m/s; only the
  dash carries a ball far.
- Hard to read: holes on a flat 50 m lawn seen from eye height, no markers for holes or balls; kill feed console-only;
  any pocket plays the player's own fall whistle (Sfx.cs:33); every bot growth plays 429R's "levelup" sound (Sfx.cs:36).
- Difficulty spike at the end: 25x12.5 m arena, 6 pits, a 5.2 m-wide boss, 6 m throws, a bot at full aggression and
  leftover balls; a charge into a pit costs DamageHeavy + 55.
- Survival: a standing player lost ~30 hp per 40 s (NOTES:483), ~145 hp over the ball phase; bots also eat the candy.
- No gear chests: GearGuard only flips the bools (GateExit.cs:198-217), unlike gates 1/2.
- Comfort: the dash and all shoves bypass the vignette (it only follows locomotion providers,
  Locomotion/Editor/ComfortVignetteSetupMenu.cs:66-71); the border sweep also moves the rig (P:475-490).
- Check the HUD flashes (ALL IN! / LAST STAND! / BREAK IT! / FIELD SHRINKS!) against the user's no-effect-words rule.

## 5. Art
- BALLS low-poly palette art, kept by the user's choice (NOTES:472): Map_Static, Rock_Border, 4 boulders, 2 pines,
  3 grass tufts on one BALLS_Palette.png in Assets/BallValley/Resources (always in builds).
- Look: daylight greens/greys; the Halloween feel is only the painted dusk sky (BallValley_Sky.png), the orange moon
  light and violet ambient.
- Balls: glossy code-made spheres with googly eyes (~660 tris, 7 renderers each, + smoke). Slimes: a Resources copy of
  Giant_Slime. Boss: the scaled Bowling Pumpkin.
- Not on REMODEL_LIST (its side paths cover only the PlayGround and bowling); no concept art yet.
- Counts in notes: step 1 bots-only ~23k tris, 120 renderers, 35 rigidbodies (NOTES:473); nothing measured for 15
  balls + candies + boss.

## 6. Shared-file risk
- Gates/PumpkinBallBoss + GateExit (all 3 gates). Prefabs/Gates/Pumpkin_Ball_Boss is rebuilt by Set Up Ball Valley if
  missing.
- Combat: Fist.Landed / IsMadeFist / Knuckles / Push (also the bonus board and FistBump); PlayerLaunch; EnemyHitDamage /
  HitSettings / WeaponPower (boss damage intake; all have uncommitted edits).
- Player: PlayerHealth FSM (float + Damage / FallDamage / DamageHeavy); Checkpoints.IsDying, PlayerRevive, HeadsetFade,
  TutorialNotice -> PauseMenu.Hold; the XR Origin's CharacterController + XRBodyTransformer (GateExit toggles them too).
- Layers by name (S:210-218); the scene's Ground box (layer 21, ~131 m square, top y~0, scene line 183496) holds the
  player over the pits (a fall is a head-over-pit check); Teleport interaction bit 31 hard-coded (P:202), shared with
  gate 2's dirt.
- Candy_1/2/4/5/7/8 (CandyKinds.Paths) assumed to have a root Rigidbody, XRGrabInteractable, Default layer
  (Food.cs:47-75). The_Seed's "Rotate and Wait" state and "next" event hard-coded (GateExit.cs:28-30).
- GAMESTAGES editors: GatePathsMenu (clones Path 2 states), PlayGroundNoticeSetup (WORKING SITE), Gate2BowlingMenu.
  The legacy Paths/Editor/Gate3PickMenu would silently cut gate 3 if re-run.
- Main-game sky lives in Assets/BallValley/Sky (Halloween_Skybox_Mat + Midnight_Sky.png); gate 2 uses it too.
- Globals: RenderSettings, camera far plane, Physics.ContactModifyEvent.
- Purple hole spares names containing "Valley" (Rooms/PurpleHole.cs:60): renaming Ball_Valley exposes it on death.
- 27 Audio/SFX clips found by file name.

## 7. Test hooks
- Menus: Tools > 429 Game > Ball Valley > Set Up / Watch (bots only) / Play Ball Valley (Play mode; no GAMESTAGES, so no
  rooftop). Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss / Gate Paths.
- Script hooks (no ContextMenu buttons): `Balls.BallValleyStage.I.ShrinkToLast(1)` -> finale + boss; `Die(null)` on
  every bot -> "ALL IN!"; `gate.Boss.SpecialHit(n)` damages the boss.
- Fastest: Play -> Play Ball Valley, then ShrinkToLast. Full chain: GAMESTAGES "Clean for Path 3" on `_claude_test`.

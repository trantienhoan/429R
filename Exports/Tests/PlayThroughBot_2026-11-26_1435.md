# Play-through bot, 2026-11-26 14:51

**Result:** the main game and gates 1-3 played. 15.4 real minutes, 19 stage changes, 3 rooms checked.

Bot: walked 561 m, 19 teleports (stuck), 0 touches, 82 punches, 0 hats lifted, **8 shortcuts**. Saves/_test_bot deleted.

## Main game and gates

- **Main game:** skipped (Start (Gates Only)).
- **Gate 1:** reached the rooftop ('Rooftop_ON PG') 6.7 min after 'Clean for Path 1' (4 shortcuts).
- **Gate 2:** reached the rooftop ('Rooftop_ON PG') 5.7 min after 'Clean for Path 2' (3 shortcuts).
- **Gate 3:** reached the rooftop ('Rooftop_ON PG') 2.6 min after 'Clean for Path 3' (1 shortcuts).

Pictures: 29 in `Exports/Tests\PlayThroughBot_2026-11-26_1435` (numbered in order, named by stage).

## Rooms: what each room made that was still there after its clean-up stage

- **Room 1** (`Clean for Path 1` ... cleaned by `Clean for Path 1`, next `Re-Create Stuffs Hodler`): clean.
- **Room 2** (`Re-Create Stuffs Hodler` ... cleaned by `Clean for Path 2`, next `Path 2 Holder`): 4 things it made are still here:
  - 1 x Skydome (under CurrentlySpawnedStuffs), first seen in `Restart Loop`
  - 1 x Cooking Rewards Notice (under scene root), first seen in `Path 1 ON`
  - 1 x CurrentlySpawnedStuffs (under -LevelManger), first seen in `Clean for Path 1`
  - (kept on purpose: 1 candies / room kits going on to the next room)
- **Room 3** (`Path 2 Holder` ... cleaned by `Clean for Path 3`, next `Path 3 Holder`): 5 things it made are still here:
  - 2 x Skydome (under CurrentlySpawnedStuffs), first seen in `Restart Loop`
  - 1 x CurrentlySpawnedStuffs (under -LevelManger), first seen in `Clean for Path 2`
  - (kept on purpose: 2 candies / room kits going on to the next room)

## At the end: things the run made at the scene's top (outside CurrentlySpawnedStuffs: no clean-up removes them)

- 1 x Ball_Valley Line (first seen in `Path 3 ON`)
- 1 x Ball_Valley (first seen in `Path 3 ON`)

## Stages in order

| # | Stage | Seconds | Moved on by |
|---|---|---|---|
| 1 | Clean for Path 1 | 0.5 | the game |
| 2 | Re-Create Stuffs Hodler | 0.5 | the game |
| 3 | Path 1 ON | 96.2 | SHORTCUT (gate 1): Gate Run: Test: Skip To The Finale |
| 4 | Tween PlayGroundZ | 5.0 | the game |
| 5 | Rooftop_ON PG | 0.1 | the game |
| 6 | Restart Loop | 1.3 | the game |
| 7 | Recreate | 2.0 | the game |
| 8 | Walk Tuttorial | 0.1 | the game |
| 9 | Clean for Path 2 | 0.5 | the game |
| 10 | Path 2 Holder | 0.5 | the game |
| 11 | Path 2 ON | 117.4 | SHORTCUT (gate 2): Gate Run: Test: Stop Waiting For This Chest |
| 12 | Rooftop_ON PG | 0.1 | the game |
| 13 | Restart Loop | 1.3 | the game |
| 14 | Recreate | 5.9 | the game |
| 15 | Walk Tuttorial | 0.1 | the game |
| 16 | Clean for Path 3 | 0.5 | the game |
| 17 | Path 3 Holder | 0.5 | the game |
| 18 | Path 3 ON | 82.7 | SHORTCUT (gate 3): Ball Valley: Test: Pocket All Balls |
| 19 | Rooftop_ON PG | 0.0 | (still here) |

## GAMESTAGES coverage: 14 of 107 states seen

Not seen this run: `Init - Stage1`, `2.5 - BR`, `3 - LivingRoom`, `Enemy2 - Wait`, `Wait For the Punch`, `Smoke Wait 2`, `Enemy1 - Wait`, `Game Over`, `Stage 1 - The Start`, `LR - Stuffs`, `Clean Enemy 2`, `2 - The BR`, `Mini Stuffs`, `Mini Enemy`, `Clean Mini`, `Send death`, `Whack 6`, `Clean Enemy 1`, `Whack 7`, `Just Wait`, `Jenga Tower`, `Clean Enemy 3`, `BOSS`, `fairy reward`, `clean all`, `recreate`, `wait`, `BRM_COME`, `Move to Fairy`, `Smoke Wait`, `BR_Stuffs`, `Smoke C Wait`, `Rooftop_ON`, `Scale LR`, `Smoke Stuffs`, `Magic_Table`, `LR - Chaos`, `Smoke S Wait`, `BR_Mini_GO`, `BR_GO`, `LR_GO`, `LR GO 4`, `Pumpkin_5`, `BR_Magic_Table`, `SmokeC Wait`, `BR_Chaos`, `Enemy 3 Stuffs`, `Pumpkin_7`, `Smoke Chaos`, `Jenga Tower 2`, `Thank For Playing`, `Clean Stage 1`, `Prepare_Whack_6`, `Prepare_Whack_7`, `Damage The Lid`, `clean it`, `Destroy PlayGroundZ`, `Enemy 4 - Get the Seed`, `CP Bedroom`, `CP LivingRoom`, `CP Rooftop`, `CP Boss`, `CP Boss Lid`, `Checkpoint?`, `Resume?`, `Door Shortcut`, `CP 2.5 - BR`, `CP Scale LR`, `CP Scale LR Size`, `Bonus Round BR`, `Bonus Round PR`, `MiniBR_MagicTable`, `Smoke Wait 3`, `Mini Stuffs Chaos`, `Mini Pumpkins`, `Smoke P Wait`, `BR_Pumpkins`, `SmokeP Wait 2`, `Demo End`, `Demo Store`, `Demo?`, `Demo? Door`, `Demo? Path 1`, `Demo? Path 2`, `Gate 3 Pick`, `Tween PlayGroundZ To Boss`, `Destroy PlayGroundZ To Boss`, `Boss PG`, `Demo? Path 3`, `Boss Escape Mini`, `Boss Escape BR`, `Boss Escape LR`, `Boss Escape Jenga`.

## Console: 3 different warnings/errors

- 4830 x Exception: MissingReferenceException: The object of type 'UnityEngine.Rigidbody' has been destroyed but you are still trying to access it. Your script should either check if it is null or you (first in Path 3 ON | UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <dc764b59c049482b93bebc78a7e33a04>:0))
- 1685 x Exception: MissingReferenceException: The object of type 'Balls.BallPart' has been destroyed but you are still trying to access it. Your script should either check if it is null or you should (first in Path 3 ON | UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <dc764b59c049482b93bebc78a7e33a04>:0))
- 29 x Warning: Your project uses a scriptable render pipeline. You can use Camera.stereoTargetEye only with the built-in renderer. (first in Path 1 ON | )

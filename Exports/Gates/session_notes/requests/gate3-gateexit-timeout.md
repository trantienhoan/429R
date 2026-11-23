# Request from gate 3: a hard timeout on GateExit's rooftop wait (shared code)

From: Gate 3 (Ball Valley) session, K: PC, 2026-11-23 08:50. For the coordinator. Not urgent: never seen in a test.

**What:** `Assets/Scripts/Gates/GateExit.cs`, `Run()`, the "In the black until the new rooftop is there" loop
(lines 139-163 today). After `roofWaitSeconds` (20 s) it logs a warning and fades the view back in, then waits forever.
The player is still held there (`Hold(true)`: the CharacterController and the mover off), so a rooftop that never comes
leaves them standing in the gate, unable to move, until they quit. Gate 3's valley also stays (it goes when
`GateExit.Running` turns false).

**Why:** the gate contract §2 #6 "Never stuck: every wait has a fallback". Every other gate wait has one (chest seeds
25 s, The_Seed 20 s, the giant pumpkin vanishing).

**The exact change (smallest; your call on the fallback):**
1. A new field next to `roofWaitSeconds`:
   ```csharp
   [Tooltip("The rooftop still not there this many seconds after the seed: GAMESTAGES is sent to Fallback State " +
            "(0 = wait forever, as before).")]
   [Min(0f)] public float roofGiveUpSeconds = 45f;
   public string roofFallbackState = "Rooftop_ON PG";
   ```
2. In the loop, after the warning branch:
   ```csharp
   if (!down && roofGiveUpSeconds > 0f && waited > roofGiveUpSeconds && !gaveUp)
   {
       gaveUp = true;
       Debug.LogWarning($"{log} No rooftop {roofGiveUpSeconds} s after the seed (GAMESTAGES in '" +
                        GameStages.ActiveState() + "'): sending it to '{roofFallbackState}'.", this);
       GameStages.Fsm()?.Fsm.SetState(roofFallbackState);
   }
   ```
   (`waited` only counts while not paused and not down, as today; `gaveUp` a local bool. The loop then ends as soon as
   Rooftop_ON PG has made the new `Rooftop_Base`, and the rest of Run() puts the player by the tree as usual.)
3. Optional: if even that brings no rooftop within another 20 s, `Hold(false)` + `Running = false` so at least the
   player can move and the gate's stage goes.

**Test:** in Play, during a gate's exit, `GameStages.Fsm().Fsm.SetState("Path 3 ON")` right after the seed is eaten
(so the rooftop never comes): expect the warning at 20 s, the fallback at 45 s, Rooftop_ON PG, then Whack 6.

Gate 3 needs nothing else for this: `BallValleyGate.GoWhenOnTheRoof` already waits for `GateExit.Running` to turn false
(or Game Over).

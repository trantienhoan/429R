# Candy Buffs — Design

Approved by the user in chat on 2026-10-26.

## Intent
Every candy the player eats gives a small boost for the current run, so eating candy is worth it beyond healing.
The boosts are lost when the run ends.

## Rules
| Candy | Boost | Per candy | Limit |
|---|---|---|---|
| Candy_5 | Move Speed points | +0.5 point | +10 points |
| Candy_3, Candy_4 | Throw Power points | +0.5 point | +10 points |
| Candy_1, Candy_2, Candy_6, Candy_7 | Toughness points | +0.5 point | +10 points |
| Candy_8 | Punch damage (Fist Size's punch damage per point) | +0.5 point | +10 points |

- 2026-10-27: past +5 on a stat, each candy adds only +0.1 to it (Player Level: Candy Full Rate Points 5, Candy Slow
  Points 0.1). +10 takes 10 candies of that stat, then 50 more.

- Candy points stack on top of the player's level-up points and work like them (same per-point values on Player Level),
  including the game's existing floors and caps (damage never below 10%, Max Throw Scale).
- Candy 8 adds punch damage only: no bigger fist, no new punches.
- Candies still heal and add Wraps as today.
- The Level Up menu keeps showing only the player's own points. No HUD display for now.

## When they go
- Cleared when a new run starts: dying with no revive left (game over), Re-Dream, beating the game (Checkpoints.StartNewRun).
- Not saved: quitting (also Get a Rest) loses them; a new scene starts with none.
- Kept through a revive-in-place (clover coin): the run goes on. (Claude's reading of "dies and restarts".)

## Approach
One sensible way (alternative rejected: reading the candy type in C# hides the numbers from the user's PlayMaker work):
- A PlayMaker action "Eat Candy Buff" (boost + points) in each candy's Damage FSM "Health 1" state (the eat state),
  added by an editor menu; the user can change amounts in the FSM.
- Candy points live on the scene's Player Level, so a new scene starts without them.
- Candy Helmet eating goes through the same state; lollipops (SuckToEat) are other items and give nothing.

## Non-goals
- Blasts, bosses, UI, saving candy points.

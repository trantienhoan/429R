# Spider web meter — design

Date: 2026-10-26. Game: 429R (Unity 6000.3, VR / Quest). Status: waiting for the user's approval.

## Why

Today every spider web ball that hits the player slows them to 40% and blocks teleport for 3 s, straight away. One
lucky ball already locks you down, and the web shows nowhere except in how you move. The user wants web to **build
up**: one spider is just annoying, a group of spiders is a real threat, and the player can see the web building on
their health heart.

## What the player gets (decided with the user)

- **Web meter**, 0–100%, per player. Each normal web ball that hits the player adds **20%**.
- **Below 100%** a ball only adds web (plus its normal damage). No slow, no teleport block from the web itself.
  (The existing Hurt Slow still gives its small hit slow for the damage, as for any damage — unchanged.)
- **At 100% = webbed:** walking at **25%** of normal speed (75% slower) and **no teleport** for **5 s**. Then the
  meter is back at **0%**.
  - Web balls that hit during those 5 s still splat and still hurt, but add nothing and do **not** extend the 5 s
    (the player can never be kept stuck).
  - **Break after a lock (user, from the builder check):** for **3 s** after the lock ends, nothing adds web —
    normal balls and the Giant Web still hit and hurt, but the meter stays at 0. This gives the player a moment to run
    or teleport away, so locks can't follow each other back to back. The heart shows no web during the break.
- **The spider boss's special move (Giant Web)** fills the meter straight to 100% → the same 5 s lock. This replaces
  its own slow today (30% for 6.5 s). Its damage, aiming, dodging and shield block stay as they are.
- **Melting:** the web melts away when the player isn't hit. It starts **2 s after the last web hit** and drops
  **20% every 2 s**. *(My call, from the builder check:)* it melts smoothly at 10% per second, and the heart shows
  the rings rounded up. So a lone hit's ring is gone 4 s after the hit, and each further ring 2 s later. One spider
  can shoot at most about every 4 s (a 1 s shot + a 3–5 s wait), so **a lone spider can never fill the meter**. A
  group can. This is a hard rule and gets a test (hits 4 s apart never fill it).
- **Stopping a ball adds nothing:** a ball swatted by a fist / held weapon, or blocked by the legendary shield, adds
  no web (as today, those already stop the ball).
- **Spiders shoot more** (`Spider_Smarter`, inherited by every spider variant that webs): wait **3–5 s** between
  shots (was 5–9 s), and up to **2** spiders may shoot at the same time (was 1). Small spiders still never shoot.
- **On the heart (look A, picked from mockups):** a white spider web over the heart (clipped to the heart shape,
  like the crack): 8 spokes, and **one ring per 20%** (1 ring at 20% … 5 rings at 100%). Each new ring makes the web
  pop a little. At 100% (the 5 s lock) the web lines are thicker, the heart turns paler (a white wash over it) and
  shakes, and a **"0:05" countdown** shows under the heart, in the style of the boong buff countdowns. The HUD stays
  in view while there's any web (like it does for boong buffs), so the player sees it melt.
- **Death / new run:** going down (health 0, dying, reviving) clears the web and ends the lock, so the player gets up
  free. A new run clears it too. A pause freezes the meter with the game (it runs on game time).

## Goals / non-goals / anti-goals

- Goals: build-up instead of instant lock; group = threat, one spider = annoyance; readable at a glance on the heart.
- Non-goals: no change to web ball damage, flight, aiming, swatting; no change to the cockroach Face Leap slow, bug
  pumpkin slow or Hurt Slow; no new sounds; no haptics (user rule); no PlayMaker changes.
- Anti-goals (a failure even if it works): the player stuck for more than 5 s in a row by web (including two locks
  back to back); teleport blocked below 100%; the web picture covering the health number or making the heart
  unreadable.

## Approaches considered

1. **New `PlayerWeb` script next to `PlayerSlow` (picked by the user).** Holds the meter, melting and the lock; web
   balls call it; the heart HUD reads it; the actual 25% / no-teleport goes through `PlayerSlow.Apply`. `PlayerSlow`
   (shared by roach bites, bug pumpkins, Hurt Slow) stays as it is apart from one small "end it" call. Testable in Play
   mode like earlier features.
2. Build the meter into `PlayerSlow`. One file fewer, but every web tweak touches the slow all other effects share.
3. A PlayMaker FSM with a `WebMeter` float. Editable in PlayMaker, but the web ball and the HUD are C#, so it would be
   glue between two worlds — harder to test and to keep in sync.

## How it's built (the parts decided)

- **`Assets/Scripts/Locomotion/PlayerWeb.cs`** (namespace `Game.Locomotion`), on the XR Origin.
  - Puts itself on the XR Origin when the game starts, the way `HurtSlow` does (the user can add it by hand to change
    the numbers in the Inspector). Settings with tooltips: lock speed 0.25, lock seconds 5, break after the lock 3 s,
    melt delay 2 s, melt 20% per 2 s.
  - Static entry points for callers: add web (a share, 0.2 for a normal ball) and fill (giant web). Read-only state for
    the HUD: the meter (0–1), the rings to show (0–5), whether it's locked, and seconds left in the lock.
  - Reaching 100% calls `PlayerSlow.Apply(lockSpeed, lockSeconds, noTeleport: true)` once.
  - *(My call, from the builder check:)* `PlayerSlow` keeps one shared slow for every `Apply` (the slowest share
    holds until the latest one ends). So a roach Face Leap bite (35%, 1.5 s) or a bug pumpkin (50%, 3 s) that lands
    near the end of the lock keeps the player at 25% for up to that much longer. Teleport still comes back at 5 s.
    This is accepted: it's the bite's or the pumpkin's own slow, not the web, and it keeps `PlayerSlow` unchanged
    for all its other users. It is not the "stuck by web" anti-goal.
  - Clears on going down (same test as `HurtSlow`: health ≤ 0, `Checkpoints.IsDying`, `PlayerRevive.IsReviving`) and
    on `Checkpoints.RunStarted`.
- **`PlayerSlow`**: one new static call that ends the `Apply`-style slow and gives teleport back (like `EndHurt`),
  used by `PlayerWeb` when the player goes down mid-lock.
- **`WebBall`**: on hitting the player it adds its web share (new serialized field, default 0.2) instead of
  `PlayerSlow.Apply(slowTo, slowSeconds, true)`. The old slow fields go. `MakeGiant` makes the ball fill the meter
  (its slow parameters go); damage event, FSM, size and unswattable stay.
- **`SpiderBrain`**: the Giant Web slow settings (`giantWebSlowTo`, `giantWebSlowSeconds`) and their passing to
  `MakeGiant` go; nothing else in the special move changes.
- **`Spider_Smarter.prefab`**: `webCooldown` (3, 5), `maxWebShooters` 2.
- **Heart HUD (`PlayerHealthHUD`)**: a new optional `web` Image over the heart (between its fill and its shine, over
  the crack), with 5 sprites (1–5 rings). Hidden at 0. Pop on a new ring; at the lock: thick-line sprite or tint,
  pale wash, shake and the countdown row. Keeps the HUD up while the meter > 0. Hidden with "Health: hidden".
- **Web pictures + set-up menu:** an Editor menu, e.g. Tools > 429 Game > Player > Add Web To Health HUD, draws 5 PNGs
  (`Assets/Images/heart_web_1..5.png`, look A, clipped by `heart_white.png`'s alpha), imports them as sprites, adds
  the Web image to the HUD in the scene and wires it. Safe to run again. The user can repaint the PNGs later.

## Left to the builder

Exact sprite drawing (line widths, sag, anti-aliasing) as long as it matches look A; pop and shake sizes; how the
countdown joins the buff timer row; whether melting is continuous or stepped (the visible result must be one ring per
2 s after the 2 s delay); test layout. Tests run in Play mode through the existing MCP harness, with the 3 save slots
backed up and restored around them (they're the user's real saves).

# Cockroach Face Cling — Design

Agreed with the user in chat on 2026-10-26 (approach A picked). Not built yet.

## Intent
A cockroach that leaps at the player's face should stay on it, crawling and biting, until the player knocks it off with
a hand. It is the "get it off me!" moment the user wants to open the Steam trailer with, and a funnier fight beat.
Today the leap only lifts the roach's model to the face for its 0.4 s bite window (0.45–0.85 s), then it drops or goes
on through the player (Through Player, asked for on 2026-10-23).

## Decided with the user
- **Knocked off by any hand slap or punch** (open hand or fist), not a grab.
- **Approach A:** a new **Cling** option on the existing Face Leap attack in `SpiderBrain`, the way Through Player was
  added (approach B, a separate script taking the roach over, was turned down: two scripts would share one roach's
  movement, lift and animation).

## Claude's calls (said to the user, not objected to)
- **Who clings:** Cockroach_Smarter and its variants Normal and Small, and the roaches inside the metal furniture.
  Cockroach_Boss and Boss_PumpkinButt keep Through Player; the spider bosses' Pounce is unchanged.
- **On the face:** it follows the head every frame, a little below the eyes, belly towards the player, head up, with a
  small side-to-side crawl. It covers part of the view, never all of it (VR comfort).
- **Bites every ~2 s** while on the face, through the normal bite (same event, damage flash, Toughness), so ignoring it
  costs health.
- **Animation:** no new one. The looping **run** played slowed down (the legs scurry like crawling), and **bite** on
  each bite. (A custom clinging animation would be the user's own Blender work, if ever wanted.)
- **One at a time:** while one roach clings, others leap the old way (through the player).
- **It also lets go** when the player goes down (death fall / revive coins), falls off an edge, teleports, or when it
  dies.
- **Knocked off:** it drops to the floor in front of the player, plays **hit**, is stunned a moment, then fights on with
  its normal cooldown.

## Constraints
- Reuse SpiderBrain's leap rise, bite (`TryBite` path), hit/fall (`FallIfLifted`) and the ghost balloon's hand-slap
  check (hand position + speed each frame, so open hands count; a fist ball or weapon hitting it counts too).
- Hands near the face for other reasons (eating candy at the mouth) only knock it off if they move into it fast.
- Settings visible in the Inspector on the attack (cling on/off, bite interval, offset), set by a menu like
  "Cockroaches Leap Through The Player".
- Back up the changed prefabs; Play-test in the Editor (save files snapshotted and restored).

## Non-goals
- New animations, a grab-and-throw removal, changing the bosses or spiders.

## Left to the builder
Exact face offset and crawl wiggle (checked with Game-view pictures), the slap speed threshold, stun length, and how
the "goes down / teleport" cases are detected.

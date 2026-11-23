# Gate 2 (Slime Bowling): new concept prompts (2026-11-22)

Replaces the 2026-11-18 `PROMPTS.md` (kept for reference). Goal: one concept picture in the SAME look as gate 1's
approved concept, then a kit `.blend` built from it.

**How to use (Gemini or ChatGPT):** start a new chat and attach `../PlayGround_Concept/concept_v1.png` (gate 1's
concept: the style to match). Optional second picture for the balls: `C:/Users/ADMIN/Downloads/ball_concept.png` (the
jack-o'-lantern ball). Paste Prompt 1. When you like one, save it here as `concept_v1.png`, then ask for Prompt 2 and
Prompt 3 "of this same design" in the same chat.

---

## Prompt 1: the whole arena from above (key art)

```text
The attached picture is the approved concept art of another level in my VR game. Match its art style, palette,
lighting, camera height and level of detail exactly, but design a NEW level. (If a second picture of a carved
jack-o'-lantern ball is attached, use it for the bowling balls.)

Concept art for a VR action-game arena: "The Slime Bowl", a haunted toy bowling alley floating in the night sky
above a misty pumpkin valley. High three-quarter bird's-eye view of the whole arena.

Keep this layout exactly (the game needs it):
- In the exact centre, a small round floating island of earth and moss, about 5 m across, where the player stands:
  a tiny pumpkin patch with four curling vines that grow orange jack-o'-lantern bowling balls, a candle lantern and
  an old treasure chest.
- From the island, four long straight bowling lanes reach out in an X, 90 degrees apart, like toy wooden bridges in
  the sky: each about 2 m wide and 20 m long, flat and level with the island, polished honey-coloured wood with
  inlaid gold arrows, candy-striped gutters and low rails. Nothing hangs or arches over the lanes: chunky wooden
  trestles and chains to small floating rocks hold them up from below.
- At the far end of each lane, a round stone pin deck like a small stage with a glowing rune ring, holding a triangle
  of six green slime bowling pins with goofy carved faces, and a big iron fire brazier on each side. Each deck glows
  its own colour: green, red, blue, yellow. Behind each deck (never over the lane) a crooked arch of stacked pumpkins
  with a curtain of purple mist.
- Open sky above the lanes and empty space between them: it is a long fall into the mist. Overhead a giant fat green
  pumpkin-slime with small bat wings flies in circles, dropping slime pins onto the decks.

Below and around: rolling mist, the tops of jelly-bubble trees and pumpkin fields far below, a few floating rocks with
hanging lanterns, small flying slimes circling a big orange harvest moon.

Style: stylised low-poly game art, chunky readable shapes with soft bevelled edges, flat colours with soft painted
gradients, soft dark contact shadows where things meet, a little gloss on the slime, like a polished indie VR game.
No photo realism, no fine noisy detail. Palette: deep indigo-violet night sky, big orange harvest moon, warm orange
horizon glow, honey wood, plum-grey stone, slime green, orange jack-o'-lantern glows; warm light pools from the
braziers against cool blue moonlight. Cosy-creepy, fun, not gory. No text, no logos, no UI.
```

## Prompt 2: the same design at eye level (what the player sees in VR)

```text
Same Slime Bowl design as before, now seen from the player's eyes: standing on the central island 1.6 m tall,
holding a pumpkin bowling ball, looking straight down one lane at its pin deck 20 m away; the other lanes run off to
the left and right; the giant green bat-winged slime flies overhead. Wide field of view like a VR headset. Show the
scale: pins about knee high, braziers taller than the player. Same style and palette, no text.
```

## Prompt 3: the kit sheet (for building it in Blender)

```text
Kit sheet for this same Slime Bowl design: separate pieces on a plain dark grey background, each at three-quarter
view with space around it, same style and lighting for all, chunky shapes that would work under 2,000 triangles each:
a straight 4 m lane segment, the lane end that meets the island, a gutter with its low rail, a wooden trestle support,
a chain with a small floating rock, the round central island, a round pin deck with its rune ring, a fire brazier,
the pumpkin arch behind a deck, a hanging lantern, a green slime bowling pin, a pumpkin bowling ball, the curling
ball vine, the treasure chest. No text, no labels.
```

---

**Tips**
- Too dark: add "brighter, readable, more moonlight and glow".
- Too busy: "fewer, bigger props, more open sky between the lanes".
- Lanes bending or crossing: "four perfectly straight lanes in an X, all starting at the island".
- Keep it buildable for Quest: chunky shapes, no thin wires or leaves, one shared palette texture.

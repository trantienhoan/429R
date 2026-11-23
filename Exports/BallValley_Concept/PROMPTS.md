# Gate 3 (Ball Valley): concept prompts (2026-11-22)

No concept existed for gate 3 (today it uses the BALLS web game's plain art). Goal: one concept picture in the SAME
look as gate 1's approved concept, then a kit `.blend` built from it.

**How to use (Gemini or ChatGPT):** start a new chat and attach `../PlayGround_Concept/concept_v1.png` (gate 1's
concept: the style to match). Optional second picture for the boss: `C:/Users/ADMIN/Downloads/ball_concept.png` (the
jack-o'-lantern ball). Paste Prompt 1. When you like one, save it here as `concept_v1.png`, then ask for Prompt 2 and
Prompt 3 "of this same design" in the same chat.

The real sizes (from the game): lawn 50 x 25 m; 6 holes about 5 m across, cut into the border like pool-table pockets
(4 corners + the middle of each long side); balls 1.6 m across at the start, growing up to 4 m; the field later shrinks
to half; the giant pumpkin ball boss is about 5 m across.

---

## Prompt 1: the whole valley from above (key art)

```text
The attached picture is the approved concept art of another level in my VR game. Match its art style, palette,
lighting, camera height and level of detail exactly, but design a NEW level. (If a second picture of a carved
jack-o'-lantern ball is attached, use it for the giant pumpkin ball.)

Concept art for a VR action-game arena: "Ball Valley", a giant haunted pool table in a moonlit pumpkin valley.
High three-quarter bird's-eye view of the whole arena.

Keep this layout exactly (the game needs it):
- A long flat rectangle of soft green moss lawn, about 50 m long and 25 m wide (twice as long as wide), smooth like
  the cloth of a giant pool table, framed all round by a low rim of chunky rounded stones and twisted roots, about
  knee high.
- Six round holes, about 5 m across, cut into the rim exactly like the pockets of a pool table: one at each corner and
  one in the middle of each long side. Each hole is a dark pit with a glowing rim (a ring of small jack-o'-lantern
  lanterns or glowing purple runes) so it reads from far away.
- About 15 big glossy rolling balls on the lawn, as tall as a person or bigger, in bright colours (red, blue, yellow,
  green, purple, white) with big googly eyes and silly faces; some drag a wobbly tail of smaller balls like a snake.
- Small bright candies scattered on the lawn, plus a few obstacles inside it with lots of open lawn between them:
  jelly-bubble trees on striped licorice trunks, mossy boulders, crooked gravestones.
- At the far end, a giant carved jack-o'-lantern ball about 5 m across, bigger than the holes, grinning.

Around the table: a sunken valley of dark rolling hills and pumpkin fields, twisted trees, lantern posts and a small
ruined gazebo outside the rim, mist in the low parts, small flying slimes circling a big orange harvest moon.

Style: stylised low-poly game art, chunky readable shapes with soft bevelled edges, flat colours with soft painted
gradients, soft dark contact shadows where things meet, a little gloss on the balls and slime, like a polished indie
VR game. No photo realism, no fine noisy detail. Palette: deep indigo-violet night sky, big orange harvest moon, warm
orange horizon glow, teal-green moss, plum-grey stone, bright candy colours on the balls, orange jack-o'-lantern
glows; warm light pools at the holes against cool blue moonlight. Cosy-creepy, fun, not gory. No text, no logos, no UI.
```

## Prompt 2: the same design at eye level (what the player sees in VR)

```text
Same Ball Valley design as before, now seen from the player's eyes: standing on the lawn 1.6 m tall near one long
side, looking along the table toward a corner hole with its glowing rim; googly-eyed balls rolling toward the player,
one big ball with a tail of smaller balls; candies on the grass at the feet. Wide field of view like a VR headset.
Show the scale: balls as tall as the player or bigger, the rim knee high, the holes wider than two balls. Same style
and palette, no text.
```

## Prompt 3: the kit sheet (for building it in Blender)

```text
Kit sheet for this same Ball Valley design: separate pieces on a plain dark grey background, each at three-quarter
view with space around it, same style and lighting for all, chunky shapes that would work under 2,000 triangles each:
a straight 4 m rim segment of rounded stones and roots, a rim end piece next to a hole, a corner hole with its glowing
lantern rim, a side hole with its rim, a square moss lawn tile, a jelly-bubble tree, a mossy boulder, a crooked
gravestone, a lantern post, three googly-eyed balls (small, medium, big), a tail ball, a few candies, the giant
jack-o'-lantern ball, the ruined gazebo. No text, no labels.
```

---

**Tips**
- Too dark: add "brighter, readable, more moonlight and glow".
- Holes hard to see: "make the six holes glow brighter, ringed with lanterns".
- Too busy: "fewer obstacles, more open lawn".
- Keep it buildable for Quest: chunky shapes, no thin wires or leaves, one shared palette texture.

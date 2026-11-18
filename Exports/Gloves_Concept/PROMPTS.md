# Gloves: concept prompts (2026-11-18)

Today's gloves are placeholders (a plain red, green or blue mitt with a cuff). These prompts are for concept art to
model the real ones quickly.

**How the gloves work in the game (what the art must suit):**
- Three kinds, one random pair per run, hanging from the Tip Knight's staff at the start; touch it to take them.
  - **Boxing Gloves**, red: bigger fists, unlocks the flying punches (jab, swing, uppercut).
  - **Throwing Gloves**, green: stronger throws, everything thrown hard explodes.
  - **Soldier Gloves**, blue: stronger weapons, a weapon hit knocks things into a blast.
- A glove only exists as a closed fist: it pops onto the hand the moment you make a fist (grip + trigger), and pops
  away when the hand opens. It grows bigger with the Fist Size stat.
- In VR you see your own gloves from behind and above (the back of the hand, the knuckles, the thumb side), right in
  front of your face, all the time. That side carries the design.
- Flying punches are copies of the glove that fly off and hit things: the silhouette must read from the back at 5 m.

**How to use (Gemini or ChatGPT):** start a new chat and paste Prompt 1. When you like a design, ask for Prompt 2
"of this same design" for each kind (the modelling turnaround). Prompt 3 checks how they read in the game.

What has to stay (for the game and for fast modelling):
- a closed fist, fingers merged into one chunky shape (a mitt), thumb clearly separate, no open-hand pose;
- real size about 12 cm wide, 14 cm tall and 16 cm from knuckles to wrist, plus a cuff about 9 cm long that covers the
  wrist of a steel knight gauntlet (the player's hands are armoured knight hands);
- punching face = the knuckles, thumb on top (a vertical fist, like a jab);
- each kind keeps its colour (red, green, blue) as the main colour, so the pair on the Knight's staff tells you which
  kind it is from far away;
- left and right are mirror images;
- simple enough to model in an hour: chunky shapes with bevelled edges, under 1,500 triangles a glove, flat colours with
  soft painted gradients (one shared colour-palette texture), details as raised shapes or painted marks, not sculpting.

---

## Prompt 1: the three pairs (key art)

```text
Concept art for the gloves of a cartoon Halloween VR action game where the player is a little knight who punches giant
bugs, slimes and evil pumpkins. Show three pairs of magic fighting gloves side by side on a dark plum background, each
pair once from the front-three-quarter view (knuckles toward the viewer, thumb on top) and once from behind (the back of
the hand and the cuff, the way the player sees their own fists in VR). Every glove is a chunky closed fist: the fingers
merged into one rounded mitt shape, the thumb separate, a short padded cuff that straps over the wrist of a steel knight
gauntlet.

1. BOXING GLOVES, glossy red leather: a classic boxing glove made fat and cartoony, a knuckle pad shaped like a carved
   jack-o'-lantern grin, laces stitched in pumpkin orange, a small gold fist emblem on the back of the hand, a cuff with
   two tiny bat wings at the wrist (for the flying punches).
2. THROWING GLOVES, slime-green suede: a grippy bomb-thrower's glove, a padded palm, a cuff strap with three little
   round candy bombs with short fuses tucked into loops, a spark-and-fuse emblem on the back of the hand, slime-green
   glowing seams.
3. SOLDIER GLOVES, deep blue steel and leather: an armoured knight gauntlet glove, rounded overlapping steel plates over
   the knuckles and the back of the hand with big friendly rivets, a small shield emblem on the back, a brown leather
   cuff with a brass buckle.

Bold readable silhouettes that tell the three kinds apart from far away. Style: stylised low-poly game art like a soft
toy diorama: chunky shapes with bevelled rounded edges, flat colours with soft painted gradients, almost no texture
noise, soft dark contact shadows, polished indie VR game, not photo-real. Warm orange rim light and cool violet
moonlight. Cosy-creepy Halloween, fun, not gory. No text, no logos, no UI, no hands or arms inside the gloves.
```

## Prompt 2: one glove as a modelling turnaround (one kind at a time)

```text
Same glove design as before, the [BOXING / THROWING / SOLDIER] glove for the RIGHT hand only, as a modelling
turnaround sheet for a 3D artist: six orthographic views on a plain flat light grey background, all at the same scale,
lined up in a neat grid, no perspective, no shadows on the ground: front (looking at the knuckles), back (the cuff
opening), top (the back of the hand), bottom (the palm and the curled fingers), the thumb side, the little-finger side.
A closed fist the size of a real fist (about 12 cm wide, 14 cm tall, 16 cm long plus a 9 cm cuff). Clean simple
chunky shapes that a low-poly model can follow (under 1,500 triangles), bevelled edges, flat colours with soft
gradients, every emblem and strap clearly drawn. No text, no labels, no measurements, no hand inside.
```

## Prompt 3: how they read in the game (first-person VR view)

```text
Same three glove designs, now in the game, from the player's eyes in first-person VR: two gloved fists raised in front
of the camera like a boxer's guard, seen from behind (the backs of the gloves and the cuffs big in the foreground). One
fist is punching forward and a glowing copy of the glove flies off toward a big cartoon spider in a cosy haunted
bedroom at night; candy and pumpkins on the floor. Show it once for each pair (red Boxing, green Throwing, blue
Soldier), three panels side by side. Same soft toy-diorama low-poly look, warm lamp light, purple moonlight. No text, no
UI.
```

## Prompt 4 (optional): the pair hanging from the Knight's staff

```text
Same glove designs: each pair tied together by its laces and hanging from the end of a small cartoon knight's wooden
staff, like a prize, slowly turning, a soft puff of its colour around it. Three versions (red, green, blue) on a dark
plum background. Same soft toy-diorama low-poly look, no text.
```

---

**Tips**
- Fingers drawn separately or an open hand: "a closed fist, the fingers merged into one rounded mitt, only the thumb
  separate".
- Too detailed to model fast: "simpler, chunkier, fewer parts, big bevelled shapes, details painted flat".
- The three look alike: "make the silhouettes more different: Boxing round and fat, Throwing slim with bombs on the
  cuff, Soldier blocky steel plates".
- Keep the colours: if the kinds change colour, say "Boxing red, Throwing green, Soldier blue as the main colour".

**Modelling notes (for when the concept is picked)**
- One mesh per glove, pivot at the knuckles: +Z where the punch goes, +Y up through the thumb side, sized like the
  placeholder (Assets/Models/Gloves/Glove_Placeholder_Mitt_R: about 12 x 14 x 16 cm, the cuff behind it to about 18 cm
  from the knuckles).
- Model the right glove, mirror it for the left.
- In Unity each kind's glove swaps in as the "Model" child of Prefabs/Player/Gloves/Glove_<Boxing|Throwing|Soldier>_<L|R>;
  its "Punch Shape" capsule (radius 5.8 cm, 16 cm long) stays, so the punches feel the same.

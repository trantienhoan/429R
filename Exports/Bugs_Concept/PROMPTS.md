# Bugs remake: pumpkin-bug concept prompts (2026-11-20)

**Goal (user, 2026-11-20):** the spider, cockroach and bee should look more like pumpkins, so they fit the
pumpkin-everywhere game and make it look and feel better.

**Result:** none yet. Save the Gemini picture you pick here as `concept_v1.png`.
- **Try 1** (`gemini_try1.png`, from Prompt 1): you said it looks fun, but it's far from your pumpkin style. It's a
  painterly illustration with darker, realistic rind, curly vines with leaves for legs, and soft glowing faces.
- **Round 2:** match the game's own pumpkins. See "Round 2" below and `Pumpkin_style_sheet.jpg`.
- **Try 2** (`gemini_try2.png`, from the round 2 short prompt): you like it and it matches the pumpkin style, but the
  bugs are too cute and chubby. They should look like they'll bite and eat you alive. See "Round 3" just below.

- **Try 3** (`gemini_try3.png`, from round 3b): you said "this looks nice". Next is a low-poly style version: see
  "Round 4" just below.

## Round 4: the same bugs in a low-poly style (2026-11-20)

Try 3's design stays; only the modelling style changes, to low-poly game models that fit a standalone VR headset.
Today's bugs are faceted low-poly, so attaching one of their renders shows Gemini the right polygon level.

**In the same Gemini chat as try 3:** attach `screenshots/Spider_Normal__Spider_Smol.png` (today's spider: the facet
level to aim for), then:

```text
Perfect design, keep it exactly: the same three bugs, the same colours, poses, faces, teeth, thorns and claws. Now
remake them in a low-poly style, as real-time 3D game models for a standalone VR headset, like the attached model
from my game:
- Visible flat polygon facets with clean, simple shapes. About 2,000-4,000 triangles per creature.
- Pumpkin bodies: the ribs as 8 bold faceted segments, sharp edges between facets, a chunky faceted stem.
- Legs: tapered faceted prisms with a few angled segments. Thorns as simple sharp pyramids. Claws as simple hooked
  wedges.
- Teeth as sharp triangular wedges. Eyes, mouth glow and drool as simple flat shapes; the drool as a few tapered
  drops.
- Textures: flat colours with a soft gradient on each part, hand-painted and simple. No fine detail, no surface
  noise, no sculpted micro detail.
- Keep the silhouettes bold and readable: nothing so thin or small that it would vanish at low poly.
Side by side, 3/4 front view, slightly low camera angle, plain grey background, soft studio light, warm glow from
the mouths and eyes. No text.
```

Variations to try after it:
- **Smoother look:** "same low-poly models but with smooth shading (no visible facets), like the attached pumpkins",
  with `Pumpkin_style_sheet.jpg` attached. This matches the pumpkins, which use smooth shading.
- **Wireframe check:** "show the same three models with a white wireframe over them". This shows how many polygons
  it imagined, which helps when you model them in Blender.

## Round 3: from cute to menacing (2026-11-20)

**Why try 2 reads as cute:**
- big round heads and eyes;
- round balloon bodies;
- short, thick, rubbery legs, the same width all the way down;
- small closed grins;
- calm, upright standing poses;
- the camera at eye level.

**What makes it menacing (same pumpkin look, same colours):**
- **Silhouette:** lean and angular instead of round; long thin legs with high bent knees; thorns along the stems and
  needle-sharp claw tips. Sharp shapes read as danger, round ones as friendly.
- **Eyes:** smaller, narrow slanted slits under a carved angry brow. Big eyes are cute; narrow eyes are hostile. The
  spider gets a cluster of small burning eyes.
- **Mouth:** the grin becomes a real jaw, torn wide open, with jagged pointed rind teeth and a glowing throat. Strings
  of pumpkin pulp and seeds hang like drool, and thorn fangs or pincers stick out. This is the biggest "it wants to eat
  you" signal.
- **Rind:** the same bright orange, with scorched black edges round every carving and a few cracks and scars.
- **Pose and camera:** crouched hunting poses leaning at the viewer, front legs raised to strike, a slightly low camera
  angle (it looks bigger), and a warm glow lighting them from inside the mouth.

For the game: longer legs mean scaling the rig's leg bones a little in Blender. The animations are mostly rotations,
so they still work. The poses are only for the concept: in the game they come from the animations.

### Round 3 prompt: in the same Gemini chat as try 2

```text
I like these, keep the same pumpkin style (the same bright orange matte rind with soft ribs, olive-green stems,
carved jack-o'-lantern faces with glowing yellow eyes, the clean 3D game-model look), but they are too cute and
chubby. Make them real menaces, monsters that want to bite you and eat you alive:
- Lean and angular instead of round; long thin stem legs with high bent knees, sharp thorns along the stems and
  needle-sharp tips like claws. Same number of legs and wings as before.
- Smaller, narrow slanted eyes glowing yellow under a heavy carved angry brow; the spider has a cluster of small
  burning eyes.
- Huge mouths: the carved grin is now a jaw torn wide open, with rows of jagged pointed pumpkin-rind teeth, a dark
  throat glowing orange inside, and strings of pumpkin pulp and seeds hanging like drool.
- Scorched black edges around every carving, a few cracks and scars in the rind.
- Spider: crouched low, front legs raised to strike, two big thorn fangs out.
- Roach: flat and low, long thorny antennae, both rind lids flared open, pincer jaws made of thorns.
- Bee: a long thorn stinger dripping orange goo, ragged wings, jaws open, diving forward.
All three in hunting poses leaning toward the viewer, as if about to lunge. Side by side, 3/4 front view, slightly
low camera angle, plain grey background, soft studio light plus a warm glow from inside their mouths and eyes.
Not a painting, no text.
```

If it goes too far (gory or too dark to read), add: "Keep them readable and not gory: menacing like a Halloween game
boss, not a horror movie."

### Round 3b: meaner, scarier teeth and claws (user, 2026-11-20)

```text
Make them meaner. Keep the same pumpkin style and colours, but push the teeth and claws much further:
- Teeth: oversized, long and crooked, needle-sharp and serrated, in two or three rows like a shark, made of hard
  pale rind shards and pumpkin seeds. The jaws are unhinged, opening wider than the head, with stringy orange pulp
  for gums and drool hanging between the teeth.
- Spider: two huge curved sickle fangs hanging in front of its mouth, dripping goo, and small grasping mandibles
  around the open jaw.
- Roach: serrated saw-blade pincer jaws, and a second mouth full of teeth inside its open rind lids.
- Bee: a jaw split into four sharp mandibles, and a long barbed dagger stinger.
- Claws: every leg ends in a long hooked talon of hard dark-brown stem, curved like a raptor's claw; backward-pointing
  rose-thorn barbs along every leg. The spider's and roach's front legs end in scythe blades like a mantis.
- Faces: furious, eyes narrowed to burning yellow slits, deep carved frowns, all teeth bared.
Same three bugs in the same hunting poses, side by side, 3/4 front view, slightly low camera angle, plain grey
background, soft studio light plus a warm glow from inside their mouths. Clean 3D game-model look, not a painting,
no blood, no text.
```

## Round 2: match my pumpkin style (2026-11-20)

`Pumpkin_style_sheet.jpg` shows the game's own pumpkins, rendered with their real materials. Most are used in
`2 Game Scene` or spawned by its prefabs:
- **Row 1:** a plain tree pumpkin (Pumpkin_On_Tree), then Skull_Pumpkin, StartGamePumpkin and Bug_Pumpkin.
- **Row 2:** Fat Pumpkin, a nesting pumpkin, and Pumpkin_1 and Pumpkin_2 with their hats.
- **Row 3:** Pumpkin_4 and Pumpkin_7 with their hats, then the two skeleton mob pumpkins.
- **Row 4:** the pumpkin music player, the pumpkin boss, a carved face close-up, and the rind and stem close-up.

Every render is in `pumpkin_style/` (4 sides each); the Pumpkin Tree isn't on the sheet because its shader renders
black in the preview.

**The style, as written for Gemini:**
- **Rind:** bright saturated orange, matte, soft mottled shading, darker orange down the grooves.
- **Shape:** round and a bit squat, with 8-10 soft rounded ribs.
- **Stem:** short, thick, olive green, slightly twisted with a hooked or tapered tip. No leaves, no vines.
- **Carved faces:** big slanted angular eyes, each with a thick dark-brown rim (the cut rind) and glowing lemon-yellow
  inside; a small dark triangle nose; a wide jagged zig-zag grin.
- **Look:** chunky toy-like 3D game models with smooth shading and simple textures, not paintings. Accessories are
  simple solid shapes (sack cap, striped elf hat, cowboy hat, witch hat, beehive). Skeletons are chunky cream-white
  bones.

### Round 2, short version: in the same Gemini chat as try 1
Attach `Pumpkin_style_sheet.jpg`, then:

```text
I like these ideas, but they don't match my game. The attached sheet shows the real pumpkins from my game, rendered
from the game itself. Redo the same three bugs (spider, roach, bee) so they look like they belong to that exact set:
the same bright saturated orange matte rind with soft ribs, the same short thick olive-green stems, the same carved
faces (big slanted angular eyes with thick dark-brown rims and glowing lemon-yellow inside, a small triangle nose, a
jagged zig-zag grin), the same chunky toy-like proportions, rendered as clean 3D game models like the sheet (smooth
shading, simple textures, plain grey background, soft studio light), not a painting. No leaves and no curly vines:
legs are chunky smooth olive-green stems like the pumpkin stems, slightly twisted, with tapered tips. Side by side,
3/4 front view, no text.
```

### Round 2, full version: in a new Gemini chat
Attach `Pumpkin_style_sheet.jpg` and `Bugs_now_overview.jpg`, then:

```text
Attached: (1) a sheet of the real pumpkins from my Halloween VR game, rendered from the game; (2) the game's three
enemy bugs as they are today (a spider, a cockroach and a bee; normal size on the left, bosses to the right).

Redesign the three bugs as pumpkin creatures that belong to the exact same set as the pumpkins in image 1. Keep each
bug's body plan from image 2, so the same skeleton and animations still fit:
- the spider has a round back body, a small head and 8 legs;
- the cockroach has a long oval body with two wing lids on its back, a small head, 6 legs and 2 long antennae;
- the bee has a big back body with a stinger, a round middle, a head, 6 legs and 4 wings.

1. Pumpkin Spider: its back body is one of my pumpkins (same orange rind, same soft ribs, the olive-green stem on
   top). Its head is a small carved jack-o'-lantern facing forward with my face style: two big slanted angular eyes
   with dark-brown rims glowing lemon-yellow, plus four tiny round glowing extra eyes, a jagged grin with two small
   fangs. Eight chunky smooth olive-green stem legs, slightly twisted, tapered tips.

2. Pumpkin Roach: a long oval pumpkin body with the ribs running front to back; the top splits into two pumpkin-rind
   lids (open a little, yellow glow inside). Small carved pumpkin head with two big angular glowing yellow eyes and a
   jagged grin. Two thin olive-green stem antennae, six olive-green stem legs.

3. Pumpkin Bee: its back body is a pumpkin with darker orange bands, and its stinger is the pumpkin's olive-green stem
   pointing backwards. A round dark-orange middle, a small carved pumpkin head with angry slanted eyes glowing
   red-orange and a jagged grin, short stem antennae, six short olive-green stem legs, four simple flat wings, pale
   and slightly see-through with a few veins.

Style: exactly like the pumpkins in image 1. Clean 3D game models with smooth shading and simple textures, the same
bright saturated orange matte rind, the same chunky toy proportions, the same carved faces. Not a painting, no leaves,
no curly vines. Character sheet, the three bugs side by side, 3/4 front view, plain grey background, soft studio
light. No text.
```

Then use Prompts 2-4 below "of these same designs", attaching `Pumpkin_style_sheet.jpg` again each time.

## What's in this folder
- `Bugs_now_overview.jpg`: today's bugs, 3/4 front view. Rows are spider, cockroach and bee. Columns are normal, boss
  and pumpkin-butt boss.
  - The bosses are the same models painted white. The bee boss also wears a small pumpkin crown.
  - The pumpkin-butt bosses add parts: the spider carries a carved pumpkin shell with a skull in it, the cockroach
    wears a skull mask, and the bee carries a pumpkin plus a skull mask.
- `Spider_now_sheet.jpg`, `Cockroach_now_sheet.jpg`, `Bee_now_sheet.jpg`: each bug's three versions in three views
  (3/4 front, side, front).
- `Bugs_now_model_variants.jpg`: each bug has 3 model variants for its normal size, and one is picked at random when
  it appears. They are close, with small shape changes.
- `screenshots/`: every render, one strip per bug, version and model variant, plus `_parts.txt` (what each one is made
  of: meshes and materials).
  - The renders use the game's real materials, in a neutral light. Spiders and cockroaches are in their idle pose;
    bees are in flight, because their wings fold away in the idle clip.

## How to use in Gemini (round 1; for the pumpkin style use round 2 above)
1. Attach `Bugs_now_overview.jpg` and paste Prompt 1.
2. When you like the designs, ask for Prompt 2 for each bug, attaching that bug's `*_now_sheet.jpg` too.
3. Then ask for Prompts 3 and 4 "of these same designs".

The look should match the soft toy-diorama style of the rooftop and bedroom concepts (`../Rooftop_Concept/TEXTURE_STYLE.md`).

## What has to stay (the game needs it)
- **Body plans and proportions close to today's models**, so the existing rigs and animations can be kept: the
  pumpkin parts are new shapes on the same skeleton.
  - Spider: a big round back body, a small head, 8 legs, two front fangs or claws. It walks, leaps and pounces, and
    shoots web balls.
  - Cockroach: an oval body with two wing lids that can open on its back, a small head, 6 legs and 2 long antennae.
    It runs fast, leaps at the face and clings to it.
  - Bee: a big striped back body with a stinger, a round middle, a head, 6 legs and 4 wings. It flies, dives and
    stings, shoots poison stings, and sometimes flashes and dives as a kamikaze bomb. It loses its wings when hurt
    and walks on.
- **Each bug readable at a glance in dark rooms in VR**: three clearly different silhouettes (spider wide and low,
  cockroach long and flat, bee upright with wings), plus glowing eyes or a glowing carved face.
- **Sizes**: normal bugs are about 0.3-0.6 m long, bosses about 1 m. A boss has to be the same bug, clearly bigger
  and fancier.
- **Paintable**: flat colours with soft gradients and big readable shapes, no fine texture noise. You paint the
  textures yourself on the model's UVs.

---

## Prompt 1: the three pumpkin bugs (lineup)

```text
The attached picture shows the three enemy bugs of my Halloween VR game as they are today: a spider, a cockroach and
a bee (normal size on the left, boss versions to the right). Keep each creature's body plan and proportions (the
spider's round back body, small head and 8 legs; the cockroach's oval body, wing lids, 6 legs and long antennae; the
bee's big striped back body with a stinger, 6 legs and 4 wings) but redesign them as pumpkin creatures, as if they
grew in a haunted pumpkin patch. The whole game is "pumpkins everywhere", and these bugs should belong to it.

Character design sheet, the three creatures side by side, 3/4 front view, same scale, on a plain neutral grey
background:

1. Pumpkin Spider: its round back body is a small ribbed orange pumpkin with a jack-o'-lantern face carved into
   the back, glowing warm orange from a candle inside. A short curly green stem on top. Its head is a small dark
   green gourd with a cluster of glowing orange eyes like pumpkin seeds lit from within. Eight legs made of dark
   green twisted pumpkin vine, with small leaf tufts at the joints and curly tendril tips. Two fangs like pale
   pumpkin seeds.

2. Pumpkin Roach: a long flat body whose back is a pumpkin-rind shell split down the middle into two lids (they can
   open like beetle wings, showing a soft orange glow inside), with pumpkin ribs running along its length.
   Head under a little stem cap, two big glowing orange eyes, two long curly vine-tendril antennae, six thin
   vine legs. Fast and sneaky looking.

3. Pumpkin Bee: its big back body is a striped decorative gourd (orange with dark green stripes) ending in a
   candy-corn stinger. A fuzzy dark green round middle, a small dark head with angry glowing red-orange carved
   eyes and two stem antennae, four wings like thin dried pumpkin leaves, pale gold-green and slightly see-through
   with leaf veins. Six short dark vine legs.

Style: stylised low-poly game art like a soft toy diorama: chunky shapes with bevelled rounded edges, flat colours
with soft painted gradients, almost no texture noise, soft dark contact shadows, matte surfaces, polished indie VR
game, not photo-real. Cute-creepy Halloween, not gory. Each creature must read clearly in a dark room: strong
silhouettes and glowing eyes or carved faces. No text, no logos, no UI.
```

## Prompt 2: one bug, turnaround for modelling (ask once per bug)

```text
Using the [Pumpkin Spider / Pumpkin Roach / Pumpkin Bee] from the image you just made (the attached sheet shows
today's model), make a character turnaround sheet for 3D modelling: front view, side view, back view and top view,
all at the same scale and lined up, neutral grey background, flat even lighting, no shadows on the background, no
perspective distortion. Same design, same colours, same proportions as before. Keep the body plan of the attached
model (number and position of legs and wings, size of body parts) so it fits the same skeleton. Clean shapes, no
text.
```

## Prompt 3: the boss versions

```text
Now the boss versions of the same three pumpkin bugs, in the same style: each one about twice as big and grander.
White "ghost pumpkin" colouring (pale cream-white pumpkin rind with soft orange glow seeping out of the carved parts
and the seams), a small crown made of a cut pumpkin top with its stem, more thorny vine spikes on the legs, and
brighter, angrier glowing faces. The same body plans as before. Character sheet, the three bosses side by side, 3/4
front view, neutral grey background. No text.
```

## Prompt 4: the pumpkin-butt bosses (the strongest versions)

```text
Now the strongest version of each boss, in the same style. Each one carries a big carved jack-o'-lantern on its back
like a hermit crab's shell, with a pale skull peeking out of the carved opening and a red gem on the skull's brow:
- the spider's whole back body is inside the big pumpkin shell;
- the cockroach wears a bone skull mask over its face, and its lids are made of carved pumpkin rind;
- the bee's back body is replaced by the pumpkin and it wears the skull mask on its face.
Keep their legs, heads and wings as in your earlier designs. Character sheet, side by side, 3/4 front view, neutral
grey background. No text.
```

## Optional: the 3 random model variants of each bug

```text
Make three small variations of each pumpkin bug for variety in a swarm (the game picks one of three at random): a
different pumpkin shape (round, tall, squat with deep ribs), different carved face or eye pattern, slightly different
stem and leaf details. Same body plan, same size, same colours. Lineup sheet, 3/4 front view, neutral grey background,
no text.
```

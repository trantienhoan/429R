# Bedroom remake: concept prompts (2026-11-16)

**Result:** `concept_v1.png` = the Gemini concept from Prompt 1 that the user picked (2026-11-16, saved at the size
it was pasted, 1024 x 572).

`Bedroom_now_sheet.jpg` = today's bedroom (brightened a little so it reads; the originals are in `screenshots/`):
- top row: the full bedroom stage (BR_Stuffs), from a high corner and from the middle;
- middle row: the same room at eye level;
- bottom row: the earlier bedroom stage (Mini Stuffs: shelves of boxes, the witch-hat table, the pumpkin painting).

`screenshots/` also has the empty room (`b_bedroom_*`) and lists of what is in each stage (`*_contents.txt`).
The fog was switched off for these pictures only.

**How to use in Gemini:** attach `Bedroom_now_sheet.jpg`, paste Prompt 1. When you like a design, ask for Prompt 2,
3 and 4 "of this same design". The look is the soft toy-diorama style you liked on the rooftop concept
(see `../Rooftop_Concept/TEXTURE_STYLE.md`).

What has to stay (the game needs it):
- one square room, about 9 x 9 m, ceiling about 3 m, flat floor (teleport);
- an open middle for fights; furniture goes along the walls and in the corners;
- the purple light beam in the middle where pumpkins and monsters appear;
- the witch-hat table (3 hats) and the coffer;
- 3 doors and 1 window;
- lots of breakables: about 100 cardboard boxes, 12 candy baskets, pumpkins, bottles, books, wooden weapon crates;
- beds, shelves, a work table and chairs, a wardrobe (cover and things to smash);
- room for the Fairy, the pumpkin music player, a clock, banners and pictures.

---

## Prompt 1: the whole room as a cut-away (key art)

```text
The attached screenshots are today's version of a level in my VR game. Keep its layout (one square bedroom with a
flat wooden floor, an open middle, furniture along the walls, a purple magic light beam in the centre) but redesign
the look to be much richer, cosier and more atmospheric.

Concept art for a VR action-game arena: "The Haunted Bedroom". A dollhouse cut-away view from high up at three-quarter
angle: the two front walls and the ceiling are removed so we look into one square room, about 9 x 9 metres with a
3 metre ceiling, inside a crooked Halloween haunted house at night.

The room is part bedroom, part forgotten storage room. Along the walls: two or three wooden beds with patchwork quilts,
tall wooden shelves crammed with cardboard boxes, books, candle stubs and glass bottles, a wardrobe with a crooked
door, a work desk with a chair and a fat candle, a carved pumpkin music box, a grandfather wall clock, a heavy
treasure coffer, stacks of cardboard boxes and wooden crates in the corners, wicker candy baskets full of sweets,
little piles of pumpkins. Walls: plum-purple damask wallpaper above dark wooden wainscoting. Floor: warm dark wooden
planks with a round rug. Three wooden doors (one chained shut with a padlock), one four-pane window showing a big
orange moon. Halloween bunting in orange, black and purple across the ceiling beams, a few cobwebs in the corners,
two green clover banners and a painting of a grumpy pumpkin.

In the exact middle the floor stays open and empty: a glowing purple rune circle with a soft beam of purple light
rising from it to the ceiling, tiny glowing skulls floating inside it. Next to it, a small wooden table holding three
witch hats (black, blue, green) under a magic glow.

Style: stylised low-poly game art like a soft toy diorama: chunky shapes with bevelled rounded edges, flat colours
with soft painted gradients, almost no texture noise, soft dark contact shadows where things meet, polished indie VR
game, not photo-real. Lighting: warm pools of candle and lamp light against cool blue-violet moonlight from the
window, purple glow from the magic beam. Cosy-creepy, not gory. No text, no logos, no UI.
```

## Prompt 2: the room at eye level

```text
Same bedroom design as before, now from the player's eyes (1.6 m tall) standing in a doorway, looking into the room
toward the purple light beam in the middle. Wide VR field of view. The open floor in front of the player is clear for
fighting; the clutter is along the walls and in the corners. Warm candle pools on the desk and shelves, moonlight
through the window, bunting overhead, the witch-hat table glowing beside the beam. Same soft toy-diorama look, no
text.
```

## Prompt 3: the room mid-fight

```text
Same bedroom design, now mid-battle, from the player's eyes near the middle of the room. Angry carved pumpkins are
hopping out of the purple light beam, big cartoon spiders hang from cobwebs in the corners and a cockroach scuttles
across the floor. Cardboard boxes have burst open, a crate is smashed into planks, candies and pumpkin chunks are
scattered over the floorboards, a bottle is flying through the air. The candles flicker, the purple beam flares.
Exciting but fun, not gory. Same soft toy-diorama look, no text.
```

## Prompt 4: bedroom props as a model sheet

```text
Game prop concept sheet, stylised low-poly, Halloween VR action game, "The Haunted Bedroom". A neat grid of 24
separate props on a plain dark grey background, three-quarter view, same lighting, chunky silhouettes with bevelled
rounded edges, flat colours with soft painted gradients, each simple enough for under 2,000 triangles. Palette: warm
dark wood, plum-purple fabric, pumpkin orange, candle yellow, purple magic glow.
Row 1: wooden bed with a patchwork quilt; bed stool; tall shelf full of boxes; small hanging wall shelf; wardrobe with
a crooked door; treasure coffer.
Row 2: work desk; wooden chair; armchair; small side table; carved pumpkin music box; grandfather wall clock.
Row 3: cardboard boxes in 5 sizes (one burst open); wooden weapon crate; wicker candy basket with sweets; stack of
old books; three glass bottles; skull with a candle.
Row 4: fat candle on a dish; witch-hat table with three hats; wooden door (normal and chained shut with a padlock);
four-pane window with a moon; Halloween bunting strip; framed painting of a grumpy pumpkin.
No text, no labels, no logos.
```

## Matching textures (optional)

Tile prompts to paint over, in the same style as the rooftop ones (`../Rooftop_Concept/TEXTURE_STYLE.md`):

```text
Seamless tileable texture, flat front view, stylised hand-painted game texture for a low-poly VR game: plum-purple
damask wallpaper with a soft lighter pattern, smooth flat colour with very soft painted blotches, no photo detail, no
strong lighting or shadows baked in. Square, edges tile perfectly.
```
Swap the subject for the other surfaces:
- "warm dark wooden floor planks with simple painted grain lines and soft dark gaps";
- "dark wooden wall panelling (wainscoting) with rounded edges";
- "worn patchwork quilt fabric in plum, orange and green";
- "cardboard, plain, soft tan with a lighter top edge".

---

**Tips**
- Too dark: "brighter, more warm candle light and moonlight". Too busy: "fewer, bigger props, more open floor".
- If Gemini fills the middle: "keep the middle of the floor empty around the purple beam, furniture only along the
  walls".
- If it makes a second floor or stairs: "one flat floor, one room, no stairs or balconies".

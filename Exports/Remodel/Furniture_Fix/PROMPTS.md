# Bedroom furniture remake: Gemini prompts (2026-11-20)

The 15 breakable furniture pieces of the storage room and the bedroom (`Bedroom_Furniture_Fix.blend`), to be redesigned
in the look of the bedroom concept (`../../Bedroom_Concept/concept_v1.png`), then modelled in the .blend and put back
in the game (Claude re-imports them; see `../../../Assets/NOTES_Next_Time.md`, "FURNITURE FIX FILE").

**Attach to Gemini (both):**
- `Furniture_now_sheet.jpg` = today's 15 pieces, numbered, with their real sizes; on the right of each, its broken
  pieces pulled apart (pieces 8, 9, 13, 14 have none of their own).
- `../../Bedroom_Concept/concept_v1.png` = the look to match (the concept you picked for the bedroom).

Paste Prompt 1. When you like it, ask for Prompt 2 and 3 "of this same design". Prompt 4 is for one piece at a time,
when you're about to model it.

**What has to stay (the game needs it):**
- each piece about the same size (the numbers on the sheet; a little smaller or bigger is fine, not much);
- flat tops where things are put: shelves hold cardboard boxes and bottles, tables and the coffer hold candles and
  props, the hanging shelves hold candles, the stool holds a candle pumpkin;
- simple, chunky shapes (a VR game on Quest: each piece under about 2,000 triangles);
- the broken pieces are the same piece cut into a few big chunks (planks, panels, legs), not splinters.

---

## Prompt 1: the 15 pieces redesigned (model sheet)

```text
The first attached image is a sheet of 15 furniture models from my VR game, numbered 1-15, each with its size in
metres. The second attached image is the concept art of the room they live in. Redesign all 15 pieces to match the
concept art's look, keeping each one the same kind of furniture, about the same size and proportions, and the same
number.

Game prop concept sheet, stylised low-poly, Halloween VR action game, "The Haunted Bedroom". A neat grid of the 15
numbered pieces on a plain dark grey background, three-quarter view, same soft lighting for all, same scale between
them (the shelves tall, the stool small).
Style exactly like the concept art: soft toy-diorama look, chunky silhouettes with bevelled rounded edges that catch
the light, flat colours with soft painted gradients, almost no texture noise, soft dark contact shadows where parts
meet, warm dark wood with a slightly crooked, hand-made, spooky-cosy feel (a little lean here and there, iron corner
straps and nails), plum-purple fabric, a few pumpkin-orange and candle-yellow accents. Each simple enough for under
2,000 triangles. Not photo-real.
1 wooden bed with a patchwork quilt and pillow (1.3 x 1.8 m, 1.2 m tall headboard).
2 small bedside stool (0.5 m, 0.4 m tall), flat top.
3 low wooden treasure coffer (1.15 m long, 0.6 m tall), flat lid.
4 tall closed wooden cupboard / wardrobe (0.7 x 1.3 m, 2.3 m tall).
5 tall dresser-shelf: a cabinet below, an open upper part (0.7 x 1.1 m, 2 m tall).
6 open shelf rack with 4 flat shelves, no back (0.4 x 1.1 m, 1.2 m tall).
7 work desk with a drawer column on one side (1.35 x 0.9 m, 0.7 m tall), flat top.
8 small ladder-back wooden chair (0.7 m tall).
9 taller ladder-back wooden chair (1 m tall).
10 small table with a lower shelf (0.6 x 1.2 m, 0.6 m tall), flat top.
11 square wooden candy basket, open top (0.55 m, 0.45 m tall).
12 wooden crate with X braces (0.57 m cube-ish).
13 long wall shelf on two brackets (1.1 m long).
14 short wall shelf on two brackets (0.75 m long).
15 round wall clock with roman numerals (0.5 m across, thin).
Number each piece with a small digit under it. No other text, no logos.
```

## Prompt 2: the same pieces broken (how each one breaks)

```text
Same 15 furniture pieces, same design and numbering as before. Now show each one smashed: an exploded view of the
piece cut into 4 to 10 big chunky parts (whole planks, side panels, legs, the lid, the drawer, the clock face in 3-4
wedge-shaped pieces), floating slightly apart in the shape of the piece, so it is clear which part is which. Clean
chunky breaks with a few jagged plank ends showing pale inner wood, no tiny splinters or dust. Same soft toy-diorama
style, same grid and lighting, dark grey background. No text except the small numbers.
```

## Prompt 3: the bedroom with the new furniture

```text
Same bedroom concept art as the second attached image, but with exactly these 15 new furniture pieces placed along
the walls: the bed and the stool by the window, the tall cupboard and the dresser-shelf against the walls with
cardboard boxes and bottles on their shelves, the open rack full of boxes, the desk with a candle and its chair, the
coffer and the candy basket near the purple beam, the crates in a corner, the two wall shelves with candles, the clock
on the wall. Same high three-quarter cut-away view, same lighting and soft toy-diorama style. No text.
```

## Prompt 4: one piece, to model from (front / side / top)

Swap in the piece's number and name from the sheet.

```text
Piece number 5 (the dresser-shelf) from the design before, as a modelling reference sheet: front view, side view, back
view and top view, flat orthographic, no perspective, same scale in all four, plain light grey background, a soft even
light so the shapes read. Then, in a second row, its broken version in the same four views, parts slightly apart.
Same chunky bevelled toy-diorama style. Size: 0.7 x 1.1 m, 2 m tall. No text.
```

---

**Tips**
- Too detailed or too thin: "chunkier, fewer and thicker parts, bigger bevels, simpler".
- Too cute/clean: "a bit more spooky: crooked, worn edges, iron straps, a few nails, darker wood".
- It changes the kind of piece (a shelf becomes a dresser): "keep piece N the same kind of furniture as in the first
  image, only restyle it".
- It drops pieces or numbers: ask for one row at a time ("pieces 1 to 5", "6 to 10", "11 to 15").
- Save the one you pick next to this file as `concept_v1.png` (and `concept_broken_v1.png` for Prompt 2).

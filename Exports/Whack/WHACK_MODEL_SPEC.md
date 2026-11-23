# Whack-a-Pumpkin: models to make (Special Stage Type 4)

The game already works with **stand-in** models, so you can model at your own pace. Pictures of the stand-in:
`standin_34.png`, `standin_top.png` and `standin_back.png` in this folder. Once your models are in, I'll swap them in.

**START FILE (2026-11-23): `Exports/Whack/Whack_Machine.blend`.** It holds the machine in this exact layout, already
on the old textures (Wood_Furniture_Mat, Wood_Boards_Mat, Bedroom_Metal_Mat), with all the empties below, plus a new
wooden mallet and a Reference collection (a pumpkin head popped up, mallets on their posts, a 1.7 m player).
- **The axes:** in that file the player stands at the origin looking along **+Y**, so the Front view (numpad 1) is
  the player's view, with Z up. The sizes below are written in Unity axes (+Z = ahead, Y = up). In Blender, read
  Unity z as Blender y, and Unity y as Blender z.
- **Inside the file:** the README text explains it all. When you're done, run the **export_fbx.py** text (Text
  editor > Run Script). It writes both FBX files.
- **Pictures:** `blend_34.png`, `blend_player_view.png`, `blend_top.png` and `blend_mallet.png`.

All sizes are in metres. In Blender: 1 unit = 1 m, Z up. In Unity: +Z = away from the player, +Y = up.

## 1. The machine (wood + metal)

- **Origin / pivot:** the spot where the player stands, on the floor (0, 0, 0). The player looks along +Z, into the
  half-ring. The open side of the ring is behind the player.
- **Shape:** a half-ring table round the player (it can be any style: barrel staves, crate boards, iron bands...).
  - Inner edge about 0.42 m from the origin (the player's belly room). Outer edge about 1.12 m.
  - It wraps 90 deg to each side of straight ahead (180 deg in all), so the end holes clear the end walls.
  - **Table top at 0.82 m high.**
- **20 holes,** each about 0.17 m across (a 0.15 m pumpkin head hides in each). The table has to be **closed underneath
  each hole, at least 0.3 m deep**: the heads wait 0.24 m below the top, and nobody may see them through the side.
- **Hole centres** (top surface, machine space). Two rows (re-arranged 2026-11-23 to fit your metal bands: wood from 0.49 to 1.06 m, end bands at +-87 deg): 8 near ones at 0.645 m (+-10/30/50/70 deg) and 12 far ones at 0.925 m, evenly from -79.5 to +79.5 deg.
  Please put an **empty named Hole_00 ... Hole_19** at each hole centre. Then you can move a hole and the game follows.

| Empty | Row | x | y | z |
|---|---|---|---|---|
| Hole_00 | near | -0.606 | 0.820 | +0.221 |
| Hole_01 | near | -0.494 | 0.820 | +0.415 |
| Hole_02 | near | -0.322 | 0.820 | +0.559 |
| Hole_03 | near | -0.112 | 0.820 | +0.635 |
| Hole_04 | near | +0.112 | 0.820 | +0.635 |
| Hole_05 | near | +0.322 | 0.820 | +0.559 |
| Hole_06 | near | +0.494 | 0.820 | +0.415 |
| Hole_07 | near | +0.606 | 0.820 | +0.221 |
| Hole_08 | far | -0.910 | 0.820 | +0.169 |
| Hole_09 | far | -0.839 | 0.820 | +0.390 |
| Hole_10 | far | -0.715 | 0.820 | +0.587 |
| Hole_11 | far | -0.545 | 0.820 | +0.747 |
| Hole_12 | far | -0.342 | 0.820 | +0.860 |
| Hole_13 | far | -0.116 | 0.820 | +0.918 |
| Hole_14 | far | +0.116 | 0.820 | +0.918 |
| Hole_15 | far | +0.342 | 0.820 | +0.860 |
| Hole_16 | far | +0.545 | 0.820 | +0.747 |
| Hole_17 | far | +0.715 | 0.820 | +0.587 |
| Hole_18 | far | +0.839 | 0.820 | +0.390 |
| Hole_19 | far | +0.910 | 0.820 | +0.169 |

- **Two mallet posts,** one each side, just behind the ends of the table. The post tops are at (-0.55, 0.97, -0.15) and
  (+0.55, 0.97, -0.15). A mallet stands on each, handle down. Empties **Post_L / Post_R** at the post tops, please.
- **The scoreboard,** behind the far row, facing the player:
  - The panel is about 1.24 wide x 0.84 high, its face centred at (0, 1.55, 1.40). It has a flat front for the text.
  - The text shows: the title, the time (big), messages (GRAB BOTH MALLETS! / GO! / TIME'S UP!), CANDIES, COMBO and BEST.
  - Empty **Board** at the centre of the panel's front face, please. Empty **Candy_Spout** above it (now at
    (0, 2.10, 1.25)): the candies won pour out of there at the end.
- **Materials:** the stand-in uses your PlayGround ones:
  - `Assets/Materials/PlayGround/PG_Wood_Furniture_Mat` for the table top and post caps.
  - `PG_Wood_Boards_Mat` for the sides and the board.
  - `PG_Metal_Mat` for the rim, hole rims, posts and board frame.
  - Any of your old wood and metal textures is fine. Tell me which file and material you used.
- The machine rises out of the floor when the chest opens and sinks back at the end, so it needs nothing under the
  floor.

## 2. Two mallets (wood)

- A wooden mallet about **0.45-0.5 m long**: a handle about 0.35 m, and a head about 0.13-0.15 m long and about
  0.09-0.10 m across.
- **Pivot:** the middle of the grip, where the hand holds it, about 8 cm from the handle's end.
  **Handle along +Y**, with the head at the top. The head's two striking faces point along +-Z (or +-X, just tell me).
  In the .blend: handle along +Z, faces along +-Y, and the child empty Mallet_Strike_Point in the middle of the head.
- One model is enough: the game makes two.
- It may weigh nothing special: let go, it floats a moment, then flies back to its post.

## 3. Pumpkin heads (no model needed)

The heads reuse `pumpkin_0` (with its carved face) at 0.15 m wide:
- **Normal:** its own material.
- **Gold:** the revive coin's gold.
- **Rainbow:** cycles colours.
- **Clock:** green, with a small clock face.
- **Skull:** the `Skull` model.

If you'd like a different head, or a proper clock-pumpkin, model it about 0.15 m wide with the pivot at the bottom
middle.

## Export

FBX into `Assets/Models/Bonus/Whack/`: `Whack_Machine.fbx` (with the empties) and `Whack_Mallet.fbx`. Then tell me, and
I'll hook them into **Tools > 429 Game > Bonus > Set Up Whack-a-Pumpkin** and test again.

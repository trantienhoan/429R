# Bedroom_Walls.blend check: models for the PlayGround (2026-11-23, gate 2 session, read-only)

The user asked: "check Bedroom_Walls.blend. I want to reuse my model in there for the Playground Kit."
Nothing was changed: `Assets/Blender/Bedroom_Walls.blend` (43.1 MB, saved by the user 11:14) was only opened headless.

- The file: 1,139 objects (1,082 meshes, 23 armatures), 57 materials, 139 images; the whole house set
  (bedroom, living room, rooftop, walls, enemies' rigs, foliage).
- Picture: `bedroom_models_sheet.jpg` (and the .png) = 65 models that could suit the PlayGround, each fitted in
  its box, with real size (m) and triangles. List with the Unity files: `bedroom_models.json`.
  - Frame colours: green = a prefab already in Unity (30), yellow = a model (FBX) in Unity but no prefab (18), grey =
    only in the .blend (17).
  - The 17 grey ones are Small_Tree_01_01, Big_Leave_01_02, Tree_Of_Light.001, Bush_01, Bush_03, Pot_Plant_05.002,
    Circle (parasol), pumpkin.001, pumpkin_fat_upper, Skull_Mask, Out_Door_Chair, Dino_Rex_Bigger, Tank_03,
    Ghost_Boy, Sleigh, XMas_Toy_05 and Bedroom_Yoyo.
  - Of the yellow ones, the two rooftop gates are part of Rooftop_Base_Z.
- Looks: the house models wear their own painted textures (the Bedroom_Furniture_Set1/Set2 atlases, Wood_Furniture,
  Bedroom_Metal ...). The PlayGround kit uses ONE palette material.
  - PlayGroundKitMenu's Build PlayGround Kit gives a NEW piece's renderers PlayGround_Kit_Mat. FBX are imported with
    materials None, and the export strips textures.
  - So a house model sent through PlayGround_Kit.blend comes in with palette colours until its own material is set
    by hand. The builder keeps a hand-set material after that.
- In the sheet, some models show white or grey:
  - the trees and leaves use vertex colours (VCOL materials);
  - Bush_01/03, Pot_Plant_05, Circle, Out_Door_Chair, Stool and XMas_Toy_05 have no material in the .blend;
  - pumpkin.001 is magenta because its "One For All" texture is missing.
- Heavy ones: Tree_Of_Light 7,338 tris, Pumpkin_Mob 6,718, Pumpkin_Tree 5,891, Dino_Rex 4,966, Tank_03 4,218
  (the kit pieces have 44-1,264).
- The simplest route:
  - Green and yellow models: no Blender step. Drag the FBX (or the prefab, when it should act like it does in the
    house: break, drop candy, light) into Play_Ground_Z in Unity. They keep their textures.
  - Grey models: one headless export each to `Assets/Models/PlayGround/<name>.fbx`, with their own materials, like
    Big_Tree_01_01 was. The .blend is not saved.
- Scripts and thumbnails: `K:/WORK_2026/08_04/_claude/gate2_2026-11-23/bedroom_check/` (list_bedroom.py, thumbs.py,
  compose.py).

## In Unity (2026-11-23 ~11:58, user: "Do it here"): 68 House pieces, looks only, nothing placed

- **Prefabs:** `Assets/Prefabs/PlayGround/House_Pieces/House_<name>.prefab` (68).
  - The root is an empty at the middle of the piece's foot. The model sits under it as an FBX instance, lifted so its
    lowest point is at y 0, so a dropped piece stands on the ground.
  - No colliders and no scripts.
  - Names and paths are fixed: the gate 1 session uses them in Play_Ground_Z/Bedroom_Models. The build script no
    longer deletes or remakes a piece (REBUILD = false).
- **The models:**
  - 43 pieces reuse the game's existing FBX (Assets/Models/Bedroom_Stuffs, Livingroom_Stuffs, Foliage_Set_1,
    Enemies ...) with the game's materials. Two of them, pumpkin_bucket and The_Cage, had embedded materials;
    their pieces wear Bedroom_Set_1_Mat.
  - 25 are new exports from Bedroom_Walls.blend: `Assets/Models/PlayGround/House/House_<name>.fbx`. The tool is
    `K:/WORK_2026/08_04/_claude/gate2_2026-11-23/bedroom_check/export_house.py`, run with
    `blender -b Assets/Blender/Bedroom_Walls.blend --python export_house.py -- <out dir>`; it never saves the .blend.
    - Each export keeps the model's current shape and pose, with its object rotation and scale baked in and its
      pivot at the middle of its foot.
    - The new exports are Big_Tree_01_01 (8.4 m), Small_Tree_01_01, Big_Leave_01_01-04, Magical_Tree_Big (4.7 m),
      Tree_Of_Light, Bush_01/03, Pot_Plant_05, Parasol (was "Circle"), Pumpkin_One_For_All, pumpkin_fat, Skull_Mask,
      Out_Door_Chair, Stool, Dino_Rex, Tank_03, Ghost_Boy, Sleigh, XMas_Toy_05, Yoyo, Round_Gate and Rooftop_Gate.
  - The FBX importers remap materials by name to the game's: Bedroom_Furniture_Set1 -> Bedroom_Set_1_Mat,
    Bedroom_Wood_Floor -> Wood_Floor_Mat, Soil_Surface -> Rock_Floor_Mat, Rooftop_Wall -> Rooftop_Wall_Mat, and so on.
- **New materials:** `Assets/M_Textures/PlayGround/House/`.
  - `House_Vertex_Colour_Mat` (URP Particles/Simple Lit, which shows vertex colours) for the trees and leaves
    (VCOL.001/002), Tree_Of_Light and Pot_Plant_05.
  - `House_Plain_Mat` (grey 0.75) for the models that had no material: Bush_01/03, Parasol, Out_Door_Chair, Stool
    and XMas_Toy_05.
  - `House_Pumpkin_Orange_Mat`: the "One For All" palette is missing on this PC
    (D:\Model 2\Graveyard\Enviroment\256 palette.png).
  - Toy materials with their colour maps copied from G:\New Model (Tank_03's came out of Tank_03_textures.rar) and
    scaled to 1024. No normal maps.
- **Picture:** `house_pieces_in_unity.jpg`, each piece rendered in Unity with its real materials and its size.
  Checked: 68 pieces, no missing or embedded materials, every foot at y 0, open scene not dirty.
- **Known looks:**
  - House_Pumpkin_Tree is the game's skinned model and shows as a dark blob without its rig. Use
    Prefabs/Items/Breakable/Pumpkin_Tree.
  - House_Tree_Of_Light is thin one-sided swirls and renders dark from the front-right.
  - House_Spider_Web and House_turn_back_sign are near invisible from that angle, the same as in Blender.
- Unity scripts: `u_build.cs` (from u_build_head/tail.cs.txt + pieces_cs.txt via make_u_build.py), `u_render.cs`.
- In use (gate 1, ~12:10): Play_Ground_Z/Bedroom_Models places House_Tank_03, House_Dino_Rex, House_Sleigh and
  House_Magical_Tree_Big. Box colliders and the Wall layer are on the placed copies only. Keep these names and paths.
  Gate 1 Play-tested to Whack 6: PASS.

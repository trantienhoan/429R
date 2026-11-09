# Steam art assets

## Logo_429.blend (the "429" title logo, v2)

Rebuild from scratch (about 30 s, overwrites the file):

    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Exports/Steam_Art/assets/logo_build.py
    ... -- render=test.png        also render a test into logo_tests/ (options are listed at the top of the script)

**How to append**
- Append the collection **`Logo_429_v2`**. Its parent empty is **`Logo_429_Root`**; move, turn and scale only that.
- **Facing: the front faces -Y.** A viewer at -Y looking toward +Y reads it. Z is up and the letters run along +X.
- **Size:** 2.0 units wide (x -1.0 to 1.0), about 0.88 tall (lettering z -0.44 to 0.44, centred on the root), 0.30 deep gold walls. The green fire card behind it is bigger (it is skipped when the scene measures the lettering width).
- **Origin:** the root sits at the middle of the lettering, with no rotation and scale 1.
- **Same as the old logo:** it faces -Y and is about 2 wide, like `Logo_429_Mesh`. As a camera child, use location (x, y, -1), rotation (-90, 0, 0) and scale about 0.14.
- **Objects** (all parented to the root):
  - `Logo_429_Digit_4`, `_2`, `_9`
  - `Logo_429_Backplate` (old style D only; style E has none)
  - `Logo_429_Fire` (the green fire card behind the digits)
  - The pumpkin top on the 9 (`Logo_429_Stem`, `_Leaf`, `_Leaf_Rib`, `_Vine`) is **off by default since r3 (2026-11-08)**, the user asked for just "429". `-- stem=1` (or `SHOW_STEM` at the top of logo_build.py) brings it back. The old file with the stem: `variants/Logo_429_v3_stem_backup.blend`.
- **Reflector cards:** the sub-collection `Logo_429_v2_Reflectors` holds 3 cards that show only in reflections. The camera never sees them, and they don't light or shadow anything. They travel with the logo so the gold always has something warm to mirror. Delete the sub-collection if the scene has its own reflectors.
- **Materials:**
  - `Logo_Gold_v2`: polished, slightly worn gold.
  - `Logo_Green_v2`: candy enamel. The back plate uses it in a darker look, through the object property `logo_plate`. The edge glow comes from the vertex attribute `rim`.
  - `Logo_Glow_v2`: the green groove line, emissive with a slight flicker.
  - `Logo_Reflector_*_v2`: the reflector cards' materials.
- **Test rig:** the collection `Logo_Test_Rig` (camera + 5 area lights) is excluded from the view layer. Do not append it.
- **Library logo:** the logo works on a transparent background; there is no glow card. The back plate is solid, so it reads on any background.

## Menace_Pumpkin.blend (the hero + small pumpkins, v4)

Your game pumpkin (`pumpkin_0` from `Assets/Blender/Knight_429.blend`, only read) with only the eyes, nose and mouth recarved to look menacing: dark sockets with glowing yellow almond pupils, sharp nose, wide fanged grin. Textures are copies of the game atlas (in `menace_pumpkin/`, also packed in the .blend).

    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Exports/Steam_Art/assets/menace_pumpkin_build.py
    ... -- style=lantern out=menace_pumpkin/Menace_Pumpkin_Lantern.blend     (the other look: hollow, cut right through)

- Collections `Menace_Pumpkin` (root `Menace_Pumpkin_Root`, `_Body`, `_Inner_Light`) and `Menace_Pumpkin_Small` (same mesh, own light).
- Same frame and size as pumpkin_0 (face toward local -Y, root at the bottom centre): give the root pumpkin_0's transform and it sits exactly where the game pumpkin did. `assemble_v2.py` does this.
- Glowing materials `MP_Pumpkin_Pupil`, `MP_Pumpkin_Mouth_Glow`, `MP_Pumpkin_Socket` each have a "Glow" value node (lighting_pass.py sets them).
- Tests: `menace_pumpkin/tests/` (`final_Compare.png` = game pumpkin vs menace).

---
name: blender-429r
description: Blender work for the 429R VR game ("Four Twenty Knight - 429"). Covers painting or repainting textures on the game's models (enemies like the cockroach, spider and bee, props, rooms) directly on their existing UVs. Also covers fixing parts that look unpainted, too shiny, the wrong colour or flat; normal maps; checking how variants share a texture; previewing; and Blender animations and FBX re-exports for Unity. Use it whenever the user mentions Blender, a .blend in Assets/Blender, a texture or atlas PNG under Assets/M_Textures, UVs, repainting a model or part of one, a normal map, or a Blender animation for this project. That includes requests that don't name Blender, like "the underwing looks unpainted", "make the spider rustier" or "the bee's stripes are too orange".
---

# Blender work for 429R

The user (a PlayMaker developer, not a 3D programmer) models in Blender and hands over texturing, fixes and
animation. Work happens either live in their open Blender through the Blender MCP (`execute_blender_code`, `look`),
or headless (`G:/steam/steamapps/common/Blender/blender.exe -b <file.blend> --python <script.py>`). Explain
results in plain words and show pictures, not code.

## Ground rules (the user set these; each cost a redo when missed)

- **Paint on the existing UVs; never move, re-unwrap or re-pack them unless asked.** The UVs are a deliberately
  shared atlas: enemy variants overlap completely and left/right halves are often stacked. Re-packing once got a
  firm "do not change the UV". If a job truly needs new UVs (new geometry), give only the NEW parts UVs, packed into
  free space with the old islands pinned (`uv.pack_islands(pin=True, pin_method='LOCKED')`), and check that the old
  ones didn't move.
- **One texture per model family.** All objects paint into the one atlas their material already uses (find it with
  `objects_using(image)`), not a new texture per object.
- **Style:** flat colours with soft painted gradients, matching the existing textures. Sample colours from the
  current texture or the user's reference image. Gentle procedural variation (value noise, veins, plates), not
  photo detail.
- **Don't save the .blend.** The user often has unsaved work open (frequently in Edit Mode with faces hidden).
  Write only the image files (`image.save()`), and back up each PNG before overwriting it (`backup()`).
- **Don't read or comment on .blend files unless the job is about one** (they're source art; the user asked once
  not to worry about them).
- Respect the user's own keys and poses in animations; complete or retime them rather than replacing them.

## Environment facts

- Blender 5.2 LTS (Steam), path above. No system Python; numpy is available inside Blender.
- The Claude app is an MSIX package: files it writes under `%APPDATA%` / `AppData\Local` (including the session
  scratchpad) are invisible to Blender and Unity. Put scripts, test copies and backups under
  `K:/WORK_2026/08_04/_claude/` (the job folder; backups in `_claude/backups/`).
- **Edit Mode:** `ob.data` UV data reads as empty while a mesh is in Edit Mode. Read through
  `bmesh.from_edit_mesh` (the skill's `mesh_of()` does this), or switch to Object Mode and back. Hidden faces are
  still in the bmesh. For previews of a hidden or tweaked model, use a temporary copy.
- **NLA:** the user stashes every action as a strip in an "[Action Stash]"-style track and is often in NLA tweak
  mode (the action is read-only and the scene shows the tweaked action). Preview another action on a temporary copy
  of the armature and mesh; keep strip lengths in sync with action ranges.
- **`look` tips:** to frame one part (a head, a hand), add a temporary mesh with just two loose vertices spanning it
  and target that (invisible, but has bounds). An Empty target has no size and the far clip slices the model. To
  tell a model's loose parts apart, put a per-part colour attribute on a temporary COPY with a temporary material
  showing it (solid shading in `look` ignores colour attributes). Delete every `_claude*` object, mesh, material and
  image afterwards.
- **New, unpainted model** (no material yet): create the texture under `Assets/M_Textures/<Model>/` and a material
  showing it on the object, then tell the user to save the .blend. Name regions from the loose parts plus, for big
  merged pieces, their UV islands sorted by height. Some parts may have flipped normals, so base patterns on
  position.
- If the MCP says "Not connected to Blender", the user has Blender closed. Work headless on the file instead (open
  it read-only by never saving), or ask them to open it if the job needs their unsaved state.

## Paint job workflow

1. **Look first.** `look` the model (material shading, the side in question; `views=[[0,0.3,-1]]` is from below).
   If the user selected faces, read the selection. Check the Unity material too (step 7): a part can look wrong
   because of the material, not the paint (the cockroach material is Metallic 1 with emission = the atlas × 0.37,
   so any bright flat patch glows).
2. **Check the sharing.** Run `scripts/check_uvs.py` (set `IMAGE` first). It lists every mesh using the texture,
   the stacked islands and which variants share texels. Painting a texel changes it on every face and variant that
   uses it, so decide what's being painted with that in mind (mirror halves together, variants together).
3. **Pick the faces** with a filter: `selected_faces`, `faces_in_group("wing_lid.L", "wing_lid.R")`,
   `faces_with_material(...)`, or your own `(ob, bm, f) -> bool`. Include both mirror halves when they're stacked.
4. **Bake the layout:** `lay = bake_layout(objs, W, H, flt)` gives per texel the object-space position and normal.
   `others = bake_layout(objs, W, H, invert(flt))` gives the texels other faces use. Paint only
   `lay.mask & ~others.mask` unless the user wants the shared parts changed too, and report the counts.
5. **Paint from position, not from UV.** Colour = function of `lay.pos` / `lay.nrm` (e.g. gradient along the part's
   length, veins fanning from a hinge, `fbm` mottling). Position-based patterns stay continuous across UV seams and
   identical on stacked halves. `abs(x)` makes a pattern mirror-symmetric.
   - **Classify by face normal on low-poly:** `bake_layout(..., smooth_normals=False)` whenever a normal decides a
     region, e.g. "the top and bottom edge strips are darker". Smoothed normals blend across hard edges, and the
     colour leaks into the neighbouring faces in an arc. That happened on Chopper's axe horn tips and the user
     rejected it. Smooth normals are fine for soft shading.
   - **Bands along an edge** (blade bevel, rim, trim): measure the distance to the actual edge polyline in the part's
     plane, e.g. the sharp vertices walked end to end. Measuring straight across one axis bends the band where the
     edge curves.
6. **Finish the image:** `dilate(col, paint, 6, blocked=lay.any_face)` pads 6 texels into empty space (so mip maps
   don't bleed background into edges). Optional normal map: build a height map in the same layout and use
   `height_to_normal(height, paint, strength)` (OpenGL / Y+, what Blender and Unity's normal-map import expect).
   Write the colour into the model's existing normal map PNG, only in the painted texels. `backup()` each PNG, then
   `write_image()`.
7. **Verify, then hand over to Unity:** `look` again from the same angles. In Unity, reimport the PNGs and render
   the prefab with its real material (see `references/unity-handoff.md`). Compare with the user's complaint
   ("too orange and shiny" means check both the colour and the material).
8. **Report** in plain words: what was wrong, what changed and where (which part, all variants?), that the UVs and
   the .blend weren't touched, where the backups are, and anything material-side worth changing, with click-by-click
   steps. Offer, don't do, changes that affect the whole model.

`scripts/uvpaint.py` holds the helpers (load with
`exec(open(r"K:/Repository/429R/.claude/skills/blender-429r/scripts/uvpaint.py", encoding="utf-8").read())`):
`mesh_of/done`, `objects_using`, the face filters (`selected_faces`, `faces_in_group`, `faces_with_material`,
`any_of`, `invert`), `pixels/write_image/new_image/backup`, `bake_layout` (a `Layout` with `mask`, `obj`, `face`,
`pos`, `nrm`, `stacked`, `any_face`), `dilate`, `height_to_normal`, and pattern helpers (`hex_color`, `lerp`,
`smoothstep`, `band`, `vnoise`, `fbm`). Arrays use Blender's pixel order: row 0 = bottom (V = 0); 8-bit PNG values
are the stored sRGB numbers.

For a quick dry run, `new_image()` and write to a test PNG under `_claude/` instead of the real texture.
`scripts/selftest.py` exercises everything headless on the cockroach file (see its header for the expected
output); run it after changing `uvpaint.py`.

## Other kinds of job

- **Animation** (new or retimed clips for SpiderBrain enemies, character clips like Chopper's walk/run/chops with a
  held weapon, FBX re-export, clip ranges): read `references/animation.md`.
- **Unity side** (reimport, material checks, rendering a prefab to compare, icons, FBX import settings): read
  `references/unity-handoff.md`.
- **Something done before?** `references/past-jobs.md` indexes the scripts already written for this project's
  models (Depot paints, spider and cockroach UV sharing, paints, normal maps, every enemy animation). Start from
  the closest one rather than from scratch.

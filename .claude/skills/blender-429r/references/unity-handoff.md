# Handing Blender work over to Unity (429R)

Unity 6000.3, URP, Quest + PC. Drive the Editor through the Unity MCP (`unity-editor-mcp`: `eval`, `eval_file`,
`recompile`, `editor_play`, ...). If its tools are missing, the Unity CLI does the same from Bash:
`C:/Users/Admin/AppData/Local/Unity/bin/unity.exe command --no-banner --json --timeout 60 eval_file --file <path>`.
Never edit a scene's YAML while Unity has it open.

## Textures

- Texture PNGs live in `Assets/M_Textures/<Model>/` (e.g. `Cockroach_Smol/Cockroach_Enemy_Atlas.png`,
  `Cockroach_Enemy_Normal.png`; spiders use `Spider_Smol/Spider_Enemy_Base_Color.png`, not the `_Atlas` one).
- After writing a PNG from Blender, reimport it:
  `AssetDatabase.ImportAsset("Assets/M_Textures/...png", ImportAssetOptions.ForceUpdate);`
- A normal map must be imported as Texture Type "Normal map" (`TextureImporter.textureType == NormalMap`). Check
  it when a new one is added. Unity's max import size may be 1024 even when the PNG is 2048 (that's the importer's
  Max Size, not a problem).

## Materials: check before blaming the paint

Read the material: `get_material_properties` (MCP) or an eval printing `_Metallic`, `_Smoothness`,
`_EmissionColor`, `_EmissionMap`, the keywords.
- `Assets/Materials/Cockroach_Smol_Mat.mat` (URP Lit): Metallic 1, Smoothness 0, `_EmissionMap` = the atlas at
  `_EmissionColor` 0.37 grey. Bright flat colours in the atlas therefore glow and look metallic. Keep cockroach
  colours dark. Changing the material affects the whole cockroach and four prefabs, so offer it with click steps,
  don't just do it.
- Shared materials affect every prefab that uses them. List users before changing one.

## Rendering a prefab to compare (no scene changes)

`scripts/unity_render_prefab.cs`: copy it, set PREFAB / OUT / which renderers / bone turns / views at the top,
and run it with `eval_file`. It renders in a preview scene with its own camera and lights, so the open scene isn't
touched, and writes a PNG strip to Read back. Enemy prefabs keep variant meshes inactive (pick one in
ONLY_RENDERERS); Animators are disabled to hold the bind pose; bone turns open parts (cockroach `wing_lid.L/R`:
local Z ±60 opens the lids).

A floor in a preview scene needs a URP material (`new Material(Shader.Find("Universal Render Pipeline/Lit"))`);
the built-in Default-Material renders magenta. Particles in a preview scene only show after
`ParticleSystem.Simulate(seconds, true, true, true)`.

## Item icons

Item icons (shop/inventory) are rendered by `PrefabIconMaker` (preview scene, transparent 256 px):
**Tools > 429 Game > Inventory > Make Missing Item Icons**, or `PrefabIconMaker.MakeNow(prefab, item)`.
Re-render after a model's look changes noticeably.

## Models and FBX

- Enemy models are FBX exports of the .blend files (e.g. `Spider_Smol_&_Anims.fbx`,
  `Cockroach_Smol_&_Anims.fbx`). The user exports. After they say "exported", check the FBX file's timestamp and
  takes before wiring anything (once nothing had been written).
- A .blend imported directly exports only the active action; that's why clips come from FBX exports with all
  actions.
- Clip ranges and loop flags: set `ModelImporter.clipAnimations` by eval, then re-read them. The first set right
  after a force reimport sometimes doesn't stick, so retry.
- **Bone changes:** all spider prefabs hold an unpacked copy of the skeleton. Adding a bone (e.g. Web_Ball) made
  every spider invisible (bindpose count mismatch) until **Tools > 429 Game > Enemies > Resync Spider Skins** was
  run. Run it after any bone change in Spider_Enemy_NEW.blend; keep new bones deform ON.

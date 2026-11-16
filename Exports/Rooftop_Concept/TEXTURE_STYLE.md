# The concept's look, and how to get it in 429R (2026-11-16)

Reference: `concept_v1.png` (zooms: `concept_v1_zoom_start.png`, `concept_v1_zoom_centre.png`).

## What makes the look (it's mostly NOT texture detail)
1. **Soft, chunky shapes with bevelled edges.** Every block, crate, tile and stone has rounded edges that catch the
   light, which gives a bright rim on top edges. This is geometry, not paint.
2. **Almost no texture noise.** Surfaces are smooth: flat colour, a soft gradient (darker at the bottom, lighter at the
   top), very soft large blotches, slight colour changes from block to block. No cracks, no photo grain.
   (Today the rooftop uses a photo-like cracked rock texture: the opposite.)
3. **Soft dark contact shadows (ambient occlusion)** where things meet: under crates, between blocks, around the
   tree roots. This alone makes it look like a toy diorama.
4. **Warm light pools and glows**: lanterns, pumpkins, the tree face, gold runes, the glowing path all light up the
   stone around them in orange. A cool blue-violet moonlight covers the rest. Warm against cool is most of the mood.
5. **A few strong colours on a quiet base**: stone is cool grey-violet, wood warm brown, pumpkins saturated orange,
   magic purple and gold. Detail goes on the props, not on the floor.

## How to get it in 429R
**Models (Blender):**
- A Bevel modifier (1-2 segments, small width) on blocks, crates, stones and roof tiles.
- Chunky shapes, 1 shared atlas per family. This is your existing "flat colours + soft gradients" rule; the concept
  confirms it.

**Textures:**
- Bake AO in Blender (Cycles) and multiply it softly into the colour texture, or into vertex colours, which is even
  cheaper.
- Paint the albedo as flat colour + a vertical gradient + per-piece variation + very soft blotches. My Blender paint
  tools (skill blender-429r) already do this on your UVs. Adding the AO bake to them is straightforward.

**Unity:**
- Materials: low smoothness (0.1-0.25), no metal except iron bars; emission on lanterns, runes, the tree face and the
  path.
- Light pools: a soft additive glow disc on the floor under each lantern (looks like a point light, costs nearly nothing
  on Quest).
- A warm directional light from the sky's moon, and bloom on the emissives.
- The rooftop has to lift the house fog while it runs (as bowling, Daruma and the gate 3 valley do); in the fog
  everything past 22 m is black.

## Prompts for matching textures (Gemini), if you want tiles to paint over
```text
Seamless tileable texture, top-down orthographic, stylised hand-painted game texture for a low-poly VR game: large
soft flagstones in cool grey-violet, smooth surfaces with very soft painted blotches, slightly lighter centres, soft
dark gaps between the stones, no cracks, no photo detail, no strong lighting or shadows baked in. Square, edges tile
perfectly.
```
Swap the subject for the other surfaces:
- "dark slate roof tiles, each tile with a slightly lighter lower edge";
- "stone wall blocks, rounded edges";
- "warm brown wooden planks with simple painted grain lines";
- "rusty dark iron, simple";
- "gnarled tree bark, chunky painted stripes".

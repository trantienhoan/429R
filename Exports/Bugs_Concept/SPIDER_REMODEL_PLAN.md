# Pumpkin spider remodel: old spider vs concept (2026-11-20)

Picture: `Spider_old_vs_concept.jpg` (the old spider in the game, 3/4 and side, next to the concept spider from
`gemini_try3.png`). Measurements come from `Assets/Blender/Spider_Enemy_NEW.blend` (read only, nothing saved), at
prefab scale 1, in metres. In the game a normal spider is scaled 0.4-0.75 and bosses are bigger.

## The old spider (what we keep: the rig and its 15 clips)
- **Meshes:** 3 model variants (Spider_Smol, _2, _3), each about 900 vertices and 970 faces. One shared 2048 texture
  set (rusty brown, a bone skull marking, glowing orange tips) with normal, metallic and roughness maps. Shading is
  "Smooth by Angle" (the geometry-nodes modifier only does shading).
- **Rig (`Spider_Armature`, 57 bones, front = -Y, 60 fps):**
  - Root and 8 leg IK targets on the floor.
  - Butt: the back body. Spine, then Head: the front body.
  - 8 legs of 4 bones each (F = the short front pair, then M, M2 and B), all parented to Head.
  - Jaw.L/R + Jaw_Fang.L/R: tiny fangs under the face.
  - 8 Eye bones, and Web_Ball (the held web ball, in front and up at 0.5 m).
- **Clips:** Appear, Idle, Walk, Walk_Back, Run, Alert, Attack, Attack_2/3 (the claw slams stretch M_Up_01 to 1.75),
  Hit, Death, Dodge Left/Right/Back, Shoot_Web_Ball.
- **Sizes:**
  - Back body: 0.45 wide x 0.54 long x 0.37 tall, an egg shape behind the head.
  - Head: only 0.14 x 0.18 x 0.14, with 8 small eyes and tiny fangs.
  - Legs: thin dark-brown faceted sticks reaching 0.29 m to each side. Knees up at 0.23-0.30 m, tips on the floor.

## The concept spider (what changes)
| Part | Old | Concept | Plan |
|---|---|---|---|
| Back body (Butt) | brown egg, 0.45 x 0.54 x 0.37, rear spikes | big round carved pumpkin, about as wide, taller than long, stem on top, scorched cracks | ribbed pumpkin about 0.46 x 0.42 x 0.40 + olive stem, where the old one is; no rear spikes |
| Head (Head) | small, 0.14 wide, 8 little eyes | a big jack-o'-lantern, about 55% of the back body's width | head pumpkin about 0.24 wide (1.8x), hanging a bit forward and down like the concept |
| Face | 8 small eyes | heavy brow, 2 big slanted yellow eyes, small glowing eyes on top, nose slits, huge open mouth with 2 rows of long teeth, glowing throat, pulp drool, scorched rims | the same; the mouth is carved open and stays open (no new animation) |
| Fangs (Jaw_Fang) | tiny | none drawn, but round 3b asked for sickle fangs | two big curved fangs at the mouth corners on Jaw_Fang.L/R, so they move in every bite |
| Front legs (F) | short feelers under the face | the big head covers that spot | short thorny grabbing claws under the mouth |
| 6 walking legs (M, M2, B) | thin brown sticks | 2-3x thicker olive-green stems, backward thorns, pointed tips | tapered stem tubes on the same bones, 2-3 thorns each, hooked dark tips that end at the bone tip (so they don't sink into the floor) |
| Colours | brown + bone + orange glow | bright orange rind, scorched dark edges, olive stems, cream teeth, yellow and orange glow | one new texture with a glow (emission) map, flat colours with soft gradients like the game's pumpkins |

## What has to stay right
- **Same bone names and hierarchy:** the Unity Spider_Animator and all 15 clips keep working on the new mesh.
- **Each part weighted to the bone that moves it:**
  - the back body to Butt; the head, face, teeth and drool to Head;
  - the fangs to Jaw_Fang.L/R and the small brow eyes to Eye bones;
  - each leg piece to its own leg bone, with a small knob at every joint to hide the bend.
- **Leg lengths unchanged:** the feet stay on the floor in walk, run and dodge (the IK targets are on the floor).
  Thorns and hooks never stick out below the foot.
- **Checks after building, in a pose sheet:** Idle, Run, Alert (rearing), Attack, Attack_2 (the leg stretch), Hit,
  Death (on its back) and Shoot_Web_Ball, looking for parts going through each other or the floor.
- **Budget:** about 3,000 triangles (the old one is about 1,800), still fine for swarms on Quest.

## Later (not in this first model)
- The 3 random variants (round, tall, squat pumpkins).
- The white "ghost pumpkin" boss texture.
- A new idea for the pumpkin-butt boss: it already IS a pumpkin now, so maybe a bigger carved shell with the skull.
- **Unity, after you approve the look:** export the FBX with the same rig, swap it into the prefab's Models list,
  and set up the new material.

## Where it goes
- **File:** `Assets/Blender/Spider_Pumpkin.blend`, a new file: the old rig + all 15 clips, the new mesh, and the old
  meshes hidden in an "old reference" collection for size. The original `Spider_Enemy_NEW.blend` isn't touched.
- **Texture:** `Assets/M_Textures/Pumpkin_Spider/`.
- **Scripts:** `K:/WORK_2026/08_04/_claude/pumpkin_spider/`.

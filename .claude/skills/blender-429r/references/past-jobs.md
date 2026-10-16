# Past Blender jobs for 429R (scripts in K:/WORK_2026/08_04/_claude/)

Start from the closest script. Each has a header comment saying what it does and how it was run.

## Painting and normal maps

| Script | What it did |
|---|---|
| `paint1.py`, `paint_v3.py`, `paint_v3b.py`, `paint_v4.py`, `paint_v4b.py`, `paint_v5.py` | Depot buildings: every object painted into ONE texture on the existing UVs, from a reference image. Building faces carry an int face attribute `part` that the paint reads (v4b re-tagged parts from mesh islands after the user rebuilt the model). |
| `build_v3.py`, `paint_and_preview_v3.py` | Depot_v3 built headless from v2, then painted with Workbench previews (Standard view transform). |
| `spider_paint.py` | Shared spider atlas. Only the "owner" faces of each shared island are painted. |
| `spider_rust.py` | Rusty-brown spider with a bone skull and glowing orange: colour AND normal map from the same shapes; writes `Spider_Enemy_Base_Color.png` + `Spider_Enemy_Normal.png`. |
| `spider_skull.py` | The skull marking ("brand" of the enemies), symmetric in X. |
| `roach_paint.py`, `roach_rust.py` | Shared cockroach atlas painting (one texture for all 3 cockroaches). |
| `roach_normal.py` | Cockroach normal map from height shapes (elytra veins, seams, eye rings, leg joints, belly plates). |
| `roach_underwing.py` | Repaint of only the selected faces (underwing), variants matched by UV overlap, raised veins in the normal map, 6 px padding. The pattern `uvpaint.py` generalises. |
| `roach_tex_check.py`, `roach_cmp.py` | Texture checks / comparisons. |
| `webball_render.py` | Render one object alone (Workbench, texture colour) from 4 sides and crop its UV region from the atlas. |

## UV layout (only when the user asked for UV work)

| Script | What it did |
|---|---|
| `spider_uv.py`, `spider_share.py` | 3 spiders onto ONE shared atlas: left half mirror-stacked on the right, identical topology copied, different topology fitted per part (dominant vertex group, bbox normalised). |
| `roach_uv.py`, `roach_refit.py`, `roach_rebuild.py`, `roach_lid_copy.py` | Same for the cockroaches (user's seams kept; Cockroach_Smol_3 is the reference). |
| `roach_wing_uvmirror.py` | Right wing given the left wing's UVs (faces matched by mirrored vertex). |

## Modelling fixes

| Script | What it did |
|---|---|
| `roach_jaws.py` | Mandibles reshaped by moving vertices only (UVs, weights, topology untouched). |
| `roach_wingfix.py` | Wings that poked out of their lids pulled in. |
| `webball_remodel.py` | Web ball re-modelled keeping its UVs in the same atlas patch. |

## Animation (see animation.md)

Spider: `spider_appear/run/attack/attack2/death/walkback/alert/hit/idle/dodge/dodge_back/dodge_side/shoot_web.py`.
Cockroach: `roach_run/alert/attack2/attack2_ground/leap/bite/walkback/hit/death/dodge/idle/appear.py`, plus the
checks `roach_leap_test.py`, `roach_alert_wingtest.py`, `roach_who.py` and `roach_fill_channels.py`.
Shared analysis: `spider_common.py`, `roach_common.py`.

## Characters

| Script | What it did |
|---|---|
| `chopper/chopper_paint.py` | Chopper (Assets/Blender/Chopper.blend, object "helper", the farm helper) painted from the concept art in the .blend onto his own UVs: regions from his 23 loose parts + the body piece's UV islands by height; cap logo, chest pocket + sprout logo, stitches, cuffs, rivets, glove X stitch. Writes Assets/M_Textures/Chopper/Chopper_Atlas.png and adds material Chopper_Mat (user saves the .blend). |
| `chopper/axe_paint.py` | Chopper's axe (object "Axe", on the weapon bone) on its own UVs and its own texture Chopper_Axe.png (1024; its UVs overlap Chopper's): steel head with a bevel measured as the distance to the sharp cutting-edge polyline (y = 0 vertices walked tip to tip) and FLAT normals for the edge strips (smooth ones made dark arcs on the horn tips, user rejected), nicks, wood grain from the angle around the handle, leather criss-cross wrap, knob rings, end grain. Points the Axe material's Image Texture at it. |

## Character animation (Chopper, 2026-10-14)

All in `chopper/`; `run_all.py` rebuilds every clip, checks the hand-offs and adds the NLA strips.

| Script | What it did |
|---|---|
| `chop_rig.py` | The posing toolkit for a pure-FK humanoid rig: a `Pose` evaluated from rest matrices (`orient`/`turn`/`move` in world axes), 2-bone leg and arm IK with a solved hinge angle + pole, boot placement that rolls on the heel/toe edge, fists (`hand_shape`), the axe hung on the `weapon` bone from the hand (`set_axe`), `bake` (every bone, every frame, into a NEW slotted action) and `nla_add`. |
| `chop_base.py`, `anim_axe_common.py` | Stance, hanging arms, shrugs; the shoulder carry of the axe; `ArmKey` (wrist + elbow in the chest frame + hand turn) splined between keys; `swing_key` (arm swing angle, reach, wrist deviation, forearm roll); `grip_hand` (closes a hand on the handle, elbow searched for the wanted wrist angle). |
| `anim_idle.py`, `anim_gait.py`, `anim_chop1.py`, `anim_chop2.py`, `anim_rest.py`, `anim_wipe.py` | Chopper_Idle / _Walk / _Run / _Chop_1Hand / _Chop_2Hands / _Rest / _Wipe_Sweat. The chops solve the swing so the blade edge lands at a target height. |
| `chop_preview.py`, `render_previews.py` | Temporary copy of the armature + body + axe 8 m aside for `look`; overlap check between body parts (BVH) and the floor; MP4 previews from a temporary Workbench scene. |

## Pumpkin boss (Assets/Blender/Pumpkin_Boss_Z.blend, armature pumpkin_boss_armature, 60 fps)

| Script | What it did |
|---|---|
| `boss/throw_power.py` | 2026-10-16: the user's own `pumpkin_boss_throw` made "cleaner, more power" WITHOUT changing their poses.<br>- Backs it up first (`pumpkin_boss_throw_user`, fake user, + `backups/pumpkin_boss_throw_original.blend`).<br>- Retimes the 13 key poses (BASE map, same 91 frames, release still at frame 39 = Unity 0.65 s).<br>- Adds a coiled hold at 33 (the wind-up pushed 12% further along 20→31).<br>- Strike 33→43 (was 31→45); AUTO (unclamped) handles on the passing keys 36/39, so the hand keeps its speed.<br>- Overlap: body + legs + counter-arm −1 frame, head +1, Stem.01–09 +1..+4.<br>- Quaternion continuity (sign flips; LowerArm.L spun the long way 78→91).<br>- Stray single-key bone slides zeroed (UpperArm.L loc @20, Thigh.R loc @36).<br>- Foot fixes done in the MCP session after it: knee-only bisection keys lift the toe out of the floor; don't "fix" a planted foot's 1 cm dip with a big knee bend. |
| `boss/throw_mirror.py` | 2026-10-16: `pumpkin_boss_throw_L` = the improved right throw mirrored (same frames and handle types, NLA strip 2177-2267). Mirrors every bone through its own rest orientation, so the one-sided Stem chain works too. Hands mirror exactly; legs within 2 cm. |
| `boss/export_fbx.py`, `boss/dump_actions.py` | Headless FBX export with the boss's proven settings (`-- out.fbx apply` = Apply Transform on, needed for an exact match), and a per-action sample dump to compare the live session with the saved .blend. Unity side (`boss/unity/`, run with eval_file): `fbx_compare*.cs` (temp import with a `Preset` of the real importer; node transforms, mesh, bind poses, every clip's curves; rotation diffs as angles) , `boss_throw_install.cs` (backup, copy over, add clip + Animator state) and `boss_throwL_test.cs` (Play test: 4-throw combo, which state/hand, pictures; its log path points at an old scratchpad, change it). |
| `boss/slam_power.py` (+ `slam_inspect.py`, `slam_check.py`) | 2026-10-16: the user's `pumpkin_boss_jump_slam` cleaned + more power, their poses kept. Rebuilds from backup `pumpkin_boss_jump_slam_before_claude` each run (STAGES set).<br>- Game timing kept: take-off ~f33, fists/feet hit f80-82 (Unity impact 1.32 s).<br>- Impact: squash key 85→83, torso crunch +30%, hold key 86, recoil 92.<br>- Feet: knees-only keys, worst frame first (bisection), for the impact + landing crouch, so the squash depth stays; Root lift (LINEAR keys) only where a planted foot still sinks ≥1 cm (take-off push, stand-up).<br>- Head + Stem.01-09 overlap: a damped spring per bone driven by the bone's own acceleration, only in a take-off window (from the user's key 31) and a landing window. Keys only at the spring's turning points + greedy keys where the fit strays over 1.5°. Directions checked: stem tip drops ~20 cm on landing, floats up after take-off.<br>- Before/after: a temporary armature copy (shared bones) far along X with an NLA strip of the backup; both looked at from -X so neither hides the other. |

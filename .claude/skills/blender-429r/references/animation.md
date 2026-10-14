# Enemy animation in Blender for 429R

The "smarter" enemies (spiders, cockroaches, bees) are driven by `Assets/Scripts/Enemies/SpiderBrain.cs`. The brain
moves the body itself (NavMeshAgent moves, lunges, slides), so **clips stay in place** (no root motion) and have
to match the brain's timing fields.

## Timing

- Clip frames = game seconds × fps × Animator state speed. Spider clips are 60 fps; the spider controller plays
  run/walk/attack at ×1.3 (attack frame = seconds × 78) and hit at ×0.75. Cockroach `run` ×3, `hit` ×0.5, most new
  states ×1. Read the controller's state speeds before converting.
- Match the brain's AttackMove fields: `hitFrom`/`hitUntil` (the contact window), `duration`, `lunge` (the brain's
  forward slide: the clip should be airborne or striding over that window), dodges (the brain slides 1 m over
  0.35 s, so the hop must land by then).
- A flinch (`hit`) is only seen for its first ~0.4 s, so put the impact in the first frames.

## Building actions

- Respect the user's keys: they often key the main poses themselves and want them completed or retimed. Keep a
  backup .blend of the original action (`_claude/<name>_original.blend`) before rebuilding in place.
- **Key every bone channel** in a new or rebuilt action, at rest if unused. An action missing channels shows stale
  poses from whatever played before (`roach_fill_channels.py` fills them at rest).
- Ground contact: run a floor pass that lifts the body so no foot or body part goes under the floor, alternating
  with the IK fit until stable. Exclude long thin parts (antennae) from the ground solve, or they prop the body up.
- Check overlaps on temporary copies (BVH tree overlap counts per frame, e.g. wings × lids × body) because the
  user's armature may be in NLA tweak mode.
- Mirrored versions: generate by travel direction rather than by name-mirroring when the bone names don't follow
  `.L/.R` cleanly (the cockroach's `M2_IK.L.001`); the spider flip rule is loc(-x, y, z), quat(w, x, -y, -z).

## NLA and export

- The user keeps every action as a strip in one NLA track. Add a strip for each new action at a free frame range,
  keep strip lengths = action ranges, and don't leave the file in tweak mode on their behalf.
- After the user re-exports the FBX: set the new clip's range and loop flag in the importer, add the Animator
  state (speed per the timing above), set the brain's animation-name field on the base prefab (variants inherit),
  and re-read everything.

## Previews

`look` with `mode="frames"` (a strip over the animation) or a list of frames. Because of tweak mode, preview other
actions on a temporary copy of the armature and mesh, then delete the copy.

## Characters (humanoid FK rigs, held weapons): Chopper, 2026-10-14

Start from `chopper/chop_rig.py` + `anim_axe_common.py` (see past-jobs.md). Lessons:

- **Build clips from scratch by script when the brief changes the character** (Chopper's old clips were bouncy hops,
  the brief was "heavy but agile"). Bake into NEW actions, keep the user's actions and strips untouched, back them up
  (`bpy.data.libraries.write`) and append strips to their track.
- **Measure the proportions first.** Chopper's hand (wrist to fist centre 0.38 m) is longer than his forearm
  (0.28 m), his head is wider than his shoulders and the axe is 1.76 m long. Many "obvious" poses were unreachable:
  no hands-over-head, no fist high on the shoulder, no fist tucked on the hip. Check reach (the IK notes "short by")
  and collisions on every frame before looking at pictures.
- **Grip mapping:** the handle leaves the fist on the thumb side, the blade edge points along the hand. Neutral wrist
  = handle at 90 deg to the forearm; ulnar deviation (up to ~55 deg) lines it up for the impact.
- **Drive the arm, hang the weapon on the hand** (wrist position + elbow direction + wrist turn). Driving the weapon
  and solving the hands onto it gave 90-180 deg wrist flips: the elbow side decides the forearm roll. For a second
  hand, solve it onto the handle with the elbow mirrored from the first and a search around it.
- Angles given in the chest frame lean with the chest (a 48 deg forward lean pointed the handle into the ground);
  give impact orientations in world terms when the body leans hard.
- Solve the impact numerically: bisect the swing angle so the blade edge hits the target height, then pick the
  forearm roll that lands it straight ahead.
- Body-part overlap check: build BVH trees per part from the evaluated mesh (faces whose vertices' dominant group is
  in the part). At rest the sleeve roots already overlap the torso (~30-40 triangles); judge against that baseline.
- Hard-code shared poses (the carry's elbow) instead of searching per clip, so clips that hand off to each other
  match exactly; check hand-offs as bone-head distances between the last and first frames.
- Previews: render MP4s from a temporary scene (Workbench, texture colour, shadows, a ground plane). In Blender 5,
  set `image_settings.media_type = 'VIDEO'` before `file_format = 'FFMPEG'`. A still render of a second scene did
  not evaluate the animation at `frame_current`; render with `animation=True` over a frame range instead.
- The user's armature keeps an active action on top of the NLA, so scrubbing the strips shows that action. Tell them
  to pick a clip in the Action Editor to watch it.

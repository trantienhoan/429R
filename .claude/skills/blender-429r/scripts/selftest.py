# Smoke test for the blender-429r scripts (never saves the .blend; writes test PNGs to K:/WORK_2026/08_04/_claude/skilltest):
#   blender -b "K:/Repository/429R/Assets/Blender/Cockroach_Enemy_NEW.blend" --python selftest.py
# Expect: the sharing report, ~500k paintable wing-lid texels, "outside ... : 0 (must be 0)", "atlas untouched: True".
import bpy, os, time, numpy as np
os.makedirs(r"K:/WORK_2026/08_04/_claude/skilltest", exist_ok=True)
IMAGE = "Cockroach_Enemy_Atlas"
t0 = time.time()
exec(open(r"K:/Repository/429R/.claude/skills/blender-429r/scripts/check_uvs.py", encoding="utf-8").read())
print(f"check_uvs took {time.time() - t0:.1f} s")

img = bpy.data.images[IMAGE]
W, H = img.size
objs = objects_using(img)
groups = [g.name for g in objs[0].vertex_groups if "wing_lid" in g.name]
print("wing_lid groups:", groups)
t0 = time.time()
flt = faces_in_group(*groups) if groups else selected_faces
lay = bake_layout(objs, W, H, face_filter=flt)
others = bake_layout(objs, W, H, face_filter=invert(flt))
paint = lay.mask & ~others.mask
print(f"filter texels {int(lay.mask.sum())}, used by other faces too {int((lay.mask & others.mask).sum())}, paintable {int(paint.sum())}, stacked {int(lay.stacked.sum())}, took {time.time() - t0:.1f} s")
col = pixels(img)
before = col.copy()
n = lay.nrm[paint]
shade = smoothstep(-0.5, 1.0, n[:, 2])
grain = fbm(lay.pos[paint], 40.0, 3)
col[paint, :3] = lerp(hex_color("#2A170C"), hex_color("#A8622E"), shade * (0.7 + 0.3 * grain))
col[paint, 3] = 1.0
out = dilate(col, paint, 6, blocked=lay.any_face)
changed = np.any(np.abs(out - before) > 1e-6, axis=2)
print(f"changed texels {int(changed.sum())} (painted {int(paint.sum())} + padding {int((changed & ~paint).sum())}); outside the filter's texels and padding: {int((changed & lay.any_face & ~paint).sum())} (must be 0)")
test = new_image("skilltest_colour", W, H)
print("wrote", write_image(test, out, r"K:/WORK_2026/08_04/_claude/skilltest/skilltest_colour.png"))
height = np.zeros((H, W), np.float32)
height[paint] = 0.5 + 0.5 * band(np.mod(lay.pos[paint][:, 1] * 40.0, 1.0) - 0.5, 0.2, 0.05)
nrm = height_to_normal(height, paint, 2.0)
nimg = new_image("skilltest_normal", W, H, non_color=True)
arr = np.ones((H, W, 4), np.float32); arr[..., :3] = nrm
print("wrote", write_image(nimg, arr, r"K:/WORK_2026/08_04/_claude/skilltest/skilltest_normal.png"))
print("atlas untouched:", bool(np.array_equal(before[~paint], pixels(img)[~paint])), "| .blend dirty (not saved):", bpy.data.is_dirty)

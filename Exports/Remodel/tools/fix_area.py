"""Fix-ups on a remodel .blend: texture paths relative to the repo (both PCs), sky hidden at the centre; prints transforms."""
import bpy, os, re
blend_dir = os.path.dirname(bpy.data.filepath)
repo = os.path.abspath(os.path.join(blend_dir, "..", ".."))
fixed = missing = 0
for img in bpy.data.images:
    p = img.filepath.replace("\\", "/")
    m = re.search(r"(Assets/.*)$", p)
    if not m: continue
    local = os.path.join(repo, m.group(1))
    if os.path.exists(local):
        img.filepath = bpy.path.relpath(local); img.reload(); fixed += 1
    else:
        missing += 1; print("MISSING", m.group(1))
print("textures repointed", fixed, "missing", missing)
sky = bpy.data.objects.get("MODEL Skydome")
if sky:
    sky.location = (0, 0, 0)
    bpy.data.collections["E_Sky"].hide_viewport = True
for e in sorted([o for o in bpy.data.objects if o.name.startswith("MODEL ")], key=lambda o: o.name):
    kids = [c for c in e.children]
    desc = ["%s rot%s scale%s" % (c.name, tuple(round(a * 57.3) for a in c.rotation_euler), tuple(round(s, 3) for s in c.scale)) for c in kids[:2]]
    print(e.name[6:], "|", "; ".join(desc))
bpy.ops.wm.save_mainfile()
print("SAVED")

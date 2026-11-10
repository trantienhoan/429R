for a in bpy.data.actions:
    print("ACTION", a.name, "users", a.users, "range", tuple(a.frame_range), "fcurves", len(a.fcurves) if hasattr(a, 'fcurves') else '?')
arm = bpy.data.objects["Pie_Knight_Armature"]
ad = arm.animation_data
print("NLA", [ (t.name, [s.action.name for s in t.strips]) for t in ad.nla_tracks] if ad else None)
# list candy-ish / effect assets in the project
import glob
root = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
hits = []
for pat in ("Assets/**/*andy*.fbx", "Assets/**/*andy*.blend", "Assets/**/*Candy*.prefab", "Assets/Blender/*.blend"):
    hits += glob.glob(os.path.join(root, pat), recursive=True)
for h in sorted(set(hits))[:80]:
    print("ASSET", os.path.relpath(h, root), os.path.getsize(h)//1024, "KB")

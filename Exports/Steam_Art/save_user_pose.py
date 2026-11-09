"""Saves the knight's pose (every bone's local matrix) and the sword's / shield's transforms from the OPEN file into
knight_pose_user.json next to this script; story_pass.py puts that pose back at the end of every run. Run it after
posing the knight by hand in Blender (on the saved file). Never saves the .blend.

    blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/save_user_pose.py
"""
import bpy, json, os
arm = bpy.data.objects["Pie_Knight_Armature"]
d = {"source": os.path.basename(bpy.data.filepath),
     "bones": {pb.name: [list(r) for r in pb.matrix_basis] for pb in arm.pose.bones},
     "objects": {}}
for n in ("Pie_Knight_Sword", "Pie_Knight_Shield"):
    o = bpy.data.objects.get(n)
    if o:
        d["objects"][n] = {"location": list(o.location), "rotation_euler": list(o.rotation_euler), "scale": list(o.scale),
                           "rotation_mode": o.rotation_mode, "hide_render": o.hide_render}
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "knight_pose_user.json")
json.dump(d, open(out, "w"), indent=1)
print("SAVED POSE", out, len(d["bones"]), "bones")

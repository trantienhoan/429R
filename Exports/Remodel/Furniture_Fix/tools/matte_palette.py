# Makes the Furniture_Palette material matte (roughness 1, low specular), like the game's low-smoothness materials; saves.
import bpy
m = bpy.data.materials["Furniture_Palette"]
b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
b.inputs["Roughness"].default_value = 1.0
for k in ("Specular IOR Level", "Specular"):
    if k in b.inputs:
        b.inputs[k].default_value = 0.15
bpy.ops.wm.save_mainfile()
print("matte saved")

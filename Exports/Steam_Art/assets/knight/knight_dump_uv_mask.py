# Writes the UV triangles of the sword and the shield (knight_sword_shield_uv.json) so improve_knight_texture.py can
# leave the "lit from above" shading off them (they hang in another frame in the rest pose). NEVER saves the .blend.
import bpy, json, os
HERE = os.path.dirname(os.path.abspath(__file__))
tris = []
for name in ("Pie_Knight_Sword", "Pie_Knight_Shield"):
    o = bpy.data.objects[name]
    me = o.data
    uv = me.uv_layers.active.data
    me.calc_loop_triangles()
    for t in me.loop_triangles:
        tris.append([[uv[li].uv.x, uv[li].uv.y] for li in t.loops])
json.dump(tris, open(os.path.join(HERE, "knight_sword_shield_uv.json"), "w"))
print("UV TRIS", len(tris))

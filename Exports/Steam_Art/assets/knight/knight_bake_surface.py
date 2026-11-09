"""Bakes, for every texel of the knight's armour atlas, where it is on the knight (world position, rest pose), which
way the surface faces there (normal) and which part it belongs to (object number) -> knight_surface_<SIZE>.npz.
improve_knight_texture.py uses it to paint the clovers straight onto the surface (so they are round on the model,
not stretched like the old UV-drawn ones). Run on Steam_Art.blend; NEVER saves the .blend.

    blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/assets/knight/knight_bake_surface.py [-- size=2048]
"""
import bpy, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
A = dict(a.split("=", 1) for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []) if "=" in a)
SIZE = int(A.get("size", 2048))
sc = bpy.context.scene
arm = bpy.data.objects["Pie_Knight_Armature"]
arm.data.pose_position = "REST"
knight = sorted([o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Pie_Knight")], key=lambda o: o.name)
for o in bpy.data.objects:
    if o.type in ("MESH", "CURVE", "VOLUME", "CURVES", "FONT"):
        o.hide_render = o not in knight
for i, o in enumerate(knight):
    o.hide_render = False
    o.hide_set(False)
    o.pass_index = i + 1
sc.render.engine = "CYCLES"
sc.cycles.samples = 1
sc.render.bake.margin = 3
sc.render.bake.use_clear = False

img = bpy.data.images.new("KN_SURF", SIZE, SIZE, alpha=True, float_buffer=True)
img.colorspace_settings.name = "Non-Color"
m = bpy.data.materials.new("KN_BAKE")
nt = m.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
em = nt.nodes.new("ShaderNodeEmission")
geo = nt.nodes.new("ShaderNodeNewGeometry")
oi = nt.nodes.new("ShaderNodeObjectInfo")
cmb = nt.nodes.new("ShaderNodeCombineXYZ")
nt.links.new(oi.outputs["Object Index"], cmb.inputs["X"])
tx = nt.nodes.new("ShaderNodeTexImage")
tx.image = img
nt.links.new(em.outputs[0], out.inputs["Surface"])
nt.nodes.active = tx
for o in knight:
    for s in o.material_slots:
        s.link = "OBJECT"
        s.material = m
bpy.ops.object.select_all(action="DESELECT")
for o in knight:
    o.select_set(True)
bpy.context.view_layer.objects.active = knight[0]

maps = {}
for key, sock in (("pos", geo.outputs["Position"]), ("nrm", geo.outputs["Normal"]), ("obj", cmb.outputs[0])):
    img.pixels.foreach_set(np.full(SIZE * SIZE * 4, -1000.0, np.float32))        # sentinel: texels nobody uses
    nt.links.new(sock, em.inputs["Color"])
    bpy.ops.object.bake(type="EMIT")
    px = np.empty(SIZE * SIZE * 4, np.float32)
    img.pixels.foreach_get(px)
    maps[key] = px.reshape(SIZE, SIZE, 4)[::-1, :, :3].copy()      # rows top-down, like the PNG
    print("BAKED", key)
used = maps["pos"][..., 0] > -999
objid = np.where(used, np.rint(maps["obj"][..., 0]), 0).astype(np.int16)
out_path = os.path.join(HERE, "knight_surface_%d.npz" % SIZE)
np.savez_compressed(out_path, pos=maps["pos"].astype(np.float32), nrm=maps["nrm"].astype(np.float32), obj=objid,
                    names=np.array([o.name for o in knight]))
print("SAVED", out_path, "used texels %.1f%%" % (100 * used.mean()), "objects", [o.name for o in knight])

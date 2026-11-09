import bpy
ob=bpy.data.objects["pumpkin_0"]
m=ob.modifiers[0]; print("MOD", m.node_group.name, [(k, getattr(m,"properties",None)) for k in [0]])
for it in m.node_group.interface.items_tree:
    try: print("  IF", it.name, it.identifier, m[it.identifier])
    except Exception as e: print("  IF", it.name, e)
mat=ob.active_material; b=next(n for n in mat.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
for i in b.inputs:
    if not i.is_linked and hasattr(i,"default_value"):
        try: print("  IN", i.name, tuple(i.default_value) if hasattr(i.default_value,"__len__") else i.default_value)
        except Exception as e: pass
mp=next(n for n in mat.node_tree.nodes if n.type=="MAPPING"); print("MAP", tuple(mp.inputs["Location"].default_value), tuple(mp.inputs["Scale"].default_value))
for n in mat.node_tree.nodes:
    if n.type=="TEX_IMAGE": print("IMG", n.name, n.image.colorspace_settings.name, n.interpolation)
print("smooth", sum(p.use_smooth for p in ob.data.polygons), len(ob.data.polygons))

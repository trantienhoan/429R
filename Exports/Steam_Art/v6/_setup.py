import bpy, os
sc = bpy.context.scene
# GPU
prefs = bpy.context.preferences.addons['cycles'].preferences
for t in ('OPTIX', 'CUDA'):
    try:
        prefs.compute_device_type = t
        prefs.get_devices()
        devs = [d for d in prefs.devices if d.type == t]
        if devs:
            for d in prefs.devices:
                d.use = (d.type == t)
            print("GPU", t, [d.name for d in devs])
            break
    except Exception as e:
        print("no", t, e)
sc.cycles.device = 'GPU'
tree = sc.compositing_node_group
print("COMP", tree.name if tree else None)
if tree:
    for n in tree.nodes:
        ins = []
        for i in n.inputs:
            if i.is_linked: ins.append(i.name + "<-" + i.links[0].from_node.name)
            elif hasattr(i, "default_value"):
                try: v = tuple(round(x,3) for x in i.default_value)
                except TypeError: v = round(i.default_value,3) if isinstance(i.default_value, float) else i.default_value
                ins.append("%s=%s" % (i.name, v))
        print(" NODE", n.bl_idname, n.name, ins[:14])
w = sc.world
print("WORLD use_nodes", w.use_nodes)
for n in w.node_tree.nodes:
    print(" WN", n.bl_idname, n.name, [(i.name, (tuple(round(x,3) for x in i.default_value) if hasattr(i.default_value,'__len__') else round(i.default_value,3)) if hasattr(i,'default_value') and not i.is_linked else 'L') for i in n.inputs][:6])
print("film_transparent", sc.render.film_transparent, "light_paths", sc.cycles.max_bounces, sc.cycles.volume_bounces, "caustics", sc.cycles.caustics_reflective)
print("VOL", [ (o.name, o.type) for o in bpy.data.objects if o.type=='VOLUME' or (o.type=='MESH' and o.active_material and any(n.bl_idname.startswith('ShaderNodeVolume') for n in (o.active_material.node_tree.nodes if o.active_material and o.active_material.node_tree else [])))][:20])

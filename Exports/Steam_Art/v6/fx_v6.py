"""v6 atmosphere and glow: haze volume behind the knight, glowing Tree of Light."""
def haze_v6(density=0.025, color=(0.55, 0.6, 1.0), aniso=0.45, y0=-1.0, y1=-10.0, x=7.0, z0=-1.0, z1=7.0):
    name = "V6_Haze"
    ob = bpy.data.objects.get(name)
    if ob is None:
        me = bpy.data.meshes.new(name)
        import bmesh
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bm.to_mesh(me); bm.free()
        ob = bpy.data.objects.new(name, me); v6col().objects.link(ob)
        m = bpy.data.materials.new(name); m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial"); pv = nt.nodes.new("ShaderNodeVolumePrincipled")
        nt.links.new(pv.outputs[0], out.inputs["Volume"])
        me.materials.append(m)
    ob.location = (0, (y0 + y1) / 2, (z0 + z1) / 2)
    ob.scale = (2 * x, abs(y1 - y0), z1 - z0)
    pv = next(n for n in ob.active_material.node_tree.nodes if n.bl_idname == "ShaderNodeVolumePrincipled")
    pv.inputs["Density"].default_value = density
    pv.inputs["Color"].default_value = (*color, 1)
    pv.inputs["Anisotropy"].default_value = aniso
    ob.hide_render = density <= 0
    SC.cycles.volume_bounces = 0
    SC.cycles.volume_step_rate = 4.0
    return ob
def tree_glow(leaf_emis=1.2, light=45.0):
    m = bpy.data.materials["ST_Pumpkin_Leaves_Mat"]
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Emission Strength"].default_value = leaf_emis
    bpy.data.objects["V6_Tree_Glow"].data.energy = light

def ember_mat(name, color, strength):
    m = bpy.data.materials.get(name)
    if not m:
        m = bpy.data.materials.new(name); m.use_nodes = True
        nt = m.node_tree; nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(em.outputs[0], out.inputs[0])
    em = next(n for n in m.node_tree.nodes if n.type == "EMISSION")
    em.inputs["Color"].default_value = (*color, 1); em.inputs["Strength"].default_value = strength
    return m
def sparks(prefix, n, center, spread, rad=(0.003, 0.007), color=(1.0, 0.45, 0.08), strength=30.0, seed=1, up_bias=0.0, avoid=None, shape="sphere"):
    """small emissive specks (embers / magic sparkles), all named prefix_i, rebuilt each run"""
    import random
    for o in [o for o in bpy.data.objects if o.name.startswith(prefix + "_")]:
        bpy.data.objects.remove(o, do_unlink=True)
    rnd = random.Random(seed)
    m = ember_mat(prefix + "_Mat", color, strength)
    base = bpy.data.meshes.get(prefix + "_Mesh")
    if base is None:
        import bmesh
        base = bpy.data.meshes.new(prefix + "_Mesh"); bm = bmesh.new()
        if shape == "sphere":
            bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
        else:   # a 4-point star card facing +Y
            import math as _m
            verts = []
            for k in range(8):
                r = 1.0 if k % 2 == 0 else 0.22
                a = k * _m.pi / 4
                verts.append(bm.verts.new((r * _m.cos(a), 0.0, r * _m.sin(a))))
            c = bm.verts.new((0, 0, 0))
            for k in range(8):
                bm.faces.new((c, verts[k], verts[(k + 1) % 8]))
        bm.to_mesh(base); bm.free()
        base.materials.append(m)
    C = Vector(center); S = Vector(spread)
    made = 0
    tries = 0
    while made < n and tries < n * 20:
        tries += 1
        p = C + Vector(((rnd.random() * 2 - 1) * S.x, (rnd.random() * 2 - 1) * S.y, (rnd.random() * 2 - 1) * S.z))
        if up_bias: p.z += abs(rnd.gauss(0, up_bias))
        if avoid and any((p - Vector(a)).length < r for a, r in avoid):
            continue
        ob = bpy.data.objects.new("%s_%d" % (prefix, made), base)
        v6col().objects.link(ob)
        r = rnd.uniform(*rad)
        ob.location = p; ob.scale = (r, r, r)
        if shape != "sphere":
            ob.rotation_euler = (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.6, 0.6), rnd.uniform(0, 6.28))
        made += 1
    return made

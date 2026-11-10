arm = bpy.data.objects["Pie_Knight_Armature"]
mw = arm.matrix_world
for bn in ("mixamorig:Hips", "mixamorig:Head", "mixamorig:Spine2", "mixamorig:RightArm", "mixamorig:LeftArm"):
    pb = arm.pose.bones[bn]
    M = mw @ pb.matrix
    print(bn, "X", tuple(round(v,2) for v in M.col[0].xyz.normalized()), "Y", tuple(round(v,2) for v in M.col[1].xyz.normalized()), "Z", tuple(round(v,2) for v in M.col[2].xyz.normalized()))
    Mr = mw @ pb.bone.matrix_local
    print("   rest X", tuple(round(v,2) for v in Mr.col[0].xyz.normalized()), "Y", tuple(round(v,2) for v in Mr.col[1].xyz.normalized()), "Z", tuple(round(v,2) for v in Mr.col[2].xyz.normalized()))
# eyes: verts of the helmet/body using eye materials
for ob in arm.children:
    if ob.type != 'MESH': continue
    names = [m.name for m in ob.data.materials if m]
    print("MATS", ob.name, names)
deps = bpy.context.evaluated_depsgraph_get()
for ob in arm.children:
    if ob.type != 'MESH': continue
    me = ob.evaluated_get(deps).to_mesh()
    idx = [i for i, m in enumerate(ob.data.materials) if m and ('Eye' in m.name or 'eye' in m.name)]
    if idx:
        pts = [ob.matrix_world @ me.vertices[v].co for p in me.polygons if p.material_index in idx for v in p.vertices]
        c = sum(pts, Vector()) / len(pts)
        print("EYES in", ob.name, "centre", tuple(round(v,3) for v in c), "n", len(pts))
    ob.evaluated_get(deps).to_mesh_clear()
print("CAM main dir", tuple(round(v,2) for v in (bpy.data.objects['CAM_Main_Capsule_1232x706'].matrix_world.to_quaternion() @ Vector((0,0,-1)))))

"""v6 staging: absolute moves (from the file's original spots), safe to run again."""
if "ORIG" not in globals():
    ORIG = {o.name: o.matrix_world.copy() for o in bpy.data.objects}
GROUPS = {
    "tree": ["ST_Pumpkin_Tree", "ST_Tree_Light", "ST_Tree_Face_Light"] + [o.name for o in bpy.data.objects if o.name.startswith("ST_Tree_Pumpkin_")],
    "hero": ["Menace_Pumpkin_Root", "ST_Hero_Face_Light", "Pumpkin_Glow", "LP_Pumpkin_Spill"],
}
def move_group(key, delta, rot_z=0.0, pivot=None):
    names = [n for n in GROUPS[key] if n in bpy.data.objects]
    piv = Vector(pivot) if pivot else ORIG[names[0]].translation
    Rz = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(rot_z), 4, 'Z') @ Matrix.Translation(-piv)
    for n in names:
        o = bpy.data.objects[n]
        if o.parent and o.parent.name in names:
            continue
        o.matrix_world = Matrix.Translation(Vector(delta)) @ Rz @ ORIG[n]
    upd()
def stage(tree_delta=(0, 0, 0), tree_rot=0.0, hero_delta=(0, 0, 0), hero_rot=0.0, knight_at=None):
    move_group("tree", tree_delta, tree_rot)
    move_group("hero", hero_delta, hero_rot)
    if knight_at is not None:
        ARM.location = Vector(knight_at); upd()

import bpy
for n in ("ST_Pumpkin_Boss", "pumpkin_boss", "pumpkin_boss_armature"):
    o = bpy.data.objects.get(n)
    if not o: print("BOSSOBJ", n, "missing"); continue
    ad = o.animation_data
    print("BOSSOBJ", n, o.type, "loc", tuple(round(v, 3) for v in o.matrix_world.translation), "scale", tuple(round(v, 2) for v in o.matrix_world.to_scale()),
          "hide_render", o.hide_render, "hide_vp", o.hide_viewport, "hide_get", o.hide_get(), "parent", o.parent.name if o.parent else None,
          "act", ad.action.name if ad and ad.action else None, "colls", [c.name for c in o.users_collection])
c = bpy.data.collections.get("V6_TMP_BossBrowse")
print("COLL V6_TMP_BossBrowse hide_render", c.hide_render, "hide_viewport", c.hide_viewport)
def lc(layer, name):
    for ch in layer.children:
        if ch.name == name: return ch
        r = lc(ch, name)
        if r: return r
l = lc(bpy.context.view_layer.layer_collection, "V6_TMP_BossBrowse")
print("LAYERCOLL exclude", l.exclude if l else None, "hide_viewport", l.hide_viewport if l else None)
print("FRAME", bpy.context.scene.frame_current)

tree = SC.compositing_node_group
cv = tree.nodes["RGB Curves"]
for ci, c in enumerate(cv.mapping.curves):
    print("curve", ci, [(round(p.location[0],3), round(p.location[1],3)) for p in c.points])
cb = tree.nodes["Color Balance"]
print("CB", cb.correction_method if hasattr(cb,'correction_method') else None, [(i.name, tuple(round(x,3) for x in i.default_value) if hasattr(i.default_value,'__len__') else i.default_value) for i in cb.inputs if not i.is_linked and hasattr(i,'default_value')])
print("view", SC.view_settings.view_transform, SC.view_settings.look, SC.view_settings.exposure)
print("mist pass", bpy.context.view_layer.use_pass_mist)

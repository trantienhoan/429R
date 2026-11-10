cam = bpy.data.objects["CAM_Main_Capsule_1232x706"]
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if "Main_Capsule" in o.name and o.name != cam.name:
        print(o.name, o.type, "parent", o.parent.name if o.parent else None, "loc", tuple(round(v,3) for v in o.location), "rot", tuple(round(math.degrees(v),1) for v in o.rotation_euler), "scale", tuple(round(v,3) for v in o.scale), "hide_r", o.hide_render, "colls", [c.name for c in o.users_collection])
        if o.type == 'LIGHT':
            print("   light", o.data.type, o.data.energy, tuple(o.data.color), "size", getattr(o.data, 'size', None))
# light linking on logo lights
o = bpy.data.objects.get("LOGO_Main_Capsule_1232x706_Key")
if o:
    ll = o.light_linking
    print("linking recv", ll.receiver_collection.name if ll.receiver_collection else None, "block", ll.blocker_collection.name if ll.blocker_collection else None)
lg = bpy.data.objects.get("LOGO_Main_Capsule_1232x706")
print("LOGO children", [c.name for c in lg.children] if lg else None, "data", lg.data.name if lg and lg.data else None)

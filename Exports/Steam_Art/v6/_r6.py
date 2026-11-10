path = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets", "Blender", "Pumpkin_Boss_Z.blend"))
with bpy.data.libraries.load(path, link=False) as (src, dst):
    print("BOSS FILE actions", src.actions)
    print("BOSS FILE objects", src.objects)

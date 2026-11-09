import bpy, math, os
from mathutils import Vector, Matrix

scene = bpy.context.scene
cam = bpy.data.objects['CAM_Promo_KeyArt_1920x1080']
right = cam.matrix_world.to_quaternion() @ Vector((1, 0, 0))
root = bpy.data.objects['Menace_Pumpkin_Root']
root.scale *= 0.58
root.location += right * -0.13
knight = bpy.data.objects['Pie_Knight_Armature']
knight.scale *= 1.22
knight.location += right * -0.10
bpy.context.view_layer.update()
print('SWORD_PARENT', bpy.data.objects['Pie_Knight_Sword'].parent_type, bpy.data.objects['Pie_Knight_Sword'].parent_bone)
print('BONES', [(b.name, tuple(b.head), tuple(b.tail)) for b in knight.pose.bones if any(t in b.name.lower() for t in ('hand', 'forearm', 'upperarm'))])
# Preserve the existing pose while opening the sword silhouette below the visor.
sword = bpy.data.objects['Pie_Knight_Sword']
sword.rotation_mode = 'XYZ'
sword.rotation_euler.rotate_axis('Y', math.radians(-25))
scene.camera = cam
scene.render.resolution_x = 1920
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 50
scene.cycles.samples = 48
scene.render.image_settings.file_format = 'PNG'
out_dir = os.path.join(os.path.dirname(bpy.data.filepath), 'renders', 'GPT_Render')
os.makedirs(out_dir, exist_ok=True)
scene.render.filepath = os.path.join(out_dir, 'Steam_Art_2_Composition_01.png')
# Apply the same per-camera visibility rules as the existing renderer.
logos = bpy.data.collections.get('Logos')
if logos:
    for obj in logos.all_objects:
        obj.hide_render = obj.hide_render or not (obj.parent == cam and cam.get('logo', False))
for prefix in str(cam.get('hide_objs', '')).split(','):
    if prefix.strip():
        for obj in bpy.data.objects:
            if obj.name.startswith(prefix.strip()):
                obj.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
bpy.ops.render.render(write_still=True)

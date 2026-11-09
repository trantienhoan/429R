import bpy, math, os
from mathutils import Vector, Matrix
s = bpy.context.scene
cam = bpy.data.objects['CAM_Promo_KeyArt_1920x1080']
q = cam.matrix_world.to_quaternion()
right = q @ Vector((1, 0, 0))
bpy.data.objects['Menace_Pumpkin_Root'].location += right * 0.28
sword = bpy.data.objects['Pie_Knight_Sword']
sword.rotation_euler.rotate_axis('Y', math.radians(25))
bpy.context.view_layer.update()
origin = sword.matrix_world.translation.copy()
rotation = Matrix.Rotation(math.radians(-38), 4, q @ Vector((0, 0, 1)))
sword.matrix_world = Matrix.Translation(origin) @ rotation @ Matrix.Translation(-origin) @ sword.matrix_world
s.camera = cam
s.render.resolution_x, s.render.resolution_y = 1920, 1080
s.render.resolution_percentage = 50
s.cycles.samples = 64
s.render.filepath = os.path.join(os.path.dirname(bpy.data.filepath), 'renders', 'GPT_Render', 'Steam_Art_2_Composition_02.png')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
bpy.ops.render.render(write_still=True)

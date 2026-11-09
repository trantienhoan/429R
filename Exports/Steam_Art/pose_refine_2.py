import bpy, math, os
from mathutils import Vector, Matrix
s = bpy.context.scene
cam = bpy.data.objects['CAM_Promo_KeyArt_1920x1080']
arm = bpy.data.objects['Pie_Knight_Armature']
bone = arm.pose.bones['mixamorig:RightForeArm']
# Store the current pose independently of existing animation actions.
if arm.animation_data:
    arm.animation_data_clear()
bpy.context.view_layer.update()
pivot = arm.matrix_world @ bone.head
axis = cam.matrix_world.to_quaternion() @ Vector((0, 0, 1))
rot = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-42), 4, axis) @ Matrix.Translation(-pivot)
bone.matrix = arm.matrix_world.inverted() @ rot @ arm.matrix_world @ bone.matrix
bpy.context.view_layer.update()
s.camera = cam
s.render.resolution_percentage = 50
s.cycles.samples = 64
s.render.filepath = os.path.join(os.path.dirname(bpy.data.filepath), 'renders', 'GPT_Render', 'Steam_Art_2_Composition_03.png')
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
bpy.ops.render.render(write_still=True)

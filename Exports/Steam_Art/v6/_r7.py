CLIPS = ['pumpkin_boss_attack', 'pumpkin_boss_punch_R', 'pumpkin_boss_throw', 'pumpkin_boss_jump_slam', 'pumpkin_boss_laugh',
         'pumpkin_boss_come', 'pumpkin_boss_clap', 'pumpkin_boss_transform', 'pumpkin_boss_vomit', 'pumpkin_boss_cyclone', 'pumpkin_boss_idle_true', 'pumpkin_boss_run']
col = bpy.data.collections["V6_TMP_BossBrowse"]
arm = next(o for o in col.objects if o.type == "ARMATURE")
body = next(o for o in col.objects if o.type == "MESH" )
acts = {a.name: a for a in bpy.data.actions}
print([n for n in acts if 'boss' in n])

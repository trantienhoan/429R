import json
mg = MOON_GROUP
d = {"moon_orig": {n: [list(r) for r in ORIG[n]] for n in mg if n in ORIG},
     "boss_mine": [list(r) for r in bpy.data.objects["ST_Pumpkin_Boss"].matrix_world],
     "face_mine": list(BOSS_FACE_NOW),
     "sky_moon_set": list(SKY_MOON_SET)}
json.dump(d, open(os.path.join(HERE, "v6_state.json"), "w"), indent=1)
print("exported", len(d["moon_orig"]), "moon parts; face", [round(v, 3) for v in BOSS_FACE_NOW])

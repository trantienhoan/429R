import bpy
for n in ("Weapon_Frying_Pan", "Weapon_Frying_Pan_break.000", "Weapon_Frying_Pan_break.001", "Weapon_Baseball_Bat",
          "Weapon_Baseball_Bat_cell", "Weapon_Baseball_Bat_cell.001", "Weapon_Baseball_Bat_cell.002"):
    o = bpy.data.objects[n]
    used = sorted({p.material_index for p in o.data.polygons})
    print("USED", n, [o.material_slots[i].material.name for i in used])

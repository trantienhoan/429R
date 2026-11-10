import bpy
for o in bpy.data.objects:
    if o.name.startswith(("BG2_Moon", "LP_Moon", "Moon_Anchor")):
        print("MOONOBJ", o.name, "parent", o.parent.name if o.parent else None, "cons", [(c.type, getattr(c, 'target', None) and c.target.name) for c in o.constraints],
              "drivers", len(o.animation_data.drivers) if o.animation_data else 0)

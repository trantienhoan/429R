for i, (dens, col, rim, ex) in enumerate([(0.009, (0.42, 0.45, 1.0), 500, -0.2), (0.012, (0.35, 0.40, 1.0), 350, -0.35), (0.006, (0.45, 0.42, 1.0), 600, -0.1)]):
    haze_v6(density=dens, color=col)
    bpy.data.objects["V6_Boss_Rim"].data.energy = rim
    R("CAM_V6_Main", pct=50, spp=48, out="light_g%d" % i, show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"), exposure=ex)

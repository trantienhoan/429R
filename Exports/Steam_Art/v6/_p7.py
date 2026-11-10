exec(open(os.path.join(HERE, "posekit.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "pose_e.py"), encoding="utf-8").read(), globals())
VARIANTS = {
 "battlecry": dict(legs="wide", r_target=(0.04, 0.16, 0.26), r_hint=(0, 1, 0), r_stretch=1.6, l_target=(0.0, -0.14, 0.20), l_hint=(0, -1, 0), l_stretch=1.3, sword_dir=(-0.15, -0.35, 1.0), head=(-8, -10, 8)),
 "shoulder_punch": dict(legs="lunge", r_target=(0.24, 0.14, 0.06), r_hint=(0, 0.3, -1), r_stretch=1.6, l_target=(0.02, -0.02, 0.10), l_hint=(0, -1, -0.5), sword_dir=(-0.8, 0.2, 0.55), head=(-12, 0, 6), shield="back"),
 "point": dict(legs="lunge", turn=-25, r_target=(0.02, 0.15, -0.02), r_hint=(0, 1, 0), r_stretch=1.3, l_target=(0.20, -0.06, 0.06), l_hint=(0, -1, -0.3), l_stretch=1.5, sword_dir=(1.0, -0.15, 0.25), head=(-15, 0, 4), shield="back"),
 "uppercut": dict(legs="jump", lift=0.08, hips_roll=-10, r_target=(0.10, 0.10, 0.34), r_hint=(0, 1, -0.2), r_stretch=2.0, l_target=(-0.04, -0.12, -0.03), l_hint=(0, -1, 0.2), sword_dir=(-0.5, -0.8, -0.35), head=(-8, -8, -6)),
}
k0 = None
for i, (nm, kw) in enumerate(VARIANTS.items()):
    pose_e(**kw)
    F, Rt, U = frame()
    k = head_w("Spine1")
    c = tmpcam("CAM_TMP_PE", k + (F * 1.0 + Rt * 0.25).normalized() * 1.25 + Vector((0, 0, 0.0)), k + Vector((0, 0, 0.07)), lens=40, w=500, h=500)
    R("CAM_TMP_PE", pct=100, spp=12, out="pose_e_%s" % nm, show_logo=False, hide=("ST_Pumpkin_Boss", "ST_Pumpkin_Tree", "ST_Tree_Pumpkin", "ST_Hero", "Menace_Pumpkin", "ST_Spider"))

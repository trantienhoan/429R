"""The line under the 429 logo ("A VR ADVENTURE GAME" by default), 2026-11-10, built with the 429 logo's OWN code
(logo_build.py, exec'd without its main()): Bookman Bold outlines cut into flat chisel facets, the style-E gold
profile (deep gold side walls, chamfers, glowing groove), the inlaid green enamel, the same materials and the green
fire card. The letters are built at the digits' size (cap height 0.88), so every bevel has the same proportions as on
the 429, then the root is scaled so the lettering is 2.0 wide like the logo.
Writes assets/tagline/Logo_Tagline.blend: collection Logo_Tagline, root Logo_Tagline_Root (front faces -Y, Z up,
lettering 2.0 wide centred on the root, exactly like Logo_429_Root).
  blender -b --factory-startup --python tagline_build.py -- [text="A VR ADVENTURE GAME"] [bold=0.024] [track=0.09]
        [reach=0.38] [render=test.png] [res=2560x720] [samples=96]"""
import os, sys
_TAG_HERE = os.path.dirname(os.path.abspath(__file__))
_src = open(os.path.join(_TAG_HERE, "logo_build.py"), encoding="utf-8").read()
_src = _src[:_src.rstrip().rindex("main()")]
exec(compile(_src, os.path.join(_TAG_HERE, "logo_build.py"), "exec"))

TAG_DIR = os.path.join(_TAG_HERE, "tagline")
os.makedirs(TAG_DIR, exist_ok=True)
TEXT = ARGS.get("text", "A VR ADVENTURE GAME")
CAP = 0.88                                   # the digits' cap height
BOLD = float(ARGS.get("bold", "0.024"))      # outward offset, keeps the thin strokes thick enough for the gold rim
TRACK = float(ARGS.get("track", "0.09"))     # extra space between letters (in cap heights)
SPACE = float(ARGS.get("space", "0.36"))     # word space (in cap heights)
SHEAR = 0.07                                 # the 429's lean


def tagline_glyphs():
    ref = facet_loops("H")
    hx0, hy0, hx1, hy1 = bbox(ref)
    k = CAP / (hy1 - hy0)
    out = []
    cursor = 0.0
    for ch in TEXT:
        if ch == " ":
            cursor += SPACE * CAP
            continue
        loops = facet_loops(ch)
        m = affine(shear=SHEAR) @ affine(scale=k, ty=-hy0 * k)
        L = xform_loops(loops, m)
        a0, b0, a1, b1 = bbox(L)
        L = xform_loops(L, affine(tx=cursor - a0))
        cursor += (a1 - a0) + TRACK * CAP
        if BOLD:
            L = [clean_loop(r[0], 2e-4) for r in sdf_offsets(L, [-BOLD], 12.0)]
        out.append((L, 0.0, ch))
    allL = [l for L, _, _ in out for l in L]
    x0, y0, x1, y1 = bbox(allL)
    cx, cy = (x0 + x1) * 0.5, CAP * 0.5
    m = affine(tx=-cx, ty=-cy)
    return [(xform_loops(L, m), z, ch) for L, z, ch in out], (x1 - x0)


def build_tagline():
    reset()
    col = new_coll("Logo_Tagline")
    root = bpy.data.objects.new("Logo_Tagline_Root", None)
    root.empty_display_type = "ARROWS"
    col.objects.link(root)
    root["text"] = TEXT
    root["facing"] = "-Y (like Logo_429_Root), Z up; lettering 2.0 wide after the root scale"
    gold = mat_gold_e("Tagline_Gold")
    side = mat_gold_e("Tagline_Gold_Side", side=True)
    green = mat_enamel_e()
    green.name = "Tagline_Enamel"
    glow = mat_glow()
    glow.name = "Tagline_Glow"
    prof = profile_for("E")
    glyphs, width = tagline_glyphs()
    for i, (loops, zoff, ch) in enumerate(glyphs):
        me = loft("Tagline_%02d_%s" % (i, ch), loops, prof, zoff=zoff, inlay=INLAY["E"], corner_deg=11.0, cap_bottom=True)
        for m in (gold, green, glow, side):
            me.materials.append(m)
        ob = bpy.data.objects.new("Tagline_%02d_%s" % (i, ch), me)
        col.objects.link(ob)
        ob.parent = root
    # the fire card, like the 429's (its distance image goes to assets/tagline/, not over the logo's)
    FIRE.update(res=int(ARGS.get("fire_res", "3072")), reach=float(ARGS.get("reach", "0.38")),
                margin=float(ARGS.get("margin", "0.36")))
    g = globals()
    keep_here = g["HERE"]
    g["HERE"] = TAG_DIR
    img, (fx0, fy0, fx1, fy1) = fire_sdf_image([(L, z) for L, z, _ in glyphs])
    g["HERE"] = keep_here
    img.name = "Tagline_Fire_SDF"
    fm = mat_fire(img, (fx0, fy0, fx1, fy1))
    fm.name = "Tagline_Fire"
    me = bpy.data.meshes.new("Tagline_Fire")
    yb = 0.06
    me.from_pydata([(fx0, yb, fy0), (fx1, yb, fy0), (fx1, yb, fy1), (fx0, yb, fy1)], [], [(0, 1, 2, 3)])
    uv = me.uv_layers.new(name="UVMap")
    for li, c in enumerate([(0, 0), (1, 0), (1, 1), (0, 1)]):
        uv.data[li].uv = c
    for m in (gold, green, glow, side, fm):
        me.materials.append(m)
    for pl in me.polygons:
        pl.material_index = MAT_INDEX["fire"]
    fo = bpy.data.objects.new("Tagline_Fire", me)
    col.objects.link(fo)
    fo.parent = root
    fo.visible_shadow = fo.visible_diffuse = fo.visible_glossy = False
    s = 2.0 / width
    root.scale = (s, s, s)
    root["built_width"] = width
    print("TAGLINE", repr(TEXT), "glyphs", len(glyphs), "built width %.3f -> scale %.4f" % (width, s))
    return col, root


build_tagline()
if ARGS.get("render"):
    build_rig("front")
    p = ARGS["render"]
    if not os.path.isabs(p):
        p = os.path.join(TAG_DIR, p)
    render(p, ARGS.get("res", "2560x720"), int(ARGS.get("samples", "96")))
lc = bpy.context.view_layer.layer_collection.children.get("Logo_Test_Rig")
if lc:
    lc.exclude = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(TAG_DIR, "Logo_Tagline.blend"))
print("SAVED", os.path.join(TAG_DIR, "Logo_Tagline.blend"))

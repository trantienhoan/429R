"""Story pass (2026-11-09): "the knight guards the Tree of Light".

    blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/story_pass.py            (apply + save)
    ... -- nosave                                                                                   (apply only)

Pipeline place: assemble_v2.py -> knight_pass.py -> story_pass.py -> lighting_pass.py -> render_all.py
(story before lighting: the lighting pass aims some lights at the hero pumpkin where the story pass put it, and it sets
each camera's hide_objs / exposure, including the icon cameras made here).
Safe to run again: everything it adds is named ST_... (collection ST_Story) and is rebuilt on each run; the pumpkins it
moves get the same numbers each time. It never edits anything under Assets/ (it only reads the models there).

What it adds / changes:
- The Tree of Light = the game's Pumpkin_Tree (Assets/Models/Bedroom_Stuffs/Pumpkin_Tree.fbx, the tree the rooftop
  grows at Pumpkin_Tree_Pos when the Lid breaks), fully grown: the FBX's grow animation at its last frame is baked into
  a plain mesh (ST_Pumpkin_Tree). Its look is the game's: Pumpkin_Tree_Mat (albedo + normal + emission x2.2) and the
  orange maple leaves (pumpkin_leaves.png, alpha), a warm light like its Rooftop_Light (1, 0.654, 0.429).
- Pumpkins growing on it: the game's tree pumpkin (pumpkin_1.fbx, Bedroom_Set_1 atlas), TREE_PUMPKINS of them hanging
  under branch tips (ST_Tree_Pumpkin_*).
- The Pumpkin Boss (Assets/Blender/Pumpkin_Boss_Z.blend, only read) looming behind the back wall: body darkened
  (BOSS_DIM) so it sits back, the yellow of its eyes and mouth glowing (BOSS_GLOW), a cool moon rim light and a small
  warm light in its face.
- The menace pumpkins turn PUMP_TURN of the way toward the knight (closing in, faces still to the camera), and the hero
  pumpkin / some small ones move (HERO_AT, SMALL_AT).
- The rooftop as it is in the game now (ROOF_FROM_UNITY): Unity's Rooftop_Base_Z.fbx with its walls up and the rebuilt
  Lid, with the Unity materials' textures (cursed-stone floor with its glow, pit, walls); the old rooftop is hidden.
- Texture paths of the other PC are pointed at this checkout first (fix_paths.py).
- Spiders (the game's, in their attack wind-up: front legs raised like the boss's fists) below the boss (SPIDERS), and
  the big pumpkin's eyes and mouth glow brighter than the small ones' (HERO_GLOW, its own material copies).
- Two square icon cameras on the knight's helmet (CAM_Icon_Shortcut_512x512 PNG, CAM_Icon_App_184x184 JPG), and the
  library logo camera gets trim = True (render_all.py crops its transparent margins).
"""
import bpy, os, sys, math, importlib.util
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.path.dirname(bpy.data.filepath)
ASSETS = os.path.normpath(os.path.join(HERE, "..", "..", "Assets"))

# ---- the Tree of Light (the game's Pumpkin_Tree)
TREE_FBX = "Models/Bedroom_Stuffs/Pumpkin_Tree.fbx"
TREE_TEX = "M_Textures/Pumpkin_Tree/Pumpkin_Tree_Pumpkin_Tree_"          # + AlbedoTransparency / Normal / Emission .png
LEAVES_TEX = "Images/pumpkin_leaves.png"
TREE_AT = (-0.55, -1.30, 0.0)        # where its trunk stands (world; the knight is at about (-0.27, 0.13)): behind him
TREE_HEIGHT = 1.05                   # top of the leaves above the floor
TREE_YAW = 180.0                     # degrees about Z; 180 turns the carved face toward the cameras (at +Y)
TREE_EXACT = {"loc": (-0.5658897757530212, -1.2723162174224854, 0.051950450986623764),   # the user's own placement
              "rot": (89.99998201391065, 0.0, 285.9588469620068),                       # (2026-11-09: turned ~106 deg,
              "scale": 0.001873383647762239}                                             # raised 5 cm); None = use above
TREE_EMISSION = 2.2                  # Pumpkin_Tree_Mat's _EmissionColor in the game
LEAF_EMISSION = 0.15                 # a little so the leaves don't go black in the night
TREE_LIGHT = (1.0, 0.654, 0.429)     # the tree's Rooftop_Light colour in the game
TREE_LIGHT_W = 30.0                  # its power (W), in the canopy
TREE_FACE_LIGHT_W = 4.0              # a small warm light just in front of the trunk's carved face
# ---- pumpkins on the tree (the game's tree pumpkin)
PUMPKIN_FBX = "Models/Bedroom_Stuffs/pumpkin_1.fbx"
PUMPKIN_TEX = "M_Textures/BedRoom_Set_1/NEW/Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_"   # + AlbedoTransparency / Normal
TREE_PUMPKINS = 7
TREE_PUMPKIN_SIZE = 0.075            # height of each
# ---- the Pumpkin Boss
BOSS_BLEND = "Blender/Pumpkin_Boss_Z.blend"
BOSS_AT = (0.05, -3.6, -1.90)        # where its rig stands at first (feet below the floor) ...
BOSS_FACE_AT = (-0.20296, -4.29108, 1.09834)   # ... (the user moved it 1 m further back, 2026-11-09; was -0.10, -3.3, 1.12) then it is moved so its FACE (the rig's Face bone) is here: over the back
                                     # wall, between the logo and the tree in the Main capsule (None = keep BOSS_AT)
BOSS_SCALE = 1.05
BOSS_YAW = 180.0                     # faces the cameras
BOSS_ACTION = "pumpkin_boss_transform"   # its pose: this animation of the boss file, at BOSS_FRAME (baked into a mesh)
BOSS_FRAME = 100                     # transform at 100 = both pumpkin fists raised high (None/"" action = rest pose)
BOSS_DIM = 0.35                      # body colour x this (sits back in the dark)
BOSS_GLOW = 8.0                      # glow of the yellow in its eyes and mouth
BOSS_EYE_W = 6.0                     # warm light in front of its face
BOSS_RIM_W = 500.0                   # cool moon rim from above behind
# ---- menace pumpkins
HERO_AT = (0.40, 0.36, 0.0)          # the big one, closing in on the knight (None = leave it)
SMALL_AT = {"3": (-0.72, -0.05, 0.0), "4": (-0.62, -0.62, 0.0), "5": (0.75, -0.45, 0.0)}
PUMP_TURN = 0.35                     # 0 = faces stay as they were (to the camera), 1 = fully toward the knight
# ---- the rooftop as it is in the game now (user 2026-11-09: "check rooftop floor in unity prefab, use the new
# texture"): Unity's Rooftop_Base_Z.fbx (cursed-stone floor, pit, walls, battlements, pebbles) and the rebuilt Lid
# (Rooftop_Base_Z_Lid.fbx), with the textures and glow of their Unity materials. They replace the old rooftop of this
# file (Rooftop_Base_Z / Rooftop_Base_Z_Lid stay in the file, hidden from renders). Same model: the old one is 0.4274.
ROOF_FROM_UNITY = True               # False = back to the old rooftop
ROOF_FBX = "Models/Walls/Rooftop_Base_Z.fbx"
ROOF_POSE_FROM = "Rooftop_Base_Z_Armature"   # the new rooftop takes this file's old rooftop rig pose (same 9 bones):
                                     # the walls stand where they stood (the ones in front of the cameras down)
ROOF_SKIP = ("Gate", "Door_Set")     # parts left out: the back gate (the open gap as before, the boss behind it) and
                                     # the stepped pit door under the Lid (it pokes through the Lid)
ROOF_SCALE = 0.4274                  # the old rooftop's scale in this file
LID_FBX = "Models/Bedroom_Stuffs/Rooftop_Base_Z_Lid.fbx"
_RT = "M_Textures/Combos/Rock_Stylize_Set_1/"
ROOF_MATS = {   # from Assets/Materials/Rooftop_*_Mat.mat (URP: emission colour x map; roughness = 1 - smoothness)
    "Rooftop_Cursed": {"albedo": _RT + "rooftop_cursed_albedo.png", "normal": _RT + "rooftop_cursed_normal.png",
                       "emission": _RT + "rooftop_cursed_emission.png", "emit": 2.2, "rough": 0.85},
    "Rooftop_Pit_Cursed": {"albedo": _RT + "rooftop_pit_cursed_albedo.png",
                           "emission": _RT + "rooftop_pit_cursed_emission.png", "emit": 2.2, "rough": 0.85},
    "Rooftop_Wall": {"albedo": _RT + "rooftop_wall_albedo.png", "normal": _RT + "rooftop_wall_normal.png", "rough": 0.88},
    "Bedroom_Metal": {"albedo": "M_Textures/Combos/Metal_Stylize_Set_2/medieval_metal_albedo_set2.png",
                      "normal": "M_Textures/Combos/Metal_Stylize_Set_2/medieval_metal_normal_set2.png", "rough": 0.7,
                      "metal": 0.6},
    "Rooftop_Base_Lid": {"albedo": "M_Textures/rooftop_lid_cursed_albedo.png",
                         "normal": "M_Textures/rooftop_lid_cursed_normal.png",
                         "emission": "M_Textures/rooftop_lid_cursed_emission.png", "emit": 1.8, "rough": 0.5},
}
# ---- Steam icons (render_all.py renders every CAM_ camera): the knight's helmet close up
# (w, h, format, camera distance, clean backdrop?) -- the user tried the "clean" dark backdrop for the Steam icons and
# went back to the knight in front of the scene ("I like it better that way", 2026-11-09)
ICONS = {"CAM_Icon_Shortcut_512x512": (512, 512, "PNG", None, False),    # Steam "Shortcut Icon": 256 or 512 square, PNG
         "CAM_Icon_App_184x184": (184, 184, "JPG", None, False),          # Steam "App Icon": 184 x 184 JPG
         # YouTube profile picture (user 2026-11-09): 800 x 800, shown as a CIRCLE -> pulled back so the round crop
         # keeps the whole helmet; the scene look like the Steam icons, plus a clean alternative
         "CAM_Promo_YouTube_Profile_800x800": (800, 800, "PNG", 0.90, False),
         "CAM_Promo_YouTube_Profile_Clean_800x800": (800, 800, "PNG", 0.90, True)}
# ---- spiders rearing up below the boss (user 2026-11-09: "add few spider rising their front legs, do the same pose as
# the pumpkin_boss_Z, right below the giant boss"): the game spider (Assets/Blender/Spider_Enemy_NEW.blend, only read),
# its Spider_Attack wind-up (both front legs high) baked per spider; its front is -Y, so yaw 180 faces the cameras
SPIDER_BLEND = "Blender/Spider_Enemy_NEW.blend"
SPIDERS = [   # (body variant, action, frame, floor spot (x, y), extra yaw deg (0 = facing the knight), size)
    # around the boss, small (user: "make them spider smaller"), more of them, EVERY leg down on the floor (user: "add
    # more spiders, make all them leg get low and touch the floor"): only poses whose 8 feet all reach the floor
    # (spider_ground_scan: the standing start of Spider_Attack, the low dodges, the end of the claw slams), and some
    # behind the boss (user: "get some of them behind the Pumpkin_Boss_Z")
    # in front of him, in the back gate's opening
    ("Spider_Smol", "Spider_Attack", 0, (-0.62, -3.55), 8.0, 0.48),
    ("Spider_Smol_2", "Spider_Dodge_Left", 2, (-0.15, -3.30), -6.0, 0.44),
    ("Spider_Smol_3", "Spider_Attack_2", 40, (0.30, -3.55), -10.0, 0.46),
    ("Spider_Smol", "Spider_Dodge_Right", 2, (0.76, -3.30), 12.0, 0.42),
    ("Spider_Smol_2", "Spider_Attack_3", 70, (0.08, -3.90), -18.0, 0.42),
    # behind him, peeking out round his legs and sides
    ("Spider_Smol_3", "Spider_Attack", 0, (-0.95, -4.85), 25.0, 0.46),
    ("Spider_Smol", "Spider_Dodge_Back", 32, (-0.10, -5.00), -5.0, 0.44),
    ("Spider_Smol_2", "Spider_Dodge_Left", 2, (0.45, -4.80), -25.0, 0.42),
    ("Spider_Smol_3", "Spider_Dodge_Right", 2, (-0.55, -5.25), 10.0, 0.40),
]
SPIDER_KEY_W = 30.0                  # warm light in front of them (as if from the pumpkins' glow), low
SPIDER_RIM_W = 160.0                  # cool moon rim from behind and above (their raised legs in silhouette)
# ---- the big pumpkin beside the knight glows like a lantern (user 2026-11-09): its own copies of the eye / mouth /
# socket materials (object-linked, so the small pumpkins sharing the mesh keep theirs) with a stronger "Glow"
HERO_GLOW = {"MP_Pumpkin_Pupil": 2.6, "MP_Pumpkin_Mouth_Glow": 2.4, "MP_Pumpkin_Socket": 0.45}
HERO_GLOW_LIGHT_W = 1.2              # a small warm light just in front of its face (the glow spills on its cheeks / floor)
# ---- the big pumpkin beside the knight = the user's ORIGINAL game pumpkin face, burning (user 2026-11-09: "use my
# original Pumpkin's eye and mouth shape ... just make it glow, with cool burning effect"): pumpkin_0 from
# Assets/Blender/Knight_429.blend (only read) takes the menace pumpkin's place (same frame), its own carved pits
# (eyes, nose, mouth) burn (flickering fire inside), and flames lick up from their edges (a fire card in front of the
# face, made like the logo's green fire but orange: a distance-from-the-carving texture + rising noise)
HERO_ORIGINAL_FACE = True
PUMPKIN_SRC = "Blender/Knight_429.blend"     # object "pumpkin_0"
PIT_FIRE_W = 6.0                     # emission of the fire inside the pits (only deep down and low: see below)
PIT_GLOW_DEPTH = (0.35, 0.95)        # user: "still need a black part in that eye": the pit stays BLACK near its rim and
PIT_GLOW_LOW = 1.3                   # in its upper part; the fire burns deep inside (depth from .. to, of the pit)
                                     # and low down (higher = the glow keeps lower), like a candle inside
# the EYES (user 2026-11-09: "a dark black eye then a burning fire as the pupil"): all black, a round burning pupil in
# the middle (the mouth and nose keep the deep, low glow above)
PUPIL_SIZE = 0.40                    # pupil radius, as a fraction of the eye's half size
PUPIL_SOFT = 0.16                    # its soft edge
PUPIL_DROP = 0.08                    # pupil centre a little below the eye's centre (fraction of the eye's height)
PUPIL_W = 9.0                        # its glow
FLAME_W = 5.0                        # emission of the flames licking out
FLAME_REACH = 0.045                  # how far (pumpkin metres, unscaled) the flames reach out from the carving's edge
FLAME_TOP = 0.16                     # room above the face for the flames (unscaled metres)
FLAME_NOISE = (5.0, 1.6)             # flame field scale across / up the card: low across = few broad tongues
FLAME_RISE = 0.34                    # how far up the tongues are pulled (fraction of the card): flames go UP only
FLAME_CORE_POW = 5.0                 # sharpness of the flame body (higher = thinner)
FLAME_SOFT = 0.0                     # soft glow all round the openings (0 = none: keeps the carving crisp)
FLAME_CUT = 0.3                      # flame values below this are dropped (no faint orange haze over the skin)
FLAME_CARD = False                   # the flat flame card above (tried 2026-11-09: it hazed over the face); off
# cartoon flame tongues rising from the top edge of each opening + embers drifting up (pumpkin units, unscaled)
TONGUES = {"eye": (0, 0.075, 0.030), "mouth": (0, 0.090, 0.032), "nose": (0, 0.045, 0.020)}   # count, height, width
# (tongues off: user 2026-11-09 "that's too much")
TONGUE_LEAN = 0.35                   # how much they lean out of the face (toward the viewer) as they rise
TONGUE_W = 6.0                       # their glow
EMBERS = 16                          # glowing sparks drifting up above the pumpkin
EMBER_W = 8.0
# ---- promo pictures (not Steam sizes; user 2026-11-09: "good art to promote our game to reddit tonight"): made like
# the Steam cameras (own logo, framed in lighting_pass.py), rendered by render_all.py with the rest
PROMO = {"CAM_Promo_KeyArt_1920x1080": (1920, 1080),       # 16:9, big in a Reddit post / anywhere
         "CAM_Promo_Portrait_1080x1350": (1080, 1350),     # 4:5, fills a phone in the Reddit app
         # YouTube channel banner (user 2026-11-09): 2560 x 1440; TVs show it all, desktop a 2560 x 423 strip through
         # the middle, phones only the centre 1235 x 338 -> logo + pumpkin + knight inside that centre
         "CAM_Promo_YouTube_Banner_2560x1440": (2560, 1440)}
# the knight's armour colour map in the renders (user 2026-11-09 picked the improved one: shaded, warmer gold, the
# shamrocks on the helmet and chest un-stretched at 70 %): assets/knight/Knight_Armour_Set_Diffuse_v2.png, made by
# assets/knight/improve_knight_texture.py. "original" = the game texture. The game files are never changed.
KNIGHT_TEXTURE = "v2"
ICONS_3D_SKIP = True                 # user 2026-11-09: the icons are THEIR painted badge (make_badge_icons.py writes
                                     # Icon_* and the YouTube profile); these 3D icon cameras are kept but not rendered
ICON_TARGET = (-0.27, 0.13, 0.30)    # the middle of his helmet
ICON_DIST = 0.62                     # camera distance (50 mm lens): the whole helmet fits, a little room around it
ICON_UP = 0.12                       # camera a little above, looking slightly down

KNIGHT = "Pie_Knight_Armature"
KNIGHT_POSE_FILE = "knight_pose_user.json"   # the user's hand pose (bones + sword / shield), restored on every run
COL = "ST_Story"


def log(*a):
    print("[story_pass]", *a)


def asset(rel):
    return os.path.join(ASSETS, rel)


def load_image(rel, non_color=False):
    p = asset(rel)
    im = bpy.data.images.load(p, check_existing=True)
    try:
        im.filepath = bpy.path.relpath(p)
    except ValueError:
        pass
    if non_color:
        im.colorspace_settings.name = "Non-Color"
    return im


def fix_paths():
    spec = importlib.util.spec_from_file_location("fix_paths", os.path.join(HERE, "fix_paths.py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m.fix(verbose=False)


def clear():
    col = bpy.data.collections.get(COL)
    if col:
        for o in list(col.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(col)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.armatures, bpy.data.actions):
        for d in list(coll):
            if d.name.startswith("ST_") and d.users == 0:
                coll.remove(d)
    col = bpy.data.collections.new(COL)
    bpy.context.scene.collection.children.link(col)
    return col


def import_baked(fbx, name, col):
    """Imports an FBX, bakes its armature's last animation frame into a plain mesh, deletes the rest."""
    before = set(bpy.data.objects)
    acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=asset(fbx))
    new = [o for o in bpy.data.objects if o not in before]
    new_acts = [a for a in bpy.data.actions if a not in acts_before]
    sc = bpy.context.scene
    keep_frame = sc.frame_current
    end = int(max((a.frame_range[1] for a in new_acts), default=keep_frame))
    sc.frame_set(end)
    dg = bpy.context.evaluated_depsgraph_get()
    meshes = [o for o in new if o.type == "MESH"]
    out = []
    for i, o in enumerate(meshes):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        me.name = name + ("" if i == 0 else "_%d" % i) + "_Mesh"
        ob = bpy.data.objects.new(name if i == 0 else "%s_%d" % (name, i), me)
        ob.matrix_world = o.matrix_world.copy()
        col.objects.link(ob)
        out.append(ob)
    sc.frame_set(keep_frame)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in new_acts:
        bpy.data.actions.remove(a)
    return out


def world_bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)]))


def place(obj, at, height, yaw):
    """Scale obj to `height`, turn it by yaw about Z, stand its lowest point on at.z, centred on at.xy."""
    obj.rotation_euler = (0, 0, 0)
    obj.location = (0, 0, 0)
    obj.scale = (1, 1, 1)
    obj.matrix_world = Matrix.Identity(4) @ obj.matrix_world
    bpy.context.view_layer.update()
    lo, hi = world_bounds([obj])
    s = height / max(1e-6, hi.z - lo.z)
    obj.scale = obj.scale * s
    obj.rotation_euler = (obj.rotation_euler.x, obj.rotation_euler.y, math.radians(yaw))
    bpy.context.view_layer.update()
    lo, hi = world_bounds([obj])
    # centre the trunk base (the lowest 15 % of the mesh) on at.xy
    me = obj.data
    zs = [(obj.matrix_world @ v.co) for v in me.vertices]
    zmin = min(p.z for p in zs)
    base = [p for p in zs if p.z < zmin + (hi.z - lo.z) * 0.15] or zs
    c = sum(base, Vector()) / len(base)
    obj.location = obj.location + Vector((at[0] - c.x, at[1] - c.y, at[2] - zmin))
    bpy.context.view_layer.update()
    return s


def principled(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    return m, nt, next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")


def tex(nt, image, x, y):
    n = nt.nodes.new("ShaderNodeTexImage")
    n.image = image
    n.location = (x, y)
    return n


def tree_materials():
    m, nt, b = principled("ST_Pumpkin_Tree_Mat")
    alb = tex(nt, load_image(TREE_TEX + "AlbedoTransparency.png"), -700, 300)
    nt.links.new(alb.outputs["Color"], b.inputs["Base Color"])
    nrm = tex(nt, load_image(TREE_TEX + "Normal.png", True), -700, -100)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.location = (-350, -100)
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    emi = tex(nt, load_image(TREE_TEX + "Emission.png"), -700, -450)
    nt.links.new(emi.outputs["Color"], b.inputs["Emission Color"])
    b.inputs["Emission Strength"].default_value = TREE_EMISSION
    b.inputs["Roughness"].default_value = 0.8
    lm, lnt, lb = principled("ST_Pumpkin_Leaves_Mat")
    lt = tex(lnt, load_image(LEAVES_TEX), -500, 200)
    lnt.links.new(lt.outputs["Color"], lb.inputs["Base Color"])
    lnt.links.new(lt.outputs["Alpha"], lb.inputs["Alpha"])
    lnt.links.new(lt.outputs["Color"], lb.inputs["Emission Color"])
    lb.inputs["Emission Strength"].default_value = LEAF_EMISSION
    lb.inputs["Roughness"].default_value = 0.6
    return m, lm


def pumpkin_material():
    m, nt, b = principled("ST_Tree_Pumpkin_Mat")
    alb = tex(nt, load_image(PUMPKIN_TEX + "AlbedoTransparency.png"), -700, 300)
    nt.links.new(alb.outputs["Color"], b.inputs["Base Color"])
    nrm = tex(nt, load_image(PUMPKIN_TEX + "Normal.png", True), -700, -100)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.location = (-350, -100)
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.55
    return m


def point_light(name, col, at, colour, watts, radius=0.1):
    L = bpy.data.objects.new(name, bpy.data.lights.new(name, "POINT"))
    L.data.color = colour
    L.data.energy = watts
    L.data.shadow_soft_size = radius
    L.location = at
    col.objects.link(L)
    return L


def add_tree(col):
    got = import_baked(TREE_FBX, "ST_Pumpkin_Tree", col)
    tree = got[0]
    for extra in got[1:]:                # (the tree FBX is one mesh; anything else is joined by parenting)
        extra.parent = tree
    bark, leaves = tree_materials()
    for s in tree.material_slots:
        s.material = leaves if (s.material and "lea" in s.material.name.lower()) else bark
    for i, s in enumerate(tree.data.materials):
        tree.data.materials[i] = leaves if (s and "lea" in s.name.lower()) else bark
    s = place(tree, TREE_AT, TREE_HEIGHT, TREE_YAW)
    turn = 0.0
    if TREE_EXACT:
        tree.location = TREE_EXACT["loc"]
        tree.rotation_euler = [math.radians(a) for a in TREE_EXACT["rot"]]
        tree.scale = (TREE_EXACT["scale"],) * 3
        turn = math.radians(TREE_EXACT["rot"][2] - TREE_YAW)
        bpy.context.view_layer.update()
    lo, hi = world_bounds([tree])
    base = trunk_base(tree)
    log("tree: scale %.4f, trunk at %s, bounds %s .. %s" % (tree.scale[0], tuple(round(v, 2) for v in base),
                                                          tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi)))
    # lights: warm in the canopy (the game's Rooftop_Light) and a little one on the carved face (turns with the tree)
    point_light("ST_Tree_Light", col, Vector((base.x, base.y, hi.z * 0.72)), TREE_LIGHT, TREE_LIGHT_W, 0.25)
    face = Matrix.Rotation(turn, 3, "Z") @ Vector((0.0, 0.35, 0.0))
    point_light("ST_Tree_Face_Light", col, Vector((base.x + face.x, base.y + face.y, hi.z * 0.38)), TREE_LIGHT,
                TREE_FACE_LIGHT_W, 0.05)
    return tree, bark


def trunk_base(tree):
    """Middle of the trunk where it meets the floor (lowest 15 % of the mesh)."""
    pts = [tree.matrix_world @ v.co for v in tree.data.vertices]
    zmin = min(p.z for p in pts)
    zmax = max(p.z for p in pts)
    low = [p for p in pts if p.z < zmin + (zmax - zmin) * 0.15] or pts
    c = sum(low, Vector()) / len(low)
    return Vector((c.x, c.y, zmin))


def branch_tips(tree, bark, n):
    """n spread-out points under the outer branches (bark faces high up and far from the trunk)."""
    me = tree.data
    idx = [i for i, m in enumerate(me.materials) if m == bark]
    mw = tree.matrix_world
    lo, hi = world_bounds([tree])
    tb = trunk_base(tree)
    axis = Vector((tb.x, tb.y, 0))
    vs = set()
    for p in me.polygons:
        if p.material_index in idx:
            vs.update(p.vertices)
    pts = [mw @ me.vertices[i].co for i in vs]
    H = hi.z - lo.z
    rmax = max(((p - axis).to_2d().length for p in pts), default=1)
    cand = [p for p in pts if p.z > lo.z + H * 0.50 and (p - axis).to_2d().length > rmax * 0.45]
    if not cand:
        return []
    # prefer the side toward the cameras (+Y) so they show
    cand.sort(key=lambda p: -p.y)
    chosen = [cand[0]]
    while len(chosen) < n:
        best = max(cand, key=lambda p: min((p - q).length for q in chosen) + 0.15 * (p.y - tb.y))
        if min((best - q).length for q in chosen) < 0.05:
            break
        chosen.append(best)
    return chosen


def add_tree_pumpkins(col, tree, bark):
    got = import_baked(PUMPKIN_FBX, "ST_Tree_Pumpkin_Src", col)
    src = got[0]
    for extra in got[1:]:
        bpy.data.objects.remove(extra, do_unlink=True)
    m = pumpkin_material()
    src.data.materials.clear()
    src.data.materials.append(m)
    lo, hi = world_bounds([src])
    s = TREE_PUMPKIN_SIZE / max(1e-6, hi.z - lo.z)
    tips = branch_tips(tree, bark, TREE_PUMPKINS)
    for i, p in enumerate(tips):
        o = bpy.data.objects.new("ST_Tree_Pumpkin_%d" % i, src.data)
        o.matrix_world = src.matrix_world.copy()
        o.scale = o.scale * s
        o.rotation_euler.z += math.radians((i * 137.0) % 360)
        col.objects.link(o)
        bpy.context.view_layer.update()
        olo, ohi = world_bounds([o])
        c = (olo + ohi) / 2
        o.location = o.location + Vector((p.x - c.x, p.y - c.y, (p.z - TREE_PUMPKIN_SIZE * 0.35) - ohi.z))
    bpy.data.objects.remove(src, do_unlink=True)
    log("tree pumpkins:", len(tips))


def add_boss(col):
    path = asset(BOSS_BLEND)
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in ("pumpkin_boss", "pumpkin_boss_armature")]
        dst.actions = [n for n in src.actions if n == BOSS_ACTION]
    got = [o for o in dst.objects if o]
    for o in got:
        o.name = "ST_" + o.name
        col.objects.link(o)
    fix_paths()
    arm = next(o for o in got if o.type == "ARMATURE")
    src_body = next(o for o in got if o.type == "MESH")
    arm.location = BOSS_AT
    arm.scale = (BOSS_SCALE,) * 3
    arm.rotation_euler = (0, 0, math.radians(BOSS_YAW))
    # the pose: its animation at BOSS_FRAME, baked into a plain mesh (the scene's own frame stays where it is)
    act = next((a for a in dst.actions if a), None)
    sc = bpy.context.scene
    keep = sc.frame_current
    if act is not None:
        ad = arm.animation_data or arm.animation_data_create()
        ad.action = act
        if act.slots:
            ad.action_slot = act.slots[0]
        sc.frame_set(int(BOSS_FRAME))
    bpy.context.view_layer.update()
    fb = arm.pose.bones.get("Face") or arm.pose.bones.get("Head")
    face_now = (arm.matrix_world @ ((fb.head + fb.tail) * 0.5)) if fb else None
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src_body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.name = "ST_Boss_Mesh"
    body = bpy.data.objects.new("ST_Pumpkin_Boss", me)
    body.matrix_world = src_body.matrix_world.copy()
    col.objects.link(body)
    sc.frame_set(keep)
    if BOSS_FACE_AT is not None and face_now is not None:
        body.location = body.location + (Vector(BOSS_FACE_AT) - face_now)
        face_now = Vector(BOSS_FACE_AT)
    bpy.data.objects.remove(src_body, do_unlink=True)
    bpy.data.objects.remove(arm, do_unlink=True)
    if act is not None:
        bpy.data.actions.remove(act)
    bpy.context.view_layer.update()
    for sl in body.material_slots:
        if not sl.material:
            continue
        m = sl.material.copy()
        m.name = "ST_Boss_Mat"
        sl.material = m
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        alb = next((l.from_node for l in nt.links if l.to_socket == b.inputs["Base Color"]), None)
        if alb is None:
            continue
        # glow where the texture is yellow (eyes, mouth): min(R,G) - B
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(alb.outputs["Color"], sep.inputs[0])
        mn = nt.nodes.new("ShaderNodeMath"); mn.operation = "MINIMUM"
        nt.links.new(sep.outputs[0], mn.inputs[0]); nt.links.new(sep.outputs[1], mn.inputs[1])
        sub = nt.nodes.new("ShaderNodeMath"); sub.operation = "SUBTRACT"
        nt.links.new(mn.outputs[0], sub.inputs[0]); nt.links.new(sep.outputs[2], sub.inputs[1])
        ma = nt.nodes.new("ShaderNodeMath"); ma.operation = "MULTIPLY_ADD"; ma.use_clamp = True
        nt.links.new(sub.outputs[0], ma.inputs[0]); ma.inputs[1].default_value = 6.0; ma.inputs[2].default_value = -1.8
        mu = nt.nodes.new("ShaderNodeMath"); mu.operation = "MULTIPLY"
        nt.links.new(ma.outputs[0], mu.inputs[0]); mu.inputs[1].default_value = BOSS_GLOW
        nt.links.new(alb.outputs["Color"], b.inputs["Emission Color"])
        nt.links.new(mu.outputs[0], b.inputs["Emission Strength"])
        mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"
        mx.inputs[0].default_value = 1.0
        nt.links.new(alb.outputs["Color"], mx.inputs[6]); mx.inputs[7].default_value = (BOSS_DIM, BOSS_DIM, BOSS_DIM, 1)
        nt.links.new(mx.outputs[2], b.inputs["Base Color"])
    bpy.context.view_layer.update()
    lo, hi = world_bounds([body])
    face = face_now if face_now is not None else Vector(((lo.x + hi.x) / 2, hi.y, hi.z - (hi.z - lo.z) * 0.13))
    point_light("ST_Boss_Face_Light", col, face + Vector((0, 0.45 * BOSS_SCALE, 0)), (1.0, 0.55, 0.1), BOSS_EYE_W, 0.05)
    rim = bpy.data.objects.new("ST_Boss_Rim", bpy.data.lights.new("ST_Boss_Rim", "AREA"))
    rim.data.color = (0.55, 0.65, 1.0)
    rim.data.energy = BOSS_RIM_W
    rim.data.size = 2.0
    rim.location = face + Vector((0, -2.5, 1.7)) * BOSS_SCALE
    rim.rotation_euler = (rim.location - face).to_track_quat("Z", "Y").to_euler()
    col.objects.link(rim)
    log("boss: top %.2f, face at %s" % (hi.z, tuple(round(v, 2) for v in face)))


def turn_pumpkins():
    knight = bpy.data.objects[KNIGHT]
    if HERO_AT is not None and bpy.data.objects.get("Menace_Pumpkin_Root"):
        bpy.data.objects["Menace_Pumpkin_Root"].location = HERO_AT
    for k, v in SMALL_AT.items():
        o = bpy.data.objects.get("Menace_Pumpkin_Small_%s_Root" % k)
        if o:
            o.location = v
    base = {}
    for o in bpy.data.objects:
        if o.name.startswith("Menace_Pumpkin") and o.name.endswith("Root") and o.parent is None \
                and o.name != "Menace_Pumpkin_Small_Root":
            # start from the yaw they had before any story pass (kept on the object) so re-runs don't keep turning
            if "st_base_yaw" not in o:
                o["st_base_yaw"] = o.rotation_euler.z
            cur = o["st_base_yaw"]
            d = knight.location - o.location
            d.z = 0
            if d.length < 0.05:
                continue
            want = math.atan2(d.x, -d.y)          # their face is local -Y
            dz = (want - cur + math.pi) % (2 * math.pi) - math.pi
            o.rotation_euler.z = cur + dz * PUMP_TURN
            base[o.name] = round(math.degrees(o.rotation_euler.z), 1)
    log("pumpkins turned:", base)


def roof_material(name):
    spec = ROOF_MATS.get(name)
    m = bpy.data.materials.get("ST_Roof_" + name)
    if m is not None:
        return m
    m, nt, b = principled("ST_Roof_" + name)
    if spec is None:
        return m
    alb = tex(nt, load_image(spec["albedo"]), -700, 300)
    nt.links.new(alb.outputs["Color"], b.inputs["Base Color"])
    if spec.get("normal"):
        nrm = tex(nt, load_image(spec["normal"], True), -700, -100)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.location = (-350, -100)
        nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    if spec.get("emission"):
        em = tex(nt, load_image(spec["emission"]), -700, -450)
        nt.links.new(em.outputs["Color"], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = spec.get("emit", 1.0)
    b.inputs["Roughness"].default_value = spec.get("rough", 0.8)
    b.inputs["Metallic"].default_value = spec.get("metal", 0.0)
    return m


def bake_import(fbx, action_suffix, keep, pose_from=None):
    """Imports an FBX, puts each animated object on its NAME|action_suffix take at that take's last frame (or, with
    pose_from, clears the takes and copies that armature's current bone pose onto the imported armature), and
    returns [(name, baked mesh, world matrix, material names)] for the meshes keep(name) accepts; the import is deleted."""
    before = set(bpy.data.objects)
    acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=asset(fbx))
    new = [o for o in bpy.data.objects if o not in before]
    new_acts = [a for a in bpy.data.actions if a not in acts_before]
    sc = bpy.context.scene
    keep_frame = sc.frame_current
    end = 1
    for o in new:
        a = next((x for x in new_acts if x.name == "%s|%s" % (o.name, action_suffix)), None) if action_suffix else None
        if a is not None:
            ad = o.animation_data or o.animation_data_create()
            ad.action = a
            if a.slots:
                ad.action_slot = a.slots[0]
            end = max(end, int(a.frame_range[1]))
        elif o.animation_data and (action_suffix or pose_from is not None):
            o.animation_data.action = None
    if pose_from is not None:
        end = keep_frame
        for o in new:
            if o.type != "ARMATURE":
                continue
            copied = 0
            for pb in o.pose.bones:
                src = pose_from.pose.bones.get(pb.name)
                if src is not None:
                    pb.rotation_mode = src.rotation_mode
                    pb.matrix_basis = src.matrix_basis.copy()
                    copied += 1
            log("rooftop pose: %d of %d bones copied from %s" % (copied, len(o.pose.bones), pose_from.name))
    sc.frame_set(end)
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    for o in new:
        if o.type != "MESH" or not keep(o.name):
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        out.append((o.name, me, o.matrix_world.copy(), [sl.material.name if sl.material else None for sl in o.material_slots]))
    sc.frame_set(keep_frame)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in new_acts:
        bpy.data.actions.remove(a)
    return out, end


def mesh_bounds(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return (Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)]))


def add_rooftop(col):
    old_roof = bpy.data.objects.get("Rooftop_Base_Z")
    old_lid = bpy.data.objects.get("Rooftop_Base_Z_Lid")
    for o in (old_roof, old_lid):
        if o is not None:
            o.hide_render = ROOF_FROM_UNITY
    if not ROOF_FROM_UNITY:
        return
    S = Matrix.Scale(ROOF_SCALE, 4)
    parts, frame = bake_import(ROOF_FBX, None, lambda n: not any(k in n for k in ROOF_SKIP),
                               pose_from=bpy.data.objects.get(ROOF_POSE_FROM))
    for name, me, mw, mats in parts:
        me.name = "ST_Roof_" + name + "_Mesh"
        ob = bpy.data.objects.new("ST_Roof_" + name, me)
        ob.matrix_world = S @ mw
        for i, mn in enumerate(mats):
            if i < len(me.materials):
                me.materials[i] = roof_material(mn.split(".")[0]) if mn else None
        col.objects.link(ob)
    log("rooftop from Unity: %d parts: %s" % (len(parts), ", ".join(n for n, _, _, _ in parts)))
    # the Lid: same place, turn, size and top height as the old one
    lids, _ = bake_import(LID_FBX, None, lambda n: True)
    if not lids:
        return
    name, me, mw, mats = lids[0]
    me.name = "ST_Roof_Lid_Mesh"
    lid = bpy.data.objects.new("ST_Roof_Lid", me)
    lid.matrix_world = mw
    me.materials.clear()
    me.materials.append(roof_material("Rooftop_Base_Lid"))
    col.objects.link(lid)
    bpy.context.view_layer.update()
    if old_lid is not None:
        olo, ohi = mesh_bounds(old_lid)
        nlo, nhi = mesh_bounds(lid)
        k = ((ohi.x - olo.x) + (ohi.y - olo.y)) / max(1e-6, (nhi.x - nlo.x) + (nhi.y - nlo.y))
        yaw = old_lid.matrix_world.to_euler().z
        nc = Vector(((nlo.x + nhi.x) / 2, (nlo.y + nhi.y) / 2, nhi.z))
        oc = Vector(((olo.x + ohi.x) / 2, (olo.y + ohi.y) / 2, ohi.z))
        lid.matrix_world = (Matrix.Translation(oc) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Scale(k, 4)
                            @ Matrix.Translation(-nc) @ lid.matrix_world)
        log("lid: scale x%.3f, turned %.0f deg, centre %s" % (k, math.degrees(yaw), tuple(round(v, 3) for v in oc)))


def icon_cameras():
    """Square cameras on the knight's helmet for Steam's two icons (made once, re-aimed on each run)."""
    cams = bpy.data.collections.get("Cameras") or bpy.context.scene.collection
    main = bpy.data.objects.get("CAM_Main_Capsule_1232x706")
    t = Vector(ICON_TARGET)
    d = ((main.matrix_world.translation - t) if main else Vector((0.2, 1.0, 0.0)))
    d.z = 0
    d = d.normalized()
    for name, spec in ICONS.items():
        w, h, fmt = spec[:3]
        dist = spec[3] if len(spec) > 3 and spec[3] else ICON_DIST
        clean = bool(spec[4]) if len(spec) > 4 else False
        c = bpy.data.objects.get(name)
        if c is None:
            c = bpy.data.objects.new(name, bpy.data.cameras.new(name))
            cams.objects.link(c)
        c.data.lens = 50.0
        c.data.clip_start = 0.01
        c.location = t + d * dist + Vector((0, 0, ICON_UP * dist / ICON_DIST))
        c.rotation_euler = (t - c.location).to_track_quat("-Z", "Y").to_euler()
        c["steam_w"], c["steam_h"] = w, h
        c["logo"], c["hide_chars"], c["transparent"] = False, False, False
        c["format"] = fmt
        c["clean_backdrop"] = clean      # render_all.py: only the knight seen, on a dark backdrop with a soft green glow
        c["skip"] = ICONS_3D_SKIP        # the icons are the user's painted badge now (make_badge_icons.py)
    lg = bpy.data.objects.get("CAM_Library_Logo_1280x720")
    if lg:
        lg["trim"] = True                # render_all.py: cut the transparent margins, logo fills 1280 or 720
        lg["render_scale"] = 1.35        # rendered bigger first, so the trimmed logo is only ever scaled down (sharp)
    log("icon cameras:", list(ICONS))


def floor_z(x, y):
    """Height of the rooftop floor at (x, y) (ray straight down onto the rooftop meshes); 0 if nothing is there."""
    dg = bpy.context.evaluated_depsgraph_get()
    origin, down = Vector((x, y, 3.0)), Vector((0.0, 0.0, -1.0))
    for _ in range(12):
        hit, loc, nrm, idx, ob, mw = bpy.context.scene.ray_cast(dg, origin, down)
        if not hit:
            return 0.0
        # only the rooftop that RENDERS: the old one is still in the file (hidden from renders) and its floor is higher
        if ob.name.startswith("ST_Roof_" if ROOF_FROM_UNITY else "Rooftop_Base") and not ob.hide_render:
            return loc.z
        origin = loc + down * 0.002
    return 0.0


def add_spiders(col):
    path = asset(SPIDER_BLEND)
    variants = sorted({sp[0] for sp in SPIDERS})
    actions = sorted({sp[1] for sp in SPIDERS})
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in variants or n == "Spider_Armature"]
        dst.actions = [n for n in src.actions if n in actions]
    got = [o for o in dst.objects if o]
    for o in got:
        col.objects.link(o)
    fix_paths()
    arm = next(o for o in got if o.type == "ARMATURE")
    acts = {a.name.split(".")[0]: a for a in dst.actions if a}
    ad = arm.animation_data or arm.animation_data_create()
    knight = bpy.data.objects[KNIGHT].matrix_world.translation
    sc = bpy.context.scene
    keep = sc.frame_current
    made = []
    for i, (variant, action, frame, spot, extra, size) in enumerate(SPIDERS):
        body = next((o for o in got if o.name.startswith(variant) and o.type == "MESH"
                     and o.name.split(".")[0] == variant), None)
        act = acts.get(action)
        if body is None or act is None:
            continue
        ad.action = act
        if act.slots:
            ad.action_slot = act.slots[0]
        # facing the knight (front is -Y: yaw 180 turns it to +Y), plus a little of its own
        dx, dy = knight.x - spot[0], knight.y - spot[1]
        yaw = math.degrees(math.atan2(-dx, dy)) + extra
        sc.frame_set(int(frame))
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        me.name = "ST_Spider_%d_Mesh" % i
        ob = bpy.data.objects.new("ST_Spider_%d" % i, me)
        rel = arm.matrix_world.inverted() @ body.matrix_world
        ob.matrix_world = (Matrix.Translation((spot[0], spot[1], 0.0)) @ Matrix.Rotation(math.radians(180.0 + yaw), 4, "Z")
                           @ Matrix.Scale(size, 4) @ rel)
        col.objects.link(ob)
        bpy.context.view_layer.update()
        zmin = min((ob.matrix_world @ v.co).z for v in me.vertices)
        # its feet on the floor: the floor under its body (the lowest vertex sits on it)
        bpy.context.view_layer.update()
        pts = [ob.matrix_world @ v.co for v in me.vertices]
        cx = sum(p.x for p in pts) / len(pts)
        cy = sum(p.y for p in pts) / len(pts)
        ob.location.z += floor_z(cx, cy) - zmin
        made.append(ob)
    sc.frame_set(keep)
    for o in got:
        bpy.data.objects.remove(o, do_unlink=True)
    for a_ in acts.values():
        bpy.data.actions.remove(a_)
    if made:
        lo, hi = world_bounds(made)
        c = (lo + hi) / 2
        if SPIDER_KEY_W > 0:
            point_light("ST_Spider_Key", col, Vector((c.x, hi.y + 0.7, 0.45)), (1.0, 0.55, 0.2), SPIDER_KEY_W, 0.3)
        if SPIDER_RIM_W > 0:
            rim = bpy.data.objects.new("ST_Spider_Rim", bpy.data.lights.new("ST_Spider_Rim", "AREA"))
            rim.data.color = (0.6, 0.72, 1.0)
            rim.data.energy = SPIDER_RIM_W
            rim.data.size = 0.8
            rim.location = Vector((c.x, lo.y - 0.9, 0.9))
            rim.rotation_euler = (rim.location - c).to_track_quat("Z", "Y").to_euler()
            col.objects.link(rim)
    log("spiders:", len(made), [tuple(round(v, 2) for v in o.location) for o in made])


def hero_glow(col):
    body = bpy.data.objects.get("Menace_Pumpkin_Body")
    if body is None:
        return
    for i, slot in enumerate(body.material_slots):
        base = body.data.materials[i] if i < len(body.data.materials) else None
        old = slot.material if slot.link == "OBJECT" else None
        slot.link = "DATA"
        if old is not None and old.name.startswith("ST_Hero_") and old.users == 0:
            bpy.data.materials.remove(old)
        if base is None or base.name not in HERO_GLOW:
            continue
        copy = base.copy()
        copy.name = "ST_Hero_" + base.name
        g = copy.node_tree.nodes.get("Glow")
        if g:
            g.outputs[0].default_value = HERO_GLOW[base.name]
        slot.link = "OBJECT"
        slot.material = copy
    if HERO_GLOW_LIGHT_W > 0:
        bpy.context.view_layer.update()
        lo, hi = world_bounds([body])
        knight = bpy.data.objects[KNIGHT]
        c = (lo + hi) / 2
        cam = bpy.data.objects.get("CAM_Main_Capsule_1232x706")
        d = ((cam.matrix_world.translation - c) if cam else Vector((0, 1, 0)))
        d.z = 0
        d = d.normalized()
        point_light("ST_Hero_Face_Light", col, c + d * ((hi.y - lo.y) * 0.75) + Vector((0, 0, (hi.z - lo.z) * 0.05)),
                    (1.0, 0.5, 0.12), HERO_GLOW_LIGHT_W, 0.08)
    log("hero pumpkin glow:", HERO_GLOW)


def knight_texture():
    """Puts the chosen armour colour map into the knight's material (the image node feeding its colour)."""
    m = bpy.data.materials.get("Knight_Armour_Sword_Shield")
    if m is None:
        return
    if KNIGHT_TEXTURE == "v2":
        path = os.path.join(HERE, "assets", "knight", "Knight_Armour_Set_Diffuse_v2.png")
    else:
        path = asset("M_Textures/Knight/Knight_Sword_Shield_Armor/Knight_Armour_Set_Diffuse.png")
    img = bpy.data.images.load(path, check_existing=True)
    img.reload()
    try:
        img.filepath = bpy.path.relpath(path)
    except ValueError:
        pass
    n_ = 0
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image and n.image.name.startswith("Knight_Armour_Set_Diffuse"):
            n.image = img
            n_ += 1
    log("knight texture: %s (%d node)" % (os.path.basename(path), n_))


def keep_user_pose():
    """The user posed the knight by hand (2026-11-09); stepping through frames (the bakes above) can move a bone, so
    their pose (knight_pose_user.json, saved from their file) is put back at the end of every run."""
    path = os.path.join(HERE, KNIGHT_POSE_FILE)
    if not KNIGHT_POSE_FILE or not os.path.exists(path):
        return
    import json
    d = json.load(open(path))
    arm = bpy.data.objects[KNIGHT]
    n = 0
    for name, m in d.get("bones", {}).items():
        pb = arm.pose.bones.get(name)
        if pb is not None:
            pb.matrix_basis = Matrix(m)
            n += 1
    for name, t in d.get("objects", {}).items():
        o = bpy.data.objects.get(name)
        if o is not None:
            o.rotation_mode = t["rotation_mode"]
            o.location, o.rotation_euler, o.scale = t["location"], t["rotation_euler"], t["scale"]
            o.hide_render = t["hide_render"]
    bpy.context.view_layer.update()
    log("knight: the user's pose put back (%d bones, from %s)" % (n, d.get("source")))


def _pit_faces(bm):
    """The carved face pits of pumpkin_0 (as menace_pumpkin_build.py finds them): faces the outer skin can't reach
    without crossing a sharp rim (front half), plus the shallow nose pit."""
    import bmesh
    outer = {max(bm.faces, key=lambda f: f.calc_center_median().y).index}
    st = list(outer)
    while st:
        f = bm.faces[st.pop()]
        for e in f.edges:
            if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle()) < 55.0:
                for g in e.link_faces:
                    if g.index not in outer:
                        outer.add(g.index)
                        st.append(g.index)
    nose = [(-0.027, 0.155), (0.041, 0.155), (0.040, 0.212), (0.019, 0.240), (-0.006, 0.240), (-0.024, 0.212)]

    def in_poly(p, poly):
        x, y = p
        ins = False
        j = len(poly) - 1
        for i in range(len(poly)):
            xi, yi = poly[i]
            xj, yj = poly[j]
            if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi:
                ins = not ins
            j = i
        return ins
    pits = set()
    for f in bm.faces:
        c = f.calc_center_median()
        if c.y > 0.0 or c.z > 0.34:
            continue
        if f.index not in outer or (c.y < -0.2 and in_poly((c.x, c.z), nose)):
            pits.add(f.index)
    return pits


def _rim_loops(bm, pits):
    """The carving outlines: edges between a pit face and the skin, chained into loops; returned as (x, z) polygons."""
    rim = [e for e in bm.edges if len(e.link_faces) == 2 and
           ((e.link_faces[0].index in pits) != (e.link_faces[1].index in pits))]
    adj = {}
    for e in rim:
        a_, b_ = e.verts
        adj.setdefault(a_.index, []).append(b_.index)
        adj.setdefault(b_.index, []).append(a_.index)
    seen, loops = set(), []
    for start in list(adj):
        if start in seen:
            continue
        loop, prev, cur = [start], None, start
        seen.add(start)
        while True:
            nxt = [v for v in adj[cur] if v != prev and v not in seen]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            seen.add(cur)
            loop.append(cur)
        if len(loop) >= 4:
            loops.append([(bm.verts[i].co.x, bm.verts[i].co.z, bm.verts[i].co.y) for i in loop])
    return loops


def _flame_sdf(loops, bb, W=384):
    import numpy as np
    x0, z0, x1, z1 = bb
    H = max(8, int(W * (z1 - z0) / (x1 - x0)))
    xs = x0 + (np.arange(W) + 0.5) / W * (x1 - x0)
    zs = z0 + (np.arange(H) + 0.5) / H * (z1 - z0)
    X, Z = np.meshgrid(xs, zs)
    P = np.stack([X.ravel(), Z.ravel()], 1)
    d = np.full(len(P), 1e9)
    inside = np.zeros(len(P), bool)
    for L_ in loops:
        A = np.array([[q[0], q[1]] for q in L_])
        B = np.roll(A, -1, 0)
        ins = np.zeros(len(P), bool)
        for (ax, az), (bx, bz) in zip(A, B):
            ab = np.array([bx - ax, bz - az])
            ap = P - np.array([ax, az])
            t = np.clip((ap @ ab) / max(1e-12, ab @ ab), 0, 1)
            q = np.array([ax, az]) + np.outer(t, ab)
            d = np.minimum(d, np.hypot(P[:, 0] - q[:, 0], P[:, 1] - q[:, 1]))
            if abs(bz - az) > 1e-12:
                c = (az > P[:, 1]) != (bz > P[:, 1])
                xi = ax + (P[:, 1] - az) * (bx - ax) / (bz - az)
                ins ^= c & (P[:, 0] < xi)
        inside |= ins
    din = np.where(inside, d, 0.0)
    d[inside] = 0.0
    v = np.clip(d / FLAME_REACH, 0.0, 1.0).reshape(H, W)
    gi = np.clip(din / 0.02, 0.0, 1.0).reshape(H, W)
    img = bpy.data.images.get("ST_Hero_Flame_SDF")
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new("ST_Hero_Flame_SDF", W, H, alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels = np.dstack([v, gi, np.zeros_like(v), np.ones_like(v)]).ravel().astype(np.float32)
    img.pack()
    return img


def _fire_ramp(nt, x, y, amount_socket):
    cr = nt.nodes.new("ShaderNodeValToRGB")
    cr.location = (x, y)
    el = cr.color_ramp.elements
    el[0].position, el[0].color = 0.0, (0.35, 0.02, 0.0, 1)
    el[1].position, el[1].color = 1.0, (1.0, 0.85, 0.35, 1)
    e = el.new(0.45)
    e.color = (1.0, 0.28, 0.02, 1)
    e = el.new(0.75)
    e.color = (1.0, 0.55, 0.08, 1)
    nt.links.new(amount_socket, cr.inputs["Fac"])
    return cr


def pit_fire_material(eyes=()):
    m = bpy.data.materials.new("ST_Hero_Pit_Fire")
    nt = m.node_tree
    nt.nodes.clear()
    L_ = nt.links.new
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (380, 0)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    tc.location = (-900, 0)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.location = (-700, 0)
    mp.inputs["Scale"].default_value = (30.0, 30.0, 9.0)          # tall cells: flame streaks rising
    L_(tc.outputs["Object"], mp.inputs["Vector"])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.location = (-500, 0)
    nz.inputs["Detail"].default_value = 6.0
    nz.inputs["Roughness"].default_value = 0.6
    nz.inputs["Distortion"].default_value = 0.8
    L_(mp.outputs["Vector"], nz.inputs["Vector"])
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.location = (-300, 0)
    mr.inputs["From Min"].default_value = 0.3
    mr.inputs["From Max"].default_value = 0.72
    mr.inputs["To Min"].default_value = 0.25
    L_(nz.outputs["Fac"], mr.inputs["Value"])
    # where it burns: deep in the pit and low in the opening (vertex attributes from hero_original_face)
    ad = nt.nodes.new("ShaderNodeAttribute"); ad.attribute_name = "pit_depth"; ad.location = (-700, -350)
    av = nt.nodes.new("ShaderNodeAttribute"); av.attribute_name = "pit_v"; av.location = (-700, -550)
    dm = nt.nodes.new("ShaderNodeMapRange"); dm.location = (-480, -350)
    dm.inputs["From Min"].default_value, dm.inputs["From Max"].default_value = PIT_GLOW_DEPTH
    L_(ad.outputs["Fac"], dm.inputs["Value"])
    lowv = nt.nodes.new("ShaderNodeMath"); lowv.operation = "SUBTRACT"; lowv.inputs[0].default_value = 1.0; lowv.use_clamp = True
    L_(av.outputs["Fac"], lowv.inputs[1])
    lowp = nt.nodes.new("ShaderNodeMath"); lowp.operation = "POWER"; lowp.inputs[1].default_value = PIT_GLOW_LOW
    L_(lowv.outputs[0], lowp.inputs[0])
    mask = nt.nodes.new("ShaderNodeMath"); mask.operation = "MULTIPLY"; mask.location = (-260, -400)
    L_(dm.outputs["Result"], mask.inputs[0]); L_(lowp.outputs[0], mask.inputs[1])
    # mouth / nose: the deep, low glow; eyes: black but a round pupil of fire
    ae = nt.nodes.new("ShaderNodeAttribute"); ae.attribute_name = "pit_eye"; ae.location = (-700, -750)
    not_eye = nt.nodes.new("ShaderNodeMath"); not_eye.operation = "SUBTRACT"; not_eye.inputs[0].default_value = 1.0
    L_(ae.outputs["Fac"], not_eye.inputs[1])
    other = nt.nodes.new("ShaderNodeMath"); other.operation = "MULTIPLY"
    L_(mask.outputs[0], other.inputs[0]); L_(not_eye.outputs[0], other.inputs[1])
    sxz = nt.nodes.new("ShaderNodeSeparateXYZ"); L_(tc.outputs["Object"], sxz.inputs[0])
    pupil = None
    for (cx, cz, rx, rz) in eyes:
        dx = nt.nodes.new("ShaderNodeMath"); dx.operation = "MULTIPLY_ADD"
        dx.inputs[1].default_value = 1.0 / rx; dx.inputs[2].default_value = -cx / rx
        L_(sxz.outputs["X"], dx.inputs[0])
        dz = nt.nodes.new("ShaderNodeMath"); dz.operation = "MULTIPLY_ADD"
        dz.inputs[1].default_value = 1.0 / rz; dz.inputs[2].default_value = -cz / rz
        L_(sxz.outputs["Z"], dz.inputs[0])
        cmb = nt.nodes.new("ShaderNodeCombineXYZ"); L_(dx.outputs[0], cmb.inputs["X"]); L_(dz.outputs[0], cmb.inputs["Y"])
        ln = nt.nodes.new("ShaderNodeVectorMath"); ln.operation = "LENGTH"; L_(cmb.outputs[0], ln.inputs[0])
        sm = nt.nodes.new("ShaderNodeMapRange"); sm.interpolation_type = "SMOOTHSTEP"
        sm.inputs["From Min"].default_value = PUPIL_SIZE + PUPIL_SOFT / 2
        sm.inputs["From Max"].default_value = PUPIL_SIZE - PUPIL_SOFT / 2      # inside -> 1, outside -> 0
        L_(ln.outputs["Value"], sm.inputs["Value"])
        if pupil is None:
            pupil = sm.outputs["Result"]
        else:
            mx = nt.nodes.new("ShaderNodeMath"); mx.operation = "MAXIMUM"
            L_(pupil, mx.inputs[0]); L_(sm.outputs["Result"], mx.inputs[1])
            pupil = mx.outputs[0]
    eye_fire = nt.nodes.new("ShaderNodeMath"); eye_fire.operation = "MULTIPLY"
    if pupil is not None:
        L_(pupil, eye_fire.inputs[0])
    else:
        eye_fire.inputs[0].default_value = 0.0
    L_(ae.outputs["Fac"], eye_fire.inputs[1])
    eye_k = nt.nodes.new("ShaderNodeMath"); eye_k.operation = "MULTIPLY"; eye_k.inputs[1].default_value = PUPIL_W / max(0.01, PIT_FIRE_W)
    L_(eye_fire.outputs[0], eye_k.inputs[0])
    both = nt.nodes.new("ShaderNodeMath"); both.operation = "MAXIMUM"
    L_(other.outputs[0], both.inputs[0]); L_(eye_k.outputs[0], both.inputs[1])
    # flicker: the pupil's core stays hot (brighter in its middle)
    heat = nt.nodes.new("ShaderNodeMath"); heat.operation = "MULTIPLY"; heat.location = (-80, -300)
    L_(both.outputs[0], heat.inputs[0]); L_(mr.outputs["Result"], heat.inputs[1])
    hc = nt.nodes.new("ShaderNodeMath"); hc.operation = "MINIMUM"; hc.inputs[1].default_value = 1.0
    L_(heat.outputs[0], hc.inputs[0])
    cr = _fire_ramp(nt, 120, 0, hc.outputs[0])
    L_(cr.outputs["Color"], em.inputs["Color"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.location = (300, -200)
    mul.inputs[1].default_value = PIT_FIRE_W
    L_(heat.outputs[0], mul.inputs[0])
    L_(mul.outputs[0], em.inputs["Strength"])
    # black charred walls where it does not burn
    bk = nt.nodes.new("ShaderNodeBsdfPrincipled"); bk.location = (380, 250)
    bk.inputs["Base Color"].default_value = (0.012, 0.006, 0.004, 1)
    bk.inputs["Roughness"].default_value = 0.9
    ad2 = nt.nodes.new("ShaderNodeAddShader"); ad2.location = (500, 0)
    L_(bk.outputs[0], ad2.inputs[0]); L_(em.outputs[0], ad2.inputs[1])
    L_(ad2.outputs[0], out.inputs["Surface"])
    return m


def flame_card_material(img, bb):
    """Orange flames rising from the carving's edges (the logo's green fire, in fire colours): distance texture sampled a
    little below each pixel by a stretched noise field, tight core + soft falloff; alpha = flame (camera rays only)."""
    x0, z0, x1, z1 = bb
    m = bpy.data.materials.new("ST_Hero_Flames")
    nt = m.node_tree
    nt.nodes.clear()
    L_ = nt.links.new
    N = nt.nodes.new
    out = N("ShaderNodeOutputMaterial")
    tc = N("ShaderNodeTexCoord")
    # card coordinates 0..1 from its object space (x across, z up)
    sub = N("ShaderNodeVectorMath"); sub.operation = "SUBTRACT"; sub.inputs[1].default_value = (x0, 0.0, z0)
    L_(tc.outputs["Object"], sub.inputs[0])
    mul = N("ShaderNodeVectorMath"); mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = (1.0 / (x1 - x0), 0.0, 1.0 / (z1 - z0))
    L_(sub.outputs[0], mul.inputs[0])
    s0 = N("ShaderNodeSeparateXYZ"); L_(mul.outputs[0], s0.inputs[0])
    uv = N("ShaderNodeCombineXYZ"); L_(s0.outputs["X"], uv.inputs["X"]); L_(s0.outputs["Z"], uv.inputs["Y"])
    # flame field: noise stretched vertically
    mp = N("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (FLAME_NOISE[0], FLAME_NOISE[1], 1.0)
    L_(uv.outputs[0], mp.inputs["Vector"])
    fn = N("ShaderNodeTexNoise"); fn.inputs["Detail"].default_value = 5.0
    fn.inputs["Roughness"].default_value = 0.62; fn.inputs["Distortion"].default_value = 0.7
    L_(mp.outputs["Vector"], fn.inputs["Vector"])
    fr = N("ShaderNodeMapRange"); fr.inputs["From Min"].default_value = 0.40; fr.inputs["From Max"].default_value = 0.70
    L_(fn.outputs["Fac"], fr.inputs["Value"])
    wob = N("ShaderNodeTexNoise"); wob.inputs["Scale"].default_value = 9.0; wob.inputs["Detail"].default_value = 3.0
    L_(uv.outputs[0], wob.inputs["Vector"])
    du = N("ShaderNodeMath"); du.operation = "MULTIPLY_ADD"; du.inputs[1].default_value = 0.03; du.inputs[2].default_value = -0.015
    L_(wob.outputs["Fac"], du.inputs[0])
    dv = N("ShaderNodeMath"); dv.operation = "MULTIPLY"; dv.inputs[1].default_value = -FLAME_RISE  # look below -> tongues rise
    L_(fr.outputs["Result"], dv.inputs[0])
    sx = N("ShaderNodeSeparateXYZ"); L_(uv.outputs[0], sx.inputs[0])
    u2 = N("ShaderNodeMath"); u2.operation = "ADD"; L_(sx.outputs["X"], u2.inputs[0]); L_(du.outputs[0], u2.inputs[1])
    v2 = N("ShaderNodeMath"); v2.operation = "ADD"; L_(sx.outputs["Y"], v2.inputs[0]); L_(dv.outputs[0], v2.inputs[1])
    cm = N("ShaderNodeCombineXYZ"); L_(u2.outputs[0], cm.inputs[0]); L_(v2.outputs[0], cm.inputs[1])
    tx = N("ShaderNodeTexImage"); tx.image = img; tx.extension = "EXTEND"; tx.interpolation = "Cubic"
    L_(cm.outputs[0], tx.inputs["Vector"])
    sc_ = N("ShaderNodeSeparateColor"); L_(tx.outputs["Color"], sc_.inputs[0])
    inv = N("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    L_(sc_.outputs["Red"], inv.inputs[1])
    core = N("ShaderNodeMath"); core.operation = "POWER"; core.inputs[1].default_value = FLAME_CORE_POW; L_(inv.outputs[0], core.inputs[0])
    soft = N("ShaderNodeMath"); soft.operation = "POWER"; soft.inputs[1].default_value = 3.0; L_(inv.outputs[0], soft.inputs[0])
    fm = N("ShaderNodeMapRange"); fm.inputs["To Min"].default_value = 0.2; L_(fr.outputs["Result"], fm.inputs["Value"])
    sm0 = N("ShaderNodeMath"); sm0.operation = "MULTIPLY"; L_(soft.outputs[0], sm0.inputs[0]); L_(fm.outputs["Result"], sm0.inputs[1])
    sm = N("ShaderNodeMath"); sm.operation = "MULTIPLY"; sm.inputs[1].default_value = FLAME_SOFT; L_(sm0.outputs[0], sm.inputs[0])
    cmul = N("ShaderNodeMath"); cmul.operation = "MULTIPLY"; L_(core.outputs[0], cmul.inputs[0]); L_(fm.outputs["Result"], cmul.inputs[1])
    amt = N("ShaderNodeMath"); amt.operation = "ADD"; amt.use_clamp = True
    L_(cmul.outputs[0], amt.inputs[0]); L_(sm.outputs[0], amt.inputs[1])
    # nothing ON the carving itself (the pit fire shows there): fade out over the inside of the openings
    cut = N("ShaderNodeMapRange"); cut.inputs["From Min"].default_value = FLAME_CUT; cut.inputs["From Max"].default_value = 1.0
    L_(amt.outputs[0], cut.inputs["Value"])
    hole = N("ShaderNodeMath"); hole.operation = "SUBTRACT"; hole.inputs[0].default_value = 1.0
    L_(sc_.outputs["Green"], hole.inputs[1])
    a2 = N("ShaderNodeMath"); a2.operation = "MULTIPLY"; L_(cut.outputs["Result"], a2.inputs[0]); L_(hole.outputs[0], a2.inputs[1])
    cr = _fire_ramp(nt, 0, 0, a2.outputs[0])
    em = N("ShaderNodeEmission"); L_(cr.outputs["Color"], em.inputs["Color"])
    st = N("ShaderNodeMath"); st.operation = "MULTIPLY"; st.inputs[1].default_value = FLAME_W
    L_(a2.outputs[0], st.inputs[0]); L_(st.outputs[0], em.inputs["Strength"])
    tr = N("ShaderNodeBsdfTransparent")
    mix = N("ShaderNodeMixShader"); L_(a2.outputs[0], mix.inputs["Fac"])
    L_(tr.outputs[0], mix.inputs[1]); L_(em.outputs[0], mix.inputs[2])
    L_(mix.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass
    return m


def _tongue_mesh(name, h, w, bend, seg=12, rings=10):
    """A cartoon flame tongue: a teardrop revolved round +Z (wide low, pointed top), flattened front-to-back, the tip
    curling sideways by `bend`. Base at the origin."""
    import bmesh
    bm = bmesh.new()
    rows = []
    for i in range(rings + 1):
        t = i / rings
        r = w * (math.sin(math.pi * min(1.0, t * 1.05)) ** 0.7) * ((1.0 - t) ** 0.55 + 0.02)
        z = h * t
        off = bend * h * t * t
        row = []
        for k in range(seg):
            a_ = 2 * math.pi * k / seg
            row.append(bm.verts.new((off + r * math.cos(a_), 0.55 * r * math.sin(a_), z)))
        rows.append(row)
    for i in range(rings):
        for k in range(seg):
            bm.faces.new((rows[i][k], rows[i][(k + 1) % seg], rows[i + 1][(k + 1) % seg], rows[i + 1][k]))
    bot = bm.verts.new((0.0, 0.0, 0.0))
    for k in range(seg):
        bm.faces.new((rows[0][(k + 1) % seg], rows[0][k], bot))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p_ in me.polygons:
        p_.use_smooth = True
    return me


def tongue_material():
    """Hot yellow-white at the base, orange then red to the tip (by height), edges fading out (facing), glowing."""
    m = bpy.data.materials.get("ST_Hero_Tongue")
    if m is not None:
        bpy.data.materials.remove(m)
    m = bpy.data.materials.new("ST_Hero_Tongue")
    nt = m.node_tree
    nt.nodes.clear()
    N, L_ = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    tc = N("ShaderNodeTexCoord")
    sx = N("ShaderNodeSeparateXYZ"); L_(tc.outputs["Generated"], sx.inputs[0])   # Generated Z: 0 base .. 1 tip
    cr = N("ShaderNodeValToRGB")
    el = cr.color_ramp.elements
    el[0].position, el[0].color = 0.0, (1.0, 0.9, 0.55, 1)
    el[1].position, el[1].color = 1.0, (0.9, 0.08, 0.0, 1)
    e = el.new(0.35); e.color = (1.0, 0.6, 0.08, 1)
    e = el.new(0.7); e.color = (1.0, 0.28, 0.02, 1)
    L_(sx.outputs["Z"], cr.inputs["Fac"])
    lw = N("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.5
    edge = N("ShaderNodeMath"); edge.operation = "SUBTRACT"; edge.inputs[0].default_value = 1.0
    L_(lw.outputs["Facing"], edge.inputs[1])
    ep = N("ShaderNodeMath"); ep.operation = "POWER"; ep.inputs[1].default_value = 1.6; L_(edge.outputs[0], ep.inputs[0])
    tip = N("ShaderNodeMath"); tip.operation = "SUBTRACT"; tip.inputs[0].default_value = 1.15
    L_(sx.outputs["Z"], tip.inputs[1])
    a_ = N("ShaderNodeMath"); a_.operation = "MULTIPLY"; a_.use_clamp = True
    L_(ep.outputs[0], a_.inputs[0]); L_(tip.outputs[0], a_.inputs[1])
    em = N("ShaderNodeEmission"); L_(cr.outputs["Color"], em.inputs["Color"])
    st = N("ShaderNodeMath"); st.operation = "MULTIPLY"; st.inputs[1].default_value = TONGUE_W
    L_(a_.outputs[0], st.inputs[0]); L_(st.outputs[0], em.inputs["Strength"])
    tr = N("ShaderNodeBsdfTransparent")
    mx = N("ShaderNodeMixShader"); L_(a_.outputs[0], mx.inputs["Fac"]); L_(tr.outputs[0], mx.inputs[1]); L_(em.outputs[0], mx.inputs[2])
    L_(mx.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass
    return m


def ember_material():
    m = bpy.data.materials.get("ST_Hero_Ember")
    if m is not None:
        bpy.data.materials.remove(m)
    m = bpy.data.materials.new("ST_Hero_Ember")
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (1.0, 0.45, 0.06, 1)
    em.inputs["Strength"].default_value = EMBER_W
    nt.links.new(em.outputs[0], out.inputs["Surface"])
    return m


def add_tongues_and_embers(col, ob, loops):
    import random
    rnd = random.Random(429)
    mat = tongue_material()
    # classify the carving outlines: the 2 highest-up = eyes, the widest = mouth, the rest = nose
    info = []
    for L_ in loops:
        xs = [q[0] for q in L_]; zs = [q[1] for q in L_]
        info.append({"loop": L_, "w": max(xs) - min(xs), "cz": sum(zs) / len(zs)})
    mouth = max(info, key=lambda d: d["w"])
    rest = sorted([d for d in info if d is not mouth], key=lambda d: -d["cz"])
    for d in rest:
        d["kind"] = "eye" if rest.index(d) < 2 else "nose"
    mouth["kind"] = "mouth"
    n_t = 0
    for d in info:
        count, h, w = TONGUES[d["kind"]]
        L_ = d["loop"]
        xs = [q[0] for q in L_]
        x0, x1 = min(xs), max(xs)
        for j in range(count):
            # spots along the TOP edge of the opening (the highest rim vertex near each x)
            xt = x0 + (x1 - x0) * (j + 0.5) / count + rnd.uniform(-0.15, 0.15) * (x1 - x0) / count
            near = sorted(L_, key=lambda q: abs(q[0] - xt))[:max(3, len(L_) // 8)]
            top = max(near, key=lambda q: q[1])
            hh = h * rnd.uniform(0.75, 1.25)
            me = _tongue_mesh("ST_Hero_Tongue_%d_Mesh" % n_t, hh, w * rnd.uniform(0.8, 1.2), rnd.uniform(-0.35, 0.35))
            me.materials.append(mat)
            t = bpy.data.objects.new("ST_Hero_Tongue_%d" % n_t, me)
            col.objects.link(t)
            t.parent = ob
            # base a little inside the opening's top edge, leaning out of the face (-Y local) as it rises
            t.location = (top[0], top[2] + 0.004, top[1] - 0.012)
            t.rotation_euler = (TONGUE_LEAN, rnd.uniform(-0.25, 0.25), 0.0)
            for attr in ("visible_diffuse", "visible_glossy", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
                setattr(t, attr, False)
            n_t += 1
    # embers: tiny glowing sparks drifting up above the face and the stem
    em_mat = ember_material()
    src = bpy.data.meshes.get("ST_Hero_Ember_Mesh")
    if src is None:
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
        src = bpy.data.meshes.new("ST_Hero_Ember_Mesh")
        bm.to_mesh(src)
        bm.free()
        src.materials.append(em_mat)
    for i in range(EMBERS):
        e = bpy.data.objects.new("ST_Hero_Ember_%d" % i, src)
        col.objects.link(e)
        e.parent = ob
        r = rnd.uniform(0.0015, 0.0045)
        e.scale = (r, r, r * rnd.uniform(1.0, 2.2))
        hgt = rnd.uniform(0.0, 1.0)
        e.location = (rnd.gauss(0.0, 0.07 + 0.10 * hgt), rnd.uniform(-0.30, -0.12), 0.30 + 0.45 * hgt)
        for attr in ("visible_diffuse", "visible_glossy", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
            setattr(e, attr, False)
    log("flames: %d tongues, %d embers" % (n_t, EMBERS))


def hero_original_face(col):
    """pumpkin_0 (the user's game pumpkin, original face) in the menace pumpkin's place, its pits burning."""
    import bmesh
    menace = bpy.data.objects.get("Menace_Pumpkin_Body")
    if menace is None:
        return
    menace.hide_render = HERO_ORIGINAL_FACE
    if not HERO_ORIGINAL_FACE:
        return
    with bpy.data.libraries.load(asset(PUMPKIN_SRC), link=False) as (src, dst):
        dst.objects = ["pumpkin_0"]
    src_ob = dst.objects[0]
    fix_paths()
    me = src_ob.data.copy()
    me.name = "ST_Hero_Pumpkin_Mesh"
    bpy.data.objects.remove(src_ob, do_unlink=True)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()
    pits = _pit_faces(bm)
    loops = _rim_loops(bm, pits)
    # how deep each pit vertex sits below the skin (vs the nearest rim vertex, from the body centre) and how high up in
    # its opening it is: the fire burns deep and low, the rest of the pit stays black
    Cc = Vector((0.0, -0.005, 0.19))
    rim_pts = [(Vector((q[0], q[2], q[1])), li) for li, L_ in enumerate(loops) for q in L_]
    zr = [(min(q[1] for q in L_), max(q[1] for q in L_)) for L_ in loops]
    dl = bm.verts.layers.float.new("pit_depth")
    vl = bm.verts.layers.float.new("pit_v")
    pit_verts = {v for f in bm.faces if f.index in pits for v in f.verts}
    for v in pit_verts:
        rp, li = min(rim_pts, key=lambda t: (t[0] - v.co).length_squared)
        depth = (rp - Cc).length - (v.co - Cc).length
        v[dl] = max(0.0, min(1.0, depth / 0.03))
        z0, z1 = zr[li]
        v[vl] = max(0.0, min(1.0, (v.co.z - z0) / max(1e-6, z1 - z0)))
    # which carving is which: the widest = mouth; of the rest the two highest = eyes
    info = []
    for li, L_ in enumerate(loops):
        xs_ = [q[0] for q in L_]; zs_ = [q[1] for q in L_]
        info.append((li, max(xs_) - min(xs_), sum(zs_) / len(zs_), min(xs_), max(xs_), min(zs_), max(zs_)))
    mouth_i = max(info, key=lambda t: t[1])[0]
    eye_is = [t[0] for t in sorted([t for t in info if t[0] != mouth_i], key=lambda t: -t[2])[:2]]
    eyes = []
    for t in info:
        if t[0] in eye_is:
            rx, rz = (t[4] - t[3]) / 2, (t[6] - t[5]) / 2
            eyes.append(((t[3] + t[4]) / 2, (t[5] + t[6]) / 2 - PUPIL_DROP * 2 * rz, rx, rz))
    el = bm.verts.layers.float.new("pit_eye")
    bm.faces.ensure_lookup_table()                 # adding a layer re-allocates the vertices: gather them again
    pit_verts = {v for f in bm.faces if f.index in pits for v in f.verts}
    for v in pit_verts:
        rp, li = min(rim_pts, key=lambda t: (t[0] - v.co).length_squared)
        v[el] = 1.0 if li in eye_is else 0.0
    fire = pit_fire_material(eyes)
    me.materials.append(fire)
    fire_i = len(me.materials) - 1
    for f in bm.faces:
        if f.index in pits:
            f.material_index = fire_i
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("ST_Hero_Pumpkin", me)
    col.objects.link(ob)
    ob.parent = menace.parent
    ob.matrix_parent_inverse = menace.matrix_parent_inverse.copy()
    ob.matrix_basis = menace.matrix_basis.copy()
    add_tongues_and_embers(col, ob, loops)
    if not FLAME_CARD:
        log("hero pumpkin: the user's pumpkin_0 face, %d pit faces burning, %d carving outlines" % (len(pits), len(loops)))
        return
    # the flame card, in the pumpkin's own frame (face toward local -Y), just in front of the carving
    xs = [q[0] for L_ in loops for q in L_]
    zs = [q[1] for L_ in loops for q in L_]
    ys = [q[2] for L_ in loops for q in L_]
    bb = (min(xs) - 0.05, min(zs) - 0.03, max(xs) + 0.05, max(zs) + FLAME_TOP)
    img = _flame_sdf(loops, bb)
    cm = bpy.data.meshes.new("ST_Hero_Flames_Mesh")
    yf = min(ys) - 0.012
    cm.from_pydata([(bb[0], yf, bb[1]), (bb[2], yf, bb[1]), (bb[2], yf, bb[3]), (bb[0], yf, bb[3])], [], [(0, 1, 2, 3)])
    cm.materials.append(flame_card_material(img, bb))
    card = bpy.data.objects.new("ST_Hero_Flames", cm)
    col.objects.link(card)
    card.parent = ob
    for attr in ("visible_diffuse", "visible_glossy", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
        setattr(card, attr, False)
    log("hero pumpkin: the user's pumpkin_0 face, %d pit faces burning, %d carving outlines, flame card %s"
        % (len(pits), len(loops), tuple(round(v, 3) for v in bb)))


def promo_cameras():
    """Cameras + logos for the promo pictures: each gets a copy of the Main capsule's logo set (logo mesh, key light,
    glow light) under its own names; lighting_pass.py frames the camera and places / lights the logo."""
    cams = bpy.data.collections.get("Cameras") or bpy.context.scene.collection
    logos = bpy.data.collections.get("Logos")
    src = "LOGO_Main_Capsule_1232x706"
    for name, (w, h) in PROMO.items():
        c = bpy.data.objects.get(name)
        if c is None:
            c = bpy.data.objects.new(name, bpy.data.cameras.new(name))
            cams.objects.link(c)
        c.data.clip_start = 0.01
        c.data.sensor_fit = "AUTO"
        c["steam_w"], c["steam_h"] = w, h
        c["logo"], c["hide_chars"], c["transparent"] = True, False, False
        lg_name = "LOGO_" + name[4:]
        for suffix in ("", "_Key", "_Light"):
            if bpy.data.objects.get(lg_name + suffix) is not None or bpy.data.objects.get(src + suffix) is None:
                continue
            o = bpy.data.objects[src + suffix].copy()
            if o.type == "LIGHT":
                o.data = o.data.copy()
            o.name = lg_name + suffix
            (logos or bpy.context.scene.collection).objects.link(o)
            o.parent = c
    log("promo cameras:", list(PROMO))


def apply(save=True):
    log("texture paths fixed:", fix_paths())
    icon_cameras()
    promo_cameras()
    col = clear()
    add_rooftop(col)
    tree, bark = add_tree(col)
    add_tree_pumpkins(col, tree, bark)
    add_boss(col)
    add_spiders(col)
    turn_pumpkins()
    hero_glow(col)
    hero_original_face(col)
    knight_texture()
    keep_user_pose()
    if save:
        bpy.ops.wm.save_mainfile()
        log("saved", bpy.data.filepath)


if __name__ == "__main__":
    apply(save="nosave" not in ARGS)

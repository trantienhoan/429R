"""
uvpaint.py - helpers for painting the 429R game's textures in Blender, straight onto the models' existing UVs.

Load it inside Blender (the Blender MCP's execute_blender_code, or headless: blender -b file.blend --python job.py):

    exec(open(r"K:/Repository/429R/.claude/skills/blender-429r/scripts/uvpaint.py", encoding="utf-8").read())

Then, typically:

    img = bpy.data.images["Cockroach_Enemy_Atlas"]
    W, H = img.size
    objs = objects_using(img)                         # every mesh painted from this image
    lay = bake_layout(objs, W, H, face_filter=selected_faces)
    others = bake_layout(objs, W, H, face_filter=invert(selected_faces))
    col = pixels(img)
    paint = lay.mask & ~others.mask                   # only texels no other face uses
    col[paint, :3] = my_colour(lay.pos[paint], lay.nrm[paint])
    backup(bpy.path.abspath(img.filepath))
    write_image(img, dilate(col, paint, 6, blocked=lay.any_face))

Nothing here changes meshes or UVs, and nothing saves the .blend; write_image() writes the image's own file.
Arrays follow Blender's pixel layout: row 0 is the BOTTOM of the image (V = 0), so +rows = +V. Values of 8-bit
PNGs are the stored (sRGB) numbers, the same as an image editor shows.
"""
import os
import shutil
import time

import bmesh
import bpy
import numpy as np


# ---------------------------------------------------------------- meshes

def mesh_of(ob):
    """A bmesh of the object's mesh that also works in Edit Mode (ob.data's UVs read as empty there).
    Returns (bm, owned); call done(bm, owned) when finished."""
    if ob.data.is_editmode:
        return bmesh.from_edit_mesh(ob.data), False
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    return bm, True


def done(bm, owned):
    if owned:
        bm.free()


def objects_using(image):
    """Mesh objects with a material whose node tree shows this image (Image Texture nodes)."""
    found = []
    for ob in bpy.data.objects:
        if ob.type != 'MESH':
            continue
        for slot in ob.material_slots:
            mat = slot.material
            if mat is None or not mat.use_nodes:
                continue
            if any(n.type == 'TEX_IMAGE' and n.image == image for n in mat.node_tree.nodes):
                found.append(ob)
                break
    return found


# Face filters for bake_layout: (ob, bm, face) -> bool.

def selected_faces(ob, bm, f):
    return f.select


def all_faces(ob, bm, f):
    return True


def faces_with_material(name):
    def test(ob, bm, f):
        mats = ob.data.materials
        return f.material_index < len(mats) and mats[f.material_index] is not None and mats[f.material_index].name == name
    return test


def faces_in_group(*groups, min_weight=0.5):
    """Faces whose vertices are all in one of these vertex groups (weight >= min_weight). Name both mirror
    halves (e.g. "wing_lid.L", "wing_lid.R") when the halves share texels, or neither side can be painted alone."""
    def test(ob, bm, f):
        layer = bm.verts.layers.deform.active
        if layer is None:
            return False
        for group in groups:
            vg = ob.vertex_groups.get(group)
            if vg is not None and all(v[layer].get(vg.index, 0.0) >= min_weight for v in f.verts):
                return True
        return False
    return test


def any_of(*filters):
    def test(ob, bm, f):
        return any(flt(ob, bm, f) for flt in filters)
    return test


# ---------------------------------------------------------------- images

def pixels(img):
    """A copy of the image as an (H, W, 4) float32 array (row 0 = bottom)."""
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    return arr.reshape(h, w, 4)


def write_image(img, arr, path=None):
    """Puts the array into the image and saves its file (or to path, as PNG). The .blend is not saved."""
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.update()
    if path:
        img.filepath_raw = path
        img.file_format = 'PNG'
    img.save()
    return bpy.path.abspath(img.filepath_raw)


def new_image(name, w, h, color=(0.0, 0.0, 0.0, 0.0), non_color=False):
    """A new image (e.g. a height or normal map), filled with one colour."""
    img = bpy.data.images.new(name, w, h, alpha=True)
    if non_color:
        img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(np.tile(np.asarray(color, np.float32), w * h))
    return img


def backup(path, folder=None):
    """Copies a file (e.g. the texture PNG) aside with a time stamp before it's overwritten. Returns the copy's
    path. Default folder: K:/WORK_2026/08_04/_claude/backups (outside AppData: the Claude app is an MSIX package,
    so its AppData writes are invisible to other programs)."""
    folder = folder or r"K:/WORK_2026/08_04/_claude/backups"
    os.makedirs(folder, exist_ok=True)
    stem, ext = os.path.splitext(os.path.basename(path))
    copy = os.path.join(folder, f"{stem}_{time.strftime('%Y%m%d_%H%M%S')}{ext}")
    shutil.copy2(path, copy)
    return copy


# ---------------------------------------------------------------- rasterising UVs

def raster(tri_uv, W, H, tolerance=1e-3):
    """Texels whose centres are inside a UV triangle: (rows, cols, barycentric weights (n, 3)) or None."""
    xs = tri_uv[:, 0] * W
    ys = tri_uv[:, 1] * H
    x0, x1 = max(0, int(np.floor(xs.min()))), min(W - 1, int(np.ceil(xs.max())))
    y0, y1 = max(0, int(np.floor(ys.min()))), min(H - 1, int(np.ceil(ys.max())))
    if x1 < x0 or y1 < y0:
        return None
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, bx, cx), (ay, by, cy) = xs, ys
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-12:
        return None
    w1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / d
    w2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / d
    w3 = 1.0 - w1 - w2
    inside = (w1 >= -tolerance) & (w2 >= -tolerance) & (w3 >= -tolerance)
    if not inside.any():
        return None
    rows, cols = np.nonzero(inside)
    return rows + y0, cols + x0, np.stack([w1[inside], w2[inside], w3[inside]], 1)


class Layout:
    """What covers each texel: mask (H, W) bool; obj / face (H, W) int (-1 = none, else index into objects / face
    index); pos / nrm (H, W, 3) float32 (object space by default, or world); stacked (H, W) bool: a texel two
    different faces of the same object cover (islands stacked on purpose, e.g. left and right wings; a few texels
    along shared edges count too); any_face (H, W) bool: covered by any face of these objects (set by bake_layout
    when face_filter is used, else = mask)."""

    def __init__(self, W, H):
        self.W, self.H = W, H
        self.mask = np.zeros((H, W), bool)
        self.obj = np.full((H, W), -1, np.int32)
        self.face = np.full((H, W), -1, np.int32)
        self.pos = np.zeros((H, W, 3), np.float32)
        self.nrm = np.zeros((H, W, 3), np.float32)
        self.stacked = np.zeros((H, W), bool)
        self.any_face = self.mask


def bake_layout(objects, W, H, face_filter=all_faces, space='OBJECT', smooth_normals=True):
    """Rasterises the faces that pass face_filter into a Layout. Later objects win shared texels, so list the one
    whose look matters most last. Reads the active UV layer; never changes anything."""
    lay = Layout(W, H)
    any_face = np.zeros((H, W), bool) if face_filter is not all_faces else None
    for k, ob in enumerate(objects):
        bm, owned = mesh_of(ob)
        try:
            uv = bm.loops.layers.uv.active
            if uv is None:
                continue
            m = ob.matrix_world
            rot = m.to_3x3().inverted_safe().transposed()
            for f in bm.faces:
                take = face_filter(ob, bm, f)
                if not take and any_face is None:
                    continue
                loops = list(f.loops)
                for i in range(1, len(loops) - 1):
                    tri = (loops[0], loops[i], loops[i + 1])
                    hit = raster(np.array([tuple(l[uv].uv) for l in tri]), W, H)
                    if hit is None:
                        continue
                    rows, cols, w = hit
                    if any_face is not None:
                        any_face[rows, cols] = True
                    if not take:
                        continue
                    if space == 'WORLD':
                        co = np.array([tuple(m @ l.vert.co) for l in tri], np.float32)
                    else:
                        co = np.array([tuple(l.vert.co) for l in tri], np.float32)
                    if smooth_normals:
                        nv = [l.vert.normal for l in tri]
                    else:
                        nv = [f.normal] * 3
                    if space == 'WORLD':
                        nv = [(rot @ n).normalized() for n in nv]
                    nr = np.array([tuple(n) for n in nv], np.float32)
                    # A texel already taken by another face of the same object is a stacked island (variants that
                    # share one layout on purpose don't count).
                    prev = lay.mask[rows, cols] & (lay.obj[rows, cols] == k) & (lay.face[rows, cols] != f.index)
                    lay.stacked[rows[prev], cols[prev]] = True
                    lay.mask[rows, cols] = True
                    lay.obj[rows, cols] = k
                    lay.face[rows, cols] = f.index
                    lay.pos[rows, cols] = w @ co
                    n = w @ nr
                    lay.nrm[rows, cols] = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-8)
        finally:
            done(bm, owned)
    lay.any_face = any_face if any_face is not None else lay.mask
    return lay


def invert(face_filter):
    """The faces a filter leaves out: bake_layout(objs, W, H, invert(selected_faces)).mask is every texel some
    OTHER face uses, so painting lay.mask & ~others.mask changes nothing else."""
    def test(ob, bm, f):
        return not face_filter(ob, bm, f)
    return test


# ---------------------------------------------------------------- image operations

def dilate(arr, filled, steps, blocked=None):
    """Grows the painted texels (filled) outward by `steps` texels into empty space, copying the neighbours'
    average, so mip-mapping doesn't pull in the background at the island edges. Texels in `blocked` (e.g. any
    other face) are left alone. Returns a new array."""
    out = arr.copy()
    filled = filled.copy()
    blocked = np.zeros_like(filled) if blocked is None else blocked
    channels = out.shape[2]
    for _ in range(steps):
        acc = np.zeros(out.shape, np.float32)
        cnt = np.zeros(filled.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(np.roll(filled, dy, 0), dx, 1)
            acc += np.where(src[..., None], np.roll(np.roll(out, dy, 0), dx, 1), 0)
            cnt += src
        grow = (cnt > 0) & ~filled & ~blocked
        out[grow] = acc[grow] / cnt[grow, None]
        if channels == 4:
            out[grow, 3] = 1.0
        filled |= grow
    return out


def height_to_normal(height, mask, strength=2.0):
    """Tangent-space normal map (OpenGL, green = +V: what Blender and Unity's 'Normal map' import use) from a
    height map in the same UV layout. strength = height units per texel of slope. Returns (H, W, 3) in 0..1 for
    the masked texels (others 0.5, 0.5, 1)."""
    h = np.where(mask, height, 0.0).astype(np.float32)
    gy, gx = np.gradient(h)
    n = np.stack([-strength * gx, -strength * gy, np.ones_like(gx)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    out = np.full(h.shape + (3,), 0.5, np.float32)
    out[..., 2] = 1.0
    out[mask] = n[mask] * 0.5 + 0.5
    return out


# ---------------------------------------------------------------- pattern helpers (numpy, per texel)

def hex_color(code):
    code = code.lstrip('#')
    return np.array([int(code[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def lerp(a, b, t):
    t = np.asarray(t, np.float32)
    if t.ndim == 1:
        t = t[:, None]
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, np.float32) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def band(d, width, soft=0.02):
    """1 inside a stripe of this width around d = 0, fading over `soft`."""
    return np.clip((width * 0.5 - np.abs(d)) / soft + 0.5, 0.0, 1.0)


def _hash3(ix, iy, iz):
    h = np.sin(ix * 127.1 + iy * 311.7 + iz * 74.7) * 43758.5453
    return h - np.floor(h)


def vnoise(P, freq):
    """Smooth value noise 0..1 at 3D points P (n, 3); a seam-free way to vary colour across UV islands."""
    g = P * freq
    i = np.floor(g)
    t = g - i
    t = t * t * (3 - 2 * t)
    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (t[:, 0] if dx else 1 - t[:, 0]) * (t[:, 1] if dy else 1 - t[:, 1]) * (t[:, 2] if dz else 1 - t[:, 2])
                out = out + w * _hash3(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz)
    return out


def fbm(P, freq, octaves=4):
    total, amp, norm = 0.0, 0.5, 0.0
    for k in range(octaves):
        total = total + amp * vnoise(P + 13.7 * k, freq * (2 ** k))
        norm += amp
        amp *= 0.5
    return total / norm

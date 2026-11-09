
# ------------------------------------------------------------------ 4. materials + textures
def load_px(img):
    W, H = img.size
    return np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)


def raster_mask(tris, W, H, grow=4):
    mask = np.zeros((H, W), bool)
    for a, b, c in tris:
        P = np.array([[a.x * W, a.y * H], [b.x * W, b.y * H], [c.x * W, c.y * H]])
        x0, y0 = np.floor(P.min(0)).astype(int) - 1
        x1, y1 = np.ceil(P.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = P
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            continue
        l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        l3 = 1 - l1 - l2
        ins = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        mask[y0:y1 + 1, x0:x1 + 1] |= ins
    for _ in range(grow):
        m2 = mask.copy()
        m2[1:] |= mask[:-1]
        m2[:-1] |= mask[1:]
        m2[:, 1:] |= mask[:, :-1]
        m2[:, :-1] |= mask[:, 1:]
        mask = m2
    return mask


def push_pull(img, valid):
    H, W = valid.shape
    if H <= 2 or W <= 2:
        out = img.copy()
        if valid.any():
            out[~valid] = img[valid].mean(0)
        return out
    h2, w2 = H // 2, W // 2
    im = img[:h2 * 2, :w2 * 2]
    va = valid[:h2 * 2, :w2 * 2].astype(np.float32)
    s = (im * va[..., None]).reshape(h2, 2, w2, 2, -1).sum((1, 3))
    c = va.reshape(h2, 2, w2, 2).sum((1, 3))
    small = np.where(c[..., None] > 0, s / np.maximum(c, 1e-6)[..., None], 0)
    small = push_pull(small, c > 0)
    up = np.repeat(np.repeat(small, 2, 0), 2, 1)
    upf = np.zeros_like(img)
    upf[:h2 * 2, :w2 * 2] = up
    if H > h2 * 2:
        upf[h2 * 2:] = upf[h2 * 2 - 1]
    if W > w2 * 2:
        upf[:, w2 * 2:] = upf[:, w2 * 2 - 1:w2 * 2]
    return np.where(valid[..., None], img, upf)


def blur(a, r=2):
    out = a.copy()
    for _ in range(r):
        out = (out + np.roll(out, 1, 0) + np.roll(out, -1, 0) + np.roll(out, 1, 1) + np.roll(out, -1, 1)) / 5.0
    return out


def repaint(img, mask, out_name, kind):
    px = load_px(img)
    H, W = mask.shape
    # fill from the texels around the patch (outside the mask, inside the pumpkin's skin colours)
    valid = ~mask
    filled = push_pull(px[..., :3], valid)
    # soften the hard push-pull blocks inside the mask a little
    sm = blur(filled, 6)
    rgb = np.where(mask[..., None], sm, px[..., :3])
    if kind == "albedo":
        rng = np.random.default_rng(429)
        nz = blur(rng.standard_normal((H, W)).astype(np.float32), 3) * 0.06
        rgb = np.where(mask[..., None], np.clip(rgb * (1 + nz[..., None]), 0, 1), rgb)
    if kind == "normal":
        n = rgb * 2 - 1
        n = n / np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
        rgb = np.where(mask[..., None], n * 0.5 + 0.5, rgb)
    out = np.concatenate([rgb, px[..., 3:]], axis=2)
    im = bpy.data.images.new(out_name, W, H, alpha=True, float_buffer=False)
    im.colorspace_settings.name = img.colorspace_settings.name
    im.pixels.foreach_set(out.ravel())
    path = os.path.join(OUT_DIR, out_name + ".png")
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    im.pack()
    log("wrote", path, "texels repainted", int(mask.sum()))
    return im



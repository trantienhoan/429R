"""Texture paths that point at another PC's checkout (e.g. C:\\Github_Repository\\429R\\Assets\\...,
E:\\Github_Repository\\429R\\Assets\\...) are pointed at THIS checkout's Assets folder, written relative to the .blend
(//../../Assets/...), so the file renders on both PCs. Only missing files are touched; packed images are left alone.

    blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/fix_paths.py            (fix + save)
    ... -- nosave                                                                                   (report only)

Also importable: fix_paths.fix() returns how many paths it changed (story_pass.py calls it after appending models
from Assets/Blender, whose paths are often absolute paths of the other PC).
"""
import bpy, os, sys

KEY = "\\429r\\assets\\"


def assets_dir():
    here = os.path.dirname(bpy.data.filepath) or os.getcwd()
    return os.path.normpath(os.path.join(here, "..", "..", "Assets"))


def fix(verbose=True):
    assets = assets_dir()
    n = 0
    for im in bpy.data.images:
        if im.source != "FILE" or im.packed_file:
            continue
        if os.path.exists(bpy.path.abspath(im.filepath)):
            continue
        p = im.filepath.replace("/", "\\")
        i = p.lower().find(KEY)
        if i < 0:
            if verbose:
                print("[fix_paths] missing, not under 429R\\Assets:", im.name, im.filepath)
            continue
        new = os.path.join(assets, p[i + len(KEY):])
        if os.path.exists(new):
            im.filepath = bpy.path.relpath(new)
            im.reload()
            n += 1
        elif verbose:
            print("[fix_paths] missing here too:", new)
    if verbose:
        print("[fix_paths] fixed", n, "image paths")
    return n


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if fix() and "nosave" not in args:
        bpy.ops.wm.save_mainfile()
        print("[fix_paths] saved", bpy.data.filepath)

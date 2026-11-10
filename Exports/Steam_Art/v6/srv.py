"""v6 helper: a headless Blender that runs the .py jobs dropped into v6/_q one by one (shared globals), writing
each job's printed output next to it as .out. Started by: blender -b <file> --python v6/srv.py"""
import bpy, os, time, sys, io, traceback, glob
HERE = os.path.dirname(os.path.abspath(__file__))
Q = os.path.join(HERE, "_q")
os.makedirs(Q, exist_ok=True)
for f in glob.glob(os.path.join(Q, "*")):
    os.remove(f)
G = {"bpy": bpy, "HERE": HERE}
print("SERVER READY", flush=True)
while True:
    jobs = sorted(glob.glob(os.path.join(Q, "*.py")))
    if not jobs:
        time.sleep(0.15)
        continue
    job = jobs[0]
    try:
        code = open(job, encoding="utf-8").read()
    except Exception:
        time.sleep(0.1)
        continue
    os.remove(job)
    buf = io.StringIO()
    old = sys.stdout
    sys.stdout = buf
    stop = False
    t0 = time.time()
    try:
        exec(compile(code, job, "exec"), G)
    except SystemExit:
        stop = True
    except Exception:
        traceback.print_exc(file=buf)
    finally:
        sys.stdout = old
    buf.write("\n[%.1f s]" % (time.time() - t0))
    tmp = job[:-3] + ".tmp"
    open(tmp, "w", encoding="utf-8").write(buf.getvalue())
    os.replace(tmp, job[:-3] + ".out")
    if stop:
        break

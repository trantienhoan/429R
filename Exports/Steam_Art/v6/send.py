"""Send a job to the v6 Blender helper and print its output. Usage: send.py <file.py | -c "code"> [timeout_s]"""
import sys, os, time, itertools
HERE = os.path.dirname(os.path.abspath(__file__))
Q = os.path.join(HERE, "_q")
args = sys.argv[1:]
if args[0] == "-c":
    code = args[1]; rest = args[2:]
else:
    code = open(args[0], encoding="utf-8").read(); rest = args[1:]
timeout = float(rest[0]) if rest else 600
name = "job_%d" % int(time.time() * 1000)
tmp = os.path.join(Q, name + ".tmp_in")
open(tmp, "w", encoding="utf-8").write(code)
os.replace(tmp, os.path.join(Q, name + ".py"))
out = os.path.join(Q, name + ".out")
t0 = time.time()
while not os.path.exists(out):
    if time.time() - t0 > timeout:
        print("TIMEOUT waiting for", name); sys.exit(1)
    time.sleep(0.2)
time.sleep(0.05)
print(open(out, encoding="utf-8").read())
os.remove(out)

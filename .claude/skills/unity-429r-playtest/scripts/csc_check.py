"""Compile-checks a Unity .csproj with Unity's own Roslyn, outside the editor (no output kept). Works on both PCs.
Usage (from anywhere):
    uv run --python 3.12 python -I <repo>/.claude/skills/unity-429r-playtest/scripts/csc_check.py Assembly-CSharp.csproj [extra.cs ...]
Sources and references come from the .csproj (Unity regenerates it); new files Unity hasn't added yet go as extra
arguments (paths relative to the repo). Project references (e.g. Assembly-CSharp-firstpass) come from
Library/ScriptAssemblies. Prints the errors only. Unity's folder: UNITY_EDITOR_DATA, else C:/Unity/<version> or the
Hub's default folder, from ProjectSettings/ProjectVersion.txt. (Based on Documents/429R_claude_backups/2026-10-30/csc_check.py.)"""
import os, re, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..")).replace("\\", "/")


def unity_data():
    env = os.environ.get("UNITY_EDITOR_DATA")
    if env and os.path.isdir(env):
        return env.replace("\\", "/")
    ver = re.search(r"m_EditorVersion:\s*(\S+)", open(os.path.join(ROOT, "ProjectSettings/ProjectVersion.txt"), encoding="utf-8").read()).group(1)
    for base in ("C:/Unity", "C:/Program Files/Unity/Hub/Editor", "D:/Unity", "E:/Unity"):
        data = f"{base}/{ver}/Editor/Data"
        if os.path.isdir(data):
            return data
    sys.exit(f"Unity {ver} not found: set UNITY_EDITOR_DATA to its Editor/Data folder")


if len(sys.argv) < 2:
    sys.exit(__doc__)
UNITY = unity_data()
proj = os.path.join(ROOT, sys.argv[1])
text = open(proj, encoding="utf-8").read()
sources = [s.replace("\\", "/") for s in re.findall(r'<Compile Include="([^"]+)"', text)]
sources += [a.replace("\\", "/") for a in sys.argv[2:] if a.replace("\\", "/") not in sources]
refs = [r.replace("\\", "/") for r in re.findall(r"<HintPath>([^<]+)</HintPath>", text)]
for name in re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', text):
    dll = os.path.join(ROOT, "Library/ScriptAssemblies", os.path.basename(name) + ".dll")
    if os.path.exists(dll):
        refs.append(dll)
defines = re.search(r"<DefineConstants>([^<]*)", text).group(1)
lang = re.search(r"<LangVersion>([^<]*)", text).group(1)
out = os.path.join(tempfile.gettempdir(), "csc_check_out.dll")
lines = ["-nologo", "-noconfig", "-nostdlib+", "-target:library", f"-langversion:{lang}", "-unsafe",
         "-nowarn:CS0618,CS0649,CS0414,CS0169,CS0067,CS0105,CS1998", "-warn:0", f"-define:{defines}", f'-out:"{out}"']
lines += [f'-r:"{r}"' for r in refs]
lines += [f'"{os.path.join(ROOT, s)}"' for s in sources]
rsp = os.path.join(tempfile.gettempdir(), "csc_check.rsp")
open(rsp, "w", encoding="utf-8").write("\n".join(lines))
r = subprocess.run([f"{UNITY}/NetCoreRuntime/dotnet.exe", f"{UNITY}/DotNetSdkRoslyn/csc.dll", f"@{rsp}"],
                   capture_output=True, text=True)
errors = [l for l in (r.stdout + r.stderr).splitlines() if ": error " in l]
print(f"{os.path.basename(proj)}: {len(sources)} files, exit {r.returncode}, {len(errors)} errors")
for e in errors[:30]:
    print("  " + e.replace(ROOT.replace("/", "\\") + "\\", ""))
sys.exit(1 if errors else 0)

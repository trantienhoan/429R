"""Read a PlayMaker FSM (default GAMESTAGES) straight from a Unity scene/prefab YAML, without Unity. Read-only.

    uv run --python 3.12 python -I gamestages.py dump  "<scene or prefab>" fsm.json [FSM name]
    uv run --python 3.12 python -I gamestages.py trace fsm.json "<state>" [max states]   # breadth-first from a state
    uv run --python 3.12 python -I gamestages.py show  fsm.json "<state>" ["<state>" ...]
    uv run --python 3.12 python -I gamestages.py name  "<scene>" <fileID>                # a GameObject's name

dump writes every state's transitions (event -> state), action types and string params (event names). FsmString,
FsmGameObject and other parameter values are not decoded: look at the state's YAML (grep "- name: <state>").
It reads the file on disk: unsaved editor changes are not in it (check the file's time / git diff first).
"""
import json
import re
import sys
from collections import deque


def dump(path, out, fsm_name="GAMESTAGES"):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    start = None
    for i, l in enumerate(lines):
        if re.match(r"^\s+name: " + re.escape(fsm_name) + r"\s*$", l):
            if any(re.match(r"^\s+states:\s*$", lines[j]) for j in range(i, min(i + 40, len(lines)))):
                start = i
                break
    if start is None:
        sys.exit(f"FSM '{fsm_name}' not found in {path}")
    i = start
    while not re.match(r"^\s+states:\s*$", lines[i]):
        i += 1
    indent = len(lines[i]) - len(lines[i].lstrip())
    i += 1
    states, cur, section = [], None, None
    while i < len(lines):
        l = lines[i]
        ind = len(l) - len(l.lstrip())
        s = l.strip()
        # Blank lines inside multi-line strings (notice texts) are not the end of the list.
        if s and ind <= indent and not s.startswith("- "):
            break
        if ind == indent and s.startswith("- name: "):
            cur = {"name": s[8:].strip().strip("'\""), "transitions": [], "actions": [], "strings": []}
            states.append(cur)
            section = None
        elif cur is not None:
            if s == "transitions:":
                section = "t"
            elif s == "actionData:":
                section = "a"
            elif section == "t" and s.startswith("name: ") and lines[i - 1].strip() == "- fsmEvent:":
                cur["transitions"].append({"event": s[6:].strip().strip("'\""), "to": None})
            elif section == "t" and s.startswith("toState: ") and cur["transitions"]:
                cur["transitions"][-1]["to"] = s[9:].strip().strip("'\"")
            elif section == "a" and re.match(r"^- [A-Za-z_][A-Za-z0-9_.]*$", s) and "." in s:
                cur["actions"].append(s[2:].split(".")[-1])
            elif section == "a" and s == "stringParams:":
                section = "s"
            elif section == "s" and s.startswith("- "):
                v = s[2:].strip().strip("'\"")
                if v:
                    cur["strings"].append(v)
            elif section == "s":
                section = "a"
        i += 1
    json.dump(states, open(out, "w", encoding="utf-8"), indent=1)
    print(f"{len(states)} states of {fsm_name} -> {out}")


def load(path):
    return {s["name"]: s for s in json.load(open(path, encoding="utf-8"))}


def show_state(S, n):
    s = S.get(n)
    if not s:
        print("MISSING", n)
        return
    print(f"[{n}] {', '.join(s['actions'])}")
    if s["strings"]:
        print("   strings:", [x[:70] for x in s["strings"]][:14])
    for t in s["transitions"]:
        print(f"     {t['event']} -> {t['to']}")


def trace(path, start, limit=60):
    S = load(path)
    seen, q = set(), deque([start])
    while q and len(seen) < limit:
        n = q.popleft()
        if n in seen or n is None:
            continue
        seen.add(n)
        show_state(S, n)
        for t in S.get(n, {"transitions": []})["transitions"]:
            if t["to"] not in seen:
                q.append(t["to"])


def name_of(path, file_id):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    for i, l in enumerate(lines):
        if l.strip() == f"--- !u!1 &{file_id}":
            for j in range(i, min(i + 20, len(lines))):
                if lines[j].strip().startswith("m_Name:"):
                    print(lines[j].split(":", 1)[1].strip())
                    return
    print("not a GameObject in this file:", file_id)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    cmd = sys.argv[1]
    if cmd == "dump":
        dump(sys.argv[2], sys.argv[3], sys.argv[4] if len(sys.argv) > 4 else "GAMESTAGES")
    elif cmd == "trace":
        trace(sys.argv[2], sys.argv[3], int(sys.argv[4]) if len(sys.argv) > 4 else 60)
    elif cmd == "show":
        S = load(sys.argv[2])
        for n in sys.argv[3:]:
            show_state(S, n)
    elif cmd == "name":
        name_of(sys.argv[2], sys.argv[3])
    else:
        sys.exit(__doc__)

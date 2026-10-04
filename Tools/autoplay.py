# -*- coding: utf-8 -*-
"""Автопрогон уровней в Play mode: солвер делает ходы, скрипт собирает ошибки и скриншоты.

python autoplay.py 1 46 76 106 121 161 [--shot-at 8]
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402
from unity_play import ev  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
args = [a for a in sys.argv[1:] if not a.startswith("--")]
shot_at = 8
if "--shot-at" in sys.argv:
    shot_at = int(sys.argv[sys.argv.index("--shot-at") + 1])
levels = [int(a) for a in args if a.isdigit()]


def errors():
    r = json.loads(send("read_console", {"action": "get", "types": ["error"], "count": 30, "format": "plain"}))
    out = []
    for e in r["result"]["data"] or []:
        m = (e if isinstance(e, str) else e.get("message", "")).split("\n")[0][:300]
        if m not in out:
            out.append(m)
    return out


def shot(name):
    import subprocess
    subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "unity_play.py"), "shot", name], check=False)


if ev("return UnityEditor.EditorApplication.isPlaying.ToString();") != "True":
    ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/AllOnShelves/Scenes/Boot.unity"); UnityEditor.EditorApplication.isPlaying = true; return "ok";')
    time.sleep(10)

send("read_console", {"action": "clear"})
for lvl in levels:
    ev(f"AllOnShelves.GameApp.I.PlayLevel({lvl}); return \"load\";")
    time.sleep(2.5)
    ev("var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>(); gc.DebugUnlockInput(); return \"ok\";")
    moves, result = 0, "?"
    for i in range(220):
        r = ev("var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>(); "
               "if (gc == null) return \"nogc\"; bool ok = gc.DebugAutoMove(); "
               "return ok + \" \" + gc.DebugState.Result + \" \" + gc.DebugFinished;")
        if r is None or r.startswith("ERROR") or r == "nogc":
            result = r
            break
        ok, res, fin = r.split()
        if ok == "True":
            moves += 1
        if moves == shot_at:
            time.sleep(0.4)
            shot(f"auto_{lvl}")
        if res != "Playing" or fin == "True":
            result = res
            break
        if ok != "True":
            time.sleep(0.4)
            r2 = ev("var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>(); return gc.DebugState.Result.ToString();")
            result = "stuck:" + str(r2)
            break
        time.sleep(0.12)
    print(f"L{lvl}: {result} after {moves} moves")
    time.sleep(1.5)
errs = errors()
print("errors:", len(errs))
for e in errs:
    print(" -", e)

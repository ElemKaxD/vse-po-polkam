# -*- coding: utf-8 -*-
"""Цикл правки UI: стоп Play → перекомпиляция → «Собрать сцены» → «Проверить сцены» (+ опционально «Подготовить проект»).

python rebuild.py [--setup] [--force]
"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402
sys.stdout.reconfigure(encoding="utf-8")


def menu(path, timeout=900):
    return send("execute_menu_item", {"menu_path": path}, timeout=timeout)


def console(filt=None, types=("error", "log")):
    r = json.loads(send("read_console", {"action": "get", "types": list(types), "count": 80}, timeout=60))
    out = []
    for e in r.get("result", {}).get("data", []) or []:
        m = e if isinstance(e, str) else (e.get("message") or "")
        if filt is None or filt in m:
            out.append(m)
    return out


try:
    send("manage_editor", {"action": "stop"})
except Exception:
    pass
time.sleep(2)
import subprocess
subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "unity_wait.py"), "300", "--refresh"])
send("read_console", {"action": "clear"})
if "--setup" in sys.argv:
    menu("Всё по полкам/1. Подготовить проект")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SCENES = [os.path.join(ROOT, "Assets", "AllOnShelves", "Scenes", n + ".unity") for n in ("Boot", "Hub", "Game")]
STAMP = os.path.join(ROOT, "Temp", "scenes_built.txt")
# ручные правки: перемещения/размеры/повороты/шрифты сборка сама запоминает и применяет (Editor/UserLayout.cs,
# Editor/Data/user_layout.json). Добавленные руками объекты не переносятся — о них предупреждаем.
if os.path.exists(STAMP):
    built = float(open(STAMP).read().strip() or 0)
    changed = [os.path.basename(p) for p in SCENES if os.path.exists(p) and os.path.getmtime(p) > built + 5]
    if changed:
        print("Сцены сохранены вручную после прошлой сборки:", ", ".join(changed), "— правки раскладки будут перенесены")
menu("Всё по полкам/Служебное/Собрать сцены без подтверждения")
time.sleep(1)
open(STAMP, "w").write(str(max(os.path.getmtime(p) for p in SCENES)))
menu("Всё по полкам/Проверить сцены")
time.sleep(1)
for m in console():
    if "AllOnShelves" in m or "error" in m.lower() and "EditorStyles" not in m:
        print(m[:3000])

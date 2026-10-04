# -*- coding: utf-8 -*-
"""Ждёт, пока Unity закончит импорт/компиляцию, и печатает ошибки консоли.

python unity_wait.py [timeout_sec] [--refresh]
"""
import io
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
args = [a for a in sys.argv[1:] if not a.startswith("--")]
timeout = float(args[0]) if args else 900
start = time.time()

if "--refresh" in sys.argv:
    try:
        send("read_console", {"action": "clear"}, timeout=20)
    except Exception:
        pass
    try:
        send("refresh_unity", {"mode": "force", "scope": "all", "compile": "request"}, timeout=60)
    except Exception as e:
        print("refresh error:", e)
    time.sleep(4)

# ВАЖНО: перед сборкой WebGL ждать состояние надо БЕЗ execute_code. Он грузит в редактор
# динамическую сборку, и потом сборка падает в LinkFileGenerator с «Illegal byte sequence».
# Поэтому состояние читаем ресурсом редактора, а execute_code оставлен только как запасной путь
# (ключ --code), когда ресурс почему-то недоступен.
BUSY = "return (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating).ToString();"
USE_CODE = "--code" in sys.argv


def is_busy():
    if USE_CODE:
        r = json.loads(send("execute_code", {"action": "execute", "code": BUSY}, timeout=15))
        return r.get("result", {}).get("data", {}).get("result") != "False"
    # статус-файл моста: его пишет сам редактор, обращаться к нему безопасно
    import glob
    here = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    for path in glob.glob(os.path.expanduser("~/.unity-mcp/unity-mcp-status-*.json")):
        try:
            st = json.load(io.open(path, encoding="utf-8"))
        except Exception:
            continue
        if os.path.normcase(here) not in os.path.normcase(st.get("project_path", "")):
            continue
        return bool(st.get("reloading"))
    raise RuntimeError("статус-файл моста не найден")


ok = 0
while time.time() - start < timeout:
    try:
        ok = 0 if is_busy() else ok + 1
    except Exception:
        ok = 0
    if ok >= 3:
        break
    time.sleep(2)
else:
    print("TIMEOUT waiting for Unity")

r = json.loads(send("read_console", {"action": "get", "types": ["error"], "count": 80, "format": "plain"}))
entries = r.get("result", {}).get("data", []) or []
seen = []
for e in entries:
    msg = e if isinstance(e, str) else (e.get("message") or json.dumps(e, ensure_ascii=False))
    line = msg.split("\n")[0][:500]
    if line not in seen:
        seen.append(line)
print(f"ready after {time.time() - start:.0f}s; errors: {len(seen)}")
for line in seen[:40]:
    print(" -", line)

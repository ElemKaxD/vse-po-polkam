# -*- coding: utf-8 -*-
"""Помощник для автотеста в Play mode: клики по товарам, скриншоты, консоль.

python unity_play.py click N            — N раз кликнуть крайний левый доступный товар (или из тележки, если можно)
python unity_play.py shot name          — скриншот Game view в Temp/name.png (+ уменьшенная копия)
python unity_play.py errors             — ошибки консоли
python unity_play.py eval "C# code"     — выполнить код
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SCRATCH = os.environ.get("SHOTS_DIR", os.path.join(ROOT, "Temp"))
os.makedirs(SCRATCH, exist_ok=True)


def ev(code):
    r = json.loads(send("execute_code", {"action": "execute", "code": code}, timeout=60))
    res = r.get("result", {})
    if not res.get("success"):
        return "ERROR: " + json.dumps(res, ensure_ascii=False)[:1500]
    return res.get("data", {}).get("result")


CLICK = r'''
var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>();
if (gc == null) return "no controller";
var views = UnityEngine.Object.FindObjectsByType<AllOnShelves.Game.ItemView>(UnityEngine.FindObjectsSortMode.None);
AllOnShelves.Game.ItemView best = null;
foreach (var v in views) {
  if (!v.Interactable || v.Spoiled || v.group.alpha < 0.5f) continue;
  if (best == null || v.rect.position.x < best.rect.position.x) best = v;
}
if (best == null) return "no item";
gc.OnItemClicked(best);
return "clicked " + best.name + " type " + best.Type;
'''


def main():
    cmd = sys.argv[1]
    if cmd == "click":
        n = int(sys.argv[2]) if len(sys.argv) > 2 else 1
        for _ in range(n):
            print(ev(CLICK))
            time.sleep(float(sys.argv[3]) if len(sys.argv) > 3 else 0.6)
    elif cmd == "shot":
        name = sys.argv[2]
        path = os.path.join(ROOT, "Temp", name + ".png").replace("\\", "/")
        if os.path.exists(path):
            os.remove(path)
        ev(f'UnityEngine.ScreenCapture.CaptureScreenshot(@"{path}"); return "ok";')
        for _ in range(20):
            time.sleep(0.5)
            if os.path.exists(path) and os.path.getsize(path) > 0:
                break
        time.sleep(0.5)
        from PIL import Image
        im = Image.open(path)
        im.thumbnail((1400, 1400))
        out = os.path.join(SCRATCH, name + ".jpg")
        im.convert("RGB").save(out, quality=85)
        print(out)
    elif cmd == "errors":
        r = json.loads(send("read_console", {"action": "get", "types": ["error", "warning"], "count": 40, "format": "plain"}))
        for e in r["result"]["data"] or []:
            print("-", (e if isinstance(e, str) else e.get("message", ""))[:400])
    elif cmd == "eval":
        print(ev(sys.argv[2]))


if __name__ == "__main__":
    main()

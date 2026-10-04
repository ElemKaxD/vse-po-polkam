# -*- coding: utf-8 -*-
"""Бот-тестировщик «Всё по полкам!»: сам играет в уровни, собирает экономику и метрики.

Запуск (нужен запущенный Unity с мостом MCP):
  python bot.py --run 1 110     # играть уровни 1..110 (можно частями: --run 1 30)
  python bot.py --econ          # снять экономику и мета-каталог в JSON
  python bot.py --report        # собрать инфографику report.html из собранных данных

Данные: Temp/balancebot/*.json, отчёт: Temp/balancebot/report.html.
Прогон прерывается безопасно (Ctrl+C) — результат дописывается по уровням.
"""
import argparse
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "Tools"))
from unity_cmd import send  # noqa: E402

OUT = os.path.join(ROOT, "Temp", "balancebot")
RUN_JSON = os.path.join(OUT, "run.json")
ECON_JSON = os.path.join(OUT, "econ.json")
SHOT_DIR = os.path.join(OUT, "shots")


def ev(code, timeout=90):
    r = json.loads(send("execute_code", {"action": "execute", "code": code}, timeout=timeout))
    res = r.get("result", {})
    if not res.get("success"):
        return "ERROR:" + (res.get("error") or "?")[:120]
    return res.get("data", {}).get("result", "")


def ensure_play():
    if ev("return UnityEditor.EditorApplication.isPlaying.ToString();") != "True":
        ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/AllOnShelves/Scenes/Boot.unity");'
           ' UnityEditor.EditorApplication.isPlaying = true; return "ok";')
        time.sleep(12)


def load_run():
    if os.path.exists(RUN_JSON):
        return json.load(open(RUN_JSON, encoding="utf-8"))
    return {}


def save_run(d):
    os.makedirs(OUT, exist_ok=True)
    json.dump(d, open(RUN_JSON, "w", encoding="utf-8"), ensure_ascii=False, indent=1)


def shot(name):
    path = os.path.join(SHOT_DIR, name)
    os.makedirs(SHOT_DIR, exist_ok=True)
    subprocess.run([sys.executable, os.path.join(ROOT, "Tools", "editor_shot.py"), path],
                   capture_output=True, text=True, encoding="utf-8")


def play_range(first, last, shots=False):
    ensure_play()
    data = load_run()
    send("read_console", {"action": "clear"})
    for lvl in range(first, last + 1):
        t0 = time.time()
        ev(f"AllOnShelves.GameApp.I.PlayLevel({lvl}); return \"load\";")
        time.sleep(2.2)
        ev("var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>();"
           " gc.DebugUnlockInput(); return \"ok\";")
        moves, result, stuck = 0, "?", 0
        for i in range(400):
            r = ev("var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>(); "
                   "if (gc == null) return \"nogc\"; bool ok = gc.DebugAutoMove(); "
                   "return ok + \" \" + gc.DebugState.Result + \" \" + gc.DebugFinished;")
            if r.startswith("ERROR") or r == "nogc":
                result = "error:" + r[:60]
                break
            ok, res, fin = r.split()
            if ok == "True":
                moves += 1
            else:
                stuck += 1
                time.sleep(0.35)
            if res != "Playing" or fin == "True":
                result = res
                break
            time.sleep(0.1)
        dur = round(time.time() - t0, 1)
        data[str(lvl)] = {"moves": moves, "result": result, "stuck": stuck, "sec": dur}
        print(f"L{lvl}: {result} moves={moves} stuck={stuck} {dur}s", flush=True)
        if shots and lvl % 10 == 1:
            time.sleep(0.3)
            shot(f"level_{lvl:03d}.png")
        save_run(data)
    errs = json.loads(send("read_console", {"action": "get", "types": ["error"], "count": 20, "format": "plain"}))
    msgs = [(e if isinstance(e, str) else e.get("message", ""))[:160]
            for e in (errs.get("result", {}).get("data") or [])]
    data["_console_errors"] = msgs
    save_run(data)
    print("errors:", len(msgs))


ECON_CODE = r'''
var sb = new System.Text.StringBuilder();
sb.Append("{");
// районы и сложности из плана
sb.Append("\"planner\": {\"districts\":" + AllOnShelves.Core.LevelPlanner.DistrictCount +
          ", \"levels\":" + AllOnShelves.Core.LevelPlanner.LevelCount + "},");
// предметы ремонта: id, имя, цена, район
sb.Append("\"reno\": [");
var reno = AllOnShelves.MetaCatalog.Items;
for (int i = 0; i < reno.Count; i++) {
    var it = reno[i];
    if (i > 0) sb.Append(",");
    sb.Append("{\"id\":\"" + it.Id + "\",\"name\":\"" + it.Name + "\",\"cost\":" + it.Cost +
              ",\"district\":" + it.District + "}");
}
sb.Append("],");
// товары магазина за рубли
sb.Append("\"shop\": [");
var shop = AllOnShelves.MetaCatalog.Products;
for (int i = 0; i < shop.Length; i++) {
    var p = shop[i];
    if (i > 0) sb.Append(",");
    sb.Append("{\"id\":\"" + p.Id + "\",\"title\":\"" + p.Title + "\",\"coins\":" + p.Coins +
              ",\"price\":\"" + (p.FallbackPrice ?? "") + "\"}");
}
sb.Append("],");
// константы экономики
var e = typeof(AllOnShelves.Core.Economy);
sb.Append("\"economy\": {");
bool first = true;
foreach (var f in e.GetFields()) {
    if (!f.IsLiteral) continue;
    if (!first) sb.Append(",");
    first = false;
    sb.Append("\"" + f.Name + "\":" + f.GetRawConstantValue());
}
sb.Append("}}");
return sb.ToString();
'''


def dump_econ():
    ensure_play()
    raw = ev(ECON_CODE, timeout=120)
    if raw.startswith("ERROR"):
        print("econ dump failed:", raw)
        return
    os.makedirs(OUT, exist_ok=True)
    json.dump(json.loads(raw), open(ECON_JSON, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("econ ->", ECON_JSON)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", nargs=2, type=int, metavar=("FROM", "TO"))
    ap.add_argument("--econ", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--shots", action="store_true")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    if args.run:
        play_range(args.run[0], args.run[1], shots=args.shots)
    if args.econ:
        dump_econ()
    if args.report:
        subprocess.run([sys.executable, os.path.join(HERE, "report.py")], check=False)


if __name__ == "__main__":
    main()

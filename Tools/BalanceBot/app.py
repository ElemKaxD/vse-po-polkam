# -*- coding: utf-8 -*-
"""Бот-тестировщик «Всё по полкам!» — отдельная программа.

Сам находит запущенный Unity (мост MCP), сам играет уровни, снимает экономику
и строит отчёт-инфографику (открывает его в браузере).

Требуется: Unity с открытым проектом Yandex_Claude (мост MCP for Unity).
Рядом с exe лежит config.json — путь к проекту (можно поменять).

Запуск: двойной клик по BalanceBot.exe — появится меню.
"""
import base64
import glob
import io
import json
import os
import socket
import struct
import subprocess
import sys
import time

# ----------------------------- мост к Unity -----------------------------

STATUS_GLOB = os.path.expanduser("~/.unity-mcp/unity-mcp-status-*.json")
PROJECT_NAME = "Yandex_Claude"
PORT_FALLBACK = 6400


def find_port():
    for f in glob.glob(STATUS_GLOB):
        try:
            d = json.load(open(f, encoding="utf-8"))
            if d.get("project_name") == PROJECT_NAME:
                return int(d["unity_port"])
        except Exception:
            pass
    return PORT_FALLBACK


def send(cmd_type, params, timeout=300):
    s = socket.create_connection(("127.0.0.1", find_port()), timeout=timeout)
    try:
        buf = b""
        while b"\n" not in buf:
            buf += s.recv(1)
        payload = json.dumps({"type": cmd_type, "params": params}).encode("utf-8")
        s.sendall(struct.pack(">Q", len(payload)) + payload)

        def read_exact(n):
            data = b""
            while len(data) < n:
                chunk = s.recv(n - len(data))
                if not chunk:
                    raise IOError("мост закрыл соединение")
                data += chunk
            return data

        length = struct.unpack(">Q", read_exact(8))[0]
        return read_exact(length).decode("utf-8")
    finally:
        s.close()


def ev(code, timeout=120):
    """Выполнить C# в Unity, вернуть строку-результат или ERROR:..."""
    for attempt in (1, 2):
        try:
            r = json.loads(send("execute_code", {"action": "execute", "code": code}, timeout=timeout))
            res = r.get("result", {})
            if not res.get("success"):
                return "ERROR:" + (res.get("error") or "компиляция")[:150]
            return res.get("data", {}).get("result", "")
        except Exception as e:
            if attempt == 2:
                return f"ERROR:{e}"
            time.sleep(2)
    return "ERROR:?"


def bridge_ok():
    try:
        r = json.loads(send("read_console", {"action": "get", "types": ["error"], "count": 1}, timeout=20))
        return r.get("status") == "success"
    except Exception:
        return False


# ----------------------------- пути и данные -----------------------------

def app_dir():
    """Папка exe (или скрипта)."""
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


def load_config():
    path = os.path.join(app_dir(), "config.json")
    default = {"project": r"D:\Work\ЯндексИгры\LaserSiege\Yandex_Claude"}
    if os.path.exists(path):
        try:
            default.update(json.load(open(path, encoding="utf-8")))
        except Exception:
            pass
    else:
        json.dump(default, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return default


def data_dir():
    d = os.path.join(app_dir(), "data")
    os.makedirs(d, exist_ok=True)
    os.makedirs(os.path.join(d, "shots"), exist_ok=True)
    return d


def levels_map(project):
    path = os.path.join(project, "Assets", "AllOnShelves", "Resources", "Levels", "levels.json")
    data = json.load(open(path, encoding="utf-8"))["levels"]
    return {l["id"]: l for l in data}


def level_count(project):
    return len(levels_map(project))


# ----------------------------- прогон уровней -----------------------------

PLAY_SHOT = r'''
var canvases = UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None);
foreach (var c in canvases)
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay) c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
return "ok";
'''


def shot(path):
    code = PLAY_SHOT + f'''
var go = new UnityEngine.GameObject("__bot_cam");
var cam = go.AddComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color(1f, 0.957f, 0.89f, 1f);
cam.cullingMask = 1 << 5;
cam.orthographic = true;
var rt = new UnityEngine.RenderTexture(1280, 720, 24);
cam.targetTexture = rt;
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceCamera) {{ c.worldCamera = cam; c.planeDistance = 10f; }}
UnityEngine.Canvas.ForceUpdateCanvases(); cam.Render(); UnityEngine.Canvas.ForceUpdateCanvases(); cam.Render();
var prev = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = prev;
System.IO.File.WriteAllBytes(@"{os.path.abspath(path).replace(chr(92), '/')}", UnityEngine.ImageConversion.EncodeToPNG(tex));
cam.targetTexture = null;
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.worldCamera == cam) {{ c.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }}
UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
return "saved";
'''
    return ev(code, timeout=90)


def ensure_play():
    if ev("return UnityEditor.EditorApplication.isPlaying.ToString();") != "True":
        print("   запускаю Play mode…")
        ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/AllOnShelves/Scenes/Boot.unity");'
           ' UnityEditor.EditorApplication.isPlaying = true; return "ok";')
        time.sleep(12)


def play_range(first, last, shots=False):
    run_path = os.path.join(data_dir(), "run.json")
    run = json.load(open(run_path, encoding="utf-8")) if os.path.exists(run_path) else {}
    ensure_play()
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
        run[str(lvl)] = {"moves": moves, "result": result, "stuck": stuck,
                         "sec": round(time.time() - t0, 1)}
        json.dump(run, open(run_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print(f"   L{lvl}: {result}  ходов={moves}  тупиков={stuck}  {run[str(lvl)]['sec']}c", flush=True)
        if shots and lvl % 10 == 1:
            shot(os.path.join(data_dir(), "shots", f"level_{lvl:03d}.png"))
    errs = json.loads(send("read_console", {"action": "get", "types": ["error"], "count": 20, "format": "plain"}))
    run["_console_errors"] = [(e if isinstance(e, str) else e.get("message", ""))[:160]
                              for e in (errs.get("result", {}).get("data") or [])]
    json.dump(run, open(run_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"   ошибки консоли: {len(run['_console_errors'])}")


# ----------------------------- экономика -----------------------------

ECON_CODE = r'''
var sb = new System.Text.StringBuilder();
sb.Append("{\"planner\": {\"districts\":" + AllOnShelves.Core.LevelPlanner.DistrictCount +
          ", \"levels\":" + AllOnShelves.Core.LevelPlanner.LevelCount + "}, \"reno\": [");
var reno = AllOnShelves.MetaCatalog.Items;
for (int i = 0; i < reno.Count; i++) {
    var it = reno[i];
    if (i > 0) sb.Append(",");
    sb.Append("{\"id\":\"" + it.Id + "\",\"name\":\"" + it.Name + "\",\"cost\":" + it.Cost +
              ",\"district\":" + it.District + "}");
}
sb.Append("], \"shop\": [");
var shop = AllOnShelves.MetaCatalog.Products;
for (int i = 0; i < shop.Length; i++) {
    var p = shop[i];
    if (i > 0) sb.Append(",");
    sb.Append("{\"id\":\"" + p.Id + "\",\"title\":\"" + p.Title + "\",\"coins\":" + p.Coins +
              ",\"price\":\"" + (p.FallbackPrice ?? "") + "\"}");
}
sb.Append("], \"economy\": {}");
return sb.ToString();
'''


def dump_econ():
    ensure_play()
    raw = ev(ECON_CODE, timeout=120)
    if raw.startswith("ERROR"):
        print("   не удалось снять экономику:", raw)
        return False
    path = os.path.join(data_dir(), "econ.json")
    json.dump(json.loads(raw), open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("   экономика ->", path)
    return True


# ----------------------------- отчёт -----------------------------

def build_report(project):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    dd = data_dir()
    run = json.load(open(os.path.join(dd, "run.json"), encoding="utf-8")) if os.path.exists(os.path.join(dd, "run.json")) else {}
    econ = json.load(open(os.path.join(dd, "econ.json"), encoding="utf-8")) if os.path.exists(os.path.join(dd, "econ.json")) else {}
    levels = levels_map(project)
    ids = sorted(int(k) for k in run if not k.startswith("_"))
    cards, warnings, conclusions, todo = [], [], [], []
    plt.rcParams.update({"figure.dpi": 110, "font.size": 9, "axes.grid": True, "grid.alpha": 0.3})

    def fig64(fig):
        buf = io.BytesIO()
        fig.savefig(buf, format="png", bbox_inches="tight")
        plt.close(fig)
        return base64.b64encode(buf.getvalue()).decode()

    d_of = lambda i: (i - 1) // 10 + 1

    if ids:
        stuck_lv = [i for i in ids if run[str(i)].get("result") == "?"]
        eff = {i: levels[i]["metrics"]["moves"] / run[str(i)]["moves"]
               for i in ids if run[str(i)].get("result") == "Win" and run[str(i)]["moves"] > 0}
        wins = [i for i in ids if run[str(i)].get("result") == "Win"]
        fails = [i for i in ids if run[str(i)].get("result") not in ("Win", "?")]
        fig, ax = plt.subplots(figsize=(10, 3.6))
        xs = sorted(eff)
        ax.bar(xs, [eff[i] for i in xs],
               color=["#d44" if eff[i] < 0.55 else "#2a8" if eff[i] > 0.9 else "#48c" for i in xs])
        for i in stuck_lv:
            ax.axvline(i, color="#c92", lw=2, alpha=0.35)
        ax.axhline(0.7, color="k", ls="--", lw=0.8)
        ax.set_xlabel("уровень (оранжевые полосы — тупик авто-игрока)")
        ax.set_ylabel("оптимум солвера / ходы бота")
        ax.set_title("Эффективность ходов (ниже — сложнее)")
        cards.append(("Проходимость", fig64(fig), ""))
        wr = len(wins) / max(1, len(ids) - len(stuck_lv))
        cards.append(("Итог прогона", "", f"Побед: <b>{len(wins)}/{len(ids) - len(stuck_lv)}</b> ({wr:.0%})."
                      + (f" Тупик авто-игрока (решается руками): {stuck_lv}." if stuck_lv else "")
                      + (f" Не пройдены: {fails}." if fails else "")))
        tight = [i for i in xs if eff[i] < 0.55]
        easy = [i for i in xs if eff[i] > 0.9]
        if tight:
            warnings.append(f"Очень плотные уровни (эффективность &lt;0.55): {tight}.")
        if easy:
            conclusions.append(f"Лёгкие уровни (эффективность &gt;0.9): {len(easy)} шт. — запас сложности не используется.")
        fig, ax = plt.subplots(figsize=(10, 3))
        ds = sorted({d_of(i) for i in ids})
        means = [sum(levels[i]["metrics"]["moves"] / run[str(i)]["moves"]
                     for i in ids if d_of(i) == d and run[str(i)].get("result") == "Win" and run[str(i)]["moves"] > 0)
                 / max(1, sum(1 for i in ids if d_of(i) == d and run[str(i)].get("result") == "Win")) for d in ds]
        ax.plot(ds, means, "o-", color="#48c")
        ax.set_xticks(ds); ax.set_xlabel("район"); ax.set_ylabel("средняя эффективность")
        ax.set_title("Сложность по районам")
        cards.append(("Сложность по районам", fig64(fig), ""))

    mech_intro, mech_count = {}, {}
    for lid, l in sorted(levels.items()):
        m = l.get("mechanic") or ""
        if m:
            mech_intro.setdefault(m, lid)
            mech_count[m] = mech_count.get(m, 0) + 1
    if mech_intro:
        fig, ax = plt.subplots(figsize=(10, 3.2))
        ms = sorted(mech_intro, key=lambda m: mech_intro[m])
        ax.barh(ms, [mech_count[m] for m in ms], color="#c96")
        for k, m in enumerate(ms):
            ax.text(0.3, k, f"с {mech_intro[m]} ур.", va="center", fontsize=8)
        ax.set_xlabel("число уровней"); ax.set_title("Покрытие механик")
        cards.append(("Механики", fig64(fig), ""))
        one = [m for m in ms if mech_count[m] <= 1]
        if one:
            todo.append(f"Механики только в 1 уровне: {one} — после ввода не повторяются.")

    if econ:
        reno = econ.get("reno", [])
        cost_by_d = {}
        for it in reno:
            cost_by_d[it["district"]] = cost_by_d.get(it["district"], 0) + it["cost"]
        stage = [0, 280, 520, 620, 930, 1050, 1140, 1250, 1340, 1430, 1520, 1620]
        income, acc = [], 0
        for lid in sorted(levels):
            l = levels[lid]
            mult = 2 if l.get("revision") else {"hard": 1.5, "superhard": 2}.get(l.get("difficulty"), 1)
            acc += int((20 + 5 * l["district"]) * mult) + 300 / 110 + 150 / 40
            income.append(acc)
        fig, ax = plt.subplots(figsize=(10, 3.6))
        ax.plot(sorted(levels), income, color="#2a8", label="накопленный доход (3★, без рекламы)")
        cx = 0
        for d in sorted(cost_by_d):
            cx += cost_by_d[d] + stage[min(d, len(stage) - 1)]
            ax.plot(d * 10 - 0.5, cx, "ro", ms=5)
            ax.axvline(d * 10 - 0.5, color="#999", lw=0.6)
        ax.set_xlabel("уровень"); ax.set_ylabel("монеты")
        ax.set_title("Экономика: доход против расходов на ремонт (красное — траты к концу района)")
        ax.legend(fontsize=8)
        cards.append(("Экономика", fig64(fig), ""))
        for d in sorted(cost_by_d):
            di = income[d * 10 - 1] - (income[d * 10 - 11] if d > 1 else 0)
            spend = cost_by_d[d] + stage[min(d, len(stage) - 1)]
            if spend > di:
                warnings.append(f"Район {d}: ремонт ({spend:.0f}) дороже дохода района ({di:.0f}) — grind.")
        conclusions.append(f"Предметов ремонта: {len(reno)} ({sum(i['cost'] for i in reno)} монет), "
                           f"товаров магазина: {len(econ.get('shop', []))}.")

    shots = sorted(glob.glob(os.path.join(dd, "shots", "*.png")))
    if shots:
        html_imgs = "".join(
            f"<figure><img src='data:image/png;base64,{base64.b64encode(open(p, 'rb').read()).decode()}'/>"
            f"<figcaption>{os.path.basename(p)}</figcaption></figure>" for p in shots)
        cards.append(("Скриншоты прогона", "", html_imgs))

    if run.get("_console_errors"):
        warnings.append("Ошибки консоли: " + "; ".join(run["_console_errors"][:5]))
    else:
        conclusions.append("Ошибок консоли за прогон нет.")
    checklist = ("<ul>" + "".join(f"<li>⚠ {w}</li>" for w in warnings)
                 + "".join(f"<li>✅ {c}</li>" for c in conclusions)
                 + "".join(f"<li>📋 {t}</li>" for t in todo) + "</ul>")
    cards.append(("Выводы бота", "", checklist))

    html = f"""<!doctype html><html><head><meta charset='utf-8'><title>Баланс «Всё по полкам!»</title>
<style>body{{font-family:'Segoe UI',Arial,sans-serif;background:#171717;color:#eee;margin:24px}}
h1{{font-size:22px}} .card{{background:#222;border:1px solid #333;border-radius:10px;padding:14px;margin:14px 0}}
.card h2{{font-size:15px;margin:0 0 10px;color:#ffd479}} img{{max-width:100%;border-radius:6px}}
figure{{display:inline-block;margin:6px}} figcaption{{font-size:11px;color:#999;text-align:center}} li{{margin:4px 0}}</style>
</head><body><h1>🤖 Отчёт бота-тестировщика — «Всё по полкам!»</h1>
<p>Прогнано уровней: {len(ids)} · {time.strftime('%d.%m.%Y %H:%M')}</p>"""
    for title, img, body in cards:
        html += f"<div class='card'><h2>{title}</h2>" + (f"<img src='data:image/png;base64,{img}'/>" if img else "") + body + "</div>"
    html += "</body></html>"
    path = os.path.join(dd, "report.html")
    open(path, "w", encoding="utf-8").write(html)
    print("   отчёт ->", path)
    try:
        os.startfile(path)
    except Exception:
        pass
    return path


# ----------------------------- меню -----------------------------

BANNER = """
==========================================================
   БОТ-ТЕСТИРОВЩИК «ВСЁ ПО ПОЛКАМ!»
==========================================================
"""


def ask_int(prompt, default):
    try:
        return int(input(prompt).strip() or default)
    except ValueError:
        return default


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    cfg = load_config()
    print(BANNER)
    print("Проверяю связь с Unity…")
    if not bridge_ok():
        print("✖ Unity с проектом Yandex_Claude не найден (мост MCP не отвечает).")
        print("  Открой Unity с проектом и запусти бота снова.")
        input("Enter — выход…")
        return
    print("✔ Unity на связи.\n")
    while True:
        print("1 — полный цикл: экономика + все уровны + отчёт (~15 мин)")
        print("2 — прогон диапазона уровней + отчёт")
        print("3 — только экономика + отчёт")
        print("4 — только отчёт по уже собранным данным")
        print("5 — сбросить собранные данные")
        print("0 — выход")
        choice = input("Выбор: ").strip()
        try:
            if choice == "1":
                n = level_count(cfg["project"])
                dump_econ()
                play_range(1, n, shots=True)
                build_report(cfg["project"])
            elif choice == "2":
                a = ask_int("от уровня [1]: ", 1)
                b = ask_int(f"до уровня [{level_count(cfg['project'])}]: ", level_count(cfg["project"]))
                play_range(a, b, shots=True)
                build_report(cfg["project"])
            elif choice == "3":
                dump_econ()
                build_report(cfg["project"])
            elif choice == "4":
                build_report(cfg["project"])
            elif choice == "5":
                dd = data_dir()
                for f in ("run.json", "econ.json"):
                    p = os.path.join(dd, f)
                    if os.path.exists(p):
                        os.remove(p)
                print("   данные сброшены")
            elif choice == "0":
                break
        except KeyboardInterrupt:
            print("\n   (остановлено, данные сохранены)")
        print()


if __name__ == "__main__":
    main()

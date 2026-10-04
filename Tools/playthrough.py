# -*- coding: utf-8 -*-
"""
Сквозной прогон «как игрок»: с нуля (сброс прогресса), уровни проходит солвер, дальше — только настоящие
кнопки: победа → «Дальше», карта → ценник уровня, обучение — нажатием, окна — их кнопками.
Каждый шаг: ошибки консоли, скриншоты в shots/pt_*. python playthrough.py [до_уровня] [--fresh]
"""
import json, os, sys, time
ROOT = r"D:/Work/ЯндексИгры/LaserSiege/Yandex_Claude"
sys.path.insert(0, ROOT + "/Tools")
os.environ.setdefault("SHOTS_DIR", os.path.dirname(os.path.abspath(__file__)) + "/shots")
import ui_tour as T  # noqa
from unity_cmd import send  # noqa
sys.stdout.reconfigure(encoding="utf-8")

until = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 20
if not T.playing():
    T.start_play((1920, 1080))
if "--fresh" in sys.argv:
    T.ev("AllOnShelves.EditorTools.ProgressTools.ResetSilent(); return \"ok\";")
    time.sleep(6)

seen_err = set()


def errors(tag):
    r = json.loads(send("read_console", {"action": "get", "types": ["error", "exception"], "count": 40, "format": "plain"}))
    for e in r["result"]["data"] or []:
        m = (e if isinstance(e, str) else e.get("message", "")).split("\n")[0][:240]
        if m not in seen_err:
            seen_err.add(m)
            print(f"  !! ОШИБКА [{tag}]: {m}")


def ev(code):
    return T.ev(T.PRE + code)


STATE = ("var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; "
         "var tut = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.TutorialOverlay>(); "
         "string tv = tut != null && tut.Visible ? tut.bubbleText.text : \"\"; "
         "string pop = \"\"; foreach (var p in UnityEngine.Object.FindObjectsByType<AllOnShelves.Popup>(UnityEngine.FindObjectsSortMode.None)) if (p.IsOpen) pop += p.GetType().Name + \",\"; "
         "return sc + \"|\" + app.Save.maxReached + \"|\" + app.Save.coins + \"|\" + pop + \"|\" + tv.Replace(\"|\", \"/\").Replace(\"\\n\", \" \");")


def state():
    sc, mx, coins, pops, tut = ev(STATE).split("|", 4)
    return sc, int(mx), int(coins), [p for p in pops.split(",") if p], tut


shots = 0


def shot(name):
    global shots
    shots += 1
    T.shot(f"pt_{shots:03d}_{name}")


def handle_hub():
    """Карта: проходим обучение и окна, пока не останется чисто."""
    for _ in range(40):
        time.sleep(1.4)
        sc, mx, coins, pops, tut = state()
        if sc != "Hub":
            return
        if tut:
            print(f"  обучение: {tut[:110]}")
            shot("hub_tut")
            ev("var o = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.TutorialOverlay>(); o.TapNow(); return \"ok\";")
            continue
        if pops:
            print(f"  окно: {pops}")
            shot("hub_pop")
            # окна закрываем их же кнопками
            ev("foreach (var p in UnityEngine.Object.FindObjectsByType<AllOnShelves.Popup>(UnityEngine.FindObjectsSortMode.None)) if (p.IsOpen) { "
               "var b = p.GetComponentsInChildren<UnityEngine.UI.Button>(); UnityEngine.UI.Button best = null; "
               "foreach (var x in b) if (x.interactable && x.gameObject.activeInHierarchy && (x.name == \"Close\" || x.name == \"Ok\" || x.name == \"Yes\" || x.name == \"Claim\" || x.name == \"Later\")) best = x; "
               "if (best != null) best.onClick.Invoke(); else p.Hide(); } return \"ok\";")
            continue
        # экран не карта (обучение открыло дом/ремонт) — назад на карту
        cur = ev("var f = typeof(AllOnShelves.Hub.HubController).GetField(\"_current\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); "
                 "var c = f.GetValue(hub); return c == null ? \"null\" : c.GetType().Name;")
        if cur == "RenovationScreen":
            r = ev("int n = 0; foreach (var b in hub.renovationScreen.GetComponentsInChildren<UnityEngine.UI.Button>()) "
                   "if (b.name == \"Buy\" && b.gameObject.activeInHierarchy) { n++; if (app.Save.coins < 3000) { app.Save.coins += 3000; } "
                   "b.onClick.Invoke(); return \"buy\"; } return \"none\";")
            if r == "buy":
                print("  ремонт: купил улучшение")
                time.sleep(0.6)
                continue
        if cur != "MapScreen":
            print(f"  экран {cur} → на карту")
            shot("hub_screen")
            ev("hub.Show(hub.mapScreen); return \"ok\";")
            continue
        return


def play_level():
    """Уровень: окна механики/смены — кнопками, ходы — солвер, победа — «Дальше»."""
    lvl = ev("return gc != null && gc.DebugState != null ? AllOnShelves.GameApp.I.PendingLevel.ToString() : \"?\";")
    first = True
    for i in range(400):
        time.sleep(0.05)
        r = ev("var tut = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.TutorialOverlay>(); "
               "if (gc.mechanicPopup.IsOpen) { gc.mechanicPopup.okButton.onClick.Invoke(); return \"mech\"; } "
               "if (gc.shiftPopup != null && gc.shiftPopup.IsOpen) { if (tut != null && tut.Visible) { tut.TapNow(); return \"shifttut\"; } "
               "  foreach (var id in gc.shiftPopup.Advice) gc.shiftPopup.DebugPick(id); gc.shiftPopup.startButton.onClick.Invoke(); return \"shift\"; } "
               "if (tut != null && tut.Visible && !gc.victory.IsOpen) tut.TapNow(); "
               "if (gc.victory.IsOpen) return \"win\"; if (gc.defeat.IsOpen) return \"lose\"; "
               "var ok = gc.DebugAutoMove(); return gc.DebugState.Result + \":\" + ok;")
        if r in ("mech", "shift", "shifttut"):
            if first:
                shot(f"l{lvl}_{r}")
            print(f"  {r}")
            time.sleep(1.0)
            continue
        if first and i > 3:
            shot(f"l{lvl}_play")
            first = False
        if r == "win":
            time.sleep(1.6)
            shot(f"l{lvl}_win")
            ev("gc.victory.nextButton.onClick.Invoke(); return \"ok\";")
            return "win"
        if r == "lose":
            shot(f"l{lvl}_lose")
            return "lose"
        if r.startswith("Win") or r.startswith("Lose"):
            time.sleep(1.5)
    return "stuck"


t0 = time.time()
for step in range(until + 10):
    sc, mx, coins, pops, tut = state()
    errors(f"{sc} {mx}")
    if mx >= until:
        break
    if sc == "Game":
        res = play_level()
        print(f"уровень → {res}; пройдено {mx}, монет {coins}")
        if res != "win":
            shot("problem")
            break
        time.sleep(4)
    elif sc == "Hub":
        handle_hub()
        sc, mx, *_ = state()
        if sc == "Hub":
            # следующий уровень — нажатием на ценник карты
            print(f"карта: жму ценник {mx + 1}")
            ev(f"var n = hub.mapScreen.nodes[{mx}]; if (n != null && n.gameObject.activeInHierarchy) n.button.onClick.Invoke(); else app.PlayLevel({mx + 1}); return \"ok\";")
            time.sleep(4)
    else:
        time.sleep(3)
errors("конец")
print(f"готово за {time.time() - t0:.0f} с, ошибок: {len(seen_err)}")

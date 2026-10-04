# -*- coding: utf-8 -*-
"""Визуальный обход игры в Play mode: выставляет сейв, открывает экраны/окна/уровни, снимает скриншоты.

python ui_tour.py [--res 1920x1080] [group ...]
  группы: hub, reno, map, popups, game, all (по умолчанию all)
Скриншоты: $SHOTS_DIR (или Temp/) — tour_<имя>.jpg. После обхода Play mode остаётся включённым.

Внимание: использует execute_code — перед WebGL-сборкой нужен domain reload (см. Docs/claude/tools.md).
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_cmd import send  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.environ.get("SHOTS_DIR", os.path.join(ROOT, "Temp"))
os.makedirs(OUT, exist_ok=True)

PRE = "var app = AllOnShelves.GameApp.I; var hub = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Hub.HubController>(); " \
      "var gc = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.GameController>(); "


def ev(code, timeout=60):
    r = json.loads(send("execute_code", {"action": "execute", "code": code}, timeout=timeout))
    res = r.get("result", {})
    if not res.get("success"):
        return "ERROR: " + json.dumps(res, ensure_ascii=False)[:800]
    return res.get("data", {}).get("result")


def run(code, wait=0.8):
    out = ev(PRE + code + ("" if "return" in code else " return \"ok\";"))
    if out and str(out).startswith("ERROR"):
        print("  !", out)
    time.sleep(wait)
    return out


def shot(name):
    path = os.path.join(ROOT, "Temp", "tour_" + name + ".png").replace("\\", "/")
    if os.path.exists(path):
        os.remove(path)
    ev(f'UnityEngine.ScreenCapture.CaptureScreenshot(@"{path}"); return "ok";')
    for _ in range(30):
        time.sleep(0.3)
        if os.path.exists(path) and os.path.getsize(path) > 0:
            break
    time.sleep(0.4)
    from PIL import Image
    im = Image.open(path)
    im.thumbnail((1280, 1280))
    out = os.path.join(OUT, "tour_" + name + ".jpg")
    im.convert("RGB").save(out, quality=82)
    print("shot", out)


def playing():
    return ev("return UnityEditor.EditorApplication.isPlaying ? \"yes\" : \"no\";") == "yes"


def start_play(res):
    w, h = res
    if playing():
        send("manage_editor", {"action": "stop"})
        time.sleep(3)
    ev(f'UnityEditor.PlayModeWindow.SetCustomRenderingResolution({w}, {h}, "tour"); return "ok";')
    ev('UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/AllOnShelves/Scenes/Boot.unity"); return "ok";')
    send("manage_editor", {"action": "play"})
    for _ in range(60):
        time.sleep(1)
        try:
            if ev("return AllOnShelves.GameApp.I != null && AllOnShelves.GameApp.I.Levels != null ? \"yes\" : \"no\";") == "yes":
                break
        except Exception:
            pass
    time.sleep(3)


# сейв «середина игры»: всё открыто, монеты есть, часть ремонта куплена
SAVE_MID = """
var s = app.Save;
s.maxReached = 45; s.coins = 4321; s.undo = 5; s.hint = 3;
s.tutorialBits = -1; s.mechanicsSeen = -1; s.activeTheme = "";
s.stars = new int[AllOnShelves.Core.LevelPlanner.LevelCount + 1]; for (int i = 1; i <= 45; i++) s.stars[i] = 1 + i % 3;
s.boughtItems.Clear();
foreach (var it in AllOnShelves.MetaCatalog.Items) if (it.District <= 3) s.boughtItems.Add(it.Id);
s.boughtItems.Add("meta_d04_chest_freezer"); s.boughtItems.Add("meta_d04_wall_clock"); s.boughtItems.Add("meta_d04_ceiling_lamps");
s.renovationDistrict = 4;
s.stickers.Clear(); foreach (var st in AllOnShelves.MetaCatalog.StickersOf(0)) { s.stickers.Add(st); if (s.stickers.Count > 5) break; }
s.pendingPacks = 0;
s.starsChestClaimed = 999;   // сундук за звёзды не должен выскакивать поверх каждого экрана
s.starterShownAtLevel = 999; s.starterBought = true; s.noAds = true;   // окно спецпредложения тоже не нужно
app.MarkDirty();
"""


def hub_group():
    run(SAVE_MID + "app.GoHub();", 4)
    run("hub.Show(hub.startScreen);", 1.2); shot("hub_start")
    run("hub.Show(hub.mapScreen);", 1.5); shot("map_current")
    run("hub.Show(hub.albumScreen);", 1.2); shot("album")
    run("hub.Show(hub.shopScreen);", 1.2); shot("shop")
    run("hub.Show(hub.dailyScreen);", 1.2); shot("daily")
    run("hub.Show(hub.leaderboardScreen);", 2.5); shot("leaders")


def map_group():
    run(SAVE_MID + "app.GoHub();", 4)
    run("hub.Show(hub.mapScreen);", 1.5)
    for d in (1, 2, 3, 5):
        run(f'var m = hub.mapScreen; typeof(AllOnShelves.Hub.MapScreen).GetMethod("ShowPage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(m, new object[] {{ {d - 1}, false }});', 1.0)
        shot(f"map_d{d:02d}")


def reno_group():
    run(SAVE_MID + "app.GoHub();", 4)
    for d in (1, 2, 3, 4):
        run(f'hub.Show(hub.renovationScreen); var r = hub.renovationScreen; typeof(AllOnShelves.Hub.RenovationScreen).GetField("_district", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(r, {d}); r.Refresh();', 1.2)
        shot(f"reno_d{d:02d}")


def popups_group():
    run(SAVE_MID + "app.GoHub();", 4)
    run("hub.Show(hub.mapScreen); hub.settingsPopup.Open();", 1.2); shot("pop_settings"); run("hub.settingsPopup.HideInstant();")
    run('hub.confirmPopup.Ask("Уровень 12", "Переиграть уровень? Звёзды обновятся, монет — 30%.", "icon_retry", null);', 1.2); shot("pop_confirm"); run("hub.confirmPopup.HideInstant();")
    run('hub.offerPopup.Open(AllOnShelves.MetaCatalog.Products[0], System.DateTimeOffset.UtcNow.AddHours(3).ToUnixTimeSeconds());', 1.2); shot("pop_offer"); run("hub.offerPopup.HideInstant();")
    run("hub.packPopup.OpenPending(hub);", 2.5); shot("pop_pack")


GAME_LEVELS = [int(x) for x in os.environ.get("TOUR_LEVELS", "1,3,11,15,21,25,31,35,41,48,51,58,61,71,81,95,110").split(",") if x]


def game_group():
    run(SAVE_MID + "", 0.5)
    for lvl in GAME_LEVELS:
        run(f"app.PlayLevel({lvl});", 4)
        shot(f"game_l{lvl:03d}_start")
        run("var mp = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.MechanicPopup>(); if (mp != null && mp.gameObject.activeSelf) mp.okButton.onClick.Invoke();", 1.0)
        for _ in range(4):
            run("gc.DebugAutoMove();", 0.5)
        shot(f"game_l{lvl:03d}_mid")
    # пауза, победа, поражение
    run("app.PlayLevel(18);", 4)
    run('typeof(AllOnShelves.Game.GameController).GetMethod("OnPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gc, null);', 1.2)
    shot("pop_pause")
    run("app.PlayLevel(5);", 4)
    for _ in range(60):
        r = run("return gc.DebugAutoMove() ? \"more\" : \"done\";", 0.35)
        if r != "more":
            break
    time.sleep(3)
    shot("pop_victory")
    run("app.PlayLevel(40);", 4)
    for _ in range(80):
        r = run("return gc.DebugToCart() ? \"more\" : \"done\";", 0.3)
        if r != "more":
            break
    time.sleep(2.5)
    shot("pop_defeat")


def main():
    args = [a for a in sys.argv[1:]]
    res = (1920, 1080)
    if "--res" in args:
        i = args.index("--res")
        w, h = args[i + 1].split("x")
        res = (int(w), int(h))
        del args[i:i + 2]
    groups = args or ["all"]
    start_play(res)
    table = {"hub": hub_group, "map": map_group, "reno": reno_group, "popups": popups_group, "game": game_group}
    for g in (table.keys() if "all" in groups else groups):
        print("==", g)
        table[g]()
    print(ev('var errs = new System.Text.StringBuilder(); return "done";'))


if __name__ == "__main__":
    main()

# -*- coding: utf-8 -*-
"""Материалы для витрины Яндекс Игр: скриншоты 1920x1080 и видео геймплея 16:9 (до 28 с).

python promo_capture.py [shots] [video]      (по умолчанию — всё)
Результат — Builds/Promo/: screen_*.png, gameplay_16x9.mp4.
Уровни: PROMO_SHOTS="7,15,24,31,45", PROMO_VIDEO="9,15".
Видео пишет Scripts/Game/PromoRecorder.cs (только в редакторе), склеивает ffmpeg.

Внимание: использует execute_code — перед WebGL-сборкой нужен domain reload (см. Docs/claude/tools.md).
"""
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_tour as T  # noqa: E402

ROOT = T.ROOT
OUT = os.path.join(ROOT, "Builds", "Promo")
os.makedirs(OUT, exist_ok=True)
SHOT_LEVELS = [int(x) for x in os.environ.get("PROMO_SHOTS", "7,15,24,31,45").split(",") if x]
VIDEO_LEVELS = [int(x) for x in os.environ.get("PROMO_VIDEO", "9,15").split(",") if x]
FPS = 30


def full_shot(name):
    path = os.path.join(OUT, name + ".png").replace("\\", "/")
    if os.path.exists(path):
        os.remove(path)
    T.ev(f'UnityEngine.ScreenCapture.CaptureScreenshot(@"{path}"); return "ok";')
    for _ in range(40):
        time.sleep(0.3)
        if os.path.exists(path) and os.path.getsize(path) > 0:
            break
    time.sleep(0.5)
    print("shot", path)


def shots():
    T.run(T.SAVE_MID, 0.5)
    for lvl in SHOT_LEVELS:
        T.run(f"app.PlayLevel({lvl});", 4)
        T.run("var mp = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.MechanicPopup>(); "
              "if (mp != null && mp.gameObject.activeSelf) mp.okButton.onClick.Invoke(); gc.DebugUnlockInput();", 1.0)
        for _ in range(int(os.environ.get("PROMO_MOVES", "12"))):
            T.run("gc.DebugAutoMove();", 0.5)
        time.sleep(1.0)
        full_shot(f"screen_l{lvl:03d}")
    T.run(T.SAVE_MID + "app.GoHub();", 4)
    T.run("hub.Show(hub.mapScreen);", 1.5)
    # страница открытого района (текущий, 5-й, ещё закрыт замком — на витрине это некрасиво)
    T.run('typeof(AllOnShelves.Hub.MapScreen).GetMethod("ShowPage", System.Reflection.BindingFlags.NonPublic | '
          'System.Reflection.BindingFlags.Instance).Invoke(hub.mapScreen, new object[] { 3, false });', 2.0)
    full_shot("screen_map")
    # ремонт в процессе: во 2-м районе куплены только первые улучшения
    T.run("var s = app.Save; int n = 0; foreach (var it in AllOnShelves.MetaCatalog.Items) "
          "if (it.District == 2 && n++ >= 2) s.boughtItems.Remove(it.Id); s.renovationDistrict = 2; app.MarkDirty();", 0.5)
    T.run("hub.Show(hub.renovationScreen); var r = hub.renovationScreen; "
          "typeof(AllOnShelves.Hub.RenovationScreen).GetField(\"_district\", System.Reflection.BindingFlags.NonPublic | "
          "System.Reflection.BindingFlags.Instance).SetValue(r, 2); r.Refresh();", 2.0)
    full_shot("screen_reno")


def video():
    frames = os.path.join(ROOT, "Temp", "promo_frames").replace("\\", "/")
    T.run(T.SAVE_MID, 0.5)
    lv = ",".join(str(x) for x in VIDEO_LEVELS)
    T.run(f'AllOnShelves.Game.PromoRecorder.Run(@"{frames}", {FPS}, new int[] {{ {lv} }}, 0.6f, 3.0f, 16f);', 1)
    for _ in range(1200):
        time.sleep(2)
        r = T.ev("var p = AllOnShelves.Game.PromoRecorder.Current; return p == null ? \"none\" : (p.Done ? \"done|\" + p.Log : \"run\");")
        if r != "run":
            print(r)
            break
    ffmpeg = shutil.which("ffmpeg") or "ffmpeg"
    mp4 = os.path.join(OUT, "gameplay_raw.mp4")
    subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", os.path.join(frames, "f%05d.jpg"),
                    "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "slow",
                    "-vf", "scale=1920:1080", mp4], check=True)
    print("video", mp4)


def main():
    groups = sys.argv[1:] or ["shots", "video"]
    T.start_play((1920, 1080))
    if "shots" in groups:
        shots()
    if "video" in groups:
        video()


if __name__ == "__main__":
    main()

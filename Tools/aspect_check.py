# -*- coding: utf-8 -*-
"""Проверка раскладки на разных пропорциях экрана (29.09.2026): белые полосы, уехавшие кнопки.

python aspect_check.py [WxH ...]      (по умолчанию 1920x1080 2560x1080 1920x1200 1440x1080 1280x1024)
Снимки — $SHOTS_DIR/aspect_<W>x<H>_<экран>.jpg. Play mode остаётся включённым.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ui_tour as T  # noqa: E402

RES = [tuple(int(v) for v in a.split("x")) for a in sys.argv[1:]] or \
      [(1920, 1080), (2560, 1080), (1920, 1200), (1440, 1080), (1280, 1024)]

RENO = ('hub.Show(hub.renovationScreen); var r = hub.renovationScreen; '
        'typeof(AllOnShelves.Hub.RenovationScreen).GetField("_district", System.Reflection.BindingFlags.NonPublic | '
        'System.Reflection.BindingFlags.Instance).SetValue(r, {d}); r.Refresh();')


def set_res(w, h):
    T.ev(f'UnityEditor.PlayModeWindow.SetCustomRenderingResolution({w}, {h}, "aspect"); return "ok";')
    time.sleep(1.5)


def main():
    T.start_play(RES[0])
    for w, h in RES:
        set_res(w, h)
        tag = f"aspect_{w}x{h}"
        T.run(T.SAVE_MID + "app.GoHub();", 4)
        T.run("hub.Show(hub.mapScreen);", 1.5)
        T.shot(tag + "_map")
        T.run(RENO.format(d=2), 1.5)
        T.shot(tag + "_reno_d02")
        T.run(RENO.format(d=3), 1.5)
        T.shot(tag + "_reno_d03")
        T.run("app.PlayLevel(24);", 4)
        T.run("var mp = UnityEngine.Object.FindFirstObjectByType<AllOnShelves.Game.MechanicPopup>(); "
              "if (mp != null && mp.gameObject.activeSelf) mp.okButton.onClick.Invoke(); gc.DebugUnlockInput();", 1.0)
        T.shot(tag + "_level")


if __name__ == "__main__":
    main()

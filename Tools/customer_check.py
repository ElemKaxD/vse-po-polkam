# -*- coding: utf-8 -*-
"""Быстрые ходы в уровнях с покупателями: каждый пришедший покупатель должен показаться на экране."""
import os, sys, time
ROOT = r"D:/Work/ЯндексИгры/LaserSiege/Yandex_Claude"
sys.path.insert(0, ROOT + "/Tools")
os.environ.setdefault("SHOTS_DIR", os.path.dirname(os.path.abspath(__file__)) + "/shots")
import ui_tour as T  # noqa
sys.stdout.reconfigure(encoding="utf-8")
if not T.playing():
    T.start_play((1920, 1080))

levels = [int(a) for a in sys.argv[1:]] or [7, 8, 12]
PROBE = ("var st = gc.DebugState; var cv = gc.customer; "
         "return st.Result + \"|\" + st.NextCustomer + \"|\" + st.CustomersServed + \"|\" + (cv.gameObject.activeSelf ? cv.group.alpha.ToString(\"0.0\") : \"off\") + \"|\" + st.Customer.Active;")
for lvl in levels:
    T.run(T.SAVE_MID + f"app.PlayLevel({lvl});", 4)
    T.run("gc.DebugStartShift(false); gc.DebugUnlockInput();", 0.5)
    seen_visible = {}
    arrived = 0
    t0 = time.time()
    for i in range(200):
        r = T.ev(T.PRE + "gc.DebugAutoMove(); " + PROBE)
        res, nxt, served, alpha, active = r.split("|")
        nxt = int(nxt)
        if alpha not in ("off",) and float(alpha.replace(",", ".")) > 0.6:
            seen_visible[nxt] = True
        # дополнительно — опрос между ходами (покупатель выходит с задержкой)
        for _ in range(2):
            r2 = T.ev(T.PRE + PROBE).split("|")
            if r2[3] != "off" and float(r2[3].replace(",", ".")) > 0.6:
                seen_visible[int(r2[1])] = True
        if res != "Playing":
            break
    time.sleep(2.0)
    r2 = T.ev(T.PRE + PROBE).split("|")
    print(f"уровень {lvl}: итог {res}, пришло {nxt}, обслужено {served}, видели покупателей с номерами {sorted(seen_visible)}"
          f" за {time.time() - t0:.0f} с")

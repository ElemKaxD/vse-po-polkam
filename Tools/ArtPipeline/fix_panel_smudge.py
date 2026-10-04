# -*- coding: utf-8 -*-
"""Убирает брак генерации — размытое пиксельное пятно посреди ровной заливки панели
(ui_panel_festive, ui_panel_dark, ui_panel_shop, 29.09.2026).

Середина у этих панелей — ровная заливка, пятно нарисовано поверх неё. Скрипт находит
внутреннюю область панели (связная область без рамки от центра), берёт её чистый цвет
и закрашивает только отличающиеся от него пиксели заливки (cv2.inpaint по окружению).
Насыщенные детали (конфетти, листья, рамка) не трогаются. Оригиналы — в ArtSource/PanelOriginals.

python fix_panel_smudge.py [--preview]     (--preview — только картинки до/после в Temp)
"""
import os
import shutil
import sys

import cv2
import numpy as np
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UI = os.path.join(ROOT, "Assets", "AllOnShelves", "Art", "UI")
ORIG = os.path.join(ROOT, "ArtSource", "PanelOriginals")
# порог насыщенности «заливки»: у магазина светлый кант рамки почти не насыщен (0.31),
# у тёмной панели сама заливка насыщеннее (0.24), а кольцо пятна — около 0.3
PANELS = {"ui_panel_festive": 0.35, "ui_panel_dark": 0.35, "ui_panel_shop": 0.2}


def load(name):
    p = os.path.join(ORIG, name + ".png")
    if not os.path.exists(p):
        p = os.path.join(UI, name + ".png")
    # cv2.imread не читает пути с кириллицей — через буфер
    return cv2.imdecode(np.fromfile(p, dtype=np.uint8), cv2.IMREAD_UNCHANGED)


def save(path, img):
    ok, buf = cv2.imencode(os.path.splitext(path)[1], img)
    buf.tofile(path)


def fix(img, sat_max):
    """Середина у этих панелей — плоская заливка. Чистый цвет остался только узкой полосой у рамки,
    всё остальное — пятно и его тёмное кольцо. Поэтому заливаем середину чистым цветом,
    не трогая 3 точки вокруг контуров и деталей (конфетти, цветы, рамка)."""
    bgr = img[:, :, :3].astype(np.float64)
    alpha = img[:, :, 3]
    h, w = alpha.shape
    b, g, r = bgr[:, :, 0], bgr[:, :, 1], bgr[:, :, 2]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    sat = (mx - mn) / np.maximum(mx, 1)
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    cy, cx = h // 2, w // 2
    c0 = img[cy, cx, :3].astype(int)

    fillish = (alpha > 250) & (lum > 60) & (sat < sat_max)
    lab, _ = ndimage.label(fillish)
    inner = lab == lab[cy, cx]
    inner = ndimage.binary_fill_holes(inner)
    # детали внутри (конфетти) и сама рамка: вокруг них 3 точки не трогаем — там сглаживание краёв
    detail = inner & ~fillish
    safe = ndimage.binary_erosion(inner, iterations=3) & ~ndimage.binary_dilation(detail, iterations=3)

    # чистый цвет — полоса 4..12 точек от рамки; повторная медиана отсекает задетое пятном
    ring = ndimage.binary_erosion(inner, iterations=4) & ~ndimage.binary_erosion(inner, iterations=12) & fillish
    ref = np.median(bgr[ring], axis=0)
    near = ring & (np.abs(bgr - ref).max(axis=2) < 8)
    ref = np.median(bgr[near], axis=0)

    # заливаем эталоном всю середину, а не только отличающиеся пиксели: иначе на границе маски
    # остаётся едва заметный контур (снаружи пиксели отличаются от эталона на 1–2 уровня)
    mask = safe
    wgt = cv2.GaussianBlur(mask.astype(np.float64), (0, 0), 1.5) * safe
    out = img.copy()
    mixed = bgr * (1 - wgt[:, :, None]) + ref * wgt[:, :, None]
    out[:, :, :3] = np.clip(np.round(mixed), 0, 255).astype(np.uint8)
    return out, mask, ref, c0


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    preview = "--preview" in sys.argv
    os.makedirs(ORIG, exist_ok=True)
    for name, sat_max in PANELS.items():
        src = os.path.join(UI, name + ".png")
        if not os.path.exists(src):
            print("нет файла", name)
            continue
        if not os.path.exists(os.path.join(ORIG, name + ".png")):
            shutil.copy(src, os.path.join(ORIG, name + ".png"))
        img = load(name)
        out, keep, ref, c0 = fix(img, sat_max)
        print(f"{name}: закрашено {int(keep.sum())} пикс, цвет заливки RGB {ref.astype(int).tolist()[::-1]}, центр был {c0.tolist()[::-1]}")
        if preview:
            tmp = os.path.join(ROOT, "Temp", "panel_fix")
            os.makedirs(tmp, exist_ok=True)
            def on_magenta(x):
                bg = np.zeros_like(x[:, :, :3]); bg[:] = (255, 0, 255)
                a = x[:, :, 3:4] / 255.0
                return (x[:, :, :3] * a + bg * (1 - a)).astype(np.uint8)
            both = np.concatenate([on_magenta(img), on_magenta(out)], axis=1)
            save(os.path.join(tmp, name + "_before_after.jpg"), both)
        else:
            save(src, out)
    print("готово" + (" (только предпросмотр)" if preview else ""))


if __name__ == "__main__":
    main()

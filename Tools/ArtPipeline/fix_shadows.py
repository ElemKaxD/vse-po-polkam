# -*- coding: utf-8 -*-
"""Замена врисованных «звёздчатых» теней (снежинки-астериски) на мягкие овальные.

Под каждым объектом арт-пакет нарисовал тень-звёздочку из тонких лучей — пользователь
просил обычные овальные. Скрипт находит в нижней части спрайта разреженное широкое
тёмное пятно (это и есть лучи), стирает его и рисует мягкий тёмный овал того же размаха.

  python fix_shadows.py --dry   — только лист «до/после» (Temp/shadow_fix_preview.png)
  python fix_shadows.py         — править файлы на месте (исходники в yandex_games не трогаем)
  python fix_shadows.py --list  — список файлов к правке
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "AllOnShelves", "Art"))
FOLDERS = ["Album", "Board", "Characters", "Icons", "Items", "Logo", "Map", "Meta", "Shop", "UI"]
# FX и Overlays не трогаем: там тёмные пиксели — сами эффекты (пыль, муха), не тени
DARK = (140, 120, 120)     # порог «тёмного» RGB
ALPHA_SOLID = 235          # непрозрачное — контур объекта, не тень


def components(mask):
    """Связные компоненты маски (8-связность, обход в ширину)."""
    H, W = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    ys, xs = np.where(mask)
    for y, x in zip(ys.tolist(), xs.tolist()):
        if visited[y, x]:
            continue
        stack = [(y, x)]
        visited[y, x] = True
        pix = []
        while stack:
            cy, cx = stack.pop()
            pix.append((cy, cx))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not visited[ny, nx]:
                        visited[ny, nx] = True
                        stack.append((ny, nx))
        yield pix


def find_asterisks(arr):
    """Разреженные широкие тёмные пятна в нижней трети — возвращаем (маска_пикселей, bbox)."""
    H, W = arr.shape[:2]
    if H < 16 or W < 16:
        return []
    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    dark = (a > 30) & (r < DARK[0]) & (g < DARK[1]) & (b < DARK[2])
    dark[: int(H * 0.62), :] = False
    out = []
    for pix in components(dark):
        if len(pix) < 50:
            continue
        cys = [p[0] for p in pix]
        cxs = [p[1] for p in pix]
        x0, x1, y0, y1 = min(cxs), max(cxs), min(cys), max(cys)
        bw, bh = x1 - x0 + 1, y1 - y0 + 1
        fill = len(pix) / (bw * bh)
        if bw / max(1, bh) > 1.9 and fill < 0.42 and bw > 0.3 * W:
            out.append((pix, (x0, x1, y0, y1), fill))
    return out


def draw_oval(arr, bbox):
    """Аккуратный мягкий овал (просьба 22.09.2026): круглый, заметный, с мягким краем.
    Если не влезает к низу холста — поднимается под объект (верх прячется за ним), а не плющится."""
    x0, x1, y0, y1 = bbox
    H, W = arr.shape[:2]
    cx = (x0 + x1) / 2.0
    rx = min((x1 - x0) * 0.47, W / 2.0 - 2.0)
    ry = max(8.0, rx * 0.34)                 # круглее, чем было (0.26 — вышло слишком плоcко)
    cy = y1 - ry * 0.10                      # низ овала чуть ниже бывшей тени
    if cy + ry > H - 1.0:                    # упирается в край — поднимаем овал целиком
        cy = H - 1.0 - ry
    ys = np.arange(0, H)[:, None]
    xs = np.arange(0, W)[None, :]
    d = np.sqrt(((xs - cx) / max(1.0, rx)) ** 2 + ((ys - cy) / max(1.0, ry)) ** 2)
    alpha = np.clip(1.0 - d, 0.0, 1.0) ** 1.2 * 170  # мягкий, но заметный овал
    shadow_rgb = np.array([40, 26, 18], dtype=np.float32)
    # тень кладётся своим цветом: у стёртых пикселей остаётся яркий «мусорный» RGB,
    # смешивание с ним делало овал серой полупрозрачной кляксой (почти невидимой)
    stronger = alpha >= arr[:, :, 3].astype(np.float32)
    for c in range(3):
        ch = arr[:, :, c].astype(np.float32)
        ch[stronger] = shadow_rgb[c]
        arr[:, :, c] = ch.astype(arr.dtype)
    arr[:, :, 3] = np.maximum(arr[:, :, 3], alpha.astype(arr.dtype)).astype(arr.dtype)


def process(path, apply, draw_ovals=True):
    im = Image.open(path).convert("RGBA")
    arr = np.asarray(im).copy()
    found = find_asterisks(arr)
    if not found:
        return None
    if not apply:
        return found
    for pix, bbox, _ in found:
        for y, x in pix:
            if arr[y, x, 3] < ALPHA_SOLID:  # контуры объекта не трогаем
                arr[y, x, 3] = 0
    if draw_ovals:
        for _, bbox, _ in found:
            draw_oval(arr, bbox)
    Image.fromarray(arr, "RGBA").save(path)
    return found


def keep_oval(rel):
    """Тени оставляем только под товарами и ценниками карты (просьба 21.09.2026)."""
    rel = rel.replace("\\", "/")
    return ((rel.startswith("Map/") and os.path.basename(rel).startswith("map_node")) or
            rel.startswith("UI/map_node_current"))


def main():
    dry = "--dry" in sys.argv
    erase_only = "--erase" in sys.argv
    if "--list" in sys.argv:
        dry = True
    files = []
    for f in FOLDERS:
        files += sorted(glob.glob(os.path.join(ART, f, "*.png")))
    changed, preview = [], []
    for p in files:
        rel = os.path.relpath(p, ART)
        oval = keep_oval(rel)
        if erase_only and oval:
            continue          # товары и ценники обрабатывает обычный запуск с овалами
        if not erase_only and not oval:
            continue          # остальное чистит запуск с --erase
        r = process(p, apply=not dry, draw_ovals=not erase_only)
        if r:
            changed.append((p, len(r)))
            if len(preview) < 24:
                preview.append(p)
    print(("НАЙДЕНО" if dry else "ИСПРАВЛЕНО"), len(changed), "файлов")
    if "--list" in sys.argv:
        for p, n in changed:
            print("  ", os.path.relpath(p, ART), n)
    if dry and preview:
        # контрольный лист: исходник сверху, снизу — строки с отмеченной тенью
        rows = []
        for p in preview:
            im = Image.open(p).convert("RGBA")
            W = 220
            im2 = im.resize((W, max(8, int(im.height * W / im.width))))
            arr = np.asarray(im).copy()
            for pix, bbox, _ in find_asterisks(arr):
                for y, x in pix:
                    arr[y, x] = (220, 30, 30, 255)
            mark = Image.fromarray(arr, "RGBA").resize((W, im2.height))
            row = Image.new("RGBA", (W * 2 + 12, im2.height), (255, 255, 255, 255))
            row.paste(im2, (0, 0), im2)
            row.paste(mark, (W + 12, 0), mark)
            rows.append(row)
        Hmax = max(r.height for r in rows)
        sheet = Image.new("RGBA", (rows[0].width, sum(r.height + 6 for r in rows)), (255, 255, 255, 255))
        y = 0
        for r in rows:
            sheet.paste(r, (0, y), r)
            y += r.height + 6
        out = os.path.normpath(os.path.join(ART, "..", "..", "..", "Temp", "shadow_fix_preview.png"))
        sheet.convert("RGB").save(out)
        print("лист для проверки:", os.path.abspath(out))


if __name__ == "__main__":
    main()

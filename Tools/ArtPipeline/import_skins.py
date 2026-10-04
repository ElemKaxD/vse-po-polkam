# -*- coding: utf-8 -*-
"""Перенос скинов (пак PROMPTS_Скины_ВсёПоПолкам_29-09) из yandex_games в игру, 30.09.2026.

python import_skins.py

* Стеллажи brd_section_<имя> — как есть (800×1968, те же пропорции и высоты полок, что у brd_section 1).
* Ленты brd_belt_tile_<имя> — обрезаются прозрачные поля сверху и снизу: у новых лент полоса занимает
  ~45 строк из 108, в игре лента кладётся плиткой в родных пропорциях (без растяжения), и поля сделали бы
  её вдвое тоньше исходной. Ролики brd_belt_roller_<имя> — значки конвейеров в «Гардеробе».
* Товары item_<id>_gift — обрезка до содержимого с полем 5 точек, как у исходных товаров
  (у сгенерированных вокруг 25–40 % пустоты — товар встал бы мельче остальных).
* Пустые файлы (целиком прозрачные) пропускаются с предупреждением.
Все стороны доводятся до кратных 4 (иначе WebGL не сожмёт текстуру).
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.abspath(os.path.join(ROOT, "..", "yandex_games", "Assets", "AllOnShelves", "Art"))
DST = os.path.join(ROOT, "Assets", "AllOnShelves", "Art")

SHELVES = ["birch", "candy", "metal", "winter", "gold"]
BELTS = ["wood", "candy", "neon", "winter", "gold"]


def up4(v):
    return (v + 3) // 4 * 4


def save(im, path):
    w, h = im.size
    if w % 4 or h % 4:
        canvas = Image.new("RGBA", (up4(w), up4(h)), (0, 0, 0, 0))
        canvas.paste(im, ((up4(w) - w) // 2, (up4(h) - h) // 2))
        im = canvas
    im.save(path)
    print("  ->", os.path.relpath(path, ROOT), im.size)


def content(im):
    a = np.array(im)[..., 3]
    ys, xs = np.where(a > 20)
    if len(xs) == 0:
        return None
    return xs.min(), ys.min(), xs.max(), ys.max()


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    for n in SHELVES:
        src = os.path.join(SRC, "Board", f"brd_section_{n}.png")
        im = Image.open(src).convert("RGBA")
        if content(im) is None:
            print("ПУСТОЙ ФАЙЛ, пропущен:", src)
            continue
        save(im, os.path.join(DST, "Board", f"brd_section_{n}.png"))
    for n in BELTS:
        src = os.path.join(SRC, "Board", f"brd_belt_tile_{n}.png")
        im = Image.open(src).convert("RGBA")
        box = content(im)
        if box is None:
            print("ПУСТОЙ ФАЙЛ, пропущен:", src)
            continue
        y0, y1 = max(0, box[1] - 2), min(im.size[1], box[3] + 3)
        save(im.crop((0, y0, im.size[0], y1)), os.path.join(DST, "Board", f"brd_belt_tile_{n}.png"))
        roller = os.path.join(SRC, "Board", f"brd_belt_roller_{n}.png")
        if os.path.exists(roller) and content(Image.open(roller).convert("RGBA")) is not None:
            save(Image.open(roller).convert("RGBA"), os.path.join(DST, "Board", f"brd_belt_roller_{n}.png"))
    for src in sorted(glob.glob(os.path.join(SRC, "Items", "item_*_gift.png"))):
        im = Image.open(src).convert("RGBA")
        box = content(im)
        if box is None:
            print("ПУСТОЙ ФАЙЛ, пропущен:", src)
            continue
        m = 5
        crop = im.crop((max(0, box[0] - m), max(0, box[1] - m), min(im.size[0], box[2] + m + 1), min(im.size[1], box[3] + m + 1)))
        save(crop, os.path.join(DST, "Items", os.path.basename(src)))
    for f in glob.glob(os.path.join(SRC, "Board", "brd_section_*.png")):
        name = os.path.basename(f)[len("brd_section_"):-4]
        if name not in SHELVES and name != "big" and content(Image.open(f).convert("RGBA")) is None:
            print("ПУСТОЙ ФАЙЛ (не из пака), пропущен:", os.path.basename(f))


if __name__ == "__main__":
    main()

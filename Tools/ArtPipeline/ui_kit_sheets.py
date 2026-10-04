# -*- coding: utf-8 -*-
"""Листы-«киты» со всей графикой интерфейса для генерации референсов другой нейросетью.

python ui_kit_sheets.py <out_dir>
Каждая картинка — в родных пропорциях, под ней имя файла. Листы: кнопки/панели, иконки, поле, товары,
персонажи, карта, магазин/ежедневки/альбом, ремонт.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "AllOnShelves", "Art")
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(OUT, exist_ok=True)
FONT = ImageFont.truetype("arialbd.ttf", 20)
TITLE = ImageFont.truetype("arialbd.ttf", 34)
BG = (236, 231, 222)


def files(folder, names=None, prefix=None):
    d = os.path.join(ART, folder)
    all_ = sorted(f[:-4] for f in os.listdir(d) if f.endswith(".png"))
    if prefix:
        all_ = [n for n in all_ if any(n.startswith(p) for p in prefix)]
    if names:
        all_ = [n for n in names if n in all_]
    return [(folder, n) for n in all_]


def sheet(name, title, items, cell=(300, 250), cols=6):
    cw, ch = cell
    rows = (len(items) + cols - 1) // cols
    W, H = cols * cw + 40, rows * ch + 110
    im = Image.new("RGB", (W, H), BG)
    dr = ImageDraw.Draw(im)
    dr.text((24, 22), title, fill=(74, 47, 36), font=TITLE)
    for i, (folder, n) in enumerate(items):
        x, y = 20 + (i % cols) * cw, 90 + (i // cols) * ch
        src = Image.open(os.path.join(ART, folder, n + ".png")).convert("RGBA")
        w0, h0 = src.size
        box_w, box_h = cw - 30, ch - 60
        k = min(box_w / w0, box_h / h0)
        pic = src.resize((max(1, int(w0 * k)), max(1, int(h0 * k))), Image.LANCZOS)
        im.paste(pic, (x + (cw - pic.width) // 2, y + 8 + (box_h - pic.height) // 2), pic)
        label = n
        tw = dr.textlength(label, font=FONT)
        dr.text((x + (cw - tw) / 2, y + ch - 44), label, fill=(60, 40, 30), font=FONT)
    path = os.path.join(OUT, name + ".png")
    im.save(path, optimize=True)
    print(path, im.size)


sheet("kit_01_buttons_panels", "Кнопки, плашки, панели (ui_*) — Art/UI", files("UI"), cell=(320, 260), cols=5)
sheet("kit_02_icons", "Иконки (icon_*) — Art/Icons, кладутся поверх ui_btn_round / кнопок", files("Icons"), cell=(220, 200), cols=9)
sheet("kit_03_board", "Поле уровня: лента, стеллаж, тележка, морозилка, коробки — Art/Board + Art/Overlays",
      files("Board") + files("Overlays"), cell=(300, 250), cols=6)
sheet("kit_04_items", "Товары (item_*) — Art/Items", files("Items"), cell=(220, 200), cols=7)
sheet("kit_05_characters", "Персонажи — Art/Characters", files("Characters"), cell=(280, 300), cols=6)
sheet("kit_06_map", "Карта: ценники уровней, аватар, фургон — Art/Map (фоны районов — map_segment_01..11)",
      files("Map", prefix=["map_node", "map_avatar", "map_van"]) + files("Map", names=["map_segment_01", "map_segment_05"]), cell=(320, 260), cols=5)
sheet("kit_07_shop_daily_album", "Магазин, завоз дня, альбом — Art/Shop + Art/Album + Art/FX + логотип",
      files("Shop") + files("Album", names=["album_book", "album_pack_closed", "album_pack_open", "sticker_apple_juice", "sticker_cheese_wheel"])
      + files("FX") + files("Logo"), cell=(280, 250), cols=6)
sheet("kit_08_renovation", "Ремонт: здания до/после и фоны улиц — Art/Meta (предметы ремонта meta_dXX_* — в той же папке)",
      files("Meta", prefix=["meta_kiosk", "meta_corner", "meta_minimarket", "meta_supermarket", "meta_hypermarket", "bg_meta"]), cell=(360, 280), cols=5)
sheet("kit_09_backgrounds", "Фоны уровней — Art/Backgrounds", files("Backgrounds"), cell=(380, 250), cols=5)

# -*- coding: utf-8 -*-
"""Склеивает скриншоты в сетку 2×2 (для экономного просмотра): python grid.py out.jpg a.jpg b.jpg c.jpg d.jpg"""
import sys
from PIL import Image, ImageDraw
out, files = sys.argv[1], sys.argv[2:]
cw, ch = 960, 540
sheet = Image.new("RGB", (cw * 2, ch * ((len(files) + 1) // 2)), (0, 0, 0))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB")
    im.thumbnail((cw, ch))
    x, y = (i % 2) * cw, (i // 2) * ch
    sheet.paste(im, (x, y))
    d.rectangle((x, y, x + cw - 1, y + ch - 1), outline=(255, 0, 255))
    d.text((x + 6, y + 4), f.replace("\\", "/").split("/")[-1], fill=(255, 0, 255))
sheet.save(out, quality=80)

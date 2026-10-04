# -*- coding: utf-8 -*-
"""Временные ореолы вокруг енота на карте (награды «Звёздного пути»).

Рисуем кольцо с элементами по кругу и мягким свечением. Это времянка: когда придут нарисованные
halo_* из промтов (Docs/PROMPTS_Пак_22-09_ВсёПоПолкам.txt), они заменят эти файлы.
Запуск: python gen_halos.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'AllOnShelves', 'Art', 'FX'))
SIZE = 512
S = 2                     # надмасштаб
OUTLINE = (74, 42, 24, 255)


def canvas():
    return Image.new('RGBA', (SIZE * S, SIZE * S), (0, 0, 0, 0))


def glow(im, color, radius=18):
    g = Image.new('RGBA', im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(g)
    c = im.size[0] / 2
    r = im.size[0] * 0.37
    d.ellipse((c - r, c - r, c + r, c + r), outline=color, width=int(im.size[0] * 0.07))
    return Image.alpha_composite(g.filter(ImageFilter.GaussianBlur(radius * S)), im)


def ring(draw_item, n, color, tilt=0.0):
    im = canvas()
    d = ImageDraw.Draw(im)
    c = im.size[0] / 2
    r = im.size[0] * 0.37
    for i in range(n):
        a = tilt + 2 * math.pi * i / n
        draw_item(d, c + math.cos(a) * r, c + math.sin(a) * r, a)
    return glow(im, color)


def leaf(d, x, y, a):
    w, h = 44 * S, 20 * S
    pts = [(x - w / 2, y), (x - w / 6, y - h), (x + w / 4, y - h * 0.8), (x + w / 2, y),
           (x + w / 6, y + h), (x - w / 4, y + h * 0.8)]
    rot = [(x + (px - x) * math.cos(a) - (py - y) * math.sin(a), y + (px - x) * math.sin(a) + (py - y) * math.cos(a)) for px, py in pts]
    d.polygon(rot, fill=(118, 190, 90, 255), outline=OUTLINE)


def petal(d, x, y, a):
    r = 17 * S
    d.ellipse((x - r, y - r, x + r, y + r), fill=(246, 206, 92, 255), outline=OUTLINE, width=3 * S)


def star(d, x, y, a):
    r, r2 = 22 * S, 9 * S
    pts = []
    for k in range(10):
        ang = a + math.pi * k / 5 - math.pi / 2
        rad = r if k % 2 == 0 else r2
        pts.append((x + math.cos(ang) * rad, y + math.sin(ang) * rad))
    d.polygon(pts, fill=(255, 216, 92, 255), outline=OUTLINE)


def crown(d, x, y, a):
    h, w = 30 * S, 22 * S
    pts = [(x - w / 2, y + h / 3), (x, y - h), (x + w / 2, y + h / 3)]
    d.polygon(pts, fill=(250, 196, 70, 255), outline=OUTLINE)
    r = 7 * S
    d.ellipse((x - r, y - h - r, x + r, y - h + r), fill=(214, 70, 70, 255), outline=OUTLINE)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    art = {
        'halo_leaf': ring(leaf, 14, (120, 200, 110, 150)),
        'halo_gold': ring(petal, 18, (250, 210, 120, 160)),
        'halo_stars': ring(star, 12, (255, 226, 130, 170)),
        'halo_crown': ring(crown, 10, (255, 200, 90, 180)),
    }
    for name, im in art.items():
        p = os.path.join(OUT, name + '.png')
        im.resize((SIZE, SIZE), Image.LANCZOS).save(p)
        print('saved', p)

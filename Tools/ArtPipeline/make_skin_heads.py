# -*- coding: utf-8 -*-
"""Аватарки-мордочки енота в скине (03.10.2026, пункт 20 правок).

Картинки скинов map_avatar_<скин> нарисованы во весь рост, а на карте и в профиле нужна только мордочка —
как у обычного енота map_avatar (круг в деревянной рамке). Скрипт вырезает голову из исходника скина
(без растяжения — только равномерный масштаб), кладёт её в кремовый круг и надевает рамку от map_avatar.
Пишет Art/Map/map_head_<скин>.png.  Запуск: python make_skin_heads.py [preview.png]
"""
import os
import sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
SRC = r'D:\Work\ЯндексИгры\LaserSiege\yandex_games\Assets\AllOnShelves\Art\Map'
DST = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Art', 'Map')

# кадр головы в долях фигуры (по непрозрачной области): x — центр, top/bottom — сверху вниз
HEADS = {
    'chef':   dict(cx=0.45, top=0.00, bottom=0.46),
    'winter': dict(cx=0.47, top=0.00, bottom=0.40),
    'pirate': dict(cx=0.46, top=0.00, bottom=0.42),
    'king':   dict(cx=0.52, top=0.00, bottom=0.42),
}
SIZE = 256


def ring_and_disc():
    """Рамка обычной аватарки и её внутренний круг (по пикселям map_avatar)."""
    base = Image.open(os.path.join(DST, 'map_avatar.png')).convert('RGBA')
    k = SIZE / max(base.size)
    base = base.resize((round(base.width * k), round(base.height * k)), Image.LANCZOS)
    canvas = Image.new('RGBA', (SIZE, SIZE))
    canvas.alpha_composite(base, ((SIZE - base.width) // 2, (SIZE - base.height) // 2))
    # внутренний кремовый круг: радиус ~ 0.40 размера (рамка ~ 10 % по краю)
    r_in = int(SIZE * 0.395)
    return canvas, r_in


def head(name, ring, r_in):
    src = Image.open(os.path.join(SRC, f'map_avatar_{name}.png')).convert('RGBA')
    bb = src.getchannel('A').point(lambda a: 255 if a > 20 else 0).getbbox()
    fig = src.crop(bb)
    p = HEADS[name]
    h = fig.height * (p['bottom'] - p['top'])
    side = h * 1.22                            # квадратный кадр чуть больше головы — шапка не режется рамкой
    cx = fig.width * p['cx']
    top = fig.height * p['top'] - h * 0.08
    box = (round(cx - side / 2), round(top), round(cx + side / 2), round(top + side))
    crop = Image.new('RGBA', (box[2] - box[0], box[3] - box[1]))
    crop.alpha_composite(fig.crop((max(0, box[0]), max(0, box[1]), min(fig.width, box[2]), box[3])),
                         (max(0, -box[0]), max(0, -box[1])))
    d = 2 * r_in
    crop = crop.resize((d, d), Image.LANCZOS)
    out = ring.copy()
    # кремовый круг и голова внутри него (сверху — шапка/корона может касаться рамки)
    disc = Image.new('L', (SIZE, SIZE))
    ImageDraw.Draw(disc).ellipse((SIZE / 2 - r_in, SIZE / 2 - r_in, SIZE / 2 + r_in, SIZE / 2 + r_in), fill=255)
    layer = Image.new('RGBA', (SIZE, SIZE))
    layer.alpha_composite(crop, (SIZE // 2 - r_in, SIZE // 2 - r_in + 4))
    layer.putalpha(Image.composite(layer.getchannel('A'), Image.new('L', (SIZE, SIZE)), disc))
    # фон круга — тот же кремовый, что у обычной аватарки (цвет из её середины, ниже мордочки)
    cream = (247, 234, 207, 255)   # кремовый фон обычной аватарки map_avatar
    bg = Image.new('RGBA', (SIZE, SIZE), cream[:3] + (255,))
    bg.putalpha(disc)
    frame = ring.copy()
    frame.putalpha(Image.composite(Image.new('L', (SIZE, SIZE)), ring.getchannel('A'), disc))   # только рамка
    out = Image.new('RGBA', (SIZE, SIZE))
    out.alpha_composite(bg)
    out.alpha_composite(layer)
    out.alpha_composite(frame)
    return out


def main():
    ring, r_in = ring_and_disc()
    outs = []
    for name in HEADS:
        im = head(name, ring, r_in)
        im.save(os.path.join(DST, f'map_head_{name}.png'))
        outs.append(im)
        print('map_head_' + name)
    if len(sys.argv) > 1:
        prev = Image.new('RGBA', (SIZE * (len(outs) + 1), SIZE), (80, 110, 80, 255))
        prev.alpha_composite(ring)
        for i, im in enumerate(outs):
            prev.alpha_composite(im, ((i + 1) * SIZE, 0))
        prev.save(sys.argv[1])


if __name__ == '__main__':
    main()

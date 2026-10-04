# -*- coding: utf-8 -*-
"""Полосы прогресса и ползунки: желобок, заливка и ручка с плоской серединой (их можно тянуть 9-slice).

Заливка белая со светотенью — цвет задаёт код (зелёный прогресс, красный затор, золотой «Звёздный путь»).
Рисуем с запасом и уменьшаем: края получаются гладкими. Запуск: python gen_bars.py
"""
import os

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'AllOnShelves', 'Art', 'UI'))
S = 4                                   # надмасштаб
OUTLINE = (74, 42, 24, 255)             # тёмно-коричневый кант, как у остальных плашек


def rounded(size, radius, fill, outline=None, width=0):
    im = Image.new('RGBA', (size[0] * S, size[1] * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), radius * S, fill=fill, outline=outline, width=width * S)
    return im


def track(w=512, h=96):
    """Желобок: коричневый кант, кремовая ниша с тенью сверху."""
    im = rounded((w, h), h // 2, (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = h // 2 * S
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, fill=(150, 104, 62, 255))
    inset = 9 * S
    d.rounded_rectangle((inset, inset, im.width - 1 - inset, im.height - 1 - inset), r - inset // 2, fill=(238, 224, 196, 255))
    # тень от канта внутри ниши
    sh = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((inset, inset, im.width - 1 - inset, im.height - 1 - inset + 6 * S),
                                         r - inset // 2, outline=(120, 90, 60, 110), width=5 * S)
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3 * S)))
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, outline=OUTLINE, width=6 * S)
    return im.resize((w, h), Image.LANCZOS)


def fill(w=512, h=72):
    """Заливка: белая с бликом сверху и затемнением снизу — тонируется цветом в коде."""
    im = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = h // 2 * S
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, fill=(255, 255, 255, 255))
    # нижняя половина чуть темнее — объём
    shade = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(shade).rounded_rectangle((0, im.height // 2, im.width - 1, im.height - 1), r, fill=(60, 60, 60, 46))
    im.alpha_composite(Image.composite(shade, Image.new('RGBA', im.size, (0, 0, 0, 0)), im.getchannel('A')))
    # блик
    gl = Image.new('RGBA', im.size, (0, 0, 0, 0))
    ImageDraw.Draw(gl).rounded_rectangle((r // 2, 5 * S, im.width - 1 - r // 2, int(h * 0.42) * S), r // 2,
                                         fill=(255, 255, 255, 150))
    im.alpha_composite(gl.filter(ImageFilter.GaussianBlur(S)))
    d.rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, outline=(120, 120, 120, 120), width=3 * S)
    return im.resize((w, h), Image.LANCZOS)


def knob(size=128):
    """Ручка ползунка: кремовый кружок с коричневым кантом и бликом."""
    im = Image.new('RGBA', (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = 5 * S
    d.ellipse((pad, pad, im.width - 1 - pad, im.height - 1 - pad), fill=(252, 243, 222, 255), outline=OUTLINE, width=7 * S)
    d.ellipse((im.width * 0.28, im.height * 0.2, im.width * 0.62, im.height * 0.42), fill=(255, 255, 255, 210))
    d.ellipse((im.width * 0.3, im.height * 0.62, im.width * 0.7, im.height * 0.78), fill=(214, 190, 150, 120))
    return im.resize((size, size), Image.LANCZOS)


if __name__ == '__main__':
    for name, im in (('ui_bar_track', track()), ('ui_bar_fill', fill()), ('ui_bar_knob', knob())):
        p = os.path.join(OUT, name + '.png')
        im.save(p)
        print('saved', p, im.size)

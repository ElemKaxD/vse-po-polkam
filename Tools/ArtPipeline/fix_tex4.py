# -*- coding: utf-8 -*-
"""Текстуры для WebGL: стороны кратны 4, не больше 2048 (27.09.2026).

Если сторона картинки не делится на 4, Unity не может сжать её в DXT/crunch и кладёт в билд
несжатой (RGBA32): слой ремонта 1024×685 весил 2,7 МБ вместо ~0,2 МБ, а таких было 75.
Картинку шире 2048 WebGL сам ужимает до 2048 — и сторона снова выходит некратной.

Что делает:
  * картинку больше 2048 уменьшает до 1024 по длинной стороне (на экране они не крупнее ~900 точек);
  * добивает стороны до кратных 4 прозрачными пикселями — поровну с двух сторон, чтобы центр
    не сдвигался (листы эффектов — только справа и снизу: у них кадры считаются от левого верхнего угла);
  * у слоёв ремонта meta_lego_dNN_iK пересчитывает X/Y/W/H в RenoLegoData.cs — слой ложится как раньше;
  * если поменялся стеллаж brd_section 1 — правит SectionView.Aspect.

Запуск: python fix_tex4.py [--dry]
"""
import os
import re
import sys

from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
A = os.path.join(ROOT, 'Assets', 'AllOnShelves')
LEGO_CS = os.path.join(A, 'Scripts', 'Services', 'RenoLegoData.cs')
SECTION_CS = os.path.join(A, 'Scripts', 'Game', 'SectionView.cs')
MAX = 2048
DOWN_TO = 1024


def pad4(im, corner=False):
    """Добить до кратных 4. Возвращает (картинка, left, top, right, bottom)."""
    w, h = im.size
    pw, ph = (4 - w % 4) % 4, (4 - h % 4) % 4
    if pw == 0 and ph == 0:
        return im, 0, 0, 0, 0
    l, t = (0, 0) if corner else (pw // 2, ph // 2)
    r, b = pw - l, ph - t
    if im.mode == 'RGBA':
        out = Image.new('RGBA', (w + pw, h + ph), (0, 0, 0, 0))
        out.paste(im, (l, t))
    else:
        # плотная картинка: край повторяем, чтобы не было чёрной полоски
        out = im.resize((w + pw, h + ph), Image.LANCZOS)
        l = t = r = b = 0
    return out, l, t, r, b


def fix_piece_in_cs(cs, sprite, w0, h0, l, t, r, b):
    """Пересчитать X/Y/W/H слоя в RenoLegoData.cs под добитую картинку."""
    pat = re.compile(r'(new Piece \{ Sprite = "' + re.escape(sprite) +
                     r'", X = )([-\d.]+)f, Y = ([-\d.]+)f, W = ([-\d.]+)f, H = ([-\d.]+)f')
    m = pat.search(cs)
    if not m:
        return cs, False
    X, Y, W, H = (float(m.group(i)) for i in range(2, 6))
    kx, ky = W / w0, H / h0                     # точек кадра на пиксель картинки
    W2, H2 = W + (l + r) * kx, H + (t + b) * ky
    X2 = X + (r - l) * kx / 2.0
    Y2 = Y - (b - t) * ky / 2.0                 # в кадре Y вверх, в картинке — вниз
    rep = f'{m.group(1)}{X2:.1f}f, Y = {Y2:.1f}f, W = {W2:.1f}f, H = {H2:.1f}f'
    return cs[:m.start()] + rep + cs[m.end():], True


def main():
    dry = '--dry' in sys.argv
    files = []
    for d, _, fs in os.walk(os.path.join(A, 'Art')):
        files += [os.path.join(d, f) for f in fs if f.endswith('.png')]
    vfx = os.path.join(A, 'Resources', 'VFX')
    if os.path.isdir(vfx):
        files += [os.path.join(vfx, f) for f in os.listdir(vfx) if f.endswith('.png')]
    cs = open(LEGO_CS, encoding='utf-8').read() if os.path.exists(LEGO_CS) else ''
    changed = 0
    section = None
    for p in sorted(files):
        name = os.path.basename(p)[:-4]
        im = Image.open(p)
        im.load()
        w0, h0 = im.size
        big = max(w0, h0) > MAX
        if not big and w0 % 4 == 0 and h0 % 4 == 0:
            continue
        if big:
            if name.startswith('meta_lego_'):
                print(f'  ! {name}: больше {MAX}, но это слой ремонта — пропускаю')
                continue
            k = DOWN_TO / max(w0, h0)
            im = im.resize((max(1, round(w0 * k)), max(1, round(h0 * k))), Image.LANCZOS)
        w1, h1 = im.size
        out, l, t, r, b = pad4(im, corner=p.startswith(vfx))
        print(f'  {name}: {w0}×{h0} → {out.size[0]}×{out.size[1]}')
        changed += 1
        if name.startswith('meta_lego_d') and '_i' in name and not big:
            cs, ok = fix_piece_in_cs(cs, name, w1, h1, l, t, r, b)
            if not ok:
                print(f'    (в RenoLegoData.cs слоя {name} нет — позиция не нужна)')
        if name == 'brd_section 1':
            section = out.size
        if not dry:
            out.save(p)
    if not dry and cs:
        open(LEGO_CS, 'w', encoding='utf-8').write(cs)
    if section and not dry:
        s = open(SECTION_CS, encoding='utf-8').read()
        s2 = re.sub(r'public const float Aspect = \d+f / \d+f;',
                    f'public const float Aspect = {section[0]}f / {section[1]}f;', s)
        open(SECTION_CS, 'w', encoding='utf-8').write(s2)
        print(f'  SectionView.Aspect = {section[0]}/{section[1]}')
    print(f'исправлено картинок: {changed}' + (' (пробный прогон)' if dry else ''))


if __name__ == '__main__':
    main()

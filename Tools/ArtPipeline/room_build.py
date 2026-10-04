# -*- coding: utf-8 -*-
"""Комнаты Торгового дома (v3.1, 03.10.2026): серии room_NN_step01..07 → голая комната + 6 слоёв декора.

Серии генератора не всегда «чистое стирание»: предмет иногда возвращается в следующем кадре, а кадр
чуть перерисовывается. Поэтому слои режутся не по соседним кадрам (как lego_extract), а так:
  * основа — последний кадр (step07), полный вид — step01;
  * пиксель, которым step01 отличается от основы, принадлежит тому предмету, после снятия которого
    он больше ни в одном кадре не похож на step01 («ушёл навсегда»);
  * содержимое слоя берётся из step01 — поэтому все 6 слоёв поверх основы дают ровно step01.
Порядок покупки: последний снятый предмет покупается первым (как в ремонте).

  python room_build.py [номера комнат]      → Art/Team/Rooms/room_NN_base.png, room_NN_iK.png,
                                              Scripts/Services/RoomLegoData.cs, отчёт по слоям
  --preview <папка>                         → картинки всех 7 состояний каждой комнаты для проверки
"""
import os
import re
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = r'D:\Work\ЯндексИгры\LaserSiege\yandex_games\Assets\AllOnShelves\Art\Team\Rooms'
DST = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Art', 'Team', 'Rooms')
CS = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Scripts', 'Services', 'RoomLegoData.cs')

W, H = 1672, 940          # родной размер генератора, стороны кратны 4 — без увеличения
DIFF = 34                 # различие канала, ниже — «тот же пиксель»
MIN_AREA = 600            # кусок меньше — шум
STEPS = 7


def load(room, s):
    im = Image.open(os.path.join(SRC, f'room_{room:02d}_step{s:02d}.png')).convert('RGB')
    if im.size != (W, H):
        im = im.resize((W, H), Image.LANCZOS)
    return np.asarray(im).astype(np.int16)


def same(a, b):
    d = np.abs(a - b).max(axis=2)
    # сглаживание: одиночные пиксели сжатия не считаются различием
    return ndimage.uniform_filter(d.astype(np.float32), 5) < DIFF


def build(room):
    f = [load(room, s) for s in range(1, STEPS + 1)]
    full, base = f[0], f[-1]
    diff = ~same(full, base)
    diff = ndimage.binary_opening(diff, iterations=2)
    # r(p): номер шага (2..7), начиная с которого пиксель ни разу не похож на полный вид
    like_full = np.stack([same(f[i], full) for i in range(STEPS)])       # [7, H, W]
    last_like = np.where(like_full, np.arange(STEPS)[:, None, None], -1).max(axis=0)  # последний кадр «как полный»
    gone_at = last_like + 1                                              # 1..6 → предмет снят на переходе в этот кадр
    layers = []
    for k in range(1, STEPS):          # переход k-1 → k (0-индексация кадров): снят предмет №k
        m = diff & (gone_at == k)
        m = ndimage.binary_closing(m, iterations=3)
        m = ndimage.binary_opening(m, iterations=2)
        lab, n = ndimage.label(m)
        if n:
            sizes = ndimage.sum(m, lab, range(1, n + 1))
            keep = np.isin(lab, 1 + np.nonzero(sizes >= MIN_AREA)[0])
            m = ndimage.binary_fill_holes(keep)
        layers.append(m)
    # кадр, сделанный не из предыдущего, а снова из полного, «не снимает» ничего — а соседний снимает два
    # предмета сразу. Пустой слой забирает половину предметов (кусков) у самого населённого соседа
    for k in range(len(layers)):
        if layers[k].mean() >= 0.003:
            continue
        best = None
        for d in range(len(layers)):
            lab, n = ndimage.label(layers[d])
            if n >= 2 and (best is None or layers[d].sum() > best[1]):
                best = (d, layers[d].sum(), lab, n)
        if best is None:
            continue
        d, _, lab, n = best
        idx = range(1, n + 1)
        area = np.array(ndimage.sum(layers[d], lab, idx))
        cx = np.array([c[1] for c in ndimage.center_of_mass(layers[d], lab, idx)])
        order = np.argsort(cx)
        cum = np.cumsum(area[order]) / area.sum()
        cut_at = int(np.searchsorted(cum, 0.5)) + 1
        cut_at = min(max(cut_at, 1), n - 1)
        a, b = order[:cut_at] + 1, order[cut_at:] + 1
        ma, mb = np.isin(lab, a), np.isin(lab, b)
        small, big = (ma, mb) if ma.sum() < mb.sum() else (mb, ma)
        # раньше покупается слой с большим индексом k → ему меньший кусок
        layers[max(k, d)], layers[min(k, d)] = small, big
    return full, base, layers


def pieces(mask, gap=24):
    """Предмет часто из нескольких кусков по всей комнате (цветы слева, часы справа): одна общая рамка
    вышла бы на полкадра прозрачной пустоты. Куски, между которыми меньше gap пикселей, режем вместе."""
    near = ndimage.binary_dilation(mask, iterations=gap // 2)
    lab, n = ndimage.label(near)
    return [mask & (lab == i) for i in range(1, n + 1)]


def cut(full, mask, feather=2):
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min() - 3, xs.max() + 4, ys.min() - 3, ys.max() + 4
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(W, x1), min(H, y1)
    # стороны кратны 4 (иначе WebGL кладёт текстуру несжатой)
    x1 = min(W, x0 + ((x1 - x0 + 3) // 4) * 4); y1 = min(H, y0 + ((y1 - y0 + 3) // 4) * 4)
    x0 = x1 - ((x1 - x0) // 4) * 4; y0 = y1 - ((y1 - y0) // 4) * 4
    a = ndimage.binary_dilation(mask, iterations=feather).astype(np.float32)
    a = np.clip(ndimage.gaussian_filter(a, 1.2), 0, 1)
    rgba = np.dstack([full.astype(np.uint8), (a * 255).astype(np.uint8)])
    im = Image.fromarray(rgba[y0:y1, x0:x1], 'RGBA')
    return im, (int(x0), int(y0), int(x1 - x0), int(y1 - y0))


def compose(base, full, layers, n):
    """Вид комнаты, когда куплено n предметов (покупаются с конца: первым — снятый последним)."""
    out = base.copy()
    for k in range(STEPS - 1 - n, STEPS - 1):
        m = layers[k]
        out[m] = full[m]
    return out


def copy_house():
    """Дом снаружи: готовый и в лесах — два кадра одного дома, игра режет их по этажам. Не обрезаются,
    чтобы совпадали пиксель в пиксель. Слой «стройка» поверх комнаты — в размер кадра комнаты."""
    team = os.path.join(os.path.dirname(SRC))
    out = os.path.join(os.path.dirname(DST))
    for f in ('house_open', 'house_scaffold'):
        im = Image.open(os.path.join(team, f + '.png')).convert('RGBA')
        w, h = im.width // 4 * 4, im.height // 4 * 4
        im.crop((0, 0, w, h)).save(os.path.join(out, f + '.png'), optimize=True)
    im = Image.open(os.path.join(SRC, 'room_build_overlay.png')).convert('RGBA')
    if im.size != (W, H):
        im = im.crop((0, 0, min(im.width, W), min(im.height, H))).resize((W, H), Image.LANCZOS)
    im.save(os.path.join(DST, 'room_build_overlay.png'), optimize=True)
    bg = Image.open(os.path.join(team, 'team_bg.png')).convert('RGB')
    bg.crop((0, 0, bg.width // 4 * 4, bg.height // 4 * 4)).save(os.path.join(out, 'team_bg.png'), optimize=True)


def main():
    copy_house()
    args = [a for a in sys.argv[1:]]
    preview = None
    if '--preview' in args:
        i = args.index('--preview'); preview = args[i + 1]; del args[i:i + 2]
    rooms = [int(a) for a in args] or list(range(1, 13))
    os.makedirs(DST, exist_ok=True)
    data = load_cs()
    for r in rooms:
        full, base, layers = build(r)
        print(f'Комната {r:02d}:')
        for f in os.listdir(DST):
            if re.match(rf'room_{r:02d}_i\d', f):
                os.remove(os.path.join(DST, f))
        items = []
        for buy in range(1, STEPS):                 # buy 1 — снятый последним (layers[5])
            m = layers[STEPS - 1 - buy]
            area = m.mean() * 100
            warn = ''
            if area < 0.3: warn = '  ← ПРЕДМЕТА НЕ ВИДНО (кадр не стёр ничего)'
            elif area > 22: warn = '  ← СЛИШКОМ БОЛЬШОЙ (кадр перерисован, а не стёрт)'
            parts = pieces(m) if m.any() else []
            for j, pm in enumerate(parts):
                im, rect = cut(full, pm)
                name = f'room_{r:02d}_i{buy}_{j + 1}'
                im.save(os.path.join(DST, name + '.png'), optimize=True)
                items.append((buy, name, rect))
            print(f'  предмет {buy}: {area:.1f}% кадра, кусков {len(parts)}{warn}')
        Image.fromarray(base.astype(np.uint8), 'RGB').save(os.path.join(DST, f'room_{r:02d}_base.png'))
        data[r] = items
        if preview:
            os.makedirs(preview, exist_ok=True)
            sheet = Image.new('RGB', (4 * 480, 2 * 270), (60, 60, 60))
            for n in range(STEPS):
                im = Image.fromarray(compose(base, full, layers, n).astype(np.uint8)).resize((480, 270))
                sheet.paste(im, ((n % 4) * 480, (n // 4) * 270))
            sheet.save(os.path.join(preview, f'room{r:02d}_states.jpg'), quality=85)
    save_cs(data)


def load_cs():
    data = {}
    if not os.path.exists(CS):
        return data
    for m in re.finditer(r'// room (\d+)\n((?:\s+new Piece\(.*\n)*)', open(CS, encoding='utf-8').read()):
        rows = re.findall(r'new Piece\((\d+), "([^"]*)", (\d+), (\d+), (\d+), (\d+)\)', m.group(2))
        data[int(m.group(1))] = [(int(i), n, tuple(map(int, v))) for i, n, *v in rows]
    return data


def save_cs(data):
    open(CS, 'w', encoding='utf-8').write(render(data))


def render(data):
    head = ['// Создаётся Tools/ArtPipeline/room_build.py — руками не править.',
            'namespace AllOnShelves',
            '{',
            '    /// <summary>Декор комнат Торгового дома (03.10.2026). Предмет (1..6 — порядок покупки) состоит из',
            '    /// кусков: картинка и её прямоугольник в кадре комнаты (пиксели от левого верхнего угла FrameW×FrameH).',
            '    /// Голая комната — room_NN_base, все куски поверх неё дают полностью обставленную комнату.</summary>',
            '    public static class RoomLegoData',
            '    {',
            f'        public const int FrameW = {W}, FrameH = {H}, Items = {STEPS - 1};',
            '',
            '        public readonly struct Piece',
            '        {',
            '            public readonly int Item; public readonly string Sprite; public readonly int X, Y, W, H;',
            '            public Piece(int item, string s, int x, int y, int w, int h) { Item = item; Sprite = s; X = x; Y = y; W = w; H = h; }',
            '        }',
            '',
            '        public static readonly Piece[][] Rooms =',
            '        {']
    body = []
    for r in range(1, 13):
        body.append('            new Piece[] {')
        body.append(f'                // room {r}')
        for item, name, (x, y, w, h) in data.get(r, []):
            body.append(f'                new Piece({item}, "{name}", {x}, {y}, {w}, {h}),')
        body.append('            },')
    return '\n'.join(head + body + ['        };', '    }', '}', ''])


if __name__ == '__main__':
    main()

# -*- coding: utf-8 -*-
"""Сборка ремонта-«лего» из серий картинок магазина в готовые файлы для игры.

Берёт серии yandex_games/.../Art/Meta/lego/meta_storeN_stepNN.png (step00 — полностью улучшенный
магазин, последний файл — голый), режет их через lego_extract и кладёт в проект:

  Art/Meta/lego/meta_lego_dNN_bg.png     фон улицы без магазина (если прислан meta_storeN_bg)
  Art/Meta/lego/meta_lego_dNN_base.png   голый магазин района (фон экрана ремонта)
  Art/Meta/lego/meta_lego_dNN_iK.png     одно улучшение с прозрачным фоном
  Scripts/Services/RenoLegoData.cs       позиции и размеры улучшений

Слой вырезан из того же кадра, поэтому ложится на базу пиксель в пиксель — «как лего».
Порядок: последний снятый предмет покупается первым, первый снятый (самый заметный) — последним.

Запуск: python lego_build.py [районы через пробел]
  без аргументов — все районы; с аргументами — только они, остальные районы в RenoLegoData.cs
  и их картинки остаются как были.
"""
import os
import re
import shutil
import sys
import tempfile

import numpy as np

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.stdout.reconfigure(encoding='utf-8')

import lego_extract  # noqa: E402
import fix_tex4  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = r'D:\Work\ЯндексИгры\LaserSiege\yandex_games\Assets\AllOnShelves\Art\Meta\lego'
DST = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Art', 'Meta', 'lego')
CS = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Scripts', 'Services', 'RenoLegoData.cs')

# какая серия какому району ремонта: серии 1–4 совпали по числу улучшений с районами 1–4
SERIES = {d: f'meta_store{d}' for d in range(1, 16)}
# кадры серии, которые брать (у store4 последний файл — чужая картинка)
LIMIT = {4: 8}
# сколько улучшений у района (файлов в серии должно быть на один больше)
ITEMS = {1: 5, 2: 6, 3: 7, 4: 7, 5: 8, 6: 8, 7: 9, 8: 9, 9: 9, 10: 10, 11: 10,
         # районы 12–15 (v4, 03.10.2026): по 9 улучшений
         12: 9, 13: 9, 14: 9, 15: 9}
BASE_W = 1280          # голый магазин в игре — фон экрана ремонта
PIECE_W = 1024         # слой крупнее этого мылить не страшно: он рисуется в размер кадра
BIG = 1920 * 1080 * 0.18   # закрашено больше 18% кадра — инпейнт задел фон, без прилёта
# соседние кадры серии отличаются больше чем на эту долю — это не стирание одного предмета,
# а перерисованный кадр (или вообще чужая картинка): такую серию не берём (27.09.2026)
MAX_STEP_CHANGE = 0.45


def step_changes(paths):
    """Доля изменившихся пикселей между соседними кадрами (с учётом прозрачности)."""
    arr = []
    for p in paths:
        im = Image.open(p).convert('RGBA').resize((480, 270))
        a = np.asarray(im).astype(np.float32)
        arr.append(np.dstack([a[..., :3] * a[..., 3:4] / 255.0, a[..., 3:4]]))
    out = []
    for i in range(len(arr) - 1):
        d = np.abs(arr[i] - arr[i + 1]).sum(2) > 60
        out.append(float(d.mean()))
    return out


def series_problem(paths):
    """Почему серию нельзя резать (None — можно)."""
    for p in paths:
        a = np.asarray(Image.open(p).convert('RGBA'))[..., 3]
        if (a < 16).mean() < 0.05:
            return f'{os.path.basename(p)} без прозрачности (нарисованный фон или «шахматка»)'
    ch = step_changes(paths)
    for i, c in enumerate(ch):
        if c > MAX_STEP_CHANGE:
            return (f'{os.path.basename(paths[i])} → {os.path.basename(paths[i + 1])}: изменилось '
                    f'{c * 100:.0f}% кадра — это не стирание одного предмета')
    return None


def old_entries():
    """Строки уже собранных районов из RenoLegoData.cs — чтобы не потерять их при частичной сборке."""
    if not os.path.exists(CS):
        return {}
    txt = open(CS, encoding='utf-8').read()
    out = {}
    for m in re.finditer(r'        static readonly (Piece|float)\[\] ([DB])(\d\d) = .*?;\n', txt):
        out.setdefault(int(m.group(3)), {})[m.group(2)] = m.group(0)
    return out


class Args:
    def __init__(self, folder):
        self.folder = folder
        self.out = None
        self.json = None


def build(only=None):
    os.makedirs(DST, exist_ok=True)
    tmp = tempfile.mkdtemp(prefix='lego_')
    data = {}
    keep = old_entries()
    for district, store in sorted(SERIES.items()):
        if only and district not in only:
            continue
        # step00 — концепт-кадр целиком (часто с фоном), в серию он не входит (пак 27.09)
        files = sorted(f for f in os.listdir(SRC) if f.startswith(store + '_step') and f.endswith('.png')
                       and not f.startswith(store + '_step00'))
        files = files[:LIMIT.get(district, len(files))]
        why = series_problem([os.path.join(SRC, f) for f in files]) if len(files) >= 2 else None
        if why:
            print(f'район {district}: серию не беру — {why}. Остаётся прежний ремонт.')
            continue
        if len(files) < 2:
            print(f'район {district}: серии {store} нет, пропускаю')
            continue
        need = ITEMS.get(district, 0) + 1
        if need > 1 and len(files) != need:
            print(f'  район {district}: в серии {len(files)} файлов, а нужно {need} '
                  f'({ITEMS[district]} улучшений + голый магазин)')
        work = os.path.join(tmp, store)
        os.makedirs(work, exist_ok=True)
        for f in files:
            shutil.copy(os.path.join(SRC, f), os.path.join(work, f))
        out = os.path.join(tmp, store + '_out')
        os.makedirs(out, exist_ok=True)
        lego_extract.series(Args(work), out, store, files)

        # фон улицы отдельным файлом, если он нарисован (пак 26.09): магазин ляжет на него слоем
        bg_src = os.path.join(SRC, store + '_bg.png')
        if os.path.exists(bg_src):
            bg = Image.open(bg_src)
            # фон обязан быть плотным кадром 16:9; прозрачный или квадратный — это не фон
            ratio = bg.width / bg.height
            hole = bg.mode == 'RGBA' and (bg.getchannel('A').point(lambda v: 255 if v < 96 else 0)
                                          .convert('L').getbbox() is not None)
            if abs(ratio - 16 / 9) > 0.15 or hole:
                print(f'  район {district}: файл {store}_bg.png не похож на фон '
                      f'({bg.width}×{bg.height}{", с прозрачностью" if hole else ""}) — пропускаю')
            else:
                bg = bg.convert('RGB').resize((1920, 1080), Image.LANCZOS)
                bg.save(os.path.join(DST, f'meta_lego_d{district:02d}_bg.png'))
                print(f'  район {district}: фон улицы взят отдельной картинкой')

        # голый магазин
        base = Image.open(os.path.join(out, f'{store}_base.png'))
        # границы вырезанного магазина в координатах кадра 1920×1080 (центр — 0, вверх — плюс):
        # по ним экран ремонта вписывает магазин в свободную зону (27.09.2026)
        bounds = None
        if base.mode == 'RGBA':
            A = np.asarray(base.getchannel('A'))
            ys, xs = np.nonzero(A > 16)
            if len(xs) and (A < 16).mean() > 0.05:
                sx, sy = 1920.0 / base.width, 1080.0 / base.height
                bounds = (xs.min() * sx - 960, 540 - (ys.max() + 1) * sy, (xs.max() + 1) * sx - 960, 540 - ys.min() * sy)
        # магазин может быть вырезан на прозрачный фон (пак 26.09) — тогда альфу сохраняем
        if base.mode == 'RGBA' and base.getchannel('A').getextrema()[0] == 255:
            base = base.convert('RGB')          # сплошной кадр — альфа не нужна
        base.thumbnail((BASE_W, BASE_W), Image.LANCZOS)
        base.save(os.path.join(DST, f'meta_lego_d{district:02d}_base.png'))

        pieces = []
        import json as js
        j = js.load(open(os.path.join(out, f'{store}_lego.json'), encoding='utf-8'))
        # в игре первым покупают последнее снятое улучшение: переворачиваем список
        for k, it in enumerate(reversed(j['items']), start=1):
            im = Image.open(os.path.join(out, it['file'])).convert('RGBA')
            name = f'meta_lego_d{district:02d}_i{k}'
            # «инпейнт задел фон» — это про реальную площадь закрашенного, а не про габариты рамки:
            # гирлянда или баннер во всю витрину имеют огромную рамку при крошечной площади (26.09.2026)
            big = it.get('area', it['size']['x'] * it['size']['y']) > BIG
            # слой шире 60% кадра (генератор перерисовал весь фасад) — проявляется на месте, без прилёта
            big = big or it['size']['x'] > 1920 * 0.6
            im.thumbnail((PIECE_W, PIECE_W), Image.LANCZOS)
            # стороны кратны 4, иначе WebGL оставит слой несжатым (fix_tex4.py)
            w1, h1 = im.size
            im, l, t, r, b = fix_tex4.pad4(im)
            im.save(os.path.join(DST, name + '.png'))
            kx, ky = it['size']['x'] / w1, it['size']['y'] / h1
            pieces.append(dict(name=name, x=round(it['pos']['x'] + (r - l) * kx / 2, 1),
                               y=round(it['pos']['y'] - (b - t) * ky / 2, 1),
                               w=round(it['size']['x'] + (l + r) * kx, 1), h=round(it['size']['y'] + (t + b) * ky, 1),
                               fly=not big))
            print(f'  район {district} предмет {k}: {name} {it["size"]["x"]}×{it["size"]["y"]}'
                  + ('' if not big else '  (слой во весь кадр — только вспышка, без прилёта)'))
        data[district] = {'pieces': pieces, 'bounds': bounds}
        keep.pop(district, None)
    shutil.rmtree(tmp, ignore_errors=True)
    write_cs(data, keep)


def write_cs(data, keep=None):
    keep = keep or {}
    lines = ['namespace AllOnShelves',
             '{',
             '    /// <summary>',
             '    /// Ремонт-«лего» (23.09.2026): голый магазин района и улучшения, вырезанные из одного',
             '    /// и того же кадра, поэтому ложатся на него пиксель в пиксель.',
             '    /// Файл создаёт Tools/ArtPipeline/lego_build.py — руками не править.',
             '    /// </summary>',
             '    public static class RenoLegoData',
             '    {',
             '        public struct Piece',
             '        {',
             '            public string Sprite;',
             '            public float X, Y, W, H;',
             '            public bool Fly;      // слой годится для прилёта (не размазан по кадру)',
             '        }',
             '']
    everyone = sorted(set(data) | set(keep))
    for d in everyone:
        if d not in data:
            for k in ('D', 'B'):
                if k in keep[d]:
                    lines.append(keep[d][k].rstrip('\n'))
            continue
        arr = ', '.join('new Piece { Sprite = "%s", X = %sf, Y = %sf, W = %sf, H = %sf, Fly = %s }'
                        % (p['name'], p['x'], p['y'], p['w'], p['h'], 'true' if p['fly'] else 'false')
                        for p in data[d]['pieces'])
        lines.append(f'        static readonly Piece[] D{d:02d} = {{ {arr} }};')
        b = data[d]['bounds']
        if b:
            lines.append(f'        static readonly float[] B{d:02d} = {{ {b[0]:.0f}f, {b[1]:.0f}f, {b[2]:.0f}f, {b[3]:.0f}f }};')
    withBounds = [d for d in everyone if (d in data and data[d]['bounds']) or (d not in data and 'B' in keep[d])]
    lines += ['',
              '        public static readonly int[] Districts = { %s };' % ', '.join(str(d) for d in everyone),
              '',
              '        /// <summary>Улучшения района в порядке покупки (пусто — у района нет «лего»-серии).</summary>',
              '        public static Piece[] Pieces(int district)',
              '        {',
              '            switch (district)',
              '            {']
    for d in everyone:
        lines.append(f'                case {d}: return D{d:02d};')
    lines += ['                default: return null;',
              '            }',
              '        }',
              '',
              '        public static bool Has(int district) => Pieces(district) != null;',
              '',
              '        /// <summary>',
              '        /// Границы вырезанного магазина в кадре 1920×1080 (x0, y0, x1, y1; центр — 0, вверх — плюс).',
              '        /// null — магазин нарисован вместе с улицей, вписывать его некуда.',
              '        /// </summary>',
              '        public static float[] Bounds(int district)',
              '        {',
              '            switch (district)',
              '            {']
    for d in withBounds:
        lines.append(f'                case {d}: return B{d:02d};')
    lines += ['                default: return null;',
              '            }',
              '        }',
              '',
              '        /// <summary>Картинка голого магазина района.</summary>',
              '        public static string Base(int district) => Has(district) ? $"meta_lego_d{district:00}_base" : null;',
              '    }',
              '}',
              '']
    open(CS, 'w', encoding='utf-8').write('\n'.join(lines))
    print('готово:', CS)


if __name__ == '__main__':
    build({int(a) for a in sys.argv[1:]} or None)

# -*- coding: utf-8 -*-
"""Нарезка улучшений ремонта («лего») из серии картинок магазина.

Как пользоваться:
1. Нарисовать ОДНУ картинку полностью улучшенного магазина вместе с фоном — meta_storeN_step00.png.
2. Инпейнтом убирать по одному улучшению, сохраняя meta_storeN_step01.png, step02.png … Последний файл —
   голый магазин. Камера и свет не меняются, инпейнт трогает только закрашенную область.
3. python lego_extract.py <папка с серией> [--out <папка>] [--json <файл>]

Скрипт сравнивает соседние картинки: что исчезло — то и есть улучшение. Пишет PNG с прозрачным фоном
(обрезанный по предмету) и JSON с местом предмета в кадре 1920×1080 от центра — его читает RenoLayoutData.

Проверка: в конце печатает, сколько предметов найдено и какую площадь занимает каждый; если предмет
«размазан» по всему кадру — значит инпейнт поменял и фон, такой шаг надо перерисовать.
"""
import argparse
import json
import os
import re

import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.stdout.reconfigure(encoding='utf-8')

W, H = 1920, 1080
DIFF = 26          # порог различия по каналу (0..255): ниже — считаем, что пиксель не менялся
MIN_AREA = 900     # меньше — шум инпейнта


def load(path):
    """RGBA: с 26.09.2026 магазин приходит вырезанным на прозрачный фон, альфу терять нельзя."""
    im = Image.open(path).convert('RGBA')
    if im.size != (W, H):
        im = im.resize((W, H), Image.LANCZOS)
    return np.asarray(im).astype(np.int16)


def layer(full, without):
    """Маска того, что есть на full и пропало на without."""
    d = np.abs(full - without).max(axis=2)
    m = d > DIFF
    m = ndimage.binary_closing(m, iterations=3)
    m = ndimage.binary_opening(m, iterations=2)
    lab, n = ndimage.label(m)
    if n == 0:
        return None
    sizes = np.array(ndimage.sum(m, lab, range(1, n + 1)))
    big = sizes.max()
    # главный кусок — это и есть улучшение; рядом с ним оставляем только соседей его размера,
    # всё остальное (инпейнт чуть перерисовал фон по всему кадру) выбрасываем
    main = int(sizes.argmax()) + 1
    ys, xs = np.nonzero(lab == main)
    pad = 0.4 * max(xs.max() - xs.min(), ys.max() - ys.min()) + 40
    bx0, bx1, by0, by1 = xs.min() - pad, xs.max() + pad, ys.min() - pad, ys.max() + pad
    boxes = ndimage.find_objects(lab)
    keep = np.zeros_like(m)
    for i, s in enumerate(sizes):
        if s < max(MIN_AREA, big * 0.12):
            continue
        sl = boxes[i]
        if sl is None:
            continue
        if sl[1].stop < bx0 or sl[1].start > bx1 or sl[0].stop < by0 or sl[0].start > by1:
            continue
        keep |= lab == i + 1
    return keep if keep.any() else None


def cut(full, mask, feather=2):
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    alpha = ndimage.binary_dilation(mask, iterations=feather).astype(np.float32)
    alpha = ndimage.gaussian_filter(alpha, 1.2)
    # прозрачность исходника остаётся прозрачностью слоя (дыры в решётках и проёмах)
    alpha = np.clip(alpha, 0, 1) * (full[..., 3].astype(np.float32) / 255.0)
    rgba = np.dstack([full[..., :3].astype(np.uint8), (np.clip(alpha, 0, 1) * 255).astype(np.uint8)])
    im = Image.fromarray(rgba[y0:y1, x0:x1], 'RGBA')
    cx = (x0 + x1) / 2 - W / 2
    cy = H / 2 - (y0 + y1) / 2
    return im, (round(float(cx), 1), round(float(cy), 1)), (int(x1 - x0), int(y1 - y0))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('folder')
    ap.add_argument('--out', default=None)
    ap.add_argument('--json', default=None)
    a = ap.parse_args()
    out = a.out or os.path.join(a.folder, 'layers')
    os.makedirs(out, exist_ok=True)

    allfiles = sorted(f for f in os.listdir(a.folder) if re.search(r'step\d+\.png$', f, re.I))
    groups = {}
    for f in allfiles:
        groups.setdefault(re.sub(r'_step\d+\.png$', '', f, flags=re.I), []).append(f)
    if not groups:
        print('нужна серия из двух и более файлов stepNN.png')
        return 1
    for store, files in sorted(groups.items()):
        series(a, out, store, files)
    return 0


def series(a, out, store, files):
    print(f'{store}: шагов {len(files)}')

    result = {'store': store, 'base': f'{store}_base.png', 'items': []}
    imgs = [load(os.path.join(a.folder, f)) for f in files]
    for i in range(len(imgs) - 1):
        mask = layer(imgs[i], imgs[i + 1])
        if mask is None:
            print(f'  {files[i]} → {files[i + 1]}: разницы нет, шаг пропущен')
            continue
        im, pos, size = cut(imgs[i], mask)
        name = f'{store}_item{i + 1:02d}.png'
        im.save(os.path.join(out, name))
        area = int(mask.sum())
        flag = '  ВНИМАНИЕ: слой занимает почти весь кадр — инпейнт задел фон' if area > W * H * 0.25 else ''
        print(f'  {name}: {size[0]}×{size[1]} в точке {pos}, площадь {area}{flag}')
        result['items'].append({'file': name, 'pos': {'x': pos[0], 'y': pos[1]}, 'size': {'x': size[0], 'y': size[1]},
                                'area': area})
    # последний файл серии — голый магазин (с прозрачностью, если он вырезан)
    Image.fromarray(imgs[-1].astype(np.uint8), 'RGBA').save(os.path.join(out, result['base']))
    path = os.path.join(out, f'{store}_lego.json')
    json.dump(result, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('готово:', path)


if __name__ == '__main__':
    raise SystemExit(main())

# -*- coding: utf-8 -*-
"""Расстановка ценников уровней по дороге на картинке района.

python map_nodes.py [preview_dir]
Для каждого Art/Map/map_segment_NN.png: маска асфальта (серый, без насыщенности) → самая большая область →
скелет → самый длинный путь → 10 точек равномерно по длине пути внутри безопасной зоны экрана
(сверху лента района, снизу меню, по бокам стрелки). Пишет Editor/MapRoadData.cs (Nodes) и Tools/map_nodes.json.
Координаты — в кадре 1920×1080 от центра (y вверх), как у ценников в SceneBuilder.Hub.
"""
import json
import os
import sys
from collections import deque

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from skimage.morphology import skeletonize

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MAP = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Art', 'Map')
W, H = 1920, 1080
# безопасная зона центров ценников (от центра экрана): лента района сверху, меню снизу, стрелки и кнопки по бокам
SAFE_X, SAFE_TOP, SAFE_BOTTOM = 640, 300, -300
COUNT = 10


# особые районы: 11-й — закат (асфальт тёплый, насыщеннее), 10-й — дорогу разрывает мост
TUNE = {10: dict(close=9), 11: dict(sat=0.30, lo=0.30, hi=0.80),
        # районы 12–15 (v4): асфальт тёплый, как у 11-го
        **{d: dict(sat=0.26, lo=0.38, hi=0.66) for d in (12, 13, 14, 15)}}


def road_mask(img, sat_max=0.13, lo=0.36, hi=0.72, close=3):
    a = np.asarray(img.convert('RGB').resize((W // 2, H // 2), Image.LANCZOS)).astype(np.float32) / 255.0
    mx, mn = a.max(axis=2), a.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    asphalt = (sat < sat_max) & (mx > lo) & (mx < hi)
    dash = (sat < 0.10) & (mx > 0.86)
    m = asphalt | (dash & ndimage.binary_dilation(asphalt, iterations=4))
    m = ndimage.binary_closing(m, iterations=close)
    m = ndimage.binary_opening(m, iterations=2)
    lab, n = ndimage.label(m)
    if n == 0:
        return m
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    return lab == (int(np.argmax(sizes)) + 1)


def longest_path(skel):
    pts = set(zip(*np.nonzero(skel)))
    nb = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

    def bfs(start):
        prev = {start: None}
        q = deque([start])
        last = start
        while q:
            p = q.popleft()
            last = p
            for dy, dx in nb:
                c = (p[0] + dy, p[1] + dx)
                if c in pts and c not in prev:
                    prev[c] = p
                    q.append(c)
        return last, prev

    start = next(iter(pts))
    a, _ = bfs(start)
    b, prev = bfs(a)
    path = []
    p = b
    while p is not None:
        path.append(p)
        p = prev[p]
    return path  # (y, x) в половинном разрешении


def to_screen(p):
    y, x = p
    return (x * 2 - W / 2, H / 2 - y * 2)


def place(path):
    pts = [to_screen(p) for p in path]
    inside = [abs(x) <= SAFE_X and SAFE_BOTTOM <= y <= SAFE_TOP for x, y in pts]
    # самый длинный непрерывный кусок пути внутри безопасной зоны
    best, cur, start = (0, 0), 0, 0
    for i, ok in enumerate(inside + [False]):
        if ok:
            if cur == 0:
                start = i
            cur += 1
        else:
            if cur > best[1] - best[0]:
                best = (start, start + cur)
            cur = 0
    seg = np.array(pts[best[0]:best[1]], dtype=np.float32)
    # слева направо — уровни идут по карте слева направо
    if seg[0][0] > seg[-1][0]:
        seg = seg[::-1]
    d = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(seg, axis=0), axis=1))])
    total = d[-1]
    nodes = []
    for k in range(COUNT):
        t = (0.03 + 0.94 * k / (COUNT - 1)) * total
        i = int(np.searchsorted(d, t))
        i = min(max(i, 1), len(seg) - 1)
        p = seg[i]
        # нормаль к дороге: ценники чуть в стороны попеременно, чтобы не лежать на разметке
        j0, j1 = max(0, i - 12), min(len(seg) - 1, i + 12)
        tang = seg[j1] - seg[j0]
        tang /= max(np.linalg.norm(tang), 1e-6)
        normal = np.array([-tang[1], tang[0]])
        if normal[1] < 0:
            normal = -normal
        off = 34.0 if k % 2 == 0 else -34.0
        q = p + normal * off
        nodes.append((round(float(q[0]), 1), round(float(np.clip(q[1], SAFE_BOTTOM, SAFE_TOP)), 1)))
    return nodes, total


def main():
    preview = sys.argv[1] if len(sys.argv) > 1 else None
    out = {}
    for d in range(1, 16):
        f = os.path.join(MAP, f'map_segment_{d:02d}.png')
        img = Image.open(f)
        t = TUNE.get(d, {})
        mask = road_mask(img, t.get('sat', 0.13), t.get('lo', 0.36), t.get('hi', 0.72), t.get('close', 3))
        skel = skeletonize(mask)
        path = longest_path(skel)
        nodes, total = place(path)
        out[d] = nodes
        print(f'district {d}: road px={int(mask.sum())} path={len(path)} usable={total:.0f}')
        if preview:
            im = img.convert('RGB').resize((W, H))
            dr = ImageDraw.Draw(im)
            ys, xs = np.nonzero(skel)
            for y, x in zip(ys[::3], xs[::3]):
                dr.point((x * 2, y * 2), fill=(255, 0, 255))
            for k, (x, y) in enumerate(nodes):
                sx, sy = x + W / 2, H / 2 - y
                dr.ellipse((sx - 26, sy - 20, sx + 26, sy + 20), fill=(220, 40, 40), outline=(255, 255, 255), width=3)
                dr.text((sx - 8, sy - 8), str(k + 1), fill=(255, 255, 255))
            dr.rectangle((W / 2 - SAFE_X, H / 2 - SAFE_TOP, W / 2 + SAFE_X, H / 2 - SAFE_BOTTOM), outline=(0, 200, 255), width=2)
            im.resize((960, 540)).save(os.path.join(preview, f'nodes_{d:02d}.jpg'), quality=80)
    json.dump(out, open(os.path.join(ROOT, 'Tools', 'map_nodes.json'), 'w'), indent=1)

    cs = ['namespace AllOnShelves.EditorTools', '{',
          '    /// <summary>Места ценников уровней на картинках районов (кадр 1920×1080 от центра, y вверх).',
          '    /// Сгенерировано Tools/ArtPipeline/map_nodes.py по дороге на картинке — не править руками.</summary>',
          '    public static class MapRoadData', '    {',
          '        public static readonly float[][] Nodes =', '        {']
    for d in range(1, 16):
        flat = ', '.join(f'{x:.1f}f, {y:.1f}f' for x, y in out[d])
        cs.append(f'            new float[] {{ {flat} }},')
    cs += ['        };', '',
           '        /// <summary>Место k-го (с нуля) ценника района.</summary>',
           '        public static UnityEngine.Vector2 Node(int district, int k)',
           '        {',
           '            var a = Nodes[district - 1];',
           '            k = System.Math.Min(k, a.Length / 2 - 1);',
           '            return new UnityEngine.Vector2(a[k * 2], a[k * 2 + 1]);',
           '        }', '    }', '}', '']
    open(os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Editor', 'MapRoadData.cs'), 'w', encoding='utf-8').write('\n'.join(cs))
    print('ok')


if __name__ == '__main__':
    main()

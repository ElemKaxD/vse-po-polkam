# -*- coding: utf-8 -*-
"""Какие картинки из Assets/AllOnShelves/Art реально нужны игре (27.09.2026).

Картинка «нужна», если:
  * её GUID стоит в сцене/префабе/ассете (кроме самой библиотеки ArtLibrary.asset), или
  * её имя встречается строкой в коде, или
  * имя подходит под шаблон, который код собирает на лету (item_*, meta_lego_*, bg_meta_* ...).

Запуск: python art_usage.py [--list]   — сводка по папкам и сколько весит лишнее.
Результат пишется в Tools/ArtPipeline/art_unused.txt (одно имя на строку).
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Art')
SCRIPTS = os.path.join(ROOT, 'Assets', 'AllOnShelves')
OUT = os.path.join(os.path.dirname(__file__), 'art_unused.txt')

# имена, которые код собирает из кусков: их не найти строкой целиком
DYNAMIC = [
    r'^item_', r'^sticker_', r'^gold_', r'^meta_lego_', r'^meta_d\d\d_', r'^bg_meta_', r'^bg_level_',
    r'^map_segment_', r'^map_node_', r'^halo_', r'^chr_raccoon', r'^skin_', r'^fx_', r'^icon_', r'^ovl_',
    r'^chr_customer_', r'^daily_', r'^album_', r'^shop_', r'^brd_', r'^obj_', r'^dept_', r'^ui_star', r'^btn_',
]


def main():
    guid_of, size_of, folder_of = {}, {}, {}
    for d, _, fs in os.walk(ART):
        for f in fs:
            if not f.endswith('.png'):
                continue
            p = os.path.join(d, f)
            name = f[:-4]
            meta = p + '.meta'
            g = None
            if os.path.exists(meta):
                m = re.search(r'guid: ([0-9a-f]{32})', open(meta, encoding='utf-8', errors='ignore').read())
                g = m.group(1) if m else None
            guid_of[name] = g
            size_of[name] = os.path.getsize(p)
            folder_of[name] = os.path.relpath(d, ART)

    # все ссылки из сцен, префабов и ассетов (кроме библиотеки арта — она берёт всё подряд)
    refs = set()
    for d, _, fs in os.walk(os.path.join(ROOT, 'Assets')):
        for f in fs:
            if not f.endswith(('.unity', '.prefab', '.asset', '.mat', '.controller', '.anim')):
                continue
            if f == 'ArtLibrary.asset':
                continue
            txt = open(os.path.join(d, f), encoding='utf-8', errors='ignore').read()
            refs.update(re.findall(r'guid: ([0-9a-f]{32})', txt))

    code = []
    for d, _, fs in os.walk(SCRIPTS):
        for f in fs:
            if f.endswith('.cs'):
                code.append(open(os.path.join(d, f), encoding='utf-8', errors='ignore').read())
    code = '\n'.join(code)
    literals = set(re.findall(r'"([A-Za-z0-9_ ]+)"', code))

    used, unused = [], []
    for name in sorted(size_of):
        if guid_of[name] in refs or name in literals or any(re.search(p, name) for p in DYNAMIC):
            used.append(name)
        else:
            unused.append(name)

    total = sum(size_of.values())
    waste = sum(size_of[n] for n in unused)
    by = {}
    for n in unused:
        by.setdefault(folder_of[n], [0, 0])
        by[folder_of[n]][0] += 1
        by[folder_of[n]][1] += size_of[n]
    print(f'картинок в Art: {len(size_of)}, {total / 1048576:.1f} МБ PNG')
    print(f'ни на что не ссылаются: {len(unused)}, {waste / 1048576:.1f} МБ PNG')
    for k, (c, s) in sorted(by.items(), key=lambda x: -x[1][1]):
        print(f'  {k:28} {c:4} шт  {s / 1048576:6.1f} МБ')
    open(OUT, 'w', encoding='utf-8').write('\n'.join(unused) + '\n')
    if '--list' in sys.argv:
        for n in unused:
            print('   ', folder_of[n], n, f'{size_of[n] // 1024} КБ')


if __name__ == '__main__':
    main()

# -*- coding: utf-8 -*-
"""Эффекты из Arcaidia Effector → игра (27.09.2026, 60 кадров — с 29.09.2026).

Берёт листы <редактор>/.snap/atlas_vfx_*.png (их выгружает Tools/ArtPipeline/vfx_export.js),
кладёт в Assets/AllOnShelves/Resources/VFX/vfx_*.png и пишет таблицу кадров
Scripts/Services/VfxCatalog.cs по Tools/ArtPipeline/vfx_meta.json.
Листы, которых нет в vfx_meta.json (игра их не вызывает), уносит в ArtSource/VFX_unused:
всё, что лежит в Resources, попадает в билд.

Запуск: python vfx_import.py
"""
import json
import os
import shutil
import sys

sys.stdout.reconfigure(encoding='utf-8')

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
SNAP = r'D:\Work\ЯндексИгры\Arcada Effector_1.0.zip\Arcada Effector_1.0\.snap'
DST = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Resources', 'VFX')
UNUSED = os.path.join(ROOT, 'ArtSource', 'VFX_unused')
CS = os.path.join(ROOT, 'Assets', 'AllOnShelves', 'Scripts', 'Services', 'VfxCatalog.cs')

# эффекты-циклы (помечены «ЦИКЛ» в Docs/VFX_PROMPTS_ВсёПоПолкам.txt): играют, пока их не остановят
LOOPS = {
    'vfx_amb_birds', 'vfx_amb_dust', 'vfx_amb_fireflies', 'vfx_amb_glass_glint', 'vfx_amb_leaves',
    'vfx_amb_snow', 'vfx_amb_steam', 'vfx_belt_speed', 'vfx_cart_warning', 'vfx_frost_breath',
    'vfx_hint_finger', 'vfx_loading_wheel', 'vfx_marker_ping', 'vfx_node_pulse', 'vfx_perish_timer',
    'vfx_road_dash', 'vfx_rush_clock', 'vfx_sale_shimmer', 'vfx_spoil_stink', 'vfx_streak_flame',
    'vfx_thread_glow', 'vfx_piggy_glow', 'vfx_build_dust', 'vfx_hint_glow',
    # v4: лучи награды, готовый сундук шкалы, блик алмаза, метка будущего улучшения
    'vfx_reward_rays', 'vfx_chest_ready', 'vfx_gem_shine', 'vfx_reno_marker_pulse',
}


def main():
    meta = json.load(open(os.path.join(HERE, 'vfx_meta.json'), encoding='utf-8'))
    os.makedirs(DST, exist_ok=True)
    rows = []
    for name, frames, cols, rws, cw, ch, fps, dur, w, h, kb in meta:
        src = os.path.join(SNAP, f'atlas_{name}.png')
        if not os.path.exists(src):
            print(f'  нет листа {src} — пропускаю')
            continue
        shutil.copyfile(src, os.path.join(DST, name + '.png'))
        rows.append((name, frames, cols, rws, cw, ch, fps, name in LOOPS))
    keep = {r[0] for r in rows}
    os.makedirs(UNUSED, exist_ok=True)
    moved = 0
    for f in os.listdir(DST):
        base = f[:-len('.png.meta')] if f.endswith('.png.meta') else os.path.splitext(f)[0]
        if base.startswith('vfx_') and base not in keep:
            if f.endswith('.meta'):
                os.remove(os.path.join(DST, f))
            else:
                shutil.move(os.path.join(DST, f), os.path.join(UNUSED, f))
                moved += 1
    if moved:
        print(f'неиспользуемых листов вынесено в {UNUSED}: {moved}')
    lines = [
        'namespace AllOnShelves',
        '{',
        '    /// <summary>',
        '    /// Эффекты из Arcaidia Effector: листы кадров в Resources/VFX (27.09.2026).',
        '    /// Файл создаёт Tools/ArtPipeline/vfx_import.py — руками не править.',
        '    /// </summary>',
        '    public static class VfxCatalog',
        '    {',
        '        public struct Info',
        '        {',
        '            public int Frames, Cols, Rows, CellW, CellH;',
        '            public float Fps;',
        '            public bool Loop;',
        '        }',
        '',
        '        public static bool TryGet(string name, out Info info)',
        '        {',
        '            info = default;',
        '            switch (name)',
        '            {',
    ]
    for name, frames, cols, rws, cw, ch, fps, loop in rows:
        lines.append(f'                case "{name}": info = new Info {{ Frames = {frames}, Cols = {cols}, Rows = {rws}, '
                     f'CellW = {cw}, CellH = {ch}, Fps = {fps}f, Loop = {"true" if loop else "false"} }}; return true;')
    lines += [
        '                default: return false;',
        '            }',
        '        }',
        '    }',
        '}',
        '',
    ]
    open(CS, 'w', encoding='utf-8').write('\n'.join(lines))
    total = sum(os.path.getsize(os.path.join(DST, r[0] + '.png')) for r in rows)
    print(f'эффектов: {len(rows)}, листы PNG: {total / 1024 / 1024:.1f} МБ → {DST}')
    print('готово:', CS)


if __name__ == '__main__':
    main()

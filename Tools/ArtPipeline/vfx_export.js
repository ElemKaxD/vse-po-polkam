// Выгрузка эффектов из Arcaidia Effector в спрайт-листы для игры.
//
// 1. В папке редактора: node server.mjs  (порт 5179)
// 2. Открыть http://localhost:5179/ и выполнить этот файл в консоли вкладки.
// 3. Листы лягут в <редактор>/.snap/atlas_vfx_*.png, таблица кадров вернётся строкой JSON —
//    её сохранить в Tools/ArtPipeline/vfx_meta.json и запустить python Tools/ArtPipeline/vfx_import.py.
//
// 29.09.2026: 60 кадров в секунду (раньше 9–20 — «глаз режет, когда вся игра плавная») и только
// эффекты, которые вызывает игра (USED): неиспользуемые лежали в Resources и зря весили в билде.
// Ячейка — 128–256 точек по размеру эффекта; если лист выходит больше 2048 (предел WebGL),
// ячейка уменьшается. Стороны ячейки кратны 4 — иначе WebGL не сожмёт лист.
(async () => {
    const USED = [
        'vfx_box_open', 'vfx_build_in', 'vfx_button_press', 'vfx_cart_overflow', 'vfx_chest_open',
        'vfx_coin_fountain', 'vfx_coins_spend', 'vfx_combo_ring', 'vfx_customer_call', 'vfx_freezer_open',
        'vfx_gift_pop', 'vfx_item_land', 'vfx_item_wrong', 'vfx_lock_break',
        'vfx_locked_shake', 'vfx_node_pulse', 'vfx_pack_tear', 'vfx_pallet_drop', 'vfx_rewind_wave',
        'vfx_section_done', 'vfx_spoil_stink', 'vfx_star_award', 'vfx_tile_bought',
        'vfx_undo_swirl',
        // 03.10.2026: Торговый дом, смена, копилка
        'vfx_piggy_coin_drop', 'vfx_piggy_break', 'vfx_piggy_glow', 'vfx_staff_arrive', 'vfx_staff_level_up',
        'vfx_staff_pick', 'vfx_build_dust', 'vfx_chains_fall', 'vfx_staff_skill',
        // 03.10.2026 (v4): подсказка на товаре и куда класть, звезда на бейдже сотрудника
        'vfx_hint_glow', 'vfx_hint_target', 'vfx_staff_badge_up',
        // 03.10.2026 (v4, раздел 11): завоз и испытание дня, задания, серия, алмазы, дом, альбом, рейтинг, гардероб
        'vfx_crate_open', 'vfx_reward_rays', 'vfx_challenge_win', 'vfx_daily_dot_fill', 'vfx_quest_done',
        'vfx_quest_token_fly', 'vfx_chest_ready', 'vfx_month_prize', 'vfx_streak_up', 'vfx_streak_lost',
        'vfx_streak_flame', 'vfx_gem_burst', 'vfx_gem_shine', 'vfx_boost_time', 'vfx_hammer_instant',
        'vfx_gold_path_unlock', 'vfx_difficulty_unlock', 'vfx_cash_collect', 'vfx_reno_marker_pulse',
        'vfx_dept_complete', 'vfx_rank_up', 'vfx_skin_equip',
    ];
    const FPS = 60, MAX = 2048;
    const even4 = v => Math.max(8, Math.floor(v / 4) * 4);
    const cellFor = (w, h) => {
        const m = Math.max(w, h);
        const t = m <= 256 ? 128 : m <= 384 ? 160 : m <= 512 ? 192 : m <= 768 ? 224 : 256;
        const k = t / m;
        return [even4(w * k), even4(h * k)];
    };
    const meta = [];
    for (const name of USED) {
        const file = name + '.json';
        const f = await fetch('/api/effect?f=' + encodeURIComponent(file)).then(r => r.json());
        const doc = await new Promise(ok => AFX.Model.loadEffectFile(f, d => ok(d)));
        const e = doc.exp;
        const t0 = Math.max(0, e.t0 || 0);
        const t1 = (e.t1 == null || e.t1 < 0) ? doc.comp.dur : Math.min(doc.comp.dur, e.t1);
        const frames = Math.max(1, Math.round((t1 - t0) * FPS));
        let [cw, ch] = cellFor(doc.comp.w, doc.comp.h);
        // сетка ближе к квадрату; лист не больше MAX — иначе уменьшаем ячейку
        let cols = Math.ceil(Math.sqrt(frames * ch / cw)), rows = Math.ceil(frames / cols);
        const k = Math.min(1, MAX / (cols * cw), MAX / (rows * ch));
        if (k < 1) { cw = even4(cw * k); ch = even4(ch * k); }
        Object.assign(e, { cols, rows, cellW: cw, cellH: ch, ss: 2, mode: 'rgba', frames });
        const { canvas, info } = AFX.Atlas.build(doc);
        const res = await fetch('/api/snap', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: 'atlas_' + name, data: canvas.toDataURL('image/png') })
        }).then(r => r.json());
        meta.push([name, info.frames, cols, rows, cw, ch, info.fps, info.duration,
                   canvas.width, canvas.height, Math.round(res.bytes / 1024)]);
    }
    return JSON.stringify(meta);
})();

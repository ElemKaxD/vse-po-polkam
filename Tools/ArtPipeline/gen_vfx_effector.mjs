// Генератор пака VFX «Всё по полкам!» для Arcaidia Effector.
// Пишет library/vfx_*.json (по одному эффекту на файл). Запуск: node gen_vfx.mjs [имя ...]
import fs from 'fs';
import path from 'path';

const ROOT = 'D:/Work/ЯндексИгры/Arcada Effector_1.0.zip/Arcada Effector_1.0';
const LIB = path.join(ROOT, 'library');
const ART = 'D:/Work/ЯндексИгры/LaserSiege/Yandex_Claude/Assets/AllOnShelves/Art';
const TEXFILES = {
    sparkle: 'FX/fx_sparkle.png', conf_a: 'FX/fx_confetti_a.png', conf_b: 'FX/fx_confetti_b.png',
    conf_c: 'FX/fx_confetti_c.png', dust: 'FX/fx_dust.png', rays: 'FX/fx_light_rays.png',
    snow: 'FX/fx_snow.png', coin: 'Icons/icon_coin.png', star: 'Icons/icon_star.png',
    snowflake: 'Icons/icon_snowflake.png', fire: 'Icons/icon_fire.png',
    // v4
    apple: 'Items/item_apple.png', gem: 'Icons/icon_gem.png', token: 'Quests/quest_token.png', check: 'Icons/icon_check.png',
    lock: 'Icons/icon_lock.png', pepper: 'Icons/icon_diff_easy.png'
};

// ---------- палитра ----------
const CREAM = '#FFF3DC', GOLD = '#FFC64A', GREEN = '#58A65C', RED = '#E2574C', BROWN = '#4A2F1B',
    SKY = '#9FD8F2', ORANGE = '#FF9A3C', WHITE = '#FFFFFF', LGOLD = '#FFE08A', DGOLD = '#E09A1E';
const FESTIVE = [GOLD, RED, GREEN, SKY, CREAM];

// ---------- примитивы данных ----------
const hx = c => Array.isArray(c) ? c : [1, 3, 5].map(i => parseInt(c.slice(i, i + 2), 16));
const r3 = v => Math.round(v * 1000) / 1000;
// ключ: [t, v, mode]; mode ставится и на вход, и на выход ('hold' — только выход)
const K = (...ks) => ({
    keys: ks.map(([t, v, m]) => {
        const mm = m || 'ease';
        return { t: r3(t), v: typeof v === 'string' ? hx(v) : (typeof v === 'number' ? r3(v) : v), i: { m: mm === 'hold' ? 'lin' : mm }, o: { m: mm } };
    })
});
const C = (...p) => ({ pts: p.map(([t, v]) => ({ t, v })) });
const G = (...s) => ({ stops: s.map(([t, c]) => ({ t, c: hx(c) })) });
const G1 = c => G([0, c], [1, c]);
const sampleK = (fn, t0, t1, n, mode) => K(...Array.from({ length: n + 1 }, (_, i) => {
    const t = t0 + (t1 - t0) * i / n;
    return [t, fn(t), mode || 'lin'];
}));
const easeIn = (u, p) => Math.pow(u, p || 2);
const easeOut = (u, p) => 1 - Math.pow(1 - u, p || 2);
const easeIO = u => u < 0.5 ? 2 * u * u : 1 - Math.pow(-2 * u + 2, 2) / 2;
const bez = (pts, u) => { // de Casteljau
    let q = pts.map(p => p.slice());
    while (q.length > 1) q = q.slice(1).map((p, i) => [q[i][0] + (p[0] - q[i][0]) * u, q[i][1] + (p[1] - q[i][1]) * u]);
    return q[0];
};
let seedRng = 1;
const rnd = () => { seedRng = (seedRng * 16807) % 2147483647; return (seedRng - 1) / 2147483646; };

// кривые за жизнь
const ONE = C([0, 1], [1, 1]);
const FADE = C([0, 1], [0.6, 0.85], [1, 0]);
const BELL = C([0, 0], [0.5, 1], [1, 0]);
const FIO = C([0, 0], [0.15, 1], [0.7, 0.8], [1, 0]);
const POP = C([0, 0.2], [0.2, 1.15], [0.35, 1], [1, 0.3]);

// ---------- контекст эффекта ----------
let CUR = null;
function spriteRef(shape, p) {
    if (shape.startsWith('tex:')) {
        const name = shape.slice(4);
        CUR.tex.add(name);
        return { kind: 'tex', texId: 'tex_aos_' + name };
    }
    return { kind: 'shape', id: shape, p: p || {} };
}
const col = c => (c && c.keys) ? { keys: c.keys.map(k => Object.assign({}, k, { v: hx(k.v) })) } : hx(c);
function common(name, type, o, blendDef) {
    const L = {
        name, type, start: o.start != null ? o.start : 0, end: o.end != null ? o.end : CUR.dur,
        blend: o.blend || blendDef, opacity: o.op != null ? o.op : 1,
        glowL: o.glowL || 0, glowLR: o.glowLR || 24
    };
    if (o.fade) Object.assign(L, { fadeOn: true, fadeX: o.fade[0], fadeY: o.fade[1], fadeR: o.fade[2], fadeSoft: o.fade[3] != null ? o.fade[3] : 0.5 });
    return L;
}
// спрайт-слой; opacity и scale всегда явные (иначе заводская анимация)
function SP(name, o) {
    const L = common(name, 'sprite', o, 'normal');
    L.sp = {
        sprite: spriteRef(o.shape || 'soft', o.p), size: o.size || 200,
        x: o.x || 0, y: o.y || 0, scale: o.scale != null ? o.scale : 1, aspect: o.aspect || 1,
        rot: o.rot || 0, color: col(o.color || WHITE), squash: 0, glow: o.glow || 0,
        glowSize: o.glowSize || 1.6, glowBlur: o.glowBlur || 0.15, fps: 0
    };
    return L;
}
// эмиттер; по умолчанию без дефолтного burst, без вращения и торможения
function EM(name, o) {
    const L = common(name, 'emitter', o, 'normal');
    L.em = Object.assign({ rate: 0, bursts: [], speedRnd: 0.3, subStep: true, seed: CUR.seed++ }, o.em);
    const pt = Object.assign({
        life: 0.8, lifeRnd: 0.2, size: 30, sizeRnd: 0.3, spin: 0, spinRnd: 0, rot: 0, rotRnd: 0, drag: 0,
        sizeOL: ONE, opacityOL: C([0, 1], [0.7, 1], [1, 0]), grad: G1(WHITE)
    }, o.pt);
    if (typeof pt.sprite === 'string') { pt.sprite = spriteRef(pt.sprite, pt.p); }
    else if (!pt.sprite) pt.sprite = spriteRef('soft');
    delete pt.p;
    if (typeof pt.grad === 'string') pt.grad = G1(pt.grad);
    L.pt = pt;
    return L;
}
// точный бесшовный цикл: копии эмиттера со сдвигом на период и тем же сидом
function loop(L, P, lifeMax) {
    const out = [];
    const kmin = Math.floor(-1 - lifeMax / P);
    for (let k = kmin; k <= 0; k++) {
        const c = JSON.parse(JSON.stringify(L));
        c.start = (L.start || 0) + k * P;
        c.end = c.start + P;
        if (k) c.name = L.name + ' ' + k;
        out.push(c);
    }
    return out;
}
// жёсткая группа: части двигаются вместе (x, y, r — поворот, s — масштаб); у каждого ключа своё время
function rigid(parts, gk, extra) {
    return parts.map(p => {
        const lx = p.x || 0, ly = p.y || 0, lr = p.rot || 0, ls = p.scale != null ? p.scale : 1;
        const X = [], Y = [], R = [], S = [];
        gk.forEach(g => {
            const a = (g.r || 0) * Math.PI / 180, s = g.s != null ? g.s : 1, m = g.m || 'ease';
            X.push([g.t, (g.x || 0) + s * (lx * Math.cos(a) - ly * Math.sin(a)), m]);
            Y.push([g.t, (g.y || 0) + s * (lx * Math.sin(a) + ly * Math.cos(a)), m]);
            R.push([g.t, (g.r || 0) + lr, m]);
            S.push([g.t, s * ls, m]);
        });
        return SP(p.name, Object.assign({}, extra || {}, p, { x: K(...X), y: K(...Y), rot: K(...R), scale: K(...S) }));
    });
}
// точки на орбите/спирали
function orbit(o) {
    const { cx = 0, cy = 0, r0, r1 = r0, a0 = -90, turns = 1, t0 = 0, t1, n = 36, ease = u => u, ry } = o;
    const k = (ry || r0) / r0;
    const at = t => { const u = ease((t - t0) / (t1 - t0)); const a = (a0 + 360 * turns * u) * Math.PI / 180; const r = r0 + (r1 - r0) * u; return [cx + r * Math.cos(a), cy + r * k * Math.sin(a), a]; };
    return {
        x: sampleK(t => at(t)[0], t0, t1, n), y: sampleK(t => at(t)[1], t0, t1, n),
        rot: sampleK(t => at(t)[2] * 180 / Math.PI + (turns >= 0 ? 90 : -90), t0, t1, n), at
    };
}
function bezPath(pts, t0, t1, n, ease) {
    ease = ease || (u => u);
    return {
        x: sampleK(t => bez(pts, ease((t - t0) / (t1 - t0)))[0], t0, t1, n),
        y: sampleK(t => bez(pts, ease((t - t0) / (t1 - t0)))[1], t0, t1, n)
    };
}
// скруглённый прямоугольник как узлы пути
function roundRect(hw, hh, r, seg) {
    const pts = [];
    const corners = [[hw - r, -hh + r, -90], [hw - r, hh - r, 0], [-hw + r, hh - r, 90], [-hw + r, -hh + r, 180]];
    corners.forEach(([cx, cy, a0]) => {
        for (let i = 0; i <= seg; i++) { const a = (a0 + 90 * i / seg) * Math.PI / 180; pts.push({ x: r3(cx + r * Math.cos(a)), y: r3(cy + r * Math.sin(a)) }); }
    });
    return pts;
}
// точка на периметре скруглённого прямоугольника (u 0..1, по часовой от верхней середины)
function rectPoint(hw, hh, u) {
    const per = 4 * hw + 4 * hh; let d = ((u % 1) + 1) % 1 * per;
    if (d < hw) return [d, -hh]; d -= hw;
    if (d < 2 * hh) return [hw, -hh + d]; d -= 2 * hh;
    if (d < 2 * hw) return [hw - d, hh]; d -= 2 * hw;
    if (d < 2 * hh) return [-hw, hh - d]; d -= 2 * hh;
    return [-hw + d, -hh];
}

// ---------- составные куски ----------
// брусок: длина len, толщина th (квадратный спрайт рисует 88 из 128 пикселей)
const bar = (len, th) => ({ shape: 'square', size: th / 0.6875, aspect: len / th });
function sparkBurst(o) { // золотые искры-звёздочки из текстуры игры
    return EM(o.name || 'Sparkles', {
        start: o.t || 0, end: (o.t || 0) + 0.05, blend: 'normal',
        em: Object.assign({ x: o.x || 0, y: o.y || 0, shape: 'ring', sx: o.r || 20, sy: 4, dir: 'out', speed: o.speed || 420, speedRnd: 0.25, bursts: [{ t: 0, n: o.n || 6 }] }, o.em || {}),
        pt: Object.assign({
            sprite: 'tex:sparkle', life: o.life || 0.55, lifeRnd: 0.2, size: o.size || 40, sizeRnd: 0.3, drag: o.drag != null ? o.drag : 3,
            sizeOL: C([0, 0.2], [0.18, 1.15], [0.6, 0.9], [1, 0]), opacityOL: C([0, 1], [0.75, 1], [1, 0]),
            spin: 160, spinRnd: 1, rotRnd: 0.2, gravY: o.gravY || 0
        }, o.pt || {})
    });
}
function glints(o) { // аддитивные блики-крестики
    return EM(o.name || 'Glints', {
        start: o.t || 0, end: (o.t || 0) + 0.05, blend: 'add',
        em: { x: o.x || 0, y: o.y || 0, shape: 'circle', sx: o.r || 30, sy: o.r || 30, dir: 'out', speed: o.speed || 300, speedRnd: 0.5, bursts: [{ t: 0, n: o.n || 10 }] },
        pt: { sprite: 'spark', life: o.life || 0.45, lifeRnd: 0.3, size: o.size || 34, sizeRnd: 0.4, drag: 2.5, sizeOL: BELL, opacityOL: C([0, 1], [0.6, 1], [1, 0]), grad: G([0, WHITE], [1, o.color || GOLD]), rotRnd: 0.1 }
    });
}
function flash(o) {
    const t = o.t || 0, d = o.d || 0.35;
    return SP(o.name || 'Flash', {
        start: t, end: t + d, blend: 'add', shape: 'soft', size: o.size || 260, color: o.color || CREAM, x: o.x || 0, y: o.y || 0, aspect: o.aspect || 1,
        scale: K([0, o.s0 || 0.25], [d * 0.4, o.s1 || 1.3]), op: K([0, o.op || 1], [d * 0.25, o.op || 1], [d, 0])
    });
}
function ringWave(o) {
    const t = o.t || 0, d = o.d || 0.45;
    return SP(o.name || 'Ring', {
        start: t, end: t + d, blend: o.blend || 'add', shape: 'ring', p: { th: o.th || 0.1 }, size: o.size || 200, color: o.color || GOLD,
        x: o.x || 0, y: o.y || 0, aspect: o.aspect || 1, glow: o.glow || 0,
        scale: K([0, o.s0 || 0.2, 'lin'], [d, o.s1 || 2, 'ease']), op: K([0, o.op || 1], [d * 0.3, (o.op || 1) * 0.85], [d, 0])
    });
}
// конфетти: полоски + кружки, по слою на цвет
function confettiBurst(o) {
    const cols = o.colors || [GOLD, RED, GREEN, SKY];
    const out = [];
    cols.forEach((c, i) => {
        out.push(EM('Confetti ' + (i + 1), {
            start: o.t || 0, end: (o.t || 0) + 0.05, blend: 'normal',
            em: Object.assign({ x: o.x || 0, y: o.y || 0, shape: 'circle', sx: 20, sy: 20, dir: o.dir || 'out', angle: o.angle != null ? o.angle : -90, spread: o.spread || 360, speed: o.speed || 650, speedRnd: 0.5, bursts: [{ t: 0, n: o.n || 10 }] }, o.em || {}),
            pt: {
                sprite: 'tex:' + ['conf_a', 'conf_b', 'conf_c', 'conf_a'][i % 4], tintTex: true, grad: G1(c),
                life: o.life || 1.4, lifeRnd: 0.2, size: o.size || 26, sizeRnd: 0.35, drag: o.drag != null ? o.drag : 2.2, gravY: o.gravY != null ? o.gravY : 300,
                turbAmp: o.turb != null ? o.turb : 220, turbFreq: 1.2, spin: 420, spinRnd: 1, rotRnd: 1,
                sizeOL: C([0, 0.4], [0.1, 1], [1, 0.9]), opacityOL: C([0, 1], [0.8, 1], [1, 0])
            }
        }));
    });
    return out;
}
function confettiFall(o) {
    const cols = o.colors || [GOLD, RED, GREEN, SKY];
    return cols.map((c, i) => EM('Confetti fall ' + (i + 1), {
        start: o.t0, end: o.t1, blend: 'normal',
        em: { x: 0, y: o.y || -300, shape: 'box', sx: o.w || 280, sy: 10, dir: 'dir', angle: 90, spread: 30, speed: o.speed || 160, speedRnd: 0.4, rate: o.rate || 10 },
        pt: {
            sprite: 'tex:' + ['conf_b', 'conf_a', 'conf_c', 'conf_b'][i % 4], tintTex: true, grad: G1(c),
            life: o.life || 1.6, lifeRnd: 0.2, size: o.size || 24, sizeRnd: 0.35, gravY: 120, drag: 0.8,
            turbAmp: 200, turbFreq: 1.1, spin: 380, spinRnd: 1, rotRnd: 1, opacityOL: C([0, 1], [0.8, 1], [1, 0])
        }
    }));
}
function firework(o) {
    const t = o.t;
    return [
        EM('Firework ' + o.i, {
            start: t, end: t + 0.05, blend: 'add',
            em: { x: o.x, y: o.y, shape: 'point', dir: 'omni', speed: o.speed || 380, speedRnd: 0.15, bursts: [{ t: 0, n: o.n || 34 }] },
            pt: {
                sprite: 'streak', life: 0.8, lifeRnd: 0.25, size: o.size || 30, sizeRnd: 0.3, drag: 2.2, gravY: 140, alignVel: true, stretch: 1.2,
                grad: G([0, WHITE], [0.3, o.c || LGOLD], [1, o.c2 || DGOLD]), opacityOL: C([0, 1], [0.6, 0.9], [1, 0]),
                sizeOL: C([0, 1], [1, 0.4]), glow: 0.4
            }
        }),
        EM('Firework twinkle ' + o.i, {
            start: t + 0.1, end: t + 0.15, blend: 'add',
            em: { x: o.x, y: o.y, shape: 'point', dir: 'omni', speed: (o.speed || 380) * 0.6, speedRnd: 0.4, bursts: [{ t: 0, n: 14 }] },
            pt: { sprite: 'spark', life: 0.7, lifeRnd: 0.3, size: 26, sizeRnd: 0.4, drag: 2.5, gravY: 160, grad: G([0, WHITE], [1, o.c || GOLD]), opacityOL: C([0, 0], [0.2, 1], [0.35, 0.3], [0.5, 1], [0.7, 0.2], [1, 0]) }
        }),
        flash({ name: 'Firework flash ' + o.i, t, d: 0.3, x: o.x, y: o.y, size: 180, color: o.c || LGOLD, op: 0.8 })
    ];
}

// ---------- сборка файла ----------
const TEXDATA = {};
function texPayload(name) {
    if (!TEXDATA[name]) TEXDATA[name] = 'data:image/png;base64,' + fs.readFileSync(path.join(ART, TEXFILES[name])).toString('base64');
    return TEXDATA[name];
}
function scaleTrack(v, s) {
    if (typeof v === 'number') return r3(v * s);
    if (v && v.keys) return { keys: v.keys.map(k => Object.assign({}, k, { v: typeof k.v === 'number' ? r3(k.v * s) : k.v })) };
    return v;
}
function scaleLayer(L, s) {
    if (s === 1) return;
    ['glowLR', 'fadeX', 'fadeY', 'fadeR'].forEach(k => { if (L[k] != null) L[k] = scaleTrack(L[k], s); });
    if (L.sp) ['size', 'x', 'y'].forEach(k => { L.sp[k] = scaleTrack(L.sp[k], s); });
    if (L.em) {
        ['x', 'y', 'sx', 'sy', 'speed'].forEach(k => { if (L.em[k] != null) L.em[k] = scaleTrack(L.em[k], s); });
        if (L.em.path && L.em.path.nodes) L.em.path.nodes.forEach(n => { n.x = scaleTrack(n.x, s); n.y = scaleTrack(n.y, s); });
    }
    if (L.pt) ['size', 'gravX', 'gravY', 'turbAmp'].forEach(k => { if (L.pt[k] != null) L.pt[k] = scaleTrack(L.pt[k], s); });
}

const EFFECTS = [];
// size: [cellW, cellH] = размер композиции; дизайн ведётся в пространстве шириной 512
function fx(name, o, build) { EFFECTS.push({ name, o, build }); }

function make(e) {
    const { name, o } = e;
    const [w, h] = o.size;
    const s = Math.max(w, h) / 512;
    const P = o.loop;
    const dur = P || o.dur;
    CUR = { dur, seed: 1, tex: new Set() };
    seedRng = name.split('').reduce((a, c) => (a * 31 + c.charCodeAt(0)) % 2147483647, 7) || 1;
    const layers = e.build({ W: w / s, H: h / s, P, dur }).flat(Infinity).filter(Boolean);
    const slug = name.replace(/^vfx_/, '');
    layers.forEach((L, i) => { scaleLayer(L, s); L.id = `lr_aos_${slug}_${i + 1}`; });
    const texNames = [...CUR.tex];
    const doc = {
        id: 'fx_aos_' + slug, name,
        comp: { w, h, dur, fps: 60 }, cam: { comp: 0 },
        exp: { cols: o.frames, rows: 1, frames: o.frames, cellW: w, cellH: h, t0: 0, t1: dur, mode: 'rgba', thr: 0, ss: 2 },
        textures: texNames.map(n => ({ id: 'tex_aos_' + n, name: path.basename(TEXFILES[n], '.png'), sheet: null })),
        layers
    };
    const tex = {};
    texNames.forEach(n => { tex['tex_aos_' + n] = texPayload(n); });
    return { app: 'arcadia-effects', v: 1, doc, tex };
}

// =====================================================================================
// 1. ПОЛЕ УРОВНЯ
// =====================================================================================
fx('vfx_item_pop', { size: [256, 256], frames: 8, dur: 0.4 }, () => [
    [90, 210, 330].map((a, i) => {
        const rad = a * Math.PI / 180;
        return SP('Sparkle ' + (i + 1), {
            shape: 'tex:sparkle', size: 44, end: 0.36,
            x: K([0, Math.cos(rad) * 40], [0.3, Math.cos(rad) * 150]), y: K([0, Math.sin(rad) * 40 - 20], [0.3, Math.sin(rad) * 150 - 20]),
            scale: K([0, 0.2], [0.08, 1.1], [0.34, 0.2]), op: K([0, 1], [0.24, 1], [0.34, 0]), rot: K([0, 0], [0.34, 70])
        });
    }),
    SP('Ring', { blend: 'add', shape: 'ring', p: { th: 0.09 }, size: 210, color: WHITE, glow: 0.3, end: 0.36, scale: K([0, 0.15, 'lin'], [0.3, 1.15]), op: K([0, 1], [0.1, 1], [0.34, 0]) }),
    SP('Flash', { blend: 'add', shape: 'soft', size: 170, color: WHITE, end: 0.22, scale: K([0, 0.3], [0.1, 1]), op: K([0, 0.9], [0.22, 0]) })
]);

fx('vfx_item_land', { size: [256, 256], frames: 8, dur: 0.45 }, () => [
    EM('Pebbles', {
        end: 0.05, em: { x: 0, y: 70, shape: 'box', sx: 40, sy: 2, dir: 'dir', angle: -90, spread: 90, speed: 300, speedRnd: 0.2, bursts: [{ t: 0, n: 2 }] },
        pt: { sprite: 'dot', size: 11, sizeRnd: 0.2, life: 0.42, lifeRnd: 0.05, gravY: 1500, grad: G1('#D6D6D6'), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
    }),
    [180, 0].map((a, i) => EM('Puff ' + (i ? 'R' : 'L'), {
        end: 0.05, em: { x: 0, y: 75, shape: 'box', sx: 16, sy: 4, dir: 'dir', angle: a, spread: 24, speed: 260, speedRnd: 0.45, bursts: [{ t: 0, n: 7 }] },
        pt: {
            sprite: 'blob', size: 50, sizeRnd: 0.4, life: 0.42, lifeRnd: 0.15, drag: 5, gravY: -60, spin: 40, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.45], [0.3, 1], [1, 0.75]), opacityOL: C([0, 0.95], [0.5, 0.75], [1, 0]), grad: G([0, WHITE], [1, '#DADADA'])
        }
    }))
]);

fx('vfx_item_wrong', { size: [256, 256], frames: 8, dur: 0.5 }, () => {
    const g = [
        { t: 0, s: 0 }, { t: 0.07, s: 1.2 }, { t: 0.12, s: 0.94 }, { t: 0.16, s: 1, x: 0 },
        { t: 0.2, x: -14, s: 1 }, { t: 0.24, x: 12, s: 1 }, { t: 0.28, x: -8, s: 1 }, { t: 0.32, x: 5, s: 1 }, { t: 0.36, x: 0, s: 1 },
        { t: 0.42, s: 1.05 }, { t: 0.5, s: 0.3 }
    ];
    const f = bar(150, 40), ol = bar(166, 56);
    return [
        rigid([
            Object.assign({ name: 'Cross A', color: RED, rot: 45 }, f),
            Object.assign({ name: 'Cross B', color: RED, rot: -45 }, f),
            Object.assign({ name: 'Cross highlight', color: '#F08478', rot: -45, y: -8 }, bar(110, 8)),
            Object.assign({ name: 'Cross outline A', color: BROWN, rot: 45 }, ol),
            Object.assign({ name: 'Cross outline B', color: BROWN, rot: -45 }, ol)
        ], g, { op: K([0, 1], [0.42, 1], [0.5, 0]) }),
        ringWave({ t: 0.04, d: 0.36, color: RED, th: 0.14, size: 200, s0: 0.35, s1: 1.7, op: 0.9 }),
        flash({ t: 0, d: 0.25, color: '#FF8A7A', size: 200, op: 0.7 })
    ];
});

fx('vfx_section_done', { size: [512, 512], frames: 12, dur: 0.9 }, () => [
    sparkBurst({ n: 8, r: 20, speed: 560, size: 58, life: 0.75, drag: 3.2 }),
    glints({ n: 14, speed: 330, size: 30, life: 0.55 }),
    ringWave({ t: 0, d: 0.55, th: 0.08, size: 220, s0: 0.2, s1: 2.1, glow: 0.5 }),
    ringWave({ name: 'Ring inner', t: 0.05, d: 0.45, th: 0.18, size: 200, color: CREAM, s0: 0.2, s1: 1.3, op: 0.7 }),
    SP('Light column', {
        blend: 'add', shape: 'soft', size: 460, aspect: 0.24, color: LGOLD, end: 0.8,
        y: K([0, -30], [0.8, -120]), scale: K([0, 0.5], [0.3, 1.05]), op: K([0, 0], [0.1, 0.95], [0.35, 0.8], [0.8, 0])
    }),
    SP('Light column core', {
        blend: 'add', shape: 'soft', size: 380, aspect: 0.1, color: WHITE, end: 0.6,
        y: K([0, -20], [0.6, -110]), scale: K([0, 0.5], [0.25, 1]), op: K([0, 0], [0.08, 0.9], [0.6, 0])
    }),
    flash({ t: 0, d: 0.38, size: 300, color: CREAM, s1: 1.5 })
]);

fx('vfx_shelf_bounce_dust', { size: [384, 384], frames: 6, dur: 0.4 }, () =>
    [180, 0].map((a, i) => EM('Dust wave ' + (i ? 'R' : 'L'), {
        end: 0.05, em: { x: 0, y: 30, shape: 'box', sx: 50, sy: 4, dir: 'dir', angle: a, spread: 10, speed: 420, speedRnd: 0.45, bursts: [{ t: 0, n: 11 }] },
        pt: {
            sprite: 'blob', size: 44, sizeRnd: 0.4, life: 0.36, lifeRnd: 0.15, drag: 4.5, gravY: -40, spin: 30, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.5], [0.35, 1], [1, 0.8]), opacityOL: C([0, 0.95], [0.5, 0.7], [1, 0]), grad: G([0, WHITE], [1, '#E2E2E2'])
        }
    }))
);

fx('vfx_cart_warning', { size: [512, 512], frames: 12, loop: 1.0 }, ({ P }) => {
    const nodes = roundRect(200, 140, 44, 4);
    const pulse = K([0, 0.35], [P / 2, 1], [P, 0.35]);
    const rim = (name, size, grad, op, glow, n) => EM(name, {
        blend: 'add', op: pulse, glowL: glow, glowLR: 14,
        em: { shape: 'point', speed: 0, speedRnd: 0, bursts: [{ t: 0, n }], path: { on: true, mode: 'emit', closed: true, smooth: false, along: 'even', nodes } },
        pt: { sprite: 'soft', size, sizeRnd: 0, life: 10, lifeRnd: 0, grad: G1(grad), opacityOL: C([0, op], [1, op]) }
    });
    return [
        rim('Rim core', 12, '#FF9A8E', 0.9, 0, 220),
        rim('Rim glow', 44, RED, 0.35, 1, 160),
        loop(EM('Heat shimmer', {
            blend: 'add', em: { x: 0, y: -145, shape: 'box', sx: 170, sy: 4, dir: 'dir', angle: -90, spread: 20, speed: 55, speedRnd: 0.4, rate: 26 },
            pt: { sprite: 'soft', size: 40, sizeRnd: 0.4, life: 0.8, lifeRnd: 0, turbAmp: 70, turbFreq: 1.5, grad: G1(RED), sizeOL: C([0, 0.6], [1, 1.2]), opacityOL: C([0, 0], [0.3, 0.3], [1, 0]) }
        }), P, 0.8)
    ];
});

fx('vfx_cart_overflow', { size: [512, 512], frames: 12, dur: 1.0 }, () => {
    const stars = [0, 1, 2].map(i => {
        const o = orbit({ cx: 0, cy: -130, r0: 70, ry: 22, a0: i * 120, turns: 1.2, t0: 0, t1: 0.75, n: 24 });
        return SP('Dizzy star ' + (i + 1), { shape: 'star', p: { n: 5, inr: 0.45 }, size: 38, color: GOLD, start: 0.25, end: 1.0, x: o.x, y: o.y, scale: K([0, 0], [0.12, 1.1], [0.2, 1], [0.6, 1], [0.75, 0]), op: 1, rot: K([0, 0, 'lin'], [0.75, 300, 'lin']) });
    });
    const starOutlines = [0, 1, 2].map(i => {
        const o = orbit({ cx: 0, cy: -130, r0: 70, ry: 22, a0: i * 120, turns: 1.2, t0: 0, t1: 0.75, n: 24 });
        return SP('Dizzy star outline ' + (i + 1), { shape: 'star', p: { n: 5, inr: 0.45 }, size: 50, color: BROWN, start: 0.25, end: 1.0, x: o.x, y: o.y, scale: K([0, 0], [0.12, 1.1], [0.2, 1], [0.6, 1], [0.75, 0]), op: 1, rot: K([0, 0, 'lin'], [0.75, 300, 'lin']) });
    });
    return [
        stars, starOutlines,
        [180, 0].map((a, i) => EM('Motion lines ' + (i + 1), {
            end: 0.05, em: { x: 0, y: 40, shape: 'box', sx: 10, sy: 60, dir: 'dir', angle: a, spread: 20, speed: 820, speedRnd: 0.3, bursts: [{ t: 0.03, n: 4 }] },
            pt: { sprite: 'streak', size: 70, sizeRnd: 0.3, life: 0.35, lifeRnd: 0.2, drag: 3.5, alignVel: true, stretch: 1.4, grad: G1('#9C8B7A'), opacityOL: C([0, 1], [0.5, 0.9], [1, 0]) }
        })),
        EM('Dust cloud', {
            end: 0.12, em: { x: 0, y: 70, shape: 'circle', sx: 50, sy: 20, dir: 'dir', angle: -90, spread: 150, speed: 400, speedRnd: 0.5, bursts: [{ t: 0, n: 12 }, { t: 0.08, n: 6 }] },
            pt: {
                sprite: 'blob', size: 100, sizeRnd: 0.4, life: 0.9, lifeRnd: 0.2, drag: 3.5, gravY: -70, spin: 40, spinRnd: 1, rotRnd: 1,
                sizeOL: C([0, 0.4], [0.3, 1], [1, 1.15]), opacityOL: C([0, 1], [0.55, 0.85], [1, 0]), grad: G([0, '#E4DBCF'], [0.5, '#B3A392'], [1, '#86786A'])
            }
        })
    ];
});

fx('vfx_spoil_stink', { size: [256, 256], frames: 12, loop: 1.2 }, ({ P }) => {
    const fly = (i, ax, ay, ph, cy) => {
        const fx_ = t => ax * Math.sin(2 * Math.PI * (t / P + ph));
        const fy_ = t => cy + ay * Math.sin(4 * Math.PI * (t / P + ph));
        return [
            SP('Fly wing ' + i, { shape: 'dot', size: 11, color: WHITE, op: 0.8, x: sampleK(t => fx_(t) - 2, 0, P, 24), y: sampleK(t => fy_(t) - 7, 0, P, 24), scale: 1, aspect: 1.4 }),
            SP('Fly ' + i, { shape: 'dot', size: 12, color: '#262626', x: sampleK(fx_, 0, P, 24), y: sampleK(fy_, 0, P, 24), scale: 1 })
        ];
    };
    return [
        fly(1, 70, 26, 0, -30), fly(2, 55, 20, 0.45, -55),
        // три волнистых завитка: точка рождения качается, пар поднимается — волна ползёт вверх
        [[-55, 0], [0, 0.33], [55, 0.66]].map(([x0, ph], i) => [
            loop(EM('Stink curl ' + (i + 1), {
                em: { x: sampleK(t => x0 + 14 * Math.sin(2 * Math.PI * (t / P * 2 + ph)), 0, P, 24), y: 90, shape: 'point', dir: 'dir', angle: -90, spread: 0, speed: 150, speedRnd: 0, rate: 45, seed: 40 + i },
                pt: { sprite: 'dot', size: 24, sizeRnd: 0, life: 1.1, lifeRnd: 0, grad: G([0, '#C6E68A'], [0.5, '#95C552'], [1, '#6E9A3A']), sizeOL: C([0, 0.5], [0.3, 1], [1, 1.5]), opacityOL: C([0, 0], [0.12, 0.85], [0.6, 0.6], [1, 0]) }
            }), P, 1.1),
            loop(EM('Stink curl outline ' + (i + 1), {
                em: { x: sampleK(t => x0 + 14 * Math.sin(2 * Math.PI * (t / P * 2 + ph)), 0, P, 24), y: 90, shape: 'point', dir: 'dir', angle: -90, spread: 0, speed: 150, speedRnd: 0, rate: 45, seed: 40 + i },
                pt: { sprite: 'dot', size: 32, sizeRnd: 0, life: 1.1, lifeRnd: 0, grad: G1('#4F6E2A'), sizeOL: C([0, 0.5], [0.3, 1], [1, 1.4]), opacityOL: C([0, 0], [0.12, 0.85], [0.6, 0.6], [1, 0]) }
            }), P, 1.1)
        ])
    ];
});

fx('vfx_perish_timer', { size: [256, 256], frames: 12, loop: 1.0 }, ({ P }) => {
    const o = orbit({ r0: 92, a0: -90, turns: 1, t0: 0, t1: P, n: 48 });
    return [
        SP('Tip spark', { blend: 'add', shape: 'spark', size: 64, color: '#FFF3DC', x: o.x, y: o.y, rot: sampleK(t => 360 * t / P * 2, 0, P, 4), scale: 1, op: 1 }),
        SP('Tip glow', { blend: 'add', shape: 'soft', size: 46, color: ORANGE, x: o.x, y: o.y, scale: 1, op: 0.9 }),
        loop(EM('Sweep trail', {
            blend: 'add', em: { x: o.x, y: o.y, shape: 'point', speed: 0, speedRnd: 0, rate: 180 },
            pt: { sprite: 'soft', size: 20, sizeRnd: 0, life: 0.55, lifeRnd: 0, grad: G([0, '#FFE0A0'], [1, ORANGE]), sizeOL: C([0, 1], [1, 0.35]), opacityOL: C([0, 0.9], [1, 0]) }
        }), P, 0.55),
        SP('Base ring', { blend: 'add', shape: 'ring', p: { th: 0.05 }, size: 196, color: ORANGE, op: 0.3, scale: 1 })
    ];
});

fx('vfx_frost_breath', { size: [384, 384], frames: 12, loop: 2.0 }, ({ P }) => [
    loop(EM('Ice crystals', {
        em: { x: 0, y: -160, shape: 'box', sx: 120, sy: 10, dir: 'dir', angle: 90, spread: 30, speed: 90, speedRnd: 0.4, rate: 4 },
        pt: { sprite: 'tex:snowflake', size: 24, sizeRnd: 0.35, life: 2.0, lifeRnd: 0.1, gravY: 40, turbAmp: 30, spin: 60, spinRnd: 1, rotRnd: 1, opacityOL: C([0, 0], [0.2, 0.9], [0.7, 0.8], [1, 0]) }
    }), P, 2.2),
    loop(EM('Cold vapour', {
        em: { x: 0, y: -180, shape: 'box', sx: 110, sy: 6, dir: 'dir', angle: 90, spread: 20, speed: 150, speedRnd: 0.3, rate: 9 },
        pt: {
            sprite: 'smoke', size: 70, sizeRnd: 0.3, life: 2.0, lifeRnd: 0.1, gravY: 70, drag: 0.5, turbAmp: 40, turbFreq: 0.8, spin: 20, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.4], [1, 1.9]), opacityOL: C([0, 0], [0.12, 0.4], [0.5, 0.22], [1, 0]), grad: G([0, WHITE], [1, SKY])
        }
    }), P, 2.2)
]);

fx('vfx_freezer_open', { size: [512, 512], frames: 10, dur: 0.9 }, () => [
    glints({ n: 12, speed: 280, color: SKY, size: 30, r: 60 }),
    EM('Ice crystals', {
        end: 0.05, em: { x: 0, y: -20, shape: 'circle', sx: 60, sy: 40, dir: 'out', speed: 380, speedRnd: 0.4, bursts: [{ t: 0, n: 12 }] },
        pt: { sprite: 'tex:snowflake', size: 26, sizeRnd: 0.4, life: 0.75, lifeRnd: 0.2, drag: 3, gravY: 120, spin: 180, spinRnd: 1, rotRnd: 1, sizeOL: C([0, 0.3], [0.2, 1], [1, 0.6]), opacityOL: C([0, 1], [0.7, 1], [1, 0]) }
    }),
    EM('Fog wave', {
        end: 0.1, em: { x: 0, y: -50, shape: 'box', sx: 110, sy: 20, dir: 'dir', angle: 90, spread: 90, speed: 320, speedRnd: 0.5, bursts: [{ t: 0, n: 14 }, { t: 0.06, n: 8 }] },
        pt: {
            sprite: 'smoke', size: 140, sizeRnd: 0.3, life: 0.85, lifeRnd: 0.15, drag: 2.8, gravY: 90, spin: 30, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.4], [0.4, 1], [1, 1.3]), opacityOL: C([0, 0.85], [0.5, 0.55], [1, 0]), grad: G([0, WHITE], [1, SKY])
        }
    }),
    flash({ t: 0, d: 0.35, color: SKY, size: 320, op: 0.8 })
]);

fx('vfx_lock_break', { size: [384, 384], frames: 10, dur: 0.7 }, () => {
    const link = (side) => {
        const sx = side;
        const x = K([0, 10 * sx], [0.55, 150 * sx]);
        const y = K([0, 0, 'ease'], [0.18, -50, 'ease'], [0.55, 90, 'ease']);
        const rot = K([0, 20 * sx], [0.55, -160 * sx]);
        const sc = K([0, 0.5], [0.08, 1.1], [0.15, 1]);
        const op = K([0, 1], [0.4, 1], [0.55, 0]);
        return [
            SP('Link ' + (sx < 0 ? 'L' : 'R'), { start: 0.05, end: 0.6, shape: 'ring', p: { th: 0.32 }, aspect: 0.62, size: 72, color: GOLD, x, y, rot, scale: sc, op }),
            SP('Link outline ' + (sx < 0 ? 'L' : 'R'), { start: 0.05, end: 0.6, shape: 'ring', p: { th: 0.5 }, aspect: 0.64, size: 84, color: BROWN, x, y, rot, scale: sc, op })
        ];
    };
    return [
        sparkBurst({ n: 6, speed: 380, size: 40, life: 0.5 }),
        link(-1), link(1),
        SP('Glint', { blend: 'add', shape: 'spark', size: 300, color: WHITE, end: 0.32, rot: K([0, 0], [0.32, 45]), scale: K([0, 0], [0.08, 1], [0.32, 0]), op: 1 }),
        flash({ t: 0, d: 0.35, size: 260, color: GOLD, s1: 1.4 })
    ];
});

fx('vfx_box_open', { size: [512, 512], frames: 12, dur: 1.1 }, () => {
    const flap = (side) => {
        const x = K([0, 50 * side], [0.12, 120 * side], [0.5, 170 * side]);
        const y = K([0, 150], [0.12, 110], [0.5, 190]);
        const rot = K([0, 0], [0.12, 35 * side], [0.5, 110 * side]);
        const op = K([0, 1], [0.35, 1], [0.5, 0]);
        const sc = K([0, 1], [0.5, 1]);
        return [
            SP('Flap ' + (side < 0 ? 'L' : 'R'), Object.assign({ end: 0.5, color: '#D89A5B', x, y, rot, op, scale: sc }, bar(110, 46))),
            SP('Flap outline ' + (side < 0 ? 'L' : 'R'), Object.assign({ end: 0.5, color: BROWN, x, y, rot, op, scale: sc }, bar(122, 58)))
        ];
    };
    return [
        confettiBurst({ x: 0, y: 110, dir: 'dir', angle: -90, spread: 70, speed: 700, n: 7, gravY: 700, drag: 1.2, life: 1.0, turb: 120 }),
        sparkBurst({ x: 0, y: 60, n: 6, speed: 520, size: 44, em: { dir: 'dir', angle: -90, spread: 80, shape: 'point' }, gravY: 200, life: 0.8, drag: 2 }),
        flap(-1), flap(1),
        SP('Rays', { shape: 'tex:rays', size: 380, y: K([0, 90], [0.3, 20]), scale: K([0, 0.2], [0.2, 1.15], [0.6, 1.25]), op: K([0, 0], [0.08, 1], [0.45, 0.9], [0.75, 0]), end: 0.8 }),
        SP('Light cone', { blend: 'add', shape: 'soft', size: 480, aspect: 0.42, color: LGOLD, y: K([0, 110], [0.5, -20]), scale: K([0, 0.3], [0.25, 1.1]), op: K([0, 0], [0.1, 1], [0.4, 0.85], [0.9, 0]), end: 0.9 }),
        flash({ t: 0, d: 0.35, y: 130, color: CREAM, size: 280 })
    ];
});

fx('vfx_pallet_drop', { size: [640, 640], frames: 10, dur: 0.8 }, () => [
    EM('Splinters', {
        end: 0.05, em: { x: 0, y: 110, shape: 'box', sx: 120, sy: 4, dir: 'dir', angle: -90, spread: 140, speed: 520, speedRnd: 0.35, bursts: [{ t: 0, n: 10 }] },
        pt: { sprite: 'shard', size: 22, sizeRnd: 0.4, life: 0.6, lifeRnd: 0.2, gravY: 1500, spin: 500, spinRnd: 1, rotRnd: 1, grad: G1('#B07A42'), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
    }),
    [180, 0].map((a, i) => EM('Shock lines ' + (i + 1), {
        end: 0.05, em: { x: 0, y: 105, shape: 'box', sx: 30, sy: 20, dir: 'dir', angle: a, spread: 14, speed: 980, speedRnd: 0.2, bursts: [{ t: 0, n: 3 }] },
        pt: { sprite: 'streak', size: 110, sizeRnd: 0.2, life: 0.3, lifeRnd: 0.1, drag: 5, alignVel: true, stretch: 2, grad: G1(CREAM), opacityOL: C([0, 1], [0.5, 1], [1, 0]) }
    })),
    [180, 0].map((a, i) => EM('Dust ring ' + (i + 1), {
        end: 0.05, em: { x: 0, y: 115, shape: 'box', sx: 40, sy: 6, dir: 'dir', angle: a, spread: 16, speed: 620, speedRnd: 0.5, bursts: [{ t: 0, n: 13 }] },
        pt: {
            sprite: 'blob', size: 80, sizeRnd: 0.4, life: 0.72, lifeRnd: 0.2, drag: 4, gravY: -40, spin: 40, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.4], [0.3, 1], [1, 1.1]), opacityOL: C([0, 1], [0.55, 0.8], [1, 0]), grad: G([0, CREAM], [0.6, '#D9C3A0'], [1, '#B89C7A'])
        }
    })),
    ringWave({ t: 0, d: 0.45, y: 115, aspect: 3.6, color: CREAM, th: 0.16, size: 180, s0: 0.3, s1: 2.2, blend: 'normal', op: 0.8 })
]);

fx('vfx_sale_shimmer', { size: [256, 256], frames: 12, loop: 1.2 }, ({ P }) => {
    const sweep = (name, size, aspect, color, op, off) => SP(name, {
        blend: 'add', shape: 'soft', size, aspect, rot: 45, color, fade: [0, 0, 150, 0.6],
        x: K([0, -200 + off, 'lin'], [0.6, 200 + off, 'hold'], [P, -200 + off, 'lin']),
        y: K([0, 200 + off, 'lin'], [0.6, -200 + off, 'hold'], [P, 200 + off, 'lin']), scale: 1, op
    });
    const tw = (i, x, y, t) => SP('Twinkle ' + i, { blend: 'add', shape: 'spark', size: 70, color: '#FFF0B8', x, y, scale: K([0, 0], [t, 0], [t + 0.1, 1.1], [t + 0.25, 0], [P, 0]), rot: K([0, 0], [P, 90]), op: 1 });
    return [
        tw(1, -40, 45, 0.22), tw(2, 50, -45, 0.4),
        sweep('Gloss band', 300, 0.22, WHITE, 0.9, 0),
        sweep('Gloss band thin', 260, 0.08, '#FFF0B8', 0.8, 45)
    ];
});

fx('vfx_bundle_tie', { size: [384, 384], frames: 10, dur: 0.9 }, () => {
    const sp = orbit({ r0: 140, r1: 4, a0: -90, turns: 1.3, t0: 0, t1: 0.38, n: 30, ease: u => easeIn(u, 1.4) });
    const whip = (name, size, color) => EM(name, {
        end: 0.38, em: { x: sp.x, y: sp.y, shape: 'point', speed: 0, speedRnd: 0, rate: 320 },
        pt: { sprite: 'dot', size, sizeRnd: 0, life: 0.28, lifeRnd: 0, grad: G1(color), sizeOL: C([0, 1], [1, 0.45]), opacityOL: C([0, 1], [0.6, 1], [1, 0]) }
    });
    const g = [{ t: 0, s: 0 }, { t: 0.08, s: 1.2 }, { t: 0.14, s: 0.94 }, { t: 0.19, s: 1 }, { t: 0.4, s: 1 }, { t: 0.55, s: 0 }];
    const lp = bar(1, 1);
    return [
        SP('Knot sparkle', { shape: 'tex:sparkle', size: 70, x: 20, y: -26, start: 0.44, end: 0.72, scale: K([0, 0], [0.08, 1.2], [0.28, 0]), rot: K([0, 0], [0.28, 90]), op: 1 }),
        rigid([
            { name: 'Knot', shape: 'dot', size: 34, color: RED },
            { name: 'Knot outline', shape: 'dot', size: 46, color: BROWN },
            { name: 'Loop L', shape: 'ring', p: { th: 0.4 }, aspect: 1.35, size: 70, color: RED, x: -40, rot: 18 },
            { name: 'Loop R', shape: 'ring', p: { th: 0.4 }, aspect: 1.35, size: 70, color: RED, x: 40, rot: -18 },
            { name: 'Loop L outline', shape: 'ring', p: { th: 0.56 }, aspect: 1.3, size: 84, color: BROWN, x: -40, rot: 18 },
            { name: 'Loop R outline', shape: 'ring', p: { th: 0.56 }, aspect: 1.3, size: 84, color: BROWN, x: 40, rot: -18 },
            Object.assign({ name: 'Tail L', color: RED, x: -16, y: 36, rot: 70 }, bar(46, 16)),
            Object.assign({ name: 'Tail R', color: RED, x: 16, y: 36, rot: -70 }, bar(46, 16)),
            Object.assign({ name: 'Tail L outline', color: BROWN, x: -16, y: 36, rot: 70 }, bar(58, 28)),
            Object.assign({ name: 'Tail R outline', color: BROWN, x: 16, y: 36, rot: -70 }, bar(58, 28))
        ].map(p => Object.assign(p, { start: 0.35, end: 0.9 })), g.map(k => k), { op: 1 }),
        whip('Ribbon whip', 24, RED), whip('Ribbon whip outline', 34, BROWN)
    ];
});

fx('vfx_belt_speed', { size: [512, 512], frames: 12, loop: 0.6 }, ({ P }) => [
    loop(EM('Speed lines', {
        em: { x: 300, y: 0, shape: 'box', sx: 20, sy: 150, dir: 'dir', angle: 180, spread: 0, speed: 1150, speedRnd: 0.25, rate: 14 },
        pt: { sprite: 'streak', size: 150, sizeRnd: 0.5, life: 0.62, lifeRnd: 0, alignVel: true, stretch: 0.6, grad: G1(WHITE), opacityOL: C([0, 0], [0.2, 0.9], [0.7, 0.7], [1, 0]) }
    }), P, 0.62)
]);

fx('vfx_customer_call', { size: [384, 384], frames: 10, dur: 0.8 }, () => {
    const g = [{ t: 0, s: 0, y: 20 }, { t: 0.14, s: 1.15, y: 20 }, { t: 0.23, s: 0.93, y: 20 }, { t: 0.31, s: 1.02, y: 20 }, { t: 0.37, s: 1, y: 20 }, { t: 0.62, s: 1, y: 20 }, { t: 0.78, s: 0, y: 20 }];
    const lines = [-125, -90, -55].map((a, i) => {
        const rad = a * Math.PI / 180, b = bar(30, 8);
        return SP('Motion line ' + (i + 1), Object.assign({}, b, {
            color: BROWN, rot: a, start: 0.12, end: 0.5,
            x: K([0, Math.cos(rad) * 125], [0.3, Math.cos(rad) * 150]), y: K([0, 20 + Math.sin(rad) * 125], [0.3, 20 + Math.sin(rad) * 150]),
            scale: K([0, 0.3], [0.08, 1], [0.3, 0.6]), op: K([0, 0], [0.06, 1], [0.22, 1], [0.34, 0])
        }));
    });
    return [
        lines,
        rigid([
            { name: 'Bubble', shape: 'dot', size: 190, aspect: 1.3, color: CREAM },
            { name: 'Bubble shine', shape: 'dot', size: 40, aspect: 1.6, color: WHITE, x: -58, y: -40, op: 0.9 },
            { name: 'Bubble tail', shape: 'poly', p: { n: 3 }, size: 56, color: CREAM, x: -52, y: 78, rot: 200 },
            { name: 'Bubble outline', shape: 'dot', size: 206, aspect: 1.28, color: BROWN },
            { name: 'Bubble tail outline', shape: 'poly', p: { n: 3 }, size: 76, color: BROWN, x: -54, y: 84, rot: 200 }
        ], g, { op: 1 })
    ];
});

fx('vfx_hint_finger', { size: [384, 384], frames: 12, loop: 1.2 }, ({ P }) => [
    [0, 1].map(i => SP('Pulse ring ' + (i + 1), {
        blend: 'add', shape: 'ring', p: { th: 0.12 }, size: 220, color: GOLD, glow: 0.4, start: i * P / 2, end: (i + 1) * P / 2,
        scale: K([0, 0.3, 'lin'], [P / 2, 1.3]), op: K([0, 0], [0.08, 0.95], [P / 2, 0])
    })),
    SP('Arrow head', { blend: 'add', shape: 'poly', p: { n: 3 }, size: 60, color: GOLD, y: K([0, -40], [P / 2, -56], [P, -40]), scale: 1, op: K([0, 0.3], [P / 2, 0.6], [P, 0.3]) }),
    SP('Arrow stem', Object.assign({ blend: 'add', color: GOLD, y: K([0, -2], [P / 2, -18], [P, -2]), scale: 1, op: K([0, 0.3], [P / 2, 0.6], [P, 0.3]) }, bar(18, 44))),
    SP('Halo', { blend: 'add', shape: 'soft', size: 200, color: GOLD, scale: K([0, 0.9], [P / 4, 1.05], [P / 2, 0.9], [3 * P / 4, 1.05], [P, 0.9]), op: K([0, 0.35], [P / 4, 0.6], [P / 2, 0.35], [3 * P / 4, 0.6], [P, 0.35]) })
]);

fx('vfx_hint_trail', { size: [512, 512], frames: 12, dur: 1.2 }, () => {
    const pts = [[-200, 160], [-120, -140], [120, -200], [200, -120]];
    const pth = bezPath(pts, 0, 0.6, 12, u => easeOut(u, 1.3));
    const bursts = Array.from({ length: 11 }, (_, i) => ({ t: r3(i * 0.06), n: 1 }));
    return [
        EM('Land twinkles', {
            blend: 'add', end: 0.62, em: { x: pth.x, y: pth.y, shape: 'point', speed: 0, speedRnd: 0, bursts },
            pt: { sprite: 'spark', size: 50, sizeRnd: 0.2, life: 0.3, lifeRnd: 0, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE, rot: 0, rotRnd: 0.1 }
        }),
        EM('Trail dots', {
            blend: 'normal', end: 0.62, em: { x: pth.x, y: pth.y, shape: 'point', speed: 0, speedRnd: 0, bursts },
            pt: { sprite: 'dot', size: 22, sizeRnd: 0, life: 0.95, lifeRnd: 0, grad: G([0, LGOLD], [0.3, GOLD], [1, GOLD]), sizeOL: C([0, 0], [0.12, 1.35], [0.25, 1], [1, 0.85]), opacityOL: C([0, 1], [0.75, 1], [1, 0]), glow: 0.4 }
        }),
        EM('Trail dots outline', {
            blend: 'normal', end: 0.62, em: { x: pth.x, y: pth.y, shape: 'point', speed: 0, speedRnd: 0, bursts },
            pt: { sprite: 'dot', size: 30, sizeRnd: 0, life: 0.95, lifeRnd: 0, grad: G1(BROWN), sizeOL: C([0, 0], [0.12, 1.3], [0.25, 1], [1, 0.87]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
        })
    ];
});

fx('vfx_undo_swirl', { size: [384, 384], frames: 10, dur: 0.7 }, () => {
    const sp = orbit({ r0: 130, r1: 18, a0: 0, turns: -1.1, t0: 0, t1: 0.45, n: 30, ease: u => easeIn(u, 1.3) });
    const band = (name, size, color) => EM(name, {
        end: 0.45, em: { x: sp.x, y: sp.y, shape: 'point', speed: 0, speedRnd: 0, rate: 420 },
        pt: { sprite: 'dot', size, sizeRnd: 0, life: 0.34, lifeRnd: 0, grad: G1(color), sizeOL: C([0, 1], [1, 0.55]), opacityOL: C([0, 1], [0.5, 1], [1, 0]) }
    });
    const head = (name, size, color) => SP(name, { shape: 'poly', p: { n: 3 }, size, color, end: 0.5, x: sp.x, y: sp.y, rot: sp.rot, scale: K([0, 0.6], [0.06, 1], [0.42, 1], [0.5, 0]), op: 1 });
    return [
        SP('Tick sparkle', { shape: 'tex:sparkle', size: 60, start: 0.42, end: 0.7, scale: K([0, 0], [0.08, 1.2], [0.28, 0]), rot: K([0, 0], [0.28, -90]), op: 1 }),
        SP('Tick glint', { blend: 'add', shape: 'spark', size: 140, color: WHITE, start: 0.4, end: 0.66, scale: K([0, 0], [0.06, 1], [0.26, 0]), op: 1 }),
        ringWave({ t: 0.42, d: 0.28, color: SKY, th: 0.12, size: 120, s0: 0.3, s1: 1.4 }),
        head('Arrow head', 50, SKY), head('Arrow head outline', 68, BROWN),
        band('Swirl', 26, SKY), band('Swirl outline', 36, BROWN)
    ];
});

fx('vfx_rewind_wave', { size: [1024, 512], frames: 12, dur: 0.9 }, () => {
    const bx = K([0, 330, 'ease'], [0.75, -330, 'ease']);
    return [
        EM('Clock sparkles', {
            blend: 'add', end: 0.75, em: { x: bx, y: 0, shape: 'box', sx: 20, sy: 110, dir: 'dir', angle: 0, spread: 40, speed: 60, speedRnd: 0.5, rate: 26 },
            pt: { sprite: 'spark', size: 30, sizeRnd: 0.4, life: 0.4, lifeRnd: 0.3, grad: G([0, WHITE], [1, SKY]), sizeOL: BELL, opacityOL: ONE, rotRnd: 0.2 }
        }),
        EM('Trailing streaks', {
            blend: 'add', end: 0.75, em: { x: bx, y: 0, shape: 'box', sx: 10, sy: 120, dir: 'dir', angle: 0, spread: 0, speed: 140, speedRnd: 0.5, rate: 80 },
            pt: { sprite: 'streak', size: 110, sizeRnd: 0.5, life: 0.35, lifeRnd: 0.3, alignVel: true, grad: G([0, WHITE], [1, SKY]), opacityOL: C([0, 0.8], [1, 0]) }
        }),
        SP('Band core', { blend: 'add', shape: 'soft', size: 330, aspect: 0.12, color: WHITE, x: bx, end: 0.9, scale: 1, op: K([0, 0], [0.1, 0.9], [0.6, 0.9], [0.85, 0]) }),
        SP('Band', { blend: 'add', shape: 'soft', size: 380, aspect: 0.32, color: SKY, x: bx, end: 0.9, scale: 1, op: K([0, 0], [0.1, 0.95], [0.6, 0.9], [0.85, 0]) })
    ];
});

fx('vfx_coin_fly', { size: [256, 256], frames: 10, dur: 0.5 }, () => {
    const out = [];
    for (let i = 0; i < 10; i++) {
        const a = Math.max(0.1, Math.abs(Math.cos(2 * Math.PI * i / 10)));
        const w = { start: i * 0.05, end: (i + 1) * 0.05, scale: 1, op: 1 };
        const edge = a < 0.35;
        out.push(
            SP('Coin shine ' + i, Object.assign({}, w, { shape: 'soft', size: 42, aspect: a, x: -24 * a, y: -26, color: WHITE, op: edge ? 0 : 0.7 })),
            SP('Coin inner ring ' + i, Object.assign({}, w, { shape: 'ring', p: { th: 0.12 }, size: 96, aspect: a, color: DGOLD, op: edge ? 0 : 1 })),
            SP('Coin face ' + i, Object.assign({}, w, { shape: 'dot', size: 132, aspect: a, color: edge ? DGOLD : GOLD })),
            SP('Coin outline ' + i, Object.assign({}, w, { shape: 'dot', size: 148, aspect: (a * 132 + 16) / 148, color: BROWN }))
        );
    }
    return [
        out,
        EM('Trail sparkles', {
            em: { x: -30, y: 25, shape: 'circle', sx: 20, sy: 20, dir: 'dir', angle: 150, spread: 40, speed: 170, speedRnd: 0.4, rate: 14 },
            pt: { sprite: 'tex:sparkle', size: 26, sizeRnd: 0.3, life: 0.35, lifeRnd: 0.2, sizeOL: BELL, opacityOL: ONE, spin: 120, spinRnd: 1, rotRnd: 1 }
        }),
        EM('Trail glints', {
            blend: 'add', start: -0.3, em: { x: -30, y: 25, shape: 'circle', sx: 24, sy: 24, dir: 'dir', angle: 150, spread: 40, speed: 190, speedRnd: 0.4, rate: 70 },
            pt: { sprite: 'spark', size: 26, sizeRnd: 0.4, life: 0.3, lifeRnd: 0.3, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE }
        })
    ];
});

fx('vfx_coin_fountain', { size: [640, 640], frames: 12, dur: 1.2 }, () => [
    EM('Twinkles', {
        blend: 'add', end: 0.3, em: { x: 0, y: 170, shape: 'box', sx: 20, sy: 4, dir: 'dir', angle: -90, spread: 55, speed: 700, speedRnd: 0.35, rate: 40 },
        pt: { sprite: 'spark', size: 34, sizeRnd: 0.4, life: 0.9, lifeRnd: 0.3, gravY: 1200, grad: G([0, WHITE], [1, GOLD]), opacityOL: C([0, 0], [0.2, 1], [0.35, 0.2], [0.5, 1], [0.7, 0.2], [0.85, 1], [1, 0]) }
    }),
    EM('Coins', {
        end: 0.2, em: { x: 0, y: 190, shape: 'box', sx: 16, sy: 4, dir: 'dir', angle: -90, spread: 55, speed: 820, speedRnd: 0.3, bursts: [{ t: 0, n: 8 }, { t: 0.07, n: 6 }, { t: 0.14, n: 5 }] },
        pt: { sprite: 'tex:coin', size: 50, sizeRnd: 0.2, life: 1.05, lifeRnd: 0.1, gravY: 1500, drag: 0.3, spin: 240, spinRnd: 1, rotRnd: 0.3, sizeOL: C([0, 0.5], [0.1, 1], [1, 1]), opacityOL: C([0, 1], [0.85, 1], [1, 0]) }
    }),
    flash({ t: 0, d: 0.35, y: 180, color: LGOLD, size: 280 })
]);

fx('vfx_star_award', { size: [512, 512], frames: 14, dur: 0.9 }, () => {
    const T = 0.26;
    const sx = K([0, 230, 'ease'], [T, 0, 'ease']), sy = K([0, -230, 'ease'], [T, 0, 'ease']);
    return [
        sparkBurst({ t: T, n: 6, r: 30, speed: 460, size: 46 }),
        SP('Star', { shape: 'tex:star', size: 170, x: sx, y: sy, rot: K([0, -120], [T, 0]), scale: K([0, 0.45], [T, 0.9], [T + 0.06, 1.25], [T + 0.14, 0.95], [T + 0.2, 1], [0.78, 1], [0.9, 0]), op: 1 }),
        EM('Star trail', {
            blend: 'add', end: T, em: { x: sx, y: sy, shape: 'circle', sx: 20, sy: 20, speed: 30, rate: 160 },
            pt: { sprite: 'spark', size: 40, sizeRnd: 0.3, life: 0.28, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: C([0, 1], [1, 0.2]), opacityOL: C([0, 1], [1, 0]) }
        }),
        ringWave({ t: T, d: 0.4, th: 0.08, size: 220, s0: 0.3, s1: 2.2, glow: 0.4 }),
        SP('Radial flash', { blend: 'add', shape: 'star', p: { n: 12, inr: 0.25 }, size: 460, color: LGOLD, start: T, end: T + 0.4, rot: K([0, 0], [0.4, 30]), scale: K([0, 0.2], [0.12, 1]), op: K([0, 0.9], [0.4, 0]) }),
        flash({ t: T, d: 0.4, color: CREAM, size: 320, s1: 1.5 })
    ];
});

fx('vfx_level_win', { size: [1024, 1024], frames: 16, dur: 1.6 }, () => [
    confettiBurst({ n: 14, speed: 720, life: 1.5, size: 28 }),
    [GOLD, RED, GREEN, CREAM].map((c, i) => EM('Paper dots ' + (i + 1), {
        end: 0.05, em: { shape: 'circle', sx: 20, sy: 20, dir: 'out', speed: 640, speedRnd: 0.5, bursts: [{ t: 0, n: 8 }] },
        pt: { sprite: 'dot', size: 16, sizeRnd: 0.4, life: 1.5, lifeRnd: 0.2, drag: 2.2, gravY: 300, turbAmp: 150, turbFreq: 1, grad: G1(c), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
    })),
    // ленты-серпантин: голова ленты — частица, хвост — трейл, сужается к основанию
    [-1, 1].map((sd, i) => EM('Streamer ' + (i + 1), {
        end: 0.05, em: { shape: 'point', dir: 'dir', angle: sd < 0 ? -140 : -40, spread: 16, speed: 900, speedRnd: 0.15, bursts: [{ t: 0, n: 2 }], seed: 60 + i },
        pt: {
            render: 'trail', trailCore: 0.3, sprite: 'soft', size: 14, sizeRnd: 0.1, life: 1.0, lifeRnd: 0.1, drag: 2.6, gravY: 420, turbAmp: 2200, turbFreq: 1.6,
            sizeOT: C([0, 0], [0.55, 0.2], [0.85, 1], [1, 1]), grad: G([0, LGOLD], [1, GOLD]), sizeOL: C([0, 1], [1, 0.8]), opacityOL: C([0, 1], [0.7, 1], [1, 0]), glow: 0.3
        }
    })),
    SP('Ray burst', { blend: 'add', shape: 'star', p: { n: 12, inr: 0.22 }, size: 520, color: LGOLD, end: 1.0, rot: K([0, 0, 'lin'], [1.0, 35, 'lin']), scale: K([0, 0.3], [0.3, 1.1], [1.0, 1.25]), op: K([0, 0], [0.1, 0.45], [0.5, 0.25], [1.0, 0]) }),
    flash({ t: 0, d: 0.55, color: CREAM, size: 420, s1: 1.6 })
]);

fx('vfx_level_lose', { size: [640, 640], frames: 12, dur: 1.2 }, () => [
    EM('Droplets', {
        start: 0.2, end: 0.25, em: { x: 0, y: 30, shape: 'box', sx: 45, sy: 4, dir: 'dir', angle: 90, spread: 10, speed: 70, speedRnd: 0.3, bursts: [{ t: 0, n: 3 }] },
        pt: { sprite: 'dot', size: 20, sizeRnd: 0.15, life: 0.75, lifeRnd: 0.1, gravY: 520, alignVel: true, stretch: 0.5, grad: G1('#7FB8E0'), opacityOL: C([0, 0], [0.1, 1], [0.8, 1], [1, 0]) }
    }),
    EM('Smoke curl', {
        start: 0.15, end: 0.7, em: { x: 0, y: -40, shape: 'box', sx: 10, sy: 4, dir: 'dir', angle: -90, spread: 12, speed: 60, speedRnd: 0.3, rate: 16 },
        pt: { sprite: 'smoke', size: 50, sizeRnd: 0.3, life: 0.9, lifeRnd: 0.2, gravY: -40, turbAmp: 110, turbFreq: 1.2, spin: 40, spinRnd: 1, rotRnd: 1, grad: G([0, '#C8C8CE'], [1, '#9A9AA4']), sizeOL: C([0, 0.4], [1, 1.3]), opacityOL: C([0, 0], [0.3, 0.45], [1, 0]) }
    }),
    EM('Deflate puff', {
        end: 0.05, em: { x: 0, y: -10, shape: 'circle', sx: 50, sy: 30, dir: 'out', speed: 50, speedRnd: 0.4, bursts: [{ t: 0, n: 7 }] },
        pt: { sprite: 'blob', size: 100, sizeRnd: 0.3, life: 1.1, lifeRnd: 0.1, drag: 2, gravY: 110, spin: 30, spinRnd: 1, rotRnd: 1, grad: G([0, '#D2D2D8'], [1, '#9A9AA4']), sizeOL: C([0, 0.3], [0.2, 1], [1, 0.55]), opacityOL: C([0, 0], [0.12, 1], [0.7, 0.8], [1, 0]) }
    })
]);

fx('vfx_combo_ring', { size: [512, 512], frames: 10, dur: 0.7 }, () => [
    EM('Twinkles', {
        blend: 'add', end: 0.05, em: { shape: 'ring', sx: 80, sy: 20, dir: 'out', speed: 210, speedRnd: 0.4, bursts: [{ t: 0.04, n: 12 }] },
        pt: { sprite: 'spark', size: 36, sizeRnd: 0.4, life: 0.5, lifeRnd: 0.3, drag: 2, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE }
    }),
    ringWave({ name: 'Ring fast', t: 0, d: 0.6, th: 0.07, size: 220, s0: 0.2, s1: 2.3, glow: 0.4 }),
    ringWave({ name: 'Ring slow', t: 0.03, d: 0.6, th: 0.14, size: 200, color: CREAM, s0: 0.2, s1: 1.45 }),
    flash({ t: 0, d: 0.3, size: 200, color: LGOLD, op: 0.8 })
]);

// =====================================================================================
// 2. КАРТА РАЙОНА
// =====================================================================================
fx('vfx_node_unlock', { size: [384, 384], frames: 12, dur: 1.0 }, () => {
    const T = 0.3;
    const piece = (i, a) => {
        const rad = a * Math.PI / 180;
        const x = K([0, Math.cos(rad) * 45], [0.55, Math.cos(rad) * 190]);
        const y = K([0, Math.sin(rad) * 45], [0.25, Math.sin(rad) * 120 - 20], [0.55, Math.sin(rad) * 170 + 90]);
        const rot = K([0, a + 90], [0.55, a + 90 + (i % 2 ? 260 : -260)]);
        const sc = 1, op = K([0, 1], [0.4, 1], [0.6, 0]);
        return [
            SP('Piece ' + i, { start: T, end: T + 0.6, shape: 'shard', p: { var: i }, size: 100, color: GOLD, x, y, rot, scale: sc, op }),
            SP('Piece outline ' + i, { start: T, end: T + 0.6, shape: 'shard', p: { var: i }, size: 118, color: BROWN, x, y, rot, scale: sc, op })
        ];
    };
    const sealX = K([0, 0], [0.16, 0], [0.19, -5], [0.22, 5], [0.25, -4], [0.28, 3], [T, 0]);
    const sealS = K([0, 0.2], [0.1, 1.05], [0.16, 1], [T, 1.04]);
    return [
        sparkBurst({ t: T, n: 8, r: 40, speed: 420, size: 40 }),
        glints({ t: T, n: 10, speed: 280, size: 30 }),
        piece(0, -90), piece(1, 30), piece(2, 150),
        SP('Seal', { end: T, shape: 'ring', p: { th: 0.3 }, size: 150, color: GOLD, x: sealX, scale: sealS, op: 1 }),
        SP('Seal outline', { end: T, shape: 'ring', p: { th: 0.42 }, size: 164, color: BROWN, x: sealX, scale: sealS, op: 1 }),
        flash({ t: T, d: 0.4, color: CREAM, size: 300, s1: 1.5 })
    ];
});

fx('vfx_node_pulse', { size: [384, 384], frames: 12, loop: 1.2 }, ({ P }) => [
    SP('Thin ring', { blend: 'add', shape: 'ring', p: { th: 0.06 }, size: 230, color: CREAM, scale: K([0, 0.85, 'lin'], [P, 1.55, 'lin']), op: K([0, 0], [0.15, 0.85], [P, 0]) }),
    SP('Halo', { blend: 'add', shape: 'ring', p: { th: 0.35 }, size: 240, color: LGOLD, glowL: 1.2, glowLR: 18, scale: K([0, 0.95], [P / 2, 1.05], [P, 0.95]), op: K([0, 0.45], [P / 2, 0.9], [P, 0.45]) })
]);

fx('vfx_road_dash', { size: [512, 128], frames: 12, loop: 1.2 }, ({ P }) => {
    const dots = (name, size, grad, blend, glow) => loop(EM(name, {
        blend, em: { x: -280, y: 0, shape: 'point', dir: 'dir', angle: 0, spread: 0, speed: 400, speedRnd: 0, bursts: Array.from({ length: 12 }, (_, i) => ({ t: r3(i * 0.1), n: 1 })) },
        pt: { sprite: 'dot', size, sizeRnd: 0, life: 1.4, lifeRnd: 0, grad, sizeOL: C([0, 0.6], [0.5, 1.25], [1, 0.6]), opacityOL: C([0, 0], [0.08, 0.6], [0.5, 1], [0.92, 0.6], [1, 0]), glow }
    }), P, 1.4);
    return [dots('Dots', 16, G([0, GOLD], [0.5, CREAM], [1, GOLD]), 'normal', 0.4), dots('Dots outline', 22, G1(BROWN), 'normal', 0)];
});

fx('vfx_district_unlock', { size: [1024, 1024], frames: 16, dur: 1.8 }, () => {
    const T = 0.35;
    const half = (side) => {
        const px = 280 * side;
        const g = [{ t: 0, x: px, s: 1 }, { t: T, x: px, s: 1 }, { t: T + 0.35, x: px + 60 * side, y: 30, r: -70 * side, s: 1 }, { t: T + 0.9, x: px + 200 * side, y: 160, r: -85 * side, s: 1 }];
        const lx = -140 * side;
        return rigid([
            Object.assign({ name: 'Ribbon stripe ' + side, color: '#F08478', x: lx, y: -12 }, bar(280, 8)),
            Object.assign({ name: 'Ribbon ' + side, color: RED, x: lx }, bar(280, 56)),
            Object.assign({ name: 'Ribbon outline ' + side, color: BROWN, x: lx }, bar(284, 68))
        ], g, { op: K([0, 1], [T + 0.6, 1], [T + 0.95, 0]) });
    };
    return [
        confettiFall({ t0: 0.4, t1: 1.3, y: -300, w: 280, rate: 9, life: 1.6 }),
        confettiBurst({ t: T, n: 8, speed: 600, life: 1.3 }),
        SP('Cut glint', { blend: 'add', shape: 'spark', size: 320, color: WHITE, start: T - 0.05, end: T + 0.3, rot: K([0, 0], [0.35, 45]), scale: K([0, 0], [0.07, 1.1], [0.35, 0]), op: 1 }),
        half(-1), half(1),
        firework({ i: 1, t: 0.42, x: -150, y: -130, c: LGOLD }),
        firework({ i: 2, t: 0.58, x: 160, y: -160, c: '#FFB08A', c2: RED }),
        firework({ i: 3, t: 0.74, x: 10, y: -210, c: LGOLD, n: 40 }),
        firework({ i: 4, t: 0.9, x: -60, y: 150, c: '#B8F0A0', c2: GREEN, n: 26 }),
        flash({ t: T, d: 0.5, color: CREAM, size: 420, s1: 1.6 })
    ];
});

fx('vfx_star_fill', { size: [192, 192], frames: 8, dur: 0.45 }, () => [
    [[-150, -110, 0.1], [150, 90, 0.18]].map(([x, y, t], i) => SP('Sparkle ' + (i + 1), { shape: 'tex:sparkle', size: 90, x, y, start: t, end: t + 0.24, scale: K([0, 0], [0.07, 1.2], [0.24, 0]), rot: K([0, 0], [0.24, 80]), op: 1 })),
    SP('Star flash', { blend: 'add', shape: 'star', p: { n: 5, inr: 0.45 }, size: 330, color: WHITE, start: 0.08, end: 0.34, scale: K([0, 1.2], [0.26, 1]), op: K([0, 0], [0.05, 0.85], [0.26, 0]) }),
    SP('Star', { shape: 'tex:star', size: 360, scale: K([0, 0], [0.12, 1.25], [0.2, 0.93], [0.27, 1.03], [0.33, 1]), op: 1 }),
    SP('Glow', { blend: 'add', shape: 'soft', size: 460, color: GOLD, scale: K([0, 0.3], [0.15, 1.1], [0.45, 0.9]), op: K([0, 0], [0.12, 0.9], [0.45, 0.25]) })
]);

// =====================================================================================
// 3. ЭКРАН РЕМОНТА
// =====================================================================================
fx('vfx_thread_glow', { size: [512, 64], frames: 12, loop: 1.2 }, ({ P }) => {
    const pulse = K([0, 0.6], [P / 2, 1], [P, 0.6]);
    const bead = (name, sprite, size, grad, glow) => loop(EM(name, {
        blend: 'add', em: { x: -262, y: 0, shape: 'point', dir: 'dir', angle: 0, spread: 0, speed: 524 / P, speedRnd: 0, bursts: [{ t: 0, n: 1 }, { t: P / 2, n: 1 }] },
        pt: { sprite, size, sizeRnd: 0, life: P, lifeRnd: 0, grad, opacityOL: ONE, glow }
    }), P, P);
    return [
        bead('Bead core', 'dot', 9, G1(WHITE), 0), bead('Bead glow', 'soft', 30, G1(LGOLD), 0.6),
        SP('Thread', Object.assign({ color: LGOLD, op: pulse, scale: 1, glowL: 1.2, glowLR: 8 }, bar(512, 3))),
        SP('Thread glow', { blend: 'add', shape: 'soft', size: 26, aspect: 22, color: GOLD, op: K([0, 0.35], [P / 2, 0.7], [P, 0.35]), scale: 1 })
    ];
});

fx('vfx_marker_ping', { size: [256, 256], frames: 10, loop: 1.0 }, ({ P }) => [
    [0, 1].map(i => SP('Ping ring ' + (i + 1), { blend: 'add', shape: 'ring', p: { th: 0.07 }, size: 230, color: GOLD, glow: 0.3, start: i * P / 2, end: (i + 1) * P / 2, scale: K([0, 0.12, 'lin'], [P / 2, 1.05]), op: K([0, 0.95], [P / 2, 0]) })),
    SP('Dot core', { shape: 'dot', size: 24, color: CREAM, scale: K([0, 1], [P / 4, 1.2], [P / 2, 1], [3 * P / 4, 1.2], [P, 1]), op: 1 }),
    SP('Dot glow', { blend: 'add', shape: 'soft', size: 70, color: GOLD, scale: 1, op: K([0, 0.6], [P / 4, 1], [P / 2, 0.6], [3 * P / 4, 1], [P, 0.6]) })
]);

fx('vfx_build_in', { size: [640, 640], frames: 14, dur: 1.0 }, () => {
    const T = 0.2;
    return [
        sparkBurst({ t: T, n: 6, r: 30, speed: 480, size: 44 }),
        EM('Wood shavings', {
            start: T, end: T + 0.05, em: { x: 0, y: 120, shape: 'box', sx: 30, sy: 4, dir: 'dir', angle: -90, spread: 70, speed: 560, speedRnd: 0.2, bursts: [{ t: 0, n: 2 }] },
            pt: { sprite: 'ring', p: { th: 0.35 }, size: 30, sizeRnd: 0.2, life: 0.75, lifeRnd: 0.1, gravY: 1300, spin: 600, spinRnd: 0.3, rotRnd: 1, grad: G1('#C98B4F'), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        }),
        [180, 0].map((a, i) => EM('Dust roll ' + (i + 1), {
            start: T, end: T + 0.05, em: { x: 0, y: 150, shape: 'box', sx: 30, sy: 6, dir: 'dir', angle: a, spread: 14, speed: 400, speedRnd: 0.45, bursts: [{ t: 0, n: 5 }] },
            pt: { sprite: 'tex:dust', size: 86, sizeRnd: 0.3, life: 0.7, lifeRnd: 0.15, drag: 3.5, gravY: -40, spin: 30, spinRnd: 1, rotRnd: 0.1, sizeOL: C([0, 0.4], [0.3, 1], [1, 0.85]), opacityOL: C([0, 1], [0.6, 1], [1, 0]) }
        })),
        SP('Snap ring', { blend: 'add', shape: 'ring', p: { th: 0.1 }, size: 240, color: GOLD, glow: 0.5, end: T + 0.2, scale: K([0, 1.9, 'ease'], [T, 0.55, 'ease'], [T + 0.2, 0.75]), op: K([0, 0], [0.08, 1], [T, 1], [T + 0.2, 0]) }),
        flash({ t: T, d: 0.4, color: CREAM, size: 320, s1: 1.5 })
    ];
});

fx('vfx_coins_spend', { size: [512, 512], frames: 12, dur: 1.0 }, () => {
    const pts = [[210, -190], [120, -290], [20, -120], [0, 170]];
    const D = 0.45;
    const coins = [], trails = [];
    for (let i = 0; i < 5; i++) {
        const t0 = i * 0.1;
        const pth = bezPath(pts, 0, D, 12, u => easeIn(u, 1.5));
        coins.push(SP('Coin ' + (i + 1), { shape: 'tex:coin', size: 70, start: t0, end: t0 + D, x: pth.x, y: pth.y, rot: K([0, 0], [D, -200]), scale: K([0, 0.8], [0.08, 1], [D, 0.75]), op: K([0, 0], [0.04, 1], [D - 0.04, 1], [D, 0]) }));
        trails.push(EM('Coin trail ' + (i + 1), {
            blend: 'add', start: t0, end: t0 + D, em: { x: pth.x, y: pth.y, shape: 'circle', sx: 14, sy: 14, speed: 20, rate: 90 },
            pt: { sprite: 'spark', size: 26, sizeRnd: 0.3, life: 0.26, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: C([0, 1], [1, 0.2]), opacityOL: C([0, 1], [1, 0]) }
        }));
    }
    return [
        coins,
        EM('Arrival pops', {
            blend: 'add', end: 0.95, em: { x: 0, y: 170, shape: 'point', dir: 'dir', angle: -90, spread: 140, speed: 240, speedRnd: 0.4, bursts: [0, 1, 2, 3, 4].map(i => ({ t: r3(D + i * 0.1), n: 4 })) },
            pt: { sprite: 'spark', size: 30, sizeRnd: 0.3, life: 0.3, lifeRnd: 0.2, drag: 3, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE }
        }),
        trails
    ];
});

fx('vfx_tile_bought', { size: [384, 384], frames: 10, dur: 0.7 }, () => [
    [[-125, 125, 0.2], [125, -125, 0.36], [125, 125, 0.46]].map(([x, y, t], i) => SP('Corner star ' + (i + 1), { shape: 'tex:sparkle', size: 64, x, y, start: t, end: t + 0.24, scale: K([0, 0], [0.07, 1.2], [0.24, 0]), rot: K([0, 0], [0.24, 90]), op: 1 })),
    SP('Gloss sweep', { blend: 'add', shape: 'soft', size: 380, aspect: 0.2, rot: 45, color: WHITE, fade: [0, 0, 175, 0.35], end: 0.55, x: K([0, -230, 'lin'], [0.5, 230]), y: K([0, 230, 'lin'], [0.5, -230]), scale: 1, op: 1 }),
    SP('Gold sweep', { blend: 'add', shape: 'soft', size: 380, aspect: 0.5, rot: 45, color: GOLD, fade: [0, 0, 175, 0.35], end: 0.6, x: K([0, -260, 'lin'], [0.55, 220]), y: K([0, 260, 'lin'], [0.55, -220]), scale: 1, op: 0.55 })
]);

fx('vfx_stage_complete', { size: [1024, 1024], frames: 16, dur: 1.8 }, () => [
    confettiFall({ t0: 0.05, t1: 1.3, y: -300, w: 290, rate: 11, life: 1.7, colors: [GOLD, RED, GREEN, CREAM] }),
    firework({ i: 1, t: 0.05, x: -140, y: -150, c: LGOLD }),
    firework({ i: 2, t: 0.3, x: 150, y: -110, c: '#FFB08A', c2: RED }),
    firework({ i: 3, t: 0.55, x: 20, y: -200, c: '#B8F0A0', c2: GREEN }),
    firework({ i: 4, t: 0.8, x: -170, y: -60, c: LGOLD, n: 26 }),
    firework({ i: 5, t: 1.0, x: 180, y: -200, c: LGOLD, n: 26 }),
    SP('Light bloom', { blend: 'add', shape: 'soft', size: 640, aspect: 1.6, color: LGOLD, y: K([0, 330], [1.4, 150]), scale: K([0, 0.6], [0.7, 1.1]), op: K([0, 0], [0.4, 0.45], [1.0, 0.38], [1.7, 0]) })
]);

fx('vfx_paint_splash', { size: [384, 384], frames: 10, dur: 0.7 }, () => {
    const sx = K([0, -150, 'ease'], [0.25, 150, 'ease']);
    const sy = K([0, 10, 'ease'], [0.25, -10, 'ease']);
    const stroke = (name, size, color, yOff) => EM(name, {
        end: 0.25, em: { x: sx, y: yOff ? K([0, 10 + yOff], [0.25, -10 + yOff]) : sy, shape: 'point', speed: 0, speedRnd: 0, rate: 420 },
        pt: { sprite: 'dot', size, sizeRnd: 0, life: 0.5, lifeRnd: 0, grad: G1(color), opacityOL: C([0, 1], [0.75, 1], [1, 0]), sizeOL: C([0, 1], [0.8, 1], [1, 0.7]) }
    });
    return [
        EM('Droplets end', {
            start: 0.22, end: 0.27, em: { x: 150, y: -10, shape: 'point', dir: 'dir', angle: -20, spread: 70, speed: 420, speedRnd: 0.4, bursts: [{ t: 0, n: 7 }] },
            pt: { sprite: 'dot', size: 18, sizeRnd: 0.4, life: 0.45, lifeRnd: 0.2, gravY: 900, grad: G([0, CREAM], [1, LGOLD]), sizeOL: C([0, 1], [1, 0.5]), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        }),
        EM('Droplets along', {
            end: 0.25, em: { x: sx, y: sy, shape: 'point', dir: 'dir', angle: -90, spread: 100, speed: 260, speedRnd: 0.4, rate: 26 },
            pt: { sprite: 'dot', size: 14, sizeRnd: 0.4, life: 0.4, lifeRnd: 0.2, gravY: 900, grad: G1(LGOLD), sizeOL: C([0, 1], [1, 0.5]), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        }),
        stroke('Stroke highlight', 16, WHITE, -14),
        stroke('Stroke yellow', 40, LGOLD, 8),
        stroke('Stroke', 60, CREAM, 0),
        stroke('Stroke outline', 70, BROWN, 0)
    ];
});

// =====================================================================================
// 4. КНОПКИ, ОКНА, ПЛАШКИ
// =====================================================================================
fx('vfx_button_press', { size: [256, 256], frames: 8, dur: 0.4 }, () => [
    [45, 135, 225, 315].map((a, i) => {
        const rad = a * Math.PI / 180;
        return SP('Dot ' + (i + 1), { shape: 'dot', size: 16, color: WHITE, end: 0.34, x: K([0, Math.cos(rad) * 50], [0.3, Math.cos(rad) * 140]), y: K([0, Math.sin(rad) * 50], [0.3, Math.sin(rad) * 140]), scale: K([0, 0.4], [0.08, 1], [0.32, 0.3]), op: K([0, 1], [0.22, 1], [0.32, 0]) });
    }),
    SP('Ring', { blend: 'add', shape: 'ring', p: { th: 0.1 }, size: 220, color: WHITE, end: 0.36, scale: K([0, 0.2, 'lin'], [0.32, 1.1]), op: K([0, 1], [0.1, 1], [0.34, 0]) })
]);

fx('vfx_popup_appear', { size: [640, 640], frames: 8, dur: 0.45 }, () => {
    const hw = 220, hh = 150;
    // две головы из противоположных углов — каждая проходит половину периметра
    const head = (u0, i) => {
        const at = t => rectPoint(hw, hh, u0 + easeOut(t / 0.36, 1.5) * 0.5);
        const px = sampleK(t => at(t)[0], 0, 0.36, 60), py = sampleK(t => at(t)[1], 0, 0.36, 60);
        return [
            EM('Rim sweep ' + i, {
                blend: 'add', end: 0.36, em: { x: px, y: py, shape: 'point', speed: 0, speedRnd: 0, rate: 1600 },
                pt: { sprite: 'soft', size: 34, sizeRnd: 0, life: 0.2, lifeRnd: 0, grad: G1(WHITE), sizeOL: C([0, 1], [1, 0.35]), opacityOL: C([0, 0.5], [1, 0]) }
            }),
            SP('Rim head ' + i, { blend: 'add', shape: 'soft', size: 60, color: WHITE, end: 0.36, x: px, y: py, scale: 1, op: K([0, 0], [0.05, 1], [0.3, 1], [0.36, 0]) })
        ];
    };
    return [
        head(0.9, 1), head(0.4, 2),
        SP('Bloom', { blend: 'add', shape: 'soft', size: 560, aspect: 1.45, color: WHITE, scale: K([0, 0.55], [0.25, 1.15]), op: K([0, 0], [0.1, 0.8], [0.45, 0]) })
    ];
});

fx('vfx_toast_glint', { size: [512, 128], frames: 8, dur: 0.45 }, () => [
    SP('Glint thin', { blend: 'add', shape: 'soft', size: 140, aspect: 0.08, rot: 20, color: WHITE, x: K([0, -330, 'lin'], [0.42, 300, 'lin']), scale: 1, op: 1 }),
    SP('Glint', { blend: 'add', shape: 'soft', size: 150, aspect: 0.2, rot: 20, color: WHITE, x: K([0, -300, 'lin'], [0.42, 330, 'lin']), scale: 1, op: 0.85 })
]);

fx('vfx_counter_tick', { size: [192, 192], frames: 8, dur: 0.35 }, () => [
    SP('Sparkle', { shape: 'tex:sparkle', size: 300, scale: K([0, 0], [0.08, 1.15], [0.3, 0]), rot: K([0, 0], [0.3, 45]), op: 1 }),
    SP('Ring', { blend: 'add', shape: 'ring', p: { th: 0.12 }, size: 420, color: GOLD, scale: K([0, 0.2, 'lin'], [0.3, 1]), op: K([0, 1], [0.32, 0]) })
]);

fx('vfx_locked_shake', { size: [256, 256], frames: 8, dur: 0.45 }, () => {
    const g = [{ t: 0, r: 0, y: 5, s: 1.6 }, { t: 0.05, r: -14, y: 5, s: 1.6 }, { t: 0.11, r: 12, y: 5, s: 1.6 }, { t: 0.17, r: -9, y: 5, s: 1.6 }, { t: 0.23, r: 6, y: 5, s: 1.6 }, { t: 0.29, r: -3, y: 5, s: 1.6 }, { t: 0.34, r: 0, y: 5, s: 1.6 }, { t: 0.45, r: 0, y: 5, s: 1.6 }];
    const GREY = '#A7ADB6', DARK = '#4A4F5A';
    const arc = (side, t) => SP('Motion arc ' + (side < 0 ? 'L' : 'R'), {
        shape: 'ring', p: { th: 0.06 }, size: 330, color: '#9AA0A8', y: 30, fade: [160 * side, 30, 90, 0.6], scale: 1,
        op: K([0, 0], [t, 0], [t + 0.03, 1], [t + 0.12, 0], [t + 0.15, 0], [t + 0.18, 0.8], [t + 0.26, 0])
    });
    return [
        rigid([
            { name: 'Keyhole', shape: 'dot', size: 20, color: DARK, y: 14 },
            Object.assign({ name: 'Keyhole slot', color: DARK, y: 26 }, bar(8, 18)),
            Object.assign({ name: 'Body shine', color: '#C8CDD4', y: -8 }, bar(70, 8)),
            Object.assign({ name: 'Body', color: GREY, y: 20 }, bar(88, 76)),
            Object.assign({ name: 'Body outline', color: BROWN, y: 20 }, bar(100, 88)),
            { name: 'Shackle', shape: 'ring', p: { th: 0.3 }, size: 84, color: GREY, y: -28 },
            { name: 'Shackle outline', shape: 'ring', p: { th: 0.46 }, size: 98, color: BROWN, y: -28 }
        ], g, { op: 1 }),
        arc(-1, 0.02), arc(1, 0.08)
    ];
});

fx('vfx_tooltip_pop', { size: [384, 128], frames: 6, dur: 0.35 }, () => [
    SP('Glow', { blend: 'add', shape: 'soft', size: 170, aspect: 3.1, color: CREAM, scale: K([0, 0.6], [0.15, 1.05]), op: K([0, 0], [0.08, 0.9], [0.35, 0]) })
]);

// =====================================================================================
// 5. НАГРАДЫ, НАКЛЕЙКИ, СУНДУКИ
// =====================================================================================
fx('vfx_chest_open', { size: [768, 768], frames: 16, dur: 1.6 }, () => [
    sparkBurst({ x: 0, y: 130, n: 14, speed: 640, size: 42, life: 1.3, drag: 1, gravY: 500, em: { dir: 'dir', angle: -90, spread: 60, shape: 'point' } }),
    EM('Coins', {
        start: 0.05, end: 0.1, em: { x: 0, y: 150, shape: 'box', sx: 20, sy: 4, dir: 'dir', angle: -90, spread: 45, speed: 760, speedRnd: 0.3, bursts: [{ t: 0, n: 10 }] },
        pt: { sprite: 'tex:coin', size: 40, sizeRnd: 0.25, life: 1.25, lifeRnd: 0.1, gravY: 1300, spin: 220, spinRnd: 1, rotRnd: 0.3, sizeOL: C([0, 0.5], [0.1, 1], [1, 1]), opacityOL: C([0, 1], [0.85, 1], [1, 0]) }
    }),
    SP('Rays tex', { shape: 'tex:rays', size: 460, y: K([0, 110], [0.4, 0]), scale: K([0, 0.2], [0.25, 1.15], [1.2, 1.3]), op: K([0, 0], [0.1, 1], [0.9, 0.8], [1.4, 0]), end: 1.5 }),
    SP('Light cone', { blend: 'add', shape: 'soft', size: 560, aspect: 0.5, color: LGOLD, y: K([0, 170], [0.6, -30]), scale: K([0, 0.3], [0.3, 1.1]), op: K([0, 0], [0.1, 1], [0.9, 0.8], [1.5, 0]), end: 1.6 }),
    SP('Rotating rays', { blend: 'add', shape: 'star', p: { n: 12, inr: 0.16 }, size: 600, color: CREAM, y: -10, rot: K([0, 0, 'lin'], [1.6, 50, 'lin']), scale: K([0, 0.4], [0.4, 1.1]), op: K([0, 0], [0.2, 0.4], [1.1, 0.3], [1.6, 0]) }),
    flash({ t: 0, d: 0.45, y: 150, color: CREAM, size: 340 })
]);

fx('vfx_pack_tear', { size: [640, 640], frames: 14, dur: 1.0 }, () => {
    const D = 0.3;
    const tx = K([0, -250, 'lin'], [D, 250, 'lin']);
    const ty = K(...Array.from({ length: 11 }, (_, i) => [i * D / 10, (i % 2 ? 14 : -14) * (i ? 1 : 0), 'lin']));
    const seam = (name, sprite, size, grad, glow) => EM(name, {
        blend: 'add', end: D, em: { x: tx, y: ty, shape: 'point', speed: 0, speedRnd: 0, rate: 700 },
        pt: { sprite, size, sizeRnd: 0, life: 0.55, lifeRnd: 0.1, grad, opacityOL: C([0, 1], [0.6, 0.9], [1, 0]), glow }
    });
    return [
        [GOLD, RED, SKY].map((c, i) => EM('Flecks ' + (i + 1), {
            start: 0.15, end: 0.4, em: { x: 0, y: 0, shape: 'box', sx: 220, sy: 10, dir: 'dir', angle: -90, spread: 140, speed: 360, speedRnd: 0.5, rate: 36 },
            pt: { sprite: 'tex:' + (i === 1 ? 'conf_a' : 'conf_c'), tintTex: true, grad: G1(c), size: 18, sizeRnd: 0.4, life: 0.7, lifeRnd: 0.2, gravY: 600, spin: 400, spinRnd: 1, rotRnd: 1, opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        })),
        seam('Seam core', 'dot', 8, G1(WHITE), 0),
        seam('Seam glow', 'soft', 26, G([0, WHITE], [1, LGOLD]), 0.6),
        SP('Light spill', { blend: 'add', shape: 'soft', size: 200, aspect: 3.2, color: LGOLD, start: 0.15, end: 0.95, scale: K([0, 0.3], [0.3, 1.2]), op: K([0, 0], [0.1, 0.9], [0.4, 0.7], [0.8, 0]) }),
        SP('Light spill core', { blend: 'add', shape: 'soft', size: 90, aspect: 5.5, color: WHITE, start: 0.2, end: 0.8, scale: K([0, 0.4], [0.25, 1.05]), op: K([0, 0], [0.08, 0.8], [0.6, 0]) })
    ];
});

fx('vfx_sticker_gold', { size: [512, 512], frames: 14, dur: 1.2 }, () => {
    const rainbow = ['#FF6B6B', '#FFB347', '#FFE66A', '#7ED957', '#6EC6FF'];
    const bands = rainbow.map((c, i) => {
        const off = (i - 2) * 16;
        return SP('Rainbow band ' + (i + 1), {
            blend: 'add', shape: 'soft', size: 420, aspect: 0.08, rot: 45, color: c, fade: [0, 0, 210, 0.5], start: 0.3, end: 0.9,
            x: K([0, -260 + off, 'lin'], [0.55, 260 + off]), y: K([0, 260 + off, 'lin'], [0.55, -260 + off]), scale: 1, op: 0.85
        });
    });
    const orbiters = Array.from({ length: 8 }, (_, i) => {
        const o = orbit({ r0: 40, r1: 220, a0: i * 45, turns: 0.35, t0: 0, t1: 0.9, n: 16, ease: u => easeOut(u, 1.6) });
        return SP('Orbit sparkle ' + (i + 1), { shape: 'tex:sparkle', size: 44, start: 0.08, end: 0.98, x: o.x, y: o.y, rot: K([0, 0], [0.9, 180]), scale: K([0, 0], [0.1, 1.1], [0.6, 0.9], [0.9, 0]), op: 1 });
    });
    return [
        orbiters, bands,
        SP('Rays gold', { blend: 'add', shape: 'star', p: { n: 12, inr: 0.12 }, size: 600, color: GOLD, rot: K([0, 0, 'lin'], [1.2, 60, 'lin']), scale: K([0, 0.3], [0.3, 1]), op: K([0, 0], [0.15, 0.8], [0.8, 0.6], [1.2, 0]) }),
        SP('Rays cream', { blend: 'add', shape: 'star', p: { n: 8, inr: 0.2 }, size: 440, color: CREAM, rot: K([0, 10, 'lin'], [1.2, -30, 'lin']), scale: K([0, 0.3], [0.3, 1]), op: K([0, 0], [0.15, 0.6], [0.8, 0.45], [1.2, 0]) }),
        flash({ t: 0, d: 0.5, color: LGOLD, size: 320, s1: 1.4 })
    ];
});

fx('vfx_sticker_place', { size: [384, 384], frames: 10, dur: 0.6 }, () => {
    const nodes = roundRect(115, 140, 22, 3);
    const rim = (name, sprite, size, grad, blend, op) => EM(name, {
        blend, end: 0.05, em: { shape: 'point', speed: 0, speedRnd: 0, bursts: [{ t: 0, n: 150 }], path: { on: true, mode: 'emit', closed: true, smooth: false, along: 'even', nodes } },
        pt: { sprite, size, sizeRnd: 0, life: 0.4, lifeRnd: 0, grad, opacityOL: C([0, op], [0.3, op], [1, 0]), sizeOL: C([0, 1.4], [0.2, 1], [1, 0.8]) }
    });
    return [
        [[-115, -140, 0.08], [115, -140, 0.16], [115, 140, 0.24]].map(([x, y, t], i) => SP('Corner sparkle ' + (i + 1), { shape: 'tex:sparkle', size: 54, x, y, start: t, end: t + 0.26, scale: K([0, 0], [0.07, 1.2], [0.26, 0]), rot: K([0, 0], [0.26, 90]), op: 1 })),
        rim('Outline flash', 'dot', 9, G1(WHITE), 'normal', 1),
        rim('Outline glow', 'soft', 28, G1(WHITE), 'add', 0.4),
        [[90, 0, 150], [180, -125, 60], [0, 125, 60]].map(([a, x, y], i) => EM('Paper dust ' + (i + 1), {
            end: 0.05, em: { x, y, shape: 'box', sx: i ? 6 : 100, sy: i ? 70 : 4, dir: 'dir', angle: a, spread: 60, speed: 210, speedRnd: 0.4, bursts: [{ t: 0.02, n: i ? 3 : 7 }] },
            pt: { sprite: 'blob', size: 34, sizeRnd: 0.4, life: 0.45, lifeRnd: 0.2, drag: 4, spin: 40, spinRnd: 1, rotRnd: 1, grad: G([0, WHITE], [1, CREAM]), sizeOL: C([0, 0.4], [0.3, 1], [1, 0.8]), opacityOL: C([0, 0.95], [0.5, 0.7], [1, 0]) }
        }))
    ];
});

fx('vfx_gift_pop', { size: [512, 512], frames: 12, dur: 1.0 }, () => {
    const T = 0.12;
    const part = (name, side, o) => {
        const x = K([0, o.x], [T, o.x], [T + 0.5, o.x + 220 * side]);
        const y = K([0, o.y], [T, o.y], [T + 0.15, o.y - 60], [T + 0.5, o.y + 90]);
        const rot = K([0, o.rot], [T, o.rot], [T + 0.5, o.rot + 200 * side]);
        const sc = K([0, 0.8], [0.06, 1.05], [T, 1]);
        const op = K([0, 1], [T + 0.35, 1], [T + 0.5, 0]);
        return SP(name, Object.assign({}, o, { end: T + 0.55, x, y, rot, scale: sc, op }));
    };
    return [
        [GOLD, RED, GREEN, SKY].map((c, i) => EM('Confetti dots ' + (i + 1), {
            start: T, end: T + 0.05, em: { shape: 'circle', sx: 20, sy: 20, dir: 'out', speed: 520, speedRnd: 0.5, bursts: [{ t: 0, n: 10 }] },
            pt: { sprite: 'dot', size: 16, sizeRnd: 0.4, life: 0.85, lifeRnd: 0.2, drag: 2, gravY: 320, grad: G1(c), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        })),
        part('Knot', 0, { shape: 'dot', size: 36, color: RED, x: 0, y: 0, rot: 0 }),
        part('Knot outline', 0, { shape: 'dot', size: 48, color: BROWN, x: 0, y: 0, rot: 0 }),
        part('Loop L', -1, { shape: 'ring', p: { th: 0.4 }, aspect: 1.35, size: 80, color: RED, x: -44, y: 0, rot: 18 }),
        part('Loop R', 1, { shape: 'ring', p: { th: 0.4 }, aspect: 1.35, size: 80, color: RED, x: 44, y: 0, rot: -18 }),
        part('Loop L outline', -1, { shape: 'ring', p: { th: 0.56 }, aspect: 1.3, size: 96, color: BROWN, x: -44, y: 0, rot: 18 }),
        part('Loop R outline', 1, { shape: 'ring', p: { th: 0.56 }, aspect: 1.3, size: 96, color: BROWN, x: 44, y: 0, rot: -18 }),
        part('Tail L', -1, Object.assign({ color: RED, x: -18, y: 40, rot: 70 }, bar(54, 18))),
        part('Tail R', 1, Object.assign({ color: RED, x: 18, y: 40, rot: -70 }, bar(54, 18))),
        part('Tail L outline', -1, Object.assign({ color: BROWN, x: -18, y: 40, rot: 70 }, bar(66, 30))),
        part('Tail R outline', 1, Object.assign({ color: BROWN, x: 18, y: 40, rot: -70 }, bar(66, 30))),
        flash({ t: T, d: 0.5, color: LGOLD, size: 340, s1: 1.6 })
    ];
});

// =====================================================================================
// 6. ЕЖЕДНЕВКИ, «ЧАС ПИК», ЧАЕВЫЕ
// =====================================================================================
fx('vfx_rush_alert', { size: [1024, 256], frames: 12, dur: 0.8 }, () => {
    const bx = K([0, -380, 'ease'], [0.5, 380, 'ease']);
    return [
        [[-140, -30, 0.2], [150, 35, 0.34]].map(([x, y, t], i) => SP('Warning sparkle ' + (i + 1), { blend: 'add', shape: 'spark', size: 90, color: '#FFE0B0', x, y, start: t, end: t + 0.3, rot: K([0, 0], [0.3, 60]), scale: K([0, 0], [0.08, 1.1], [0.3, 0]), op: 1 })),
        EM('Speed streaks', {
            end: 0.5, em: { x: bx, y: 0, shape: 'box', sx: 30, sy: 60, dir: 'dir', angle: 180, spread: 0, speed: 90, speedRnd: 0.5, rate: 70 },
            pt: { sprite: 'streak', size: 110, sizeRnd: 0.5, life: 0.35, lifeRnd: 0.3, grad: G([0, ORANGE], [1, RED]), opacityOL: C([0, 0.9], [1, 0]) }
        }),
        SP('Band', Object.assign({ color: RED, rot: 20, x: bx, end: 0.8, scale: 1, op: K([0, 1], [0.5, 1], [0.75, 0]) }, bar(70, 200))),
        SP('Band orange', Object.assign({ color: ORANGE, rot: 20, x: K([0, -410, 'ease'], [0.5, 350, 'ease']), end: 0.8, scale: 1, op: K([0, 1], [0.5, 1], [0.75, 0]) }, bar(40, 200))),
        SP('Band glow', { blend: 'add', shape: 'soft', size: 260, aspect: 0.5, rot: 20, color: ORANGE, x: bx, end: 0.8, scale: 1, op: K([0, 0.7], [0.5, 0.7], [0.75, 0]) })
    ];
});

fx('vfx_rush_clock', { size: [256, 256], frames: 12, loop: 1.0 }, ({ P }) => {
    const o = orbit({ r0: 92, a0: -90, turns: 1, t0: 0, t1: P, n: 48 });
    const pulse = K([0, 0.6], [P * 0.8, 1], [P, 0.6]);
    return [
        SP('Tip spark', { blend: 'add', shape: 'spark', size: 64, color: '#FFD0C8', x: o.x, y: o.y, rot: sampleK(t => 720 * t / P, 0, P, 4), scale: 1, op: pulse }),
        SP('Tip glow', { blend: 'add', shape: 'soft', size: 50, color: RED, x: o.x, y: o.y, scale: 1, op: 1 }),
        loop(EM('Sweep trail', {
            blend: 'add', em: { x: o.x, y: o.y, shape: 'point', speed: 0, speedRnd: 0, rate: 180 },
            pt: { sprite: 'soft', size: 22, sizeRnd: 0, life: 0.6, lifeRnd: 0, grad: G([0, '#FF9A8E'], [1, RED]), sizeOL: C([0, 1], [1, 0.4]), opacityOL: C([0, 0.95], [1, 0]) }
        }), P, 0.6),
        SP('Base ring', { blend: 'add', shape: 'ring', p: { th: 0.06 }, size: 196, color: RED, glowL: 0.8, glowLR: 10, scale: 1, op: K([0, 0.25], [P * 0.8, 0.55], [P, 0.25]) })
    ];
});

fx('vfx_truck_puff', { size: [384, 384], frames: 10, dur: 0.9 }, () => [
    EM('Exhaust clouds', {
        end: 0.3, em: { x: 90, y: 110, shape: 'point', dir: 'dir', angle: -150, spread: 20, speed: 260, speedRnd: 0.2, bursts: [{ t: 0, n: 1 }, { t: 0.12, n: 1 }, { t: 0.24, n: 1 }] },
        pt: { sprite: 'tex:dust', size: 150, sizeRnd: 0.15, life: 0.62, lifeRnd: 0.05, drag: 1.3, gravY: -70, spin: 30, spinRnd: 1, rotRnd: 0.1, sizeOL: C([0, 0.2], [0.2, 1], [1, 1.35]), opacityOL: C([0, 1], [0.5, 0.85], [1, 0]) }
    }),
    [180, 0].map((a, i) => EM('Ground dust ' + (i + 1), {
        end: 0.05, em: { x: 40, y: 150, shape: 'box', sx: 20, sy: 4, dir: 'dir', angle: a, spread: 20, speed: 160, speedRnd: 0.4, bursts: [{ t: 0.05, n: 3 }] },
        pt: { sprite: 'blob', size: 60, sizeRnd: 0.3, life: 0.5, lifeRnd: 0.2, drag: 3, gravY: -30, spin: 50, spinRnd: 1, rotRnd: 1, grad: G([0, '#EDE8E1'], [1, '#BDB6AC']), sizeOL: C([0, 0.4], [0.3, 1], [1, 0.8]), opacityOL: C([0, 0.9], [0.5, 0.7], [1, 0]) }
    }))
]);

fx('vfx_tips_rain', { size: [768, 768], frames: 14, dur: 1.4 }, () => {
    const out = [];
    const xs = [-190, -110, -40, 30, 100, 170, -150, 60, 140, -70];
    const starts = [0.28, 0.05, 0.4, 0.14, 0.33, 0.0, 0.21, 0.47, 0.09, 0.55];
    xs.forEach((x, i) => {
        const t0 = starts[i], tHit = 0.45 + rnd() * 0.1, yHit = 170 + rnd() * 30;
        const y = K([0, -300, 'lin'], [tHit, yHit, 'ease'], [tHit + 0.12, yHit - 45, 'ease'], [tHit + 0.24, yHit, 'ease']);
        out.push(SP('Coin ' + (i + 1), { shape: 'tex:coin', size: 64, x, y, start: t0, end: t0 + tHit + 0.34, rot: K([0, rnd() * 60 - 30], [tHit + 0.3, rnd() * 120 - 60]), scale: 1, op: K([0, 1], [tHit + 0.26, 1], [tHit + 0.34, 0]) }));
        out.push(SP('Twinkle ' + (i + 1), { blend: 'add', shape: 'spark', size: 60, color: LGOLD, x: x + 18, y: yHit - 18, start: t0 + tHit, end: t0 + tHit + 0.22, rot: K([0, 0], [0.22, 45]), scale: K([0, 0], [0.06, 1], [0.22, 0]), op: 1 }));
    });
    return out;
});

fx('vfx_streak_flame', { size: [256, 256], frames: 12, loop: 1.0 }, ({ P }) => {
    const frames = [];
    for (let i = 0; i < 12; i++) {
        const ph = 2 * Math.PI * i / 12;
        const h = 1 + 0.09 * Math.sin(2 * ph) + 0.04 * Math.sin(3 * ph + 1);
        const w = 1 - 0.5 * (h - 1);
        frames.push(SP('Flame f' + i, { shape: 'tex:fire', size: 180, start: i * P / 12, end: (i + 1) * P / 12, aspect: w / h, scale: h, y: 10 + (1 - h) * 90, rot: 3 * Math.sin(ph + 0.5), op: 1 }));
    }
    return [
        loop(EM('Embers', {
            blend: 'add', em: { x: 0, y: -10, shape: 'box', sx: 30, sy: 6, dir: 'dir', angle: -90, spread: 30, speed: 120, speedRnd: 0.3, rate: 5 },
            pt: { sprite: 'ember', size: 14, sizeRnd: 0.3, life: 0.8, lifeRnd: 0, turbAmp: 70, turbFreq: 1.5, grad: G([0, LGOLD], [1, ORANGE]), opacityOL: C([0, 0], [0.2, 1], [1, 0]) }
        }), P, 0.8),
        frames,
        SP('Glow', { blend: 'add', shape: 'soft', size: 250, color: ORANGE, y: 10, scale: K([0, 0.95], [P / 2, 1.05], [P, 0.95]), op: K([0, 0.35], [P / 2, 0.6], [P, 0.35]) })
    ];
});

// =====================================================================================
// 7. АТМОСФЕРА
// =====================================================================================
fx('vfx_amb_dust', { size: [1024, 1024], frames: 16, loop: 4.0 }, ({ P }) => [
    loop(EM('Dust motes', {
        blend: 'add', em: { shape: 'box', sx: 270, sy: 270, dir: 'dir', angle: -90, spread: 90, speed: 26, speedRnd: 0.6, rate: 3 },
        pt: { sprite: 'soft', size: 14, sizeRnd: 0.5, life: 4, lifeRnd: 0, turbAmp: 18, turbFreq: 0.6, grad: G([0, CREAM], [1, LGOLD]), opacityOL: C([0, 0], [0.2, 0.45], [0.8, 0.45], [1, 0]), glow: 0.3 }
    }), P, 4)
]);

fx('vfx_amb_leaves', { size: [1024, 1024], frames: 16, loop: 4.0 }, ({ P }) => {
    const leaf = (name, color, rate, seed, size) => [
        loop(EM(name, {
            em: { x: -140, y: -300, shape: 'box', sx: 330, sy: 10, dir: 'dir', angle: 62, spread: 16, speed: 135, speedRnd: 0.25, rate, seed },
            pt: { sprite: 'flame', p: { var: 1, taper: 0.9 }, size, sizeRnd: 0.2, life: 5, lifeRnd: 0, turbAmp: 60, turbFreq: 0.8, spin: 110, spinRnd: 1, rotRnd: 1, grad: G1(color), opacityOL: C([0, 0], [0.05, 1], [0.95, 1], [1, 0]) }
        }), P, 5),
        loop(EM(name + ' outline', {
            em: { x: -140, y: -300, shape: 'box', sx: 330, sy: 10, dir: 'dir', angle: 62, spread: 16, speed: 135, speedRnd: 0.25, rate, seed },
            pt: { sprite: 'flame', p: { var: 1, taper: 0.9 }, size: size * 1.22, sizeRnd: 0.2, life: 5, lifeRnd: 0, turbAmp: 60, turbFreq: 0.8, spin: 110, spinRnd: 1, rotRnd: 1, grad: G1(BROWN), opacityOL: C([0, 0], [0.05, 1], [0.95, 1], [1, 0]) }
        }), P, 5)
    ];
    return [leaf('Leaves green', '#7CB342', 0.75, 11, 60), leaf('Leaves yellow', '#F2C94C', 0.5, 23, 54)];
});

fx('vfx_amb_fireflies', { size: [1024, 1024], frames: 16, loop: 4.0 }, ({ P }) => [
    loop(EM('Fireflies', {
        blend: 'add', em: { shape: 'box', sx: 230, sy: 230, dir: 'omni', speed: 22, speedRnd: 0.5, rate: 2, seed: 77 },
        pt: { sprite: 'soft', size: 26, sizeRnd: 0.3, life: 4, lifeRnd: 0, turbAmp: 45, turbFreq: 0.6, grad: G1('#FFE66A'), glow: 0.9, opacityOL: C([0, 0], [0.1, 1], [0.2, 0.3], [0.35, 1], [0.5, 0.35], [0.65, 1], [0.8, 0.3], [0.9, 0.9], [1, 0]) }
    }), P, 4),
    loop(EM('Firefly cores', {
        blend: 'add', em: { shape: 'box', sx: 230, sy: 230, dir: 'omni', speed: 22, speedRnd: 0.5, rate: 2, seed: 77 },
        pt: { sprite: 'dot', size: 7, sizeRnd: 0.3, life: 4, lifeRnd: 0, turbAmp: 45, turbFreq: 0.6, grad: G1('#FFF8D0'), opacityOL: C([0, 0], [0.1, 1], [0.2, 0.3], [0.35, 1], [0.5, 0.35], [0.65, 1], [0.8, 0.3], [0.9, 0.9], [1, 0]) }
    }), P, 4)
]);

fx('vfx_amb_snow', { size: [1024, 1024], frames: 16, loop: 4.0 }, ({ P }) => [
    loop(EM('Snow big', {
        em: { x: 0, y: -290, shape: 'box', sx: 300, sy: 6, dir: 'dir', angle: 90, spread: 12, speed: 75, speedRnd: 0.25, rate: 2.5 },
        pt: { sprite: 'tex:snow', size: 26, sizeRnd: 0.25, life: 8, lifeRnd: 0, turbAmp: 30, turbFreq: 0.5, spin: 30, spinRnd: 1, rotRnd: 1, opacityOL: C([0, 1], [0.95, 1], [1, 0]) }
    }), P, 8),
    loop(EM('Snow small', {
        em: { x: 0, y: -280, shape: 'box', sx: 300, sy: 6, dir: 'dir', angle: 90, spread: 12, speed: 55, speedRnd: 0.3, rate: 6 },
        pt: { sprite: 'dot', size: 9, sizeRnd: 0.3, life: 11, lifeRnd: 0, turbAmp: 25, turbFreq: 0.6, grad: G1(WHITE), opacityOL: C([0, 0.9], [0.95, 0.9], [1, 0]) }
    }), P, 11)
]);

fx('vfx_amb_steam', { size: [384, 384], frames: 12, loop: 2.0 }, ({ P }) => [
    // завиток: точка рождения качается по синусу, частицы всплывают — синусоида ползёт вверх
    [[-34, 0], [30, 0.5]].map(([x0, ph], i) => loop(EM('Steam ' + (i + 1), {
        em: { x: sampleK(t => x0 + 16 * Math.sin(2 * Math.PI * (t / P + ph)), 0, P, 24), y: 170, shape: 'point', dir: 'dir', angle: -90, spread: 4, speed: 150, speedRnd: 0, rate: 40 },
        pt: { sprite: 'soft', size: 26, sizeRnd: 0.1, life: 2, lifeRnd: 0, drag: 0.4, gravX: 6, grad: G1(WHITE), sizeOL: C([0, 0.5], [1, 2.6]), opacityOL: C([0, 0], [0.15, 0.4], [0.6, 0.22], [1, 0]) }
    }), P, 2))
]);

fx('vfx_amb_glass_glint', { size: [512, 512], frames: 12, loop: 3.0 }, ({ P }) => {
    const band = (name, aspect, op, off) => SP(name, {
        blend: 'add', shape: 'soft', size: 720, aspect, rot: 30, color: WHITE, fade: [0, 0, 250, 0.6],
        x: K([0, -380 + off, 'lin'], [2.4, 380 + off, 'hold'], [P, -380 + off, 'lin']), scale: 1, op
    });
    return [band('Glint thin', 0.03, 0.35, -60), band('Glint', 0.08, 0.3, 0)];
});

fx('vfx_amb_birds', { size: [1024, 256], frames: 16, loop: 4.0 }, ({ P }) => {
    const out = [];
    const pattern = [1, -1, 1, -1, 0, 0, 0, 0, 1, -1, 1, -1, 0, 0, 0, 0];
    // фазы кратны 1/16: перескок справа налево приходится ровно на границу кадров (за кадром)
    [[0, -20, 1.5], [5 / 16, -40, 1.25], [10 / 16, 5, 1.1]].forEach(([ph, y0, sc], b) => {
        const xAt = t => -300 + 600 * ((((t / P) + ph) % 1 + 1) % 1);
        const flap = i => pattern[(i + b * 3) % 16];
        const wing = side => {
            const X = [], Y = [], R = [];
            for (let i = 0; i < 16; i++) {
                const t0 = i * P / 16, t1 = (i + 1) * P / 16 - 0.002;
                // центр крыла = сустав + side*half*(cos, sin); кончик вверх: у левого угол +, у правого −
                const ang = flap(i) === 1 ? -side * 35 : flap(i) === -1 ? side * 20 : -side * 8;
                [t0, t1].forEach((t, j) => {
                    const x = j ? xAt(t0) + 600 * (P / 16) / P : xAt(t0);
                    const y = y0 + 6 * Math.sin(2 * Math.PI * (t / P + ph));
                    const a = ang * Math.PI / 180, half = 11 * sc;
                    X.push([t, x + side * half * Math.cos(a), 'lin']);
                    Y.push([t, y + side * half * Math.sin(a), 'lin']);
                    R.push([t, ang, 'lin']);
                });
            }
            return SP('Bird ' + (b + 1) + (side < 0 ? ' wing L' : ' wing R'), Object.assign({ color: '#3E4450', scale: sc, op: 1, x: K(...X), y: K(...Y), rot: K(...R) }, bar(22, 5)));
        };
        out.push(wing(-1), wing(1));
    });
    return out;
});

// =====================================================================================
// 8. ПЕРЕХОДЫ
// =====================================================================================
fx('vfx_wipe_awning', { size: [1024, 576], frames: 12, dur: 0.8 }, () => {
    const g = [{ t: 0, y: -190 }, { t: 0.45, y: 205 }, { t: 0.55, y: 182 }, { t: 0.63, y: 190 }, { t: 0.8, y: 190 }];
    const parts = [];
    const N = 10, w = 64;
    for (let i = 0; i < N; i++) {
        const x = -288 + w / 2 + i * w, c = i % 2 ? CREAM : RED;
        parts.push({ name: 'Scallop ' + i, shape: 'dot', size: 70, color: c, x, y: 0, _z: 0 });
        parts.push(Object.assign({ name: 'Stripe ' + i, color: c, x, y: -190, _z: 1 }, bar(w + 1, 380)));
        parts.push({ name: 'Scallop outline ' + i, shape: 'dot', size: 82, color: BROWN, x, y: 2, _z: 2 });
        parts.push({ name: 'Scallop shadow ' + i, shape: 'soft', size: 110, color: '#000000', x, y: 18, op: 0.3, _z: 3 });
    }
    parts.sort((a, b) => a._z - b._z).forEach(p => delete p._z);
    return rigid(parts, g, { op: 1 });
});

fx('vfx_wipe_paper', { size: [1024, 576], frames: 12, dur: 0.8 }, () => {
    const g = [{ t: 0, x: 760, y: 560, r: -8 }, { t: 0.5, x: -8, y: -6, r: -8 }, { t: 0.62, x: 0, y: 0, r: -8 }, { t: 0.8, x: 0, y: 0, r: -8 }];
    const hw = 380, hh = 280;
    const edge = [];
    // рваный край: ромбики разного размера с шагом меньше размера — зубчатая кромка
    for (let x = -hw; x <= hw; x += 16) { const d = 14 + rnd() * 18; edge.push({ name: 'Torn top ' + edge.length, shape: 'square', size: d, rot: 45 + rnd() * 20 - 10, color: CREAM, x: x + rnd() * 6, y: -hh + rnd() * 4 }); }
    for (let y = -hh + 16; y <= hh; y += 16) { const d = 14 + rnd() * 18; edge.push({ name: 'Torn left ' + edge.length, shape: 'square', size: d, rot: 45 + rnd() * 20 - 10, color: CREAM, x: -hw + rnd() * 4, y: y + rnd() * 6 }); }
    const shadowG = g.map(k => Object.assign({}, k, { x: k.x + 16, y: k.y + 18 }));
    return [
        rigid(edge.concat([Object.assign({ name: 'Paper', color: CREAM }, bar(hw * 2, hh * 2))]), g, { op: 1 }),
        rigid([Object.assign({ name: 'Paper shadow', shape: 'soft', color: BROWN, size: 900, aspect: 1.35 })], shadowG, { op: 0.35 })
    ];
});

fx('vfx_loading_wheel', { size: [256, 256], frames: 12, loop: 1.0 }, ({ P }) => {
    const spin = a => K([0, a, 'lin'], [P, a + 360, 'lin']);
    const bolt = orbit({ cx: 10, r0: 44, a0: 0, turns: 1, t0: 0, t1: P, n: 36 });
    const spokes = [0, 60, 120].map((a, i) => SP('Spoke ' + (i + 1), Object.assign({ color: '#7D838F', x: 10, rot: spin(a), scale: 1, op: 1 }, bar(110, 12))));
    return [
        SP('Hub', { shape: 'dot', size: 36, color: '#C9CDD6', x: 10, scale: 1, op: 1 }),
        SP('Hub outline', { shape: 'dot', size: 48, color: BROWN, x: 10, scale: 1, op: 1 }),
        SP('Bolt', { shape: 'dot', size: 14, color: '#4A4A55', x: bolt.x, y: bolt.y, scale: 1, op: 1 }),
        spokes,
        SP('Rim', { shape: 'dot', size: 124, color: '#E1E4EA', x: 10, scale: 1, op: 1 }),
        SP('Rim outline', { shape: 'dot', size: 134, color: BROWN, x: 10, scale: 1, op: 1 }),
        SP('Tyre', { shape: 'dot', size: 178, color: '#4A4A55', x: 10, scale: 1, op: 1 }),
        SP('Tyre outline', { shape: 'dot', size: 194, color: BROWN, x: 10, scale: 1, op: 1 }),
        SP('Motion arc', { shape: 'ring', p: { th: 0.08 }, size: 236, color: '#C9CDD6', x: 10, fade: [-95, -40, 85, 0.6], scale: 1, op: K([0, 0.6], [P / 2, 1], [P, 0.6]) }),
        SP('Motion arc 2', { shape: 'ring', p: { th: 0.06 }, size: 270, color: '#C9CDD6', x: 10, fade: [-110, -50, 70, 0.6], scale: 1, op: K([0, 0.35], [P / 2, 0.7], [P, 0.35]) })
    ];
});

// =====================================================================================
// 9. ТОРГОВЫЙ ДОМ, СМЕНА, КОПИЛКА (03.10.2026)
// =====================================================================================
const PINK = '#F7A9BC', DPINK = '#C95F7E', LPINK = '#FFD6E0', STEEL = '#5B606B', DSTEEL = '#24262C',
    LGREEN = '#9BE36B';

// монетка падает в щель копилки (окно победы, свинка на площади)
fx('vfx_piggy_coin_drop', { size: [256, 256], frames: 16, dur: 0.7 }, () => {
    const T = 0.36, SLOT = -40;
    const y = sampleK(t => -200 + (SLOT + 200) * easeIn(t / T, 2), 0, T, 10);
    return [
        sparkBurst({ t: T, y: SLOT, n: 6, speed: 320, size: 40, life: 0.32, gravY: 300, em: { dir: 'dir', angle: -90, spread: 130, shape: 'point' } }),
        SP('Slot glint', { blend: 'add', shape: 'spark', size: 220, color: WHITE, y: SLOT, start: T, end: T + 0.24, rot: K([0, 0], [0.24, 45]), scale: K([0, 0], [0.06, 1], [0.24, 0]), op: 1 }),
        SP('Coin', { shape: 'tex:coin', size: 130, y, end: T + 0.1, rot: K([0, -40], [T, 160]), scale: K([0, 0.85], [T - 0.04, 1], [T + 0.1, 0.25]), op: K([0, 0], [0.05, 1], [T, 1], [T + 0.1, 0]) }),
        EM('Coin trail', {
            blend: 'add', end: T, em: { x: 0, y, shape: 'circle', sx: 16, sy: 16, speed: 20, rate: 80 },
            pt: { sprite: 'spark', size: 30, sizeRnd: 0.3, life: 0.22, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: C([0, 1], [1, 0.2]), opacityOL: C([0, 1], [1, 0]) }
        }),
        flash({ t: T, d: 0.28, y: SLOT, size: 200, color: LGOLD, op: 0.85 })
    ];
});

// копилку разбили: черепки, фонтан монет, конфетти
fx('vfx_piggy_break', { size: [512, 512], frames: 16, dur: 1.1 }, () => {
    // черепок и его контур — один сид и одни параметры, контур крупнее и ниже
    const shards = (name, size, color) => EM(name, {
        end: 0.05, em: { x: 0, y: 10, shape: 'circle', sx: 70, sy: 50, dir: 'out', speed: 400, speedRnd: 0.45, bursts: [{ t: 0, n: 14 }], seed: 501 },
        pt: { sprite: 'shard', size, sizeRnd: 0.35, life: 0.8, lifeRnd: 0.15, drag: 2, gravY: 900, spin: 420, spinRnd: 1, rotRnd: 1, grad: G1(color), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
    });
    return [
        sparkBurst({ t: 0.04, n: 8, r: 30, speed: 520, size: 46, life: 0.6 }),
        EM('Coins', {
            start: 0.04, end: 0.1, em: { x: 0, y: 30, shape: 'box', sx: 30, sy: 6, dir: 'dir', angle: -90, spread: 60, speed: 600, speedRnd: 0.3, bursts: [{ t: 0, n: 9 }, { t: 0.05, n: 6 }] },
            pt: { sprite: 'tex:coin', size: 52, sizeRnd: 0.2, life: 0.85, lifeRnd: 0.1, gravY: 1500, drag: 0.3, spin: 260, spinRnd: 1, rotRnd: 0.3, sizeOL: C([0, 0.5], [0.1, 1], [1, 1]), opacityOL: C([0, 1], [0.85, 1], [1, 0]) }
        }),
        shards('Shards', 58, PINK),
        shards('Shards outline', 72, DPINK),
        confettiBurst({ t: 0.06, n: 6, speed: 420, life: 0.9, gravY: 420, drag: 3, colors: [GOLD, PINK, GREEN, SKY] }),
        EM('Ceramic dust', {
            end: 0.05, em: { x: 0, y: 20, shape: 'circle', sx: 50, sy: 40, dir: 'out', speed: 220, speedRnd: 0.5, bursts: [{ t: 0, n: 9 }] },
            pt: { sprite: 'blob', size: 90, sizeRnd: 0.35, life: 0.6, lifeRnd: 0.2, drag: 3.5, gravY: -30, spin: 40, spinRnd: 1, rotRnd: 1, grad: G([0, WHITE], [1, LPINK]), sizeOL: C([0, 0.4], [0.3, 1], [1, 1.1]), opacityOL: C([0, 0.9], [0.5, 0.6], [1, 0]) }
        }),
        ringWave({ t: 0, d: 0.45, th: 0.08, size: 220, s0: 0.3, s1: 2.2, color: GOLD, glow: 0.4 }),
        flash({ t: 0, d: 0.4, color: CREAM, size: 360, s1: 1.5 })
    ];
});

// сияние за полной копилкой: лучи медленно крутятся, по краю мерцают искорки (цикл)
fx('vfx_piggy_glow', { size: [384, 384], frames: 12, loop: 1.5 }, ({ P }) => [
    loop(EM('Twinkles', {
        blend: 'add', em: { shape: 'ring', sx: 175, sy: 30, dir: 'out', speed: 18, speedRnd: 0.5, rate: 7, seed: 31 },
        pt: { sprite: 'spark', size: 46, sizeRnd: 0.4, life: 0.9, lifeRnd: 0, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE, rotRnd: 0.1 }
    }), P, 0.9),
    // лучи симметричны: 12 лучей за цикл поворачиваются на 30°, 8 — на 45°, стык цикла не виден
    SP('Rays gold', { blend: 'add', shape: 'star', p: { n: 12, inr: 0.16 }, size: 500, color: GOLD, fade: [0, 0, 245, 0.6], rot: K([0, 0, 'lin'], [P, 30, 'lin']), scale: 1, op: 0.42 }),
    SP('Rays cream', { blend: 'add', shape: 'star', p: { n: 8, inr: 0.24 }, size: 410, color: CREAM, fade: [0, 0, 200, 0.6], rot: K([0, 0, 'lin'], [P, -45, 'lin']), scale: 1, op: 0.35 }),
    SP('Halo', { blend: 'add', shape: 'soft', size: 380, color: LGOLD, scale: K([0, 0.94], [P / 2, 1.06], [P, 0.94]), op: K([0, 0.4], [P / 2, 0.6], [P, 0.4]) })
]);

// жилец въехал: луч света сверху, кольцо у ног, искры вверх
fx('vfx_staff_arrive', { size: [512, 512], frames: 16, dur: 1.2 }, () => {
    const FEET = 200, T = 0.22;
    return [
        sparkBurst({ t: T, y: -20, n: 8, r: 40, speed: 460, size: 46, life: 0.7 }),
        EM('Rising sparkles', {
            start: 0.05, end: 0.8, em: { x: 0, y: FEET, shape: 'box', sx: 120, sy: 6, dir: 'dir', angle: -90, spread: 10, speed: 300, speedRnd: 0.35, rate: 34 },
            pt: { sprite: 'tex:sparkle', size: 34, sizeRnd: 0.35, life: 0.85, lifeRnd: 0.2, drag: 0.6, spin: 160, spinRnd: 1, rotRnd: 1, sizeOL: BELL, opacityOL: ONE }
        }),
        glints({ t: T, y: -20, n: 12, speed: 320, size: 36 }),
        SP('Feet ring', { blend: 'add', shape: 'ring', p: { th: 0.12 }, size: 170, aspect: 2.8, color: GOLD, glow: 0.4, y: FEET, start: 0.1, end: 0.9, scale: K([0, 0.3], [0.5, 1.0]), op: K([0, 0], [0.08, 1], [0.4, 0.8], [0.8, 0]) }),
        SP('Beam core', { blend: 'add', shape: 'soft', size: 560, aspect: 0.12, color: WHITE, y: -10, end: 1.0, scale: K([0, 0.5], [0.25, 1]), op: K([0, 0], [0.15, 0.7], [0.6, 0.45], [1.0, 0]) }),
        SP('Beam', { blend: 'add', shape: 'soft', size: 600, aspect: 0.34, color: LGOLD, y: -10, end: 1.2, scale: K([0, 0.6], [0.3, 1]), op: K([0, 0], [0.15, 0.8], [0.7, 0.55], [1.2, 0]) }),
        flash({ t: T, d: 0.4, y: -20, color: CREAM, size: 320, s1: 1.4 })
    ];
});

// жилец поднял уровень: три золотые стрелки-галочки вверх, звёзды
fx('vfx_staff_level_up', { size: [512, 512], frames: 16, dur: 1.1 }, () => {
    const chevron = (i, t0) => {
        const D = 0.7;
        const parts = [
            Object.assign({ name: `Chevron ${i} L`, color: GOLD, x: -34, y: 0, rot: -40 }, bar(104, 30)),
            Object.assign({ name: `Chevron ${i} R`, color: GOLD, x: 34, y: 0, rot: 40 }, bar(104, 30)),
            Object.assign({ name: `Chevron ${i} L outline`, color: BROWN, x: -34, y: 0, rot: -40 }, bar(118, 44)),
            Object.assign({ name: `Chevron ${i} R outline`, color: BROWN, x: 34, y: 0, rot: 40 }, bar(118, 44))
        ];
        const gk = [{ t: 0, y: 150, s: 0.5 }, { t: 0.12, y: 90, s: 1.1 }, { t: 0.2, y: 60, s: 1 }, { t: D, y: -170, s: 0.9 }];
        return rigid(parts, gk, { start: t0, end: t0 + D, op: K([0, 0], [0.06, 1], [D * 0.65, 1], [D, 0]) });
    };
    return [
        EM('Stars', {
            start: 0.3, end: 0.35, em: { x: 0, y: -40, shape: 'circle', sx: 30, sy: 30, dir: 'out', speed: 420, speedRnd: 0.3, bursts: [{ t: 0, n: 6 }] },
            pt: { sprite: 'tex:star', size: 52, sizeRnd: 0.25, life: 0.7, lifeRnd: 0.15, drag: 2.6, gravY: 120, spin: 220, spinRnd: 1, rotRnd: 1, sizeOL: C([0, 0.3], [0.2, 1.1], [1, 0.6]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
        }),
        chevron(1, 0), chevron(2, 0.14), chevron(3, 0.28),
        EM('Rising sparkles', {
            start: 0.05, end: 0.7, em: { x: 0, y: 180, shape: 'box', sx: 110, sy: 6, dir: 'dir', angle: -90, spread: 12, speed: 340, speedRnd: 0.3, rate: 26 },
            pt: { sprite: 'spark', size: 34, sizeRnd: 0.4, life: 0.7, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE }
        }),
        ringWave({ t: 0.3, d: 0.45, th: 0.08, size: 220, s0: 0.3, s1: 2.1, glow: 0.4 }),
        flash({ t: 0.3, d: 0.4, color: CREAM, size: 320, s1: 1.4 })
    ];
});

// сотрудник выбран на смену (карточка в окне смены)
fx('vfx_staff_pick', { size: [256, 256], frames: 10, dur: 0.5 }, () => {
    const T = 0.12;
    return [
        sparkBurst({ t: T, n: 6, r: 40, speed: 400, size: 40, life: 0.38 }),
        glints({ t: T, n: 8, speed: 260, size: 30, life: 0.35 }),
        SP('Snap ring', { blend: 'add', shape: 'ring', p: { th: 0.1 }, size: 300, color: GOLD, glow: 0.5, end: T + 0.25, scale: K([0, 1.6, 'ease'], [T, 0.6, 'ease'], [T + 0.25, 0.9]), op: K([0, 0], [0.05, 1], [T, 1], [T + 0.25, 0]) }),
        flash({ t: T, d: 0.3, color: CREAM, size: 300, s1: 1.3 })
    ];
});

// идёт стройка: пыль клубами, искры от молотков, щепки (цикл)
fx('vfx_build_dust', { size: [512, 512], frames: 12, loop: 1.2 }, ({ P }) => {
    const hits = [[-90, 110, 0.1], [80, 140, 0.7]];
    return [
        hits.map(([x, y, t], i) => [
            loop(EM('Hammer sparks ' + (i + 1), {
                blend: 'add', em: { x, y, shape: 'point', dir: 'dir', angle: -90, spread: 130, speed: 420, speedRnd: 0.35, bursts: [{ t, n: 8 }], seed: 40 + i },
                pt: { sprite: 'streak', size: 26, sizeRnd: 0.3, life: 0.35, lifeRnd: 0.2, gravY: 900, alignVel: true, stretch: 1.4, grad: G([0, WHITE], [0.4, LGOLD], [1, ORANGE]), opacityOL: C([0, 1], [0.7, 1], [1, 0]) }
            }), P, 0.45),
            loop(EM('Wood chips ' + (i + 1), {
                em: { x, y, shape: 'point', dir: 'dir', angle: -90, spread: 100, speed: 380, speedRnd: 0.3, bursts: [{ t, n: 3 }], seed: 50 + i },
                pt: { sprite: 'ring', p: { th: 0.35 }, size: 22, sizeRnd: 0.2, life: 0.6, lifeRnd: 0.1, gravY: 1300, spin: 600, spinRnd: 0.3, rotRnd: 1, grad: G1('#C98B4F'), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
            }), P, 0.7),
            SP('Hit flash ' + (i + 1), { blend: 'add', shape: 'soft', size: 130, color: LGOLD, x, y, scale: 1, op: K([0, 0, 'hold'], [t, 0.9], [t + 0.16, 0], [P, 0]) })
        ]),
        loop(EM('Dust', {
            em: { x: 0, y: 190, shape: 'box', sx: 170, sy: 8, dir: 'dir', angle: -90, spread: 70, speed: 110, speedRnd: 0.4, rate: 8, seed: 61 },
            pt: { sprite: 'tex:dust', size: 110, sizeRnd: 0.3, life: 1.2, lifeRnd: 0, drag: 1.4, gravY: -40, spin: 40, spinRnd: 1, rotRnd: 1, sizeOL: C([0, 0.4], [0.4, 1], [1, 1.3]), opacityOL: C([0, 0], [0.2, 0.75], [1, 0]) }
        }), P, 1.2)
    ];
});

// цепи этажа лопнули: звенья и замок падают вниз
fx('vfx_chains_fall', { size: [512, 512], frames: 16, dur: 1.0 }, () => {
    const G_ = 1500;
    const links = [];
    for (let i = 0; i < 10; i++) {
        const d = i % 2 ? 1 : -1;                       // две диагонали — крест, как цепи на этаже
        const u = ((i >> 1) - 2) / 2;                   // −1 … 1 вдоль диагонали
        if (Math.abs(u) < 0.1) continue;                // в центре — замок
        const x0 = u * 150, y0 = d * u * 95;
        const vx = Math.sign(x0) * (40 + rnd() * 50), vy = -(120 + rnd() * 110);
        const td = 0.04 + Math.abs(u) * 0.08, D = 0.95;   // до td звено висит на месте
        const fall = t => Math.max(0, t - td);
        const xs = sampleK(t => x0 + vx * fall(t), 0, D, 12), ys = sampleK(t => y0 + vy * fall(t) + G_ * fall(t) * fall(t) / 2, 0, D, 16);
        const a0 = Math.atan2(d * 95, 150) * 180 / Math.PI + (rnd() * 20 - 10);
        const spin = (rnd() < 0.5 ? -1 : 1) * (240 + rnd() * 200);
        const rot = sampleK(t => a0 + spin * fall(t) / (D - td), 0, D, 6);
        const op = K([0, 1], [D * 0.8, 1], [D, 0]);
        const t0 = 0;
        links.push(
            SP('Link ' + i, { start: t0, end: t0 + D, shape: 'ring', p: { th: 0.5 }, aspect: 1.6, size: 58, color: STEEL, x: xs, y: ys, rot, scale: 1, op }),
            SP('Link outline ' + i, { start: t0, end: t0 + D, shape: 'ring', p: { th: 0.64 }, aspect: 1.55, size: 68, color: DSTEEL, x: xs, y: ys, rot, scale: 1, op })
        );
    }
    // замок: дужка, корпус, скважина — одной группой падает с поворотом
    const lock = [
        { name: 'Keyhole', shape: 'dot', size: 18, color: BROWN, x: 0, y: 12 },
        Object.assign({ name: 'Lock body', color: GOLD, x: 0, y: 14 }, bar(84, 64)),
        Object.assign({ name: 'Lock body outline', color: BROWN, x: 0, y: 14 }, bar(98, 78)),
        { name: 'Shackle', shape: 'ring', p: { th: 0.3 }, aspect: 0.85, size: 70, color: DGOLD, x: 0, y: -26 },
        { name: 'Shackle outline', shape: 'ring', p: { th: 0.46 }, aspect: 0.85, size: 82, color: BROWN, x: 0, y: -26 }
    ];
    const gk = [{ t: 0, y: 0, s: 1 }, { t: 0.08, y: -24, s: 1.12 }];
    for (let k = 1; k <= 8; k++) { const t = 0.08 + 0.8 * k / 8, tt = t - 0.08; gk.push({ t, y: -24 - 180 * tt + G_ * tt * tt / 2, r: 35 * k / 8, s: 1, m: 'lin' }); }
    return [
        sparkBurst({ t: 0.04, n: 8, r: 30, speed: 440, size: 42, life: 0.5 }),
        glints({ t: 0.04, n: 10, speed: 300, size: 32, color: WHITE }),
        rigid(lock, gk, { end: 0.9, op: K([0, 1], [0.7, 1], [0.9, 0]) }),
        links,
        flash({ t: 0.02, d: 0.35, color: CREAM, size: 300, s1: 1.4 })
    ];
});

// умение сотрудника сработало / сотрудник вышел на смену (значок у заказа)
fx('vfx_staff_skill', { size: [256, 256], frames: 10, dur: 0.7 }, () => [
    EM('Boost', {
        end: 0.2, em: { x: 0, y: 60, shape: 'box', sx: 70, sy: 6, dir: 'dir', angle: -90, spread: 18, speed: 300, speedRnd: 0.3, bursts: [{ t: 0, n: 5 }, { t: 0.12, n: 4 }] },
        pt: { sprite: 'star', p: { n: 4, inr: 0.36 }, size: 40, sizeRnd: 0.3, life: 0.55, lifeRnd: 0.15, drag: 1, grad: G([0, WHITE], [1, LGREEN]), sizeOL: BELL, opacityOL: ONE, glow: 0.3 }
    }),
    glints({ t: 0.04, n: 8, speed: 260, size: 30, life: 0.4 }),
    ringWave({ t: 0, d: 0.5, th: 0.12, size: 200, s0: 0.4, s1: 1.9, color: LGREEN, glow: 0.4 }),
    flash({ t: 0, d: 0.35, color: '#E9FFD6', size: 260, s1: 1.3 })
]);

// =====================================================================================
// 10. ПОДСКАЗКА, БЕЙДЖ СОТРУДНИКА (v4, 03.10.2026)
// =====================================================================================
// подсказка на товаре: кольцо «дышит», две искорки обходят его по кругу, сверху прыгает стрелка-листик (цикл)
fx('vfx_hint_glow', { size: [384, 384], frames: 12, loop: 1.0 }, ({ P }) => {
    const breathe = (a, b) => K([0, a], [P / 2, b], [P, a]);
    const sparks = [];
    [-90, 90].forEach((a0, j) => {
        [[0, 58, 1], [-14, 40, 0.6], [-27, 28, 0.35]].forEach(([lag, size, op], i) => {
            const o = orbit({ r0: 158, a0: a0 + lag, turns: 1, t0: 0, t1: P, n: 36 });
            sparks.push(SP(`Orbit spark ${j + 1}.${i + 1}`, { blend: 'add', shape: 'spark', size, color: i ? LGOLD : WHITE, x: o.x, y: o.y,
                rot: K([0, 0, 'lin'], [P, 180, 'lin']), scale: 1, op }));
        });
    });
    // стрелка вниз: треугольник + ножка, листовой зелёный с коричневым контуром; подпрыгивает
    const ay = K([0, -196], [P * 0.5, -174], [P, -196]);
    const arrow = (name, size, stem, color) => [
        SP(name + ' head', { shape: 'poly', p: { n: 3 }, size, color, y: ay, rot: 180, scale: 1, op: 1 }),
        SP(name + ' stem', Object.assign({ color, y: K([0, -222], [P * 0.5, -200], [P, -222]), scale: 1, op: 1 }, bar(stem[0], stem[1])))
    ];
    return [
        sparks,
        arrow('Arrow', 66, [22, 34], LGREEN), arrow('Arrow outline', 86, [34, 46], BROWN),
        SP('Ring gold', { blend: 'add', shape: 'ring', p: { th: 0.07 }, size: 318, color: GOLD, glow: 0.5, scale: breathe(0.96, 1.04), op: breathe(0.7, 1) }),
        SP('Ring green', { blend: 'normal', shape: 'ring', p: { th: 0.16 }, size: 330, color: LGREEN, scale: breathe(0.96, 1.04), op: breathe(0.35, 0.55) }),
        SP('Halo', { blend: 'add', shape: 'ring', p: { th: 0.3 }, size: 360, color: LGREEN, fade: [0, 0, 180, 0.8], scale: breathe(0.94, 1.05), op: breathe(0.18, 0.32) })
    ];
});

// куда класть: кольцо «приземления» и три искры (пара к vfx_hint_glow)
fx('vfx_hint_target', { size: [256, 256], frames: 10, dur: 0.6 }, () => [
    sparkBurst({ t: 0.1, n: 3, r: 40, speed: 360, size: 44, life: 0.42, em: { shape: 'point', dir: 'dir', angle: -90, spread: 120 } }),
    SP('Land ring', { blend: 'add', shape: 'ring', p: { th: 0.1 }, size: 300, aspect: 1, color: LGREEN, glow: 0.5, end: 0.5,
        scale: K([0, 1.5, 'ease'], [0.18, 0.75, 'ease'], [0.5, 0.95]), op: K([0, 0], [0.06, 1], [0.25, 1], [0.5, 0]) }),
    ringWave({ t: 0.16, d: 0.42, th: 0.08, size: 200, s0: 0.5, s1: 1.8, color: GOLD, glow: 0.3 }),
    flash({ t: 0.16, d: 0.3, color: '#E9FFD6', size: 260, s1: 1.2, op: 0.8 })
]);

// новая звезда на бейдже сотрудника: влетает в гнездо, хлопок, золотые искры
fx('vfx_staff_badge_up', { size: [384, 384], frames: 14, dur: 0.8 }, () => {
    const T = 0.3;
    const star = (name, size, color) => SP(name, { shape: 'tex:star', size, color, end: T + 0.12,
        x: K([0, -150], [T, 0]), y: K([0, -170], [T, 0]), rot: K([0, -200], [T, 0]),
        scale: K([0, 0.5], [T - 0.05, 1.1], [T, 1], [T + 0.12, 1.3]), op: K([0, 0], [0.05, 1], [T, 1], [T + 0.12, 0]) });
    return [
        sparkBurst({ t: T, n: 8, r: 30, speed: 460, size: 42, life: 0.45 }),
        glints({ t: T, n: 10, speed: 300, size: 32, life: 0.4 }),
        star('Star', 120, WHITE),
        EM('Star trail', {
            blend: 'add', end: T, em: { x: K([0, -150], [T, 0]), y: K([0, -170], [T, 0]), shape: 'circle', sx: 14, sy: 14, speed: 20, rate: 70 },
            pt: { sprite: 'spark', size: 34, sizeRnd: 0.3, life: 0.25, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: C([0, 1], [1, 0.2]), opacityOL: C([0, 1], [1, 0]) }
        }),
        ringWave({ t: T, d: 0.4, th: 0.1, size: 200, s0: 0.3, s1: 2.0, color: GOLD, glow: 0.4 }),
        flash({ t: T, d: 0.35, color: CREAM, size: 300, s1: 1.4 })
    ];
});

// =====================================================================================
// 11. ЗАВОЗ И ИСПЫТАНИЕ ДНЯ, ЗАДАНИЯ, СЕРИЯ, АЛМАЗЫ, ЗОЛОТОЙ ПУТЬ, СЛОЖНОСТЬ, ДОМ, АЛЬБОМ (v4, 03.10.2026)
// =====================================================================================
const MINT = '#5FE0C8', DMINT = '#1E9C8C', LMINT = '#C9FFF4', SMOKE = '#8E8A86';

// монеты/предметы дугой вверх и тают
const arcBurst = (name, tex, o) => EM(name, {
    start: o.t || 0, end: (o.t || 0) + 0.06,
    em: { x: o.x || 0, y: o.y || 0, shape: 'box', sx: o.w || 30, sy: 6, dir: 'dir', angle: -90, spread: o.spread || 70, speed: o.speed || 620, speedRnd: 0.3, bursts: [{ t: 0, n: o.n || 4 }] },
    pt: { sprite: 'tex:' + tex, size: o.size || 60, sizeRnd: 0.2, life: o.life || 0.85, lifeRnd: 0.15, gravY: o.gravY || 1300, drag: 0.4, spin: 240, spinRnd: 1, rotRnd: 0.3,
        sizeOL: C([0, 0.4], [0.12, 1], [1, 0.8]), opacityOL: C([0, 1], [0.7, 1], [1, 0]) }
});

// ящик «Завоза дня» открылся: подпрыгнул, столб света, яблоки и монеты дугой, соломенная пыль
fx('vfx_crate_open', { size: [512, 512], frames: 16, dur: 1.2 }, () => {
    const T = 0.22;
    return [
        arcBurst('Apples', 'apple', { t: T, y: 40, n: 3, size: 64, speed: 700 }),
        arcBurst('Coins', 'coin', { t: T + 0.05, y: 40, n: 4, size: 50, speed: 640 }),
        sparkBurst({ t: T, y: 20, n: 7, r: 30, speed: 460, size: 44, life: 0.6, em: { dir: 'dir', angle: -90, spread: 120, shape: 'point' } }),
        EM('Straw dust', {
            start: 0.05, end: 0.1, em: { x: 0, y: 150, shape: 'box', sx: 150, sy: 6, dir: 'dir', angle: -90, spread: 120, speed: 160, speedRnd: 0.5, bursts: [{ t: 0, n: 7 }] },
            pt: { sprite: 'tex:dust', size: 90, sizeRnd: 0.3, life: 0.7, lifeRnd: 0.2, drag: 2.5, spin: 40, spinRnd: 1, rotRnd: 1, grad: G1('#F3D9A0'), sizeOL: C([0, 0.4], [0.4, 1], [1, 1.2]), opacityOL: C([0, 0.8], [1, 0]) }
        }),
        SP('Light column', { blend: 'add', shape: 'soft', size: 560, aspect: 0.32, color: LGOLD, y: -90, start: T - 0.04, end: 1.2,
            scale: K([0, 0.3], [0.25, 1]), op: K([0, 0], [0.1, 0.85], [0.6, 0.5], [0.98, 0]) }),
        SP('Light core', { blend: 'add', shape: 'soft', size: 520, aspect: 0.12, color: WHITE, y: -80, start: T - 0.04, end: 1.0,
            scale: K([0, 0.3], [0.25, 1]), op: K([0, 0], [0.1, 0.75], [0.76, 0]) }),
        ringWave({ t: 0, d: 0.3, th: 0.1, size: 260, aspect: 2.6, y: 150, s0: 0.5, s1: 1.6, color: CREAM, op: 0.8 }),
        flash({ t: T, d: 0.35, y: 0, color: CREAM, size: 320, s1: 1.4 })
    ];
});

// лучи позади награды в окне «Получено!» (цикл)
fx('vfx_reward_rays', { size: [512, 512], frames: 12, loop: 1.0 }, ({ P }) => [
    loop(EM('Twinkles', {
        blend: 'add', em: { shape: 'circle', sx: 200, sy: 200, dir: 'out', speed: 14, speedRnd: 0.5, rate: 8, seed: 71 },
        pt: { sprite: 'spark', size: 44, sizeRnd: 0.4, life: 1.0, lifeRnd: 0, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE, rotRnd: 0.1 }
    }), P, 1.0),
    SP('Rays gold', { blend: 'add', shape: 'star', p: { n: 24, inr: 0.1 }, size: 640, color: GOLD, fade: [0, 0, 310, 0.6], rot: K([0, 0, 'lin'], [P, 15, 'lin']), scale: 1, op: 0.45 }),
    SP('Rays cream', { blend: 'add', shape: 'star', p: { n: 16, inr: 0.18 }, size: 520, color: CREAM, fade: [0, 0, 250, 0.6], rot: K([0, 0, 'lin'], [P, -22.5, 'lin']), scale: 1, op: 0.35 }),
    SP('Halo', { blend: 'add', shape: 'soft', size: 420, color: LGOLD, scale: K([0, 0.95], [P / 2, 1.05], [P, 0.95]), op: K([0, 0.45], [P / 2, 0.6], [P, 0.45]) })
]);

// медаль «Испытание дня»: падает сверху на ленте, качается, вспышка, звёзды, кольцо
fx('vfx_challenge_win', { size: [512, 512], frames: 16, dur: 1.4 }, () => {
    const T = 0.32;
    const medal = [
        { name: 'Ribbon L', shape: 'square', size: 90, aspect: 0.42, color: RED, x: -20, y: -92, rot: 16 },
        { name: 'Ribbon R', shape: 'square', size: 90, aspect: 0.42, color: RED, x: 20, y: -92, rot: -16 },
        { name: 'Medal leaf', shape: 'blob', size: 40, color: GREEN, x: 26, y: -40 },
        { name: 'Medal inner', shape: 'dot', size: 84, color: LGOLD, x: 0, y: 0 },
        { name: 'Medal', shape: 'dot', size: 130, color: GOLD, x: 0, y: 0 },
        { name: 'Medal outline', shape: 'dot', size: 146, color: BROWN, x: 0, y: 0 }
    ];
    const gk = [{ t: 0, y: -330, r: 0 }, { t: T, y: 0, r: 0, m: 'lin' }, { t: T + 0.18, y: -14, r: 10 }, { t: T + 0.4, y: 0, r: -7 }, { t: T + 0.62, y: 0, r: 4 }, { t: 1.2, y: 0, r: 0 }];
    return [
        EM('Stars', {
            start: T, end: T + 0.05, em: { shape: 'circle', sx: 40, sy: 40, dir: 'out', speed: 460, speedRnd: 0.3, bursts: [{ t: 0, n: 7 }] },
            pt: { sprite: 'tex:star', size: 50, sizeRnd: 0.25, life: 0.75, lifeRnd: 0.15, drag: 2.6, gravY: 160, spin: 220, spinRnd: 1, rotRnd: 1, sizeOL: C([0, 0.3], [0.2, 1.1], [1, 0.6]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
        }),
        rigid(medal, gk, { end: 1.4, op: K([0, 0], [0.06, 1], [1.15, 1], [1.4, 0]) }),
        SP('Medal shine', { blend: 'add', shape: 'spark', size: 200, color: WHITE, x: -24, y: -24, start: T + 0.3, end: T + 0.62, rot: K([0, 0], [0.32, 90]), scale: K([0, 0], [0.12, 1], [0.32, 0]), op: 1 }),
        ringWave({ t: T, d: 0.5, th: 0.08, size: 240, s0: 0.4, s1: 2.2, color: GOLD, glow: 0.4 }),
        glints({ t: T, n: 12, speed: 340, size: 34 }),
        flash({ t: T, d: 0.4, color: CREAM, size: 360, s1: 1.5 })
    ];
});

// кружок «дней подряд» загорелся: зелёная капля-вспышка и искры
fx('vfx_daily_dot_fill', { size: [256, 256], frames: 10, dur: 0.5 }, () => [
    SP('Drop', { blend: 'add', shape: 'dot', size: 130, color: LGREEN, end: 0.4, scale: K([0, 0.2], [0.12, 1.15], [0.22, 0.95], [0.4, 1.3]), op: K([0, 0.9], [0.25, 0.9], [0.4, 0]) }),
    sparkBurst({ t: 0.08, n: 5, r: 24, speed: 300, size: 32, life: 0.35, pt: { grad: G1(LGREEN) } }),
    ringWave({ t: 0.06, d: 0.36, th: 0.12, size: 180, s0: 0.4, s1: 1.7, color: LGREEN, glow: 0.4 }),
    flash({ t: 0.04, d: 0.28, color: '#E9FFD6', size: 220, s1: 1.2, op: 0.8 })
]);

// задание выполнено: штамп-галочка «шлёп», конфетти-листики, волна
fx('vfx_quest_done', { size: [384, 384], frames: 12, dur: 0.7 }, () => {
    const T = 0.14;
    return [
        SP('Check', { shape: 'tex:check', size: 170, end: 0.7, scale: K([0, 2.2, 'ease'], [T, 0.9, 'ease'], [T + 0.08, 1.05], [T + 0.16, 1]), op: K([0, 0], [0.05, 1], [0.55, 1], [0.7, 0]) }),
        EM('Leaves', {
            start: T, end: T + 0.05, em: { shape: 'circle', sx: 30, sy: 30, dir: 'out', speed: 420, speedRnd: 0.4, bursts: [{ t: 0, n: 10 }] },
            pt: { sprite: 'blob', size: 26, sizeRnd: 0.35, life: 0.55, lifeRnd: 0.2, drag: 2.8, gravY: 260, spin: 360, spinRnd: 1, rotRnd: 1, grad: G([0, LGREEN], [1, GREEN]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
        }),
        ringWave({ t: T, d: 0.4, th: 0.1, size: 220, s0: 0.4, s1: 1.9, color: LGREEN, glow: 0.4 }),
        flash({ t: T, d: 0.3, color: '#E9FFD6', size: 280, s1: 1.3 })
    ];
});

// листики-очки летят к шкале (как монеты к счётчику): дуга справа налево вверх, хлопок в конце
fx('vfx_quest_token_fly', { size: [512, 256], frames: 12, dur: 0.6 }, () => {
    const out = [];
    [0, 0.06, 0.12].forEach((d, i) => {
        const T = 0.4;
        const p = bezPath([[-200, 80 - i * 14], [-60, -120], [120, -40], [200, -60]], d, d + T, 12, u => easeIO(u));
        out.push(SP('Token ' + (i + 1), { shape: 'tex:token', size: 64, start: d, end: d + T + 0.05, x: p.x, y: p.y, rot: K([0, -30], [T, 200]),
            scale: K([0, 0.6], [0.1, 1], [T, 0.75]), op: K([0, 0], [0.05, 1], [T, 1], [T + 0.05, 0]) }));
    });
    return [out,
        sparkBurst({ t: 0.5, x: 200, y: -60, n: 5, r: 16, speed: 300, size: 32, life: 0.3, pt: { grad: G1(LGREEN) } }),
        flash({ t: 0.48, d: 0.12, x: 200, y: -60, color: '#E9FFD6', size: 160, s1: 1.2 })];
});

// сундук шкалы готов: подпрыгивает (это делает игра), золотой ореол и искры сверху (цикл)
fx('vfx_chest_ready', { size: [384, 384], frames: 12, loop: 1.2 }, ({ P }) => [
    loop(EM('Sparks up', {
        blend: 'add', em: { x: 0, y: -40, shape: 'box', sx: 90, sy: 6, dir: 'dir', angle: -90, spread: 30, speed: 120, speedRnd: 0.4, rate: 7, seed: 81 },
        pt: { sprite: 'tex:sparkle', size: 36, sizeRnd: 0.4, life: 1.2, lifeRnd: 0, spin: 120, spinRnd: 1, rotRnd: 1, sizeOL: BELL, opacityOL: ONE }
    }), P, 1.2),
    SP('Halo', { blend: 'add', shape: 'soft', size: 330, color: LGOLD, scale: K([0, 0.92], [P / 2, 1.06], [P, 0.92]), op: K([0, 0.4], [P / 2, 0.7], [P, 0.4]) }),
    SP('Ring', { blend: 'add', shape: 'ring', p: { th: 0.06 }, size: 280, color: GOLD, glow: 0.4, scale: K([0, 0.96], [P / 2, 1.04], [P, 0.96]), op: K([0, 0.35], [P / 2, 0.7], [P, 0.35]) })
]);

// главный приз месяца: фонтан монет и алмазов, лучи, конфетти
fx('vfx_month_prize', { size: [768, 768], frames: 16, dur: 1.6 }, () => [
    arcBurst('Coins', 'coin', { t: 0.08, n: 9, size: 56, speed: 900, spread: 80, life: 1.1, gravY: 1500 }),
    arcBurst('Gems', 'gem', { t: 0.14, n: 6, size: 60, speed: 820, spread: 70, life: 1.1, gravY: 1500 }),
    confettiBurst({ t: 0.1, n: 9, speed: 720, life: 1.3 }),
    SP('Rays', { blend: 'add', shape: 'star', p: { n: 14, inr: 0.12 }, size: 760, color: GOLD, fade: [0, 0, 370, 0.6], end: 1.6,
        rot: K([0, 0, 'lin'], [1.6, 40, 'lin']), scale: K([0, 0.3], [0.35, 1]), op: K([0, 0], [0.15, 0.55], [1.1, 0.45], [1.6, 0]) }),
    ringWave({ t: 0.06, d: 0.55, th: 0.07, size: 300, s0: 0.3, s1: 2.4, color: GOLD, glow: 0.4 }),
    glints({ t: 0.08, n: 14, speed: 420, size: 40 }),
    flash({ t: 0.04, d: 0.45, color: CREAM, size: 480, s1: 1.6 })
]);

// серия: огонёк вспыхнул и вырос, кольцо искр, тёплая вспышка
fx('vfx_streak_up', { size: [256, 256], frames: 12, dur: 0.8 }, () => [
    SP('Flame', { shape: 'tex:fire', size: 120, y: -6, end: 0.7, scale: K([0, 0.6], [0.14, 1.45], [0.3, 1.15], [0.7, 1.25]), op: K([0, 0], [0.05, 1], [0.5, 1], [0.7, 0]) }),
    SP('Flame glow', { blend: 'add', shape: 'soft', size: 220, color: ORANGE, end: 0.7, scale: K([0, 0.4], [0.18, 1.2], [0.7, 1]), op: K([0, 0], [0.1, 0.8], [0.7, 0]) }),
    sparkBurst({ t: 0.12, n: 8, r: 30, speed: 380, size: 34, life: 0.45, em: { shape: 'ring', sx: 30, sy: 30 } }),
    EM('Embers', {
        blend: 'add', start: 0.1, end: 0.16, em: { y: 10, shape: 'circle', sx: 20, sy: 10, dir: 'dir', angle: -90, spread: 70, speed: 260, speedRnd: 0.4, bursts: [{ t: 0, n: 8 }] },
        pt: { sprite: 'ember', size: 22, sizeRnd: 0.4, life: 0.5, lifeRnd: 0.2, drag: 1.5, grad: G([0, LGOLD], [1, ORANGE]), opacityOL: C([0, 1], [1, 0]) }
    }),
    ringWave({ t: 0.1, d: 0.4, th: 0.1, size: 180, s0: 0.4, s1: 1.8, color: ORANGE, glow: 0.4 })
]);

// серия сгорела: огонёк сжимается и гаснет серой струйкой дыма, угольки падают
fx('vfx_streak_lost', { size: [256, 256], frames: 12, dur: 0.9 }, () => [
    SP('Flame', { shape: 'tex:fire', size: 110, y: -6, end: 0.42, scale: K([0, 1], [0.1, 1.08], [0.42, 0.1]), op: K([0, 1], [0.3, 1], [0.42, 0]) }),
    EM('Smoke', {
        start: 0.25, end: 0.6, em: { x: 0, y: -10, shape: 'circle', sx: 8, sy: 8, dir: 'dir', angle: -90, spread: 16, speed: 90, speedRnd: 0.3, rate: 26 },
        pt: { sprite: 'smoke', size: 46, sizeRnd: 0.3, life: 0.55, lifeRnd: 0.2, drag: 0.8, turbAmp: 60, turbFreq: 1.4, spin: 60, spinRnd: 1, rotRnd: 1, grad: G([0, '#B9B4AE'], [1, SMOKE]),
            sizeOL: C([0, 0.5], [1, 1.6]), opacityOL: C([0, 0], [0.2, 0.7], [1, 0]) }
    }),
    EM('Embers', {
        blend: 'add', start: 0.32, end: 0.38, em: { y: 20, shape: 'circle', sx: 14, sy: 6, dir: 'dir', angle: 90, spread: 60, speed: 80, speedRnd: 0.5, bursts: [{ t: 0, n: 3 }] },
        pt: { sprite: 'ember', size: 18, sizeRnd: 0.3, life: 0.5, lifeRnd: 0.2, gravY: 500, grad: G([0, ORANGE], [1, '#7A2E12']), opacityOL: C([0, 1], [1, 0]) }
    })
]);

// покупка алмазов: брызги бирюзовых алмазиков, блики-крестики, кольцо
fx('vfx_gem_burst', { size: [512, 512], frames: 14, dur: 0.9 }, () => [
    EM('Gems', {
        start: 0.04, end: 0.1, em: { shape: 'circle', sx: 30, sy: 30, dir: 'out', speed: 560, speedRnd: 0.35, bursts: [{ t: 0, n: 10 }] },
        pt: { sprite: 'tex:gem', size: 48, sizeRnd: 0.3, life: 0.75, lifeRnd: 0.2, drag: 2.4, gravY: 420, spin: 300, spinRnd: 1, rotRnd: 1,
            sizeOL: C([0, 0.3], [0.15, 1.1], [1, 0.7]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
    }),
    glints({ t: 0.04, n: 14, speed: 380, size: 40, color: MINT }),
    ringWave({ t: 0, d: 0.45, th: 0.08, size: 240, s0: 0.3, s1: 2.1, color: MINT, glow: 0.5 }),
    flash({ t: 0, d: 0.35, color: LMINT, size: 340, s1: 1.4 })
]);

// алмазы летят к счётчику: дуга вверх вправо с блестящим шлейфом
fx('vfx_gems_fly', { size: [512, 256], frames: 12, dur: 0.6 }, () => {
    const out = [];
    [0, 0.05, 0.1].forEach((d, i) => {
        const T = 0.42;
        const path = [[-200, 90 - i * 16], [-40, -110], [110, -40], [210, -80]];
        const p = bezPath(path, d, d + T, 12, u => easeIO(u));
        out.push(SP('Gem ' + (i + 1), { shape: 'tex:gem', size: 60, start: d, end: d + T + 0.04, x: p.x, y: p.y, rot: K([0, -20], [T, 25]),
            scale: K([0, 0.6], [0.1, 1], [T, 0.7]), op: K([0, 0], [0.05, 1], [T, 1], [T + 0.04, 0]) }));
        out.push(EM('Trail ' + (i + 1), {
            blend: 'add', start: d, end: d + T, em: { x: p.x, y: p.y, shape: 'circle', sx: 10, sy: 10, speed: 16, rate: 70 },
            pt: { sprite: 'spark', size: 28, sizeRnd: 0.3, life: 0.22, lifeRnd: 0.2, grad: G([0, WHITE], [1, MINT]), sizeOL: C([0, 1], [1, 0.2]), opacityOL: C([0, 1], [1, 0]) }
        }));
    });
    return [out, flash({ t: 0.5, d: 0.12, x: 210, y: -80, color: LMINT, size: 160, s1: 1.2 })];
});

// блик пробегает по алмазу раз в цикл (карточки магазина, счётчик)
fx('vfx_gem_shine', { size: [256, 256], frames: 12, loop: 2.0 }, ({ P }) => [
    SP('Shine band', { blend: 'add', shape: 'soft', size: 220, aspect: 0.22, color: WHITE, rot: 35, fade: [0, 0, 100, 0.5],
        x: K([0, -170, 'hold'], [0.9, -170, 'lin'], [1.35, 170, 'lin'], [P, 170]), y: K([0, 60, 'hold'], [0.9, 60, 'lin'], [1.35, -60, 'lin'], [P, -60]), scale: 1,
        op: K([0, 0, 'hold'], [0.9, 0, 'lin'], [1.0, 0.85, 'lin'], [1.25, 0.85, 'lin'], [1.35, 0], [P, 0]) }),
    SP('Star glint', { blend: 'add', shape: 'spark', size: 120, color: WHITE, x: 40, y: -40, rot: K([0, 0, 'hold'], [1.3, 0, 'lin'], [1.6, 90], [P, 90]),
        scale: K([0, 0, 'hold'], [1.3, 0], [1.42, 1], [1.6, 0], [P, 0]), op: 1 })
]);

// ускоритель стройки: стрелки часов быстро крутятся, «вжух»-линии
fx('vfx_boost_time', { size: [384, 384], frames: 12, dur: 0.8 }, () => {
    // стрелка от центра наружу: жёсткая группа, поворот по шагам
    const hand = (name, len, th, color, turns, t1) => {
        const gk = [];
        for (let k = 0; k <= 24; k++) { const u = k / 24; gk.push({ t: t1 * u, r: 360 * turns * easeOut(u, 2), m: 'lin' }); }
        return rigid([Object.assign({ name, color, x: 0, y: -len / 2 + th / 2 }, bar(th, len))], gk, { end: 0.75, op: K([0, 0], [0.06, 1], [0.6, 1], [0.75, 0]) });
    };
    return [
        SP('Pin', { shape: 'dot', size: 26, color: GOLD, end: 0.75, scale: 1, op: K([0, 0], [0.06, 1], [0.6, 1], [0.75, 0]) }),
        hand('Hand long', 80, 12, BROWN, 3, 0.6), hand('Hand short', 54, 16, GREEN, 1, 0.6),
        SP('Dial', { shape: 'dot', size: 200, color: CREAM, end: 0.75, scale: K([0, 0.6], [0.12, 1.05], [0.2, 1]), op: K([0, 0], [0.06, 1], [0.6, 1], [0.75, 0]) }),
        SP('Dial outline', { shape: 'dot', size: 222, color: BROWN, end: 0.75, scale: K([0, 0.6], [0.12, 1.05], [0.2, 1]), op: K([0, 0], [0.06, 1], [0.6, 1], [0.75, 0]) }),
        EM('Whoosh', {
            blend: 'add', start: 0.05, end: 0.5, em: { shape: 'ring', sx: 140, sy: 140, dir: 'omni', speed: 260, speedRnd: 0.3, rate: 40 },
            pt: { sprite: 'streak', size: 34, sizeRnd: 0.3, life: 0.2, lifeRnd: 0.2, alignVel: true, stretch: 2, grad: G([0, WHITE], [1, LGREEN]), opacityOL: C([0, 1], [1, 0]) }
        }),
        ringWave({ t: 0.55, d: 0.25, th: 0.1, size: 220, s0: 0.8, s1: 1.6, color: LGREEN, glow: 0.4 })
    ];
});

// мгновенная стройка: золотой молоток бьёт, ударная волна, щепки, звёзды
fx('vfx_hammer_instant', { size: [512, 512], frames: 16, dur: 1.0 }, () => {
    const T = 0.28;
    const hammer = [
        Object.assign({ name: 'Handle', color: '#C98B4F', x: 0, y: 70 }, bar(26, 150)),
        Object.assign({ name: 'Handle outline', color: BROWN, x: 0, y: 70 }, bar(38, 162)),
        Object.assign({ name: 'Head', color: GOLD, x: 0, y: -20 }, bar(130, 62)),
        Object.assign({ name: 'Head outline', color: BROWN, x: 0, y: -20 }, bar(144, 76))
    ];
    const gk = [{ t: 0, x: 120, y: -150, r: 50 }, { t: T * 0.6, x: 130, y: -160, r: 70 }, { t: T, x: 30, y: -40, r: -40, m: 'lin' }, { t: T + 0.12, x: 50, y: -70, r: -10 }, { t: 0.8, x: 60, y: -90, r: 5 }];
    return [
        rigid(hammer, gk, { end: 0.85, op: K([0, 0], [0.06, 1], [0.65, 1], [0.85, 0]) }),
        EM('Chips', {
            start: T, end: T + 0.05, em: { x: -40, y: 40, shape: 'point', dir: 'dir', angle: -90, spread: 140, speed: 480, speedRnd: 0.35, bursts: [{ t: 0, n: 8 }] },
            pt: { sprite: 'shard', size: 24, sizeRnd: 0.3, life: 0.6, lifeRnd: 0.15, gravY: 1300, spin: 600, spinRnd: 1, rotRnd: 1, grad: G1('#C98B4F'), opacityOL: C([0, 1], [0.8, 1], [1, 0]) }
        }),
        EM('Stars', {
            start: T, end: T + 0.05, em: { x: -40, y: 30, shape: 'circle', sx: 20, sy: 20, dir: 'out', speed: 440, speedRnd: 0.3, bursts: [{ t: 0, n: 6 }] },
            pt: { sprite: 'tex:star', size: 46, sizeRnd: 0.25, life: 0.6, lifeRnd: 0.15, drag: 2.6, gravY: 200, spin: 220, spinRnd: 1, rotRnd: 1, sizeOL: C([0, 0.3], [0.2, 1.1], [1, 0.6]), opacityOL: C([0, 1], [0.75, 1], [1, 0]) }
        }),
        ringWave({ t: T, d: 0.45, th: 0.1, size: 220, aspect: 2.2, x: -40, y: 60, s0: 0.3, s1: 2.2, color: GOLD, glow: 0.4 }),
        flash({ t: T, d: 0.35, x: -40, y: 40, color: CREAM, size: 320, s1: 1.4 })
    ];
});

// Золотой путь куплен: замки на золотом ряду лопаются слева направо, золотая волна по ряду
fx('vfx_gold_path_unlock', { size: [1024, 256], frames: 16, dur: 1.4 }, () => {
    const out = [];
    const xs = [-200, -100, 0, 100, 200];
    xs.forEach((x, i) => {
        const t = 0.1 + i * 0.16;
        out.push(
            SP('Lock ' + (i + 1), { shape: 'tex:lock', size: 60, x, y: 0, end: t + 0.12, scale: K([0, 1], [t, 1], [t + 0.06, 1.3], [t + 0.12, 0.3]), op: K([0, 1], [t, 1], [t + 0.12, 0]) }),
            sparkBurst({ name: 'Pop ' + (i + 1), t: t + 0.04, x, n: 6, r: 10, speed: 220, size: 24, life: 0.4 }),
            flash({ name: 'Pop flash ' + (i + 1), t: t + 0.03, d: 0.25, x, size: 110, color: LGOLD, s1: 1.2 })
        );
    });
    out.push(SP('Gold wave', { blend: 'add', shape: 'soft', size: 160, aspect: 0.6, color: GOLD, end: 1.3, y: 0,
        x: K([0, -300, 'lin'], [1.0, 300, 'lin']), scale: 1, op: K([0, 0], [0.1, 0.8], [0.9, 0.8], [1.2, 0]) }));
    return out;
});

// новая сложность: три перчика по очереди вспыхивают огоньками, салют, конфетти
fx('vfx_difficulty_unlock', { size: [768, 768], frames: 16, dur: 1.6 }, () => {
    const xs = [-200, 0, 200];
    return [
        xs.map((x, i) => {
            const t = 0.1 + i * 0.22;
            return [
                SP('Pepper ' + (i + 1), { shape: 'tex:pepper', size: 140, x, y: 40, start: t, end: 1.5, scale: K([0, 0], [0.12, 1.25], [0.24, 1]), op: K([0, 1], [1.2, 1], [1.4, 0]) }),
                SP('Pepper fire ' + (i + 1), { shape: 'tex:fire', size: 90, x, y: -40, start: t + 0.1, end: 1.5, scale: K([0, 0.3], [0.15, 1.1], [0.3, 0.9], [0.5, 1.05], [0.7, 0.95]), op: K([0, 0], [0.08, 1], [1.1, 1], [1.4, 0]) }),
                flash({ name: 'Pepper flash ' + (i + 1), t, d: 0.3, x, y: 40, color: ORANGE, size: 240, s1: 1.3, op: 0.8 })
            ];
        }),
        firework({ i: 1, t: 0.85, x: -220, y: -230, c: LGOLD }),
        firework({ i: 2, t: 1.0, x: 230, y: -210, c: '#FFB18A', c2: RED }),
        confettiBurst({ t: 0.8, n: 8, speed: 650, life: 1.0, y: 40 })
    ];
});

// касса «дзынь»: вспышка, монеты подпрыгивают вверх
fx('vfx_cash_collect', { size: [384, 384], frames: 12, dur: 0.8 }, () => [
    arcBurst('Coins', 'coin', { t: 0.06, y: 40, n: 6, size: 52, speed: 620, spread: 60, life: 0.75 }),
    SP('Ding', { blend: 'add', shape: 'spark', size: 200, color: WHITE, x: 60, y: -60, start: 0.04, end: 0.34, rot: K([0, 0], [0.3, 60]), scale: K([0, 0], [0.08, 1], [0.3, 0]), op: 1 }),
    glints({ t: 0.06, n: 8, speed: 300, size: 32 }),
    ringWave({ t: 0.02, d: 0.4, th: 0.1, size: 200, s0: 0.4, s1: 1.8, color: GOLD, glow: 0.4 }),
    flash({ t: 0.02, d: 0.3, color: CREAM, size: 280, s1: 1.3 })
]);

// метка будущего улучшения: мягкое кольцо-пульс (цикл)
fx('vfx_reno_marker_pulse', { size: [256, 256], frames: 12, loop: 1.2 }, ({ P }) => [
    // обычное смешивание, не «сложение света»: на светлых фасадах ремонта аддитивное кольцо пропадало.
    // Кольцо — во весь кадр: метка (≈ 60 % кадра) закрывает середину, видна волна вокруг неё
    SP('Pulse 1', { shape: 'ring', p: { th: 0.08 }, size: 480, color: GREEN, scale: K([0, 0.62, 'lin'], [P, 1.0, 'lin']), op: K([0, 1, 'lin'], [P, 0, 'lin']) }),
    SP('Pulse 1 light', { shape: 'ring', p: { th: 0.035 }, size: 474, color: '#E9FFD6', scale: K([0, 0.62, 'lin'], [P, 1.0, 'lin']), op: K([0, 1, 'lin'], [P, 0, 'lin']) }),
    SP('Pulse 2', { shape: 'ring', p: { th: 0.08 }, size: 480, color: GREEN,
        scale: K([0, 0.81, 'lin'], [P / 2, 1.0, 'hold'], [P / 2 + 0.001, 0.62, 'lin'], [P, 0.81, 'lin']), op: K([0, 0.5, 'lin'], [P / 2, 0, 'hold'], [P / 2 + 0.001, 1, 'lin'], [P, 0.5, 'lin']) })
]);

// отдел альбома собран: золотая волна по странице, рамка-аватарка вылетает и встаёт в центр
fx('vfx_dept_complete', { size: [768, 768], frames: 16, dur: 1.4 }, () => {
    const T = 0.55;
    return [
        SP('Gold sweep', { blend: 'add', shape: 'soft', size: 520, aspect: 0.35, color: GOLD, rot: 20, end: 0.7,
            x: K([0, -480, 'lin'], [0.6, 480, 'lin']), scale: 1, op: K([0, 0], [0.1, 0.7], [0.5, 0.7], [0.7, 0]) }),
        EM('Winks', {
            blend: 'add', start: 0.05, end: 0.6, em: { shape: 'box', sx: 300, sy: 280, speed: 0, rate: 22 },
            pt: { sprite: 'spark', size: 44, sizeRnd: 0.4, life: 0.35, lifeRnd: 0.2, grad: G([0, WHITE], [1, GOLD]), sizeOL: BELL, opacityOL: ONE, rotRnd: 0.1 }
        }),
        SP('Frame ring', { shape: 'ring', p: { th: 0.14 }, size: 220, color: GOLD, glow: 0.3, start: T - 0.25, end: 1.4,
            y: K([0, 260], [0.25, 0]), scale: K([0, 0.3], [0.25, 1.15], [0.35, 1]), op: K([0, 0], [0.08, 1], [0.7, 1], [0.85, 0]) }),
        SP('Frame ring outline', { shape: 'ring', p: { th: 0.22 }, size: 236, color: BROWN, start: T - 0.25, end: 1.4,
            y: K([0, 260], [0.25, 0]), scale: K([0, 0.3], [0.25, 1.15], [0.35, 1]), op: K([0, 0], [0.08, 1], [0.7, 1], [0.85, 0]) }),
        sparkBurst({ t: T, n: 9, r: 60, speed: 480, size: 44, life: 0.6 }),
        ringWave({ t: T, d: 0.5, th: 0.07, size: 260, s0: 0.5, s1: 2.3, color: GOLD, glow: 0.4 }),
        flash({ t: T, d: 0.4, color: CREAM, size: 380, s1: 1.4 })
    ];
});

// строка игрока поднялась: зелёная стрелка вверх, блеск по строке
fx('vfx_rank_up', { size: [768, 192], frames: 12, dur: 0.8 }, () => {
    const ay = K([0, 26], [0.3, -14], [0.6, -24]), sy = K([0, 50], [0.3, 10], [0.6, 0]);
    const op = K([0, 0], [0.08, 1], [0.5, 1], [0.7, 0]);
    return [
        SP('Shine', { blend: 'add', shape: 'soft', size: 150, aspect: 0.4, color: WHITE, rot: 20, end: 0.7, x: K([0, -300, 'lin'], [0.6, 300, 'lin']), scale: 1, op: K([0, 0], [0.1, 0.7], [0.5, 0.7], [0.7, 0]) }),
        SP('Arrow head', { shape: 'poly', p: { n: 3 }, size: 48, color: LGREEN, x: -200, y: ay, end: 0.7, scale: 1, op }),
        SP('Arrow stem', Object.assign({ color: LGREEN, x: -200, y: sy, end: 0.7, scale: 1, op }, bar(16, 28))),
        SP('Arrow head outline', { shape: 'poly', p: { n: 3 }, size: 62, color: BROWN, x: -200, y: ay, end: 0.7, scale: 1, op }),
        SP('Arrow stem outline', Object.assign({ color: BROWN, x: -200, y: sy, end: 0.7, scale: 1, op }, bar(24, 36))),
        sparkBurst({ t: 0.3, x: -200, y: -20, n: 5, r: 10, speed: 200, size: 22, life: 0.35, pt: { grad: G1(LGREEN) } })
    ];
});

// надел скин: вихрь искр вокруг аватарки, хлопок
fx('vfx_skin_equip', { size: [384, 384], frames: 12, dur: 0.7 }, () => {
    const out = [];
    for (let i = 0; i < 6; i++) {
        const o = orbit({ r0: 150, r1: 30, a0: i * 60, turns: 0.8, t0: 0, t1: 0.4, n: 16, ease: u => easeIn(u, 1.5) });
        out.push(SP('Swirl ' + (i + 1), { blend: 'add', shape: 'spark', size: 50, color: i % 2 ? LGOLD : WHITE, x: o.x, y: o.y, end: 0.42, rot: K([0, 0], [0.4, 180]), scale: 1, op: K([0, 0], [0.05, 1], [0.42, 0.6]) }));
    }
    return [out,
        sparkBurst({ t: 0.4, n: 8, r: 30, speed: 420, size: 40, life: 0.3 }),
        ringWave({ t: 0.4, d: 0.3, th: 0.1, size: 220, s0: 0.4, s1: 1.8, color: GOLD, glow: 0.4 }),
        flash({ t: 0.38, d: 0.3, color: CREAM, size: 300, s1: 1.3 })];
});

// ---------- запись ----------
const only = process.argv.slice(2);
let n = 0;
for (const e of EFFECTS) {
    if (only.length && !only.includes(e.name)) continue;
    const file = make(e);
    fs.writeFileSync(path.join(LIB, e.name + '.json'), JSON.stringify(file));
    n++;
}
console.log('written', n, 'of', EFFECTS.length);

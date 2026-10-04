# -*- coding: utf-8 -*-
"""Превью раскладки ремонта: собирает кадр 1920x1080 для каждого этапа (район) по reno_layout.json.

python reno_preview.py <out_dir> [district ...]   — превью
python reno_preview.py --cs                      — записать Editor/RenoLayoutData.cs для SceneBuilder
"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "..", "Assets", "AllOnShelves", "Art")
L = json.load(open(os.path.join(HERE, "reno_layout.json"), encoding="utf-8"))


def load(name):
    for root, _, files in os.walk(ART):
        if name + ".png" in files:
            return Image.open(os.path.join(root, name + ".png")).convert("RGBA")
    raise FileNotFoundError(name)


def by_h(im, h):
    w = int(im.width * h / im.height)
    return im.resize((max(1, w), int(h)), Image.LANCZOS)


def shadow(canvas, cx, cy, w):
    sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(sh)
    d.ellipse((cx - w / 2, cy - w * 0.09, cx + w / 2, cy + w * 0.09), fill=(40, 30, 20, 110))
    canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(12)))


def place(canvas, im, x, y):
    """x, y — координаты холста от центра (y вверх); низ-центр картинки."""
    px, py = int(960 + x - im.width / 2), int(540 - y - im.height)
    canvas.alpha_composite(im, (px, py))


def slot_pos(slot, cx, hw, base):
    side, extra, dy = slot
    sgn = 1 if side > 0 else -1
    return cx + side * hw + (sgn * extra if abs(side) >= 1 else 0), base + dy


def ground_pos(k, v, cx, hw, base):
    """Позиция наземного предмета: слот + ограничение краями сцены (предмет целиком виден)."""
    x, y = slot_pos(L["slots"][v["slot"]], cx, hw, base)
    im = load(k)
    half = im.width * v["h"] / im.height / 2
    x = max(L["scene"]["xmin"] + half, min(L["scene"]["xmax"] - half, x))
    return x, y


NL = chr(10)


def emit_cs(path):
    """C#-таблица для SceneBuilder: id, тип, x, y (низ-центр, от центра сцены), высота."""
    cx, base = L["scene"]["cx"], L["scene"]["base"]
    rows = []
    for sid, st in L["stores"].items():
        b = load(f"meta_{sid}_new"); W = st["w"]; H = W * b.height / b.width; hw = W / 2
        rows.append(f'            {{ "{sid}", new Store({cx}f, {base}f, {W}f) }},')
    items = []
    for k, v in L["items"].items():
        d = int(k[6:8])
        sid = next(s for s, st in L["stores"].items() if d in st["districts"])
        st = L["stores"][sid]; b = load(f"meta_{sid}_new"); W = st["w"]; H = W * b.height / b.width; hw = W / 2
        if v["kind"] == "ground":
            x, y = ground_pos(k, v, cx, hw, base)
            items.append(f'            {{ "{k}", new Item(false, {x:.0f}f, {y:.0f}f, {v["h"]}f) }},')
        elif v["kind"] == "attach":
            items.append(f'            {{ "{k}", new Item(true, {cx + v["x"] * W:.0f}f, {base + v["y"] * H:.0f}f, {v["h"]}f) }},')
    cs = """// Сгенерировано Tools/ArtPipeline/reno_preview.py из reno_layout.json — правьте JSON и перегенерируйте.
using System.Collections.Generic;

namespace AllOnShelves.EditorTools
{
    /// <summary>Раскладка сцены ремонта: здание и предметы (низ-центр, координаты 1920×1080 от центра сцены).</summary>
    public static class RenoLayoutData
    {
        public struct Store { public float X, Base, Width; public Store(float x, float b, float w) { X = x; Base = b; Width = w; } }
        public struct Item { public bool Attached; public float X, Y, Height; public Item(bool a, float x, float y, float h) { Attached = a; X = x; Y = y; Height = h; } }

        public static readonly Dictionary<string, Store> Stores = new Dictionary<string, Store>
        {
""" + NL.join(rows) + """
        };

        /// <summary>Предметы, которых нет в словаре, — только на карточке (нарисованы как весь магазин целиком).</summary>
        public static readonly Dictionary<string, Item> Items = new Dictionary<string, Item>
        {
""" + NL.join(items) + """
        };
    }
}
"""
    open(path, "w", encoding="utf-8").write(cs)


def render(store_id, district, stage_new, out):
    st = L["stores"][store_id]
    cx, base = L["scene"]["cx"], L["scene"]["base"]
    canvas = load("bg_meta_" + store_id).resize((1920, 1080), Image.LANCZOS)
    b = load(f"meta_{store_id}_{'new' if stage_new else 'worn'}")
    W = st["w"]; b = b.resize((W, int(W * b.height / b.width)), Image.LANCZOS); H = b.height
    hw = W / 2
    items = {k: v for k, v in L["items"].items() if int(k[6:8]) in st["districts"]}
    ground = [(k, v) for k, v in items.items() if v["kind"] == "ground" and int(k[6:8]) == district]
    attach = [(k, v) for k, v in items.items() if v["kind"] == "attach"]
    placed = []
    for k, v in ground:
        x, y = ground_pos(k, v, cx, hw, base)
        placed.append((y, k, v, x))
    placed.sort(key=lambda t: -t[0])  # дальние (выше) — раньше
    back = [p for p in placed if p[0] > base]
    front = [p for p in placed if p[0] <= base]
    for y, k, v, x in back:
        im = by_h(load(k), v["h"]); shadow(canvas, 960 + x, 540 - y, im.width * 0.9); place(canvas, im, x, y)
    shadow(canvas, 960 + cx, 540 - base, W * 1.05)
    place(canvas, b, cx, base)
    for k, v in attach:
        im = by_h(load(k), v["h"]); place(canvas, im, cx + v["x"] * W, base + v["y"] * H)
    for y, k, v, x in front:
        im = by_h(load(k), v["h"]); shadow(canvas, 960 + x, 540 - y, im.width * 0.9); place(canvas, im, x, y)
    d = ImageDraw.Draw(canvas)
    d.rectangle((960 + 300, 60, 1920 - 20, 1020), fill=(245, 235, 215, 200))
    canvas.convert("RGB").resize((960, 540), Image.LANCZOS).save(os.path.join(out, f"reno_d{district:02d}.jpg"), quality=82)


if __name__ == "__main__":
    out = sys.argv[1]
    if out == "--cs":
        emit_cs(os.path.join(HERE, "..", "..", "Assets", "AllOnShelves", "Editor", "RenoLayoutData.cs"))
        sys.exit(0)
    only = [int(a) for a in sys.argv[2:]]
    for sid, st in L["stores"].items():
        for i, d in enumerate(st["districts"]):
            if only and d not in only: continue
            render(sid, d, True, out)

# -*- coding: utf-8 -*-
"""Нормализация сгенерированной графики «Всё по полкам!» для Unity.

Берёт исходники из yandex_games/Assets/AllOnShelves/Art, обрезает прозрачные поля,
уменьшает до игрового размера (по длинной стороне), добавляет прозрачный отступ
и кладёт в Yandex_Claude/Assets/AllOnShelves/Art с той же структурой папок.
Полнокадровые картинки (фоны, сегменты карты, разворот альбома) не обрезаются.

Запуск: python normalize_art.py
"""
import os
import sys
import time
from PIL import Image

SRC = r"D:\Work\ЯндексИгры\LaserSiege\yandex_games\Assets\AllOnShelves\Art"
DST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "AllOnShelves", "Art")
MARKETING = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Marketing")

FULL_FRAME = ("bg_", "team_build_screen_bg", "map_segment_", "map_town_district_", "album_book.", "album_desk",
              # страницы нового магазина и рейтинга (01.10.2026): весь экран одной картинкой
              "shop_page_", "lb_page_",
              # площадь Торгового дома (03.10.2026)
              "team_bg",
              # v4 (03.10.2026): экраны «Завоз дня» и «Задания» — фон во весь экран
              "daily_empty", "quests_empty")
# та же картинка уже лежит как bg_hub_start.png — второй раз не тащим
SKIP = {"bg_main_menu_logo_clean_v2.png",
        # строки рейтинга пришли сплющенными (полоса 40 px из 100, круги стали овалами) — не берём,
        # строки собираются из ровной плашки ui_tile_button (01.10.2026), промт на перегенерацию — в паке рейтинга
        "ui_lb_row.png", "ui_lb_row_me.png", "ui_lb_row_top.png", "ui_lb_reward_row.png",
        # сияние и эффекты копилки сделаны в Arcaidia Effector (03.10.2026) — картинки не нужны
        "fx_piggy_glow.png", "vfx_piggy_break.png", "vfx_piggy_coin_drop.png"}
# карты районов (22.09.2026): новые map_town_district_NN (каждый район свой) встают на место map_segment_NN;
# прежние сегменты, их *_clean и живописный набор map_route_unique_* (другой стиль) не переносим
RENAME = {f"map_town_district_{d:02d}.png": f"map_segment_{d:02d}.png" for d in range(1, 16)}
# серии «лего» ремонта режет отдельный скрипт lego_build.py — сюда их не тащим
SKIP_PREFIX = ("map_segment_", "map_route_unique_", "meta_store", "_", "reno_concept",
               # Торговый дом (03.10.2026): комнаты и слои дома кладёт room_build.py без обрезки (слои совпадают
               # пиксель в пиксель); дом-разрез v3 отменён; мешочек пришёл сценой с фоном — не берём.
               # Лифт заменило окно этажа с ячейками (03.10.2026) — не тащим в сборку
               "room_0", "room_1", "house_open", "house_scaffold", "team_house_step", "team_room_build",
               "team_scaffold_floor", "team_coin_bag", "btn_team", "ui_elevator", "icon_elevator")
# замок поверх комнаты (v3.2) — кадр комнаты целиком: поля не обрезаем, иначе цепи съедут с углов
# одно имя в двух папках источника: берём новую, старую не тащим (03.10.2026: карточка сотрудника v3.3 в UI → v4 в Team)
SKIP_PATHS = {"UI/ui_staff_card.png",
              # закрытая карточка режима пришла другой формы (широкая, не как ui_diff_card) — закрытую рисуем серой
              "Icons/ui_diff_card_locked.png",
              # «Для дома» пришла со старой доской вкладок (горит «Монеты») — окно «Алмазы» обе вкладки рисует на shop_page_6_gems
              "Shop/shop_page_7_house.png"}
NO_CROP = ("room_lock_overlay",)
# пользователь вернул прежний вид уровня и ценники на карте (20.09.2026):
# новые картинки поля и узлов карты ему не понравились — не перетаскивать их поверх
KEEP_OLD = {
    "brd_cart_4", "brd_cart_5", "brd_cart_5_farm", "brd_cart_5_gold", "brd_cart_5_night",
    "brd_cart_5_winter", "brd_cart_tipped", "brd_shelf_frame", "brd_section", "brd_section_big",
    "brd_slot_glow", "brd_slot_outline", "brd_access_ring", "brd_belt_tile", "brd_belt_roller",
    "brd_tag_base", "brd_hatch", "brd_freezer_open", "brd_freezer_closed",
    "obj_box_open", "obj_box_closed", "obj_lock_open", "obj_lock_closed", "obj_pallet",
    "obj_sale_tag", "obj_tape",
    "map_node_normal", "map_node_locked", "map_node_hard", "map_node_superhard", "map_node_revision",
    "ui_pill_counter",
    # кнопка «▶ x2»: 26.09.2026 исходник заменили сплющенным (3,08:1 вместо 2,04:1) —
    # пользователь вернул прежнюю из бэкапа 21.09, новую не переносить
    "btn_video_x2",
    # у панелей посередине было размытое пятно (брак генерации) — убрано fix_panel_smudge.py
    # 29.09.2026; исходники с пятном не переносить поверх исправленных
    "ui_panel_festive", "ui_panel_dark", "ui_panel_shop",
    # наложения поверх товара: новые версии нарисованы как отдельные иллюстрации, а не как накладки
    "ovl_timer_badge", "ovl_frost", "ovl_fly", "ovl_stink",
    # альбом: у новой книги чёрный фон, пачки потеряли енота
    "album_book", "album_pack_closed", "album_pack_open",
}


def target(name):
    """Максимальный размер по длинной стороне."""
    if name.startswith("staff_") and name.endswith("_face.png"):
        return 256   # значок сотрудника
    rules = [
        # v4 (03.10.2026): эти имена должны сработать раньше общих «staff_», «daily_», «icon_», «ui_»
        ("staff_picker_empty", 1600), ("daily_dot", 96),
        # режимы сложности (v4): баннер «Новая сложность», карточка режима, перчики
        ("diff_unlock_banner", 1280), ("ui_diff_card", 640), ("icon_diff_", 256),
        # дорожки «Звёздного пути», замок золотой строки, «+» на метках ремонта, лента-закладка альбома, ползунок рейтинга
        ("ui_track_row_", 1024), ("ui_track_lock_gold", 160), ("ui_badge_plus", 128), ("album_tab", 640),
        ("ui_scroll_knob_v", 192), ("ui_scroll_track_v", 384), ("icon_dept_bonus", 192), ("daily_empty", 1920),
        ("icon_boost_", 256), ("icon_rule_", 192), ("icon_challenge", 256), ("icon_streak_lost", 256),
        ("icon_gem_small", 96), ("icon_gem", 192), ("quest_token", 160), ("gem_pack_", 512), ("chest_", 384),
        ("ui_mech_socket", 256), ("ui_staff_badge_level", 256), ("ui_staff_card", 512), ("ui_quest_note", 768),
        ("ui_streak_plate", 1024), ("ui_streak_step", 160),
        ("item_", 256), ("sticker_", 192), ("ovl_", 192), ("obj_", 256),
        ("brd_cart", 1024), ("brd_belt_tile", 512), ("brd_section", 640), ("brd_freezer", 640),
        ("brd_shelf_frame", 512), ("brd_hatch", 420), ("brd_", 256),
        ("bg_", 1280), ("team_bg", 1672), ("house_chain", 1024), ("house_padlock", 256), ("house_", 256),
        # Торговый дом v3.2 (03.10.2026): сотрудник в комнате ~450 точек, значок — до 200
        ("staff_", 512), ("room_lock_overlay", 1024), ("team_crane", 512),
        ("ui_floor_panel", 1024), ("ui_room_cell", 512), ("piggy_", 384), ("map_segment_", 1672), ("map_town_district_", 1672), ("map_", 256),
        # v4 (03.10.2026): значки механик в окне смены ~140 точек, касса и стройка в доме — крупные; фон стройки во весь
        # экран; окна-«пустышки» из концептов (shift_empty и т. п.) — крупные плашки, игра кладёт значки по их пикселям
        ("icon_mech_", 256), ("icon_cash_register", 256), ("icon_build_spot", 256),
        ("team_build_screen_bg", 1920), ("shift_empty", 1600), ("staff_picker_empty", 1600),
        ("daily_empty", 1920), ("quests_empty", 1920), ("streak_empty", 1600),
        ("chr_", 512), ("icon_", 128),
        # готовые кнопки (иконка уже нарисована внутри)
        ("btn_play", 640), ("btn_settings_wide", 512), ("btn_next", 512), ("btn_video_x2", 512),
        ("btn_", 384), ("tile_", 256),
        # плашки и кнопки выводятся крупно (окно победы — 640+ точек): в 384 они мылились (22.09.2026).
        # 9-slice-рамки (ui_progress_*, ui_tile_button) не увеличиваем: у них толщина края зависит от пикселей
        ("ui_progress", 384), ("ui_tile_button", 640),
        ("ui_btn", 512), ("ui_panel", 1024), ("ui_pill", 384), ("ui_speech", 768),
        ("ui_header", 768), ("ui_sign", 768), ("ui_tab", 512), ("ui_title", 512),
        ("ui_ribbon_banner", 768), ("ui_ribbon_title", 1024),
        # плашки из пака 26.09: их рисуют крупно, мельче 768 они мылятся
        ("ui_toast", 1024), ("ui_plate_wide", 1024), ("ui_tooltip", 768),
        ("ui_tile_slot", 512), ("ui_door_plate", 768), ("ui_elevator", 256), ("ui_badge_round", 256), ("ui_", 512),
        ("meta_d", 256), ("meta_", 640), ("album_desk", 1672), ("album_book", 1280), ("album_", 384),
        ("daily_", 512), ("shop_page_", 1920), ("lb_page_", 1920), ("shop_", 512),
        ("av_", 256), ("frame_", 320), ("lb_claim", 384), ("lb_", 256), ("fx_", 128), ("logo_", 1024), ("mkt_", 512),
    ]
    for prefix, size in rules:
        if name.startswith(prefix):
            return size
    return 512


def hole_warning(name, im):
    """Плашка с прозрачной дыркой посередине — брак генерации (26.09.2026).

    Формулировка «середина ровная» когда-то заставила нейронку вырезать центр в прозрачность.
    Такая рамка в игре покажет сквозь себя затемнённый экран, поэтому о ней надо знать сразу.
    """
    if not name.startswith(("ui_panel", "ui_plate", "ui_toast", "ui_tooltip", "ui_tile_slot",
                            "ui_badge", "ui_ribbon", "ui_header", "ui_speech")):
        return
    import numpy as np
    from scipy import ndimage
    a = np.asarray(im.getchannel("A"))
    clear = a < 96
    if not clear.any():
        return
    # дырка = прозрачное пятно, до которого НЕ дотянуться от края картинки
    lab, n = ndimage.label(clear)
    edge = set(lab[0].tolist()) | set(lab[-1].tolist()) | set(lab[:, 0].tolist()) | set(lab[:, -1].tolist())
    inside = np.isin(lab, [k for k in range(1, n + 1) if k not in edge])
    share = inside.mean()
    if share > 0.01:
        print(f"  ! {name}: внутри прозрачная дырка на {share * 100:.0f}% площади — "
              f"перегенерировать (см. Docs/PROMPTS_Ларьки_ВсёПоПолкам_26-09.txt, часть 3)")


def process(path, out_path):
    name = os.path.basename(path)
    im = Image.open(path).convert("RGBA")
    hole_warning(name, im)
    t = target(name)
    if name.startswith(NO_CROP):
        im.thumbnail((t, t), Image.LANCZOS)
    elif name.startswith(FULL_FRAME):
        im = im.convert("RGB").convert("RGBA")
        im.thumbnail((t, t), Image.LANCZOS)
    else:
        bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
        if bbox:
            im = im.crop(bbox)
        inner = int(t * 0.94)
        im.thumbnail((inner, inner), Image.LANCZOS)
        pad = max(2, int(max(im.size) * 0.03))
        canvas = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
        canvas.paste(im, (pad, pad))
        im = canvas
    # стороны кратны 4 — иначе DXT/crunch не применяется и текстура уходит в сборку несжатой
    w4, h4 = (im.width + 3) // 4 * 4, (im.height + 3) // 4 * 4
    if (w4, h4) != im.size:
        if name.startswith(FULL_FRAME):
            im = im.resize((w4, h4), Image.LANCZOS)
        else:
            c = Image.new("RGBA", (w4, h4), (0, 0, 0, 0))
            c.paste(im, ((w4 - im.width) // 2, (h4 - im.height) // 2))
            im = c
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    if name.startswith(FULL_FRAME):
        im.convert("RGB").save(out_path, optimize=True)
    else:
        im.save(out_path, optimize=True)
    return im.size


def main():
    # python normalize_art.py [--newer ЧАСОВ] — только свежие файлы, иначе всё подряд
    newer = 0.0
    if "--newer" in sys.argv:
        newer = time.time() - float(sys.argv[sys.argv.index("--newer") + 1]) * 3600
    count, total = 0, 0
    for root, _, files in os.walk(SRC):
        for f in files:
            if not f.lower().endswith(".png") or f in SKIP or f.startswith(SKIP_PREFIX) or "_concept" in f:   # концепты — только образец
                continue
            if os.path.splitext(f)[0] in KEEP_OLD:
                continue  # прежний вид, возвращён по просьбе пользователя
            if newer and os.path.getmtime(os.path.join(root, f)) < newer:
                continue
            rel = os.path.relpath(os.path.join(root, RENAME.get(f, f)), SRC)
            if rel.replace("\\", "/") in SKIP_PATHS:
                continue
            dst_root = MARKETING if f.startswith("mkt_") else DST
            out = os.path.join(dst_root, rel)
            if "--missing" in sys.argv and os.path.exists(out):
                continue  # только те, которых у нас ещё нет
            process(os.path.join(root, f), out)
            count += 1
            total += os.path.getsize(out)
    print(f"{count} files, {total / 2 ** 20:.1f} MB")


if __name__ == "__main__":
    sys.exit(main())

# -*- coding: utf-8 -*-
"""Симулятор экономики v6 «Всё по полкам!» — финальная настройка (04.10.2026).

python Tools/economy_sim_v6.py                     — сравнить build (как в билде 24) и v6, 8 прогонов
python Tools/economy_sim_v6.py --preset v6 -v      — подробно: по отрезкам по 10 уровней
python Tools/economy_sim_v6.py --preset v6 --table — таблица всех цен v6 (ремонт, комнаты, декор с уровнями открытия)

Что считает сверх economy_sim_v5.py:
  * декор открывается по уровням (через N уровней после заселения) — нельзя прокачать жильца до 4★ сразу;
  * цены декора считаются от дохода за уровень в момент открытия (W) — дорожают вместе с игроком;
  * «избыток»: монеты на руках после всех покупок, когда игроку НЕЧЕГО купить (главный враг — сейчас это белка до 4★);
  * «выбор»: сколько доступных покупок игрок не может оплатить сразу (2–3 = он думает, что важнее);
  * «упор»: дни, когда следующий район закрыт ремонтом и монет не хватает, — время гринда;
  * платящий покупает самый дешёвый полезный пак, когда упёрся больше чем на полдня.
Числа — Tools/economy_v6.json. Уровни и формула монет за уровень — из кода (levels.json, Economy.WinCoins).
"""
import argparse
import io
import json
import os
import random
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import economy_sim as V2  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CFG = json.load(io.open(os.path.join(ROOT, "Tools", "economy_v6.json"), encoding="utf-8"))
LEVELS = V2.load_levels()
WIN_PENALTY = [0.0, 0.08, 0.15]
TIME_MULT = [1.0, 1.15, 1.3]
SKIP = [(0, 0), (5, 1), (30, 3), (60, 5), (240, 15), (1440, 60), (10080, 300)]


def W(level_id):
    """Средний доход за первое прохождение уровня около level_id (без «×2») — единица цены."""
    lo, hi = max(1, level_id - 5), min(150, level_id + 5)
    xs = [V2.win_coins(l, 2) for l in LEVELS if lo <= l["id"] <= hi]
    return sum(xs) / len(xs)


def r5(x, step=10):
    return int(round(x / step) * step)


def skip_cost(minutes):
    if minutes <= 0:
        return 0
    for (m0, c0), (m1, c1) in zip(SKIP, SKIP[1:]):
        if minutes <= m1:
            return max(1, int(round(c0 + (c1 - c0) * (minutes - m0) / (m1 - m0))))
    return SKIP[-1][1]


def build_prices(P):
    """Цены и уровни открытия всех трат меты из пресета."""
    _, _, items_per, _, first = V2.load_code()
    reno = {}
    for d in range(1, 16):
        n = items_per.get(d, 9)
        tot = P["stage_cost"][d]
        w = [0.7 + 0.6 * i / max(1, n - 1) for i in range(n)]
        ws, acc, cs = sum(w), 0, []
        for i in range(n):
            c = tot - acc if i == n - 1 else r5(tot * w[i] / ws, 5)
            acc += c
            cs.append(c)
        reno[d] = cs
    reno[1][0] = first or 30
    decor = []   # [(room, item, unlock_level, price)]
    for r in range(12):
        u0 = P["house_unlock"][r]
        for i in range(6):
            if "decor_offsets" in P:
                ul = min(150, u0 + P["decor_offsets"][i])
                price = r5(P["decor_w"][i] * W(ul) * P["decor_room_mult"][r])
            else:   # как в билде: всё сразу, цена от этажа
                ul = u0
                price = r5(P["decor_base"][i] * P["floor_mult"][r // 3])
            if r == 0 and i == 0:
                price = 0
            decor.append((r, i, ul, price))
    return reno, decor


class Player:
    def __init__(s, P, a, r):
        s.P, s.a, s.r = P, a, r
        s.reno, s.decor = build_prices(P)
        s.coins = s.gems = 0
        s.inc, s.out = {}, {}
        s.mode, s.reached, s.stars = 0, [0, 0, 0], {}
        s.bought = {d: 0 for d in range(1, 16)}
        s.decor_bought = set()
        s.built = [False] * 12
        s.building, s.build_end, s.build_ads = -1, 0.0, 0
        s.cash_since = None
        s.streak = 0
        s.ads = s.rub = 0
        s.grind = s.minutes = 0.0
        s.star_claimed = 0
        s.piggy = 0
        s.done = {}
        s.seg = {}          # отрезок по 10 уровней «Лёгкого» → списки метрик
        s.squirrel4 = None  # уровень, когда белка стала 4★
        s.buys = []
        s.blocked_since = None
        s.late_left, s.late_next = [], None

    def earn(s, n, why):
        n = int(n)
        if n > 0:
            s.coins += n
            s.inc[why] = s.inc.get(why, 0) + n
            if why in ("уровни", "×2 за видео"):
                s.piggy = min(4000, s.piggy + n // 20 + (20 if why == "уровни" else 0))

    def spend(s, n, why):
        s.coins -= n
        s.out[why] = s.out.get(why, 0) + n

    @staticmethod
    def dist(lid):
        return (lid - 1) // 10 + 1

    def stage_done(s, d):
        return s.bought[d] >= len(s.reno[d])

    def lvl(s):
        return s.reached[0]

    def can_next(s):
        n = s.reached[s.mode] + 1
        if n > 150:
            return False
        return s.mode > 0 or s.dist(n) == 1 or s.stage_done(s.dist(n) - 1)

    # ---- что игрок может купить прямо сейчас (открыто, не куплено)
    def open_items(s):
        out = []
        if s.mode == 0:
            cur = s.dist(min(150, s.lvl() + 1))
            for d in range(1, cur + 1):
                if not s.stage_done(d):
                    out.append(("reno", d, s.reno[d][s.bought[d]]))
                    break
        if s.lvl() >= s.P["house_unlock"][0]:
            if s.building < 0:
                for i in range(1, 12):
                    if not s.built[i] and max(s.reached) >= s.P["house_unlock"][i]:
                        out.append(("room", i, s.P["house_build"][i]))
                        break
            for (r, i, ul, price) in s.decor:
                if s.built[r] and (r, i) not in s.decor_bought and max(s.reached) >= ul \
                        and all((r, j) in s.decor_bought for j in range(i)):
                    out.append(("decor", (r, i), price))
        if s.late_left:
            out.append(("late", 0, s.late_left[0]))
        return out

    def buy_all(s, now):
        P = s.P
        # дом: касса, стройка
        if s.lvl() >= P["house_unlock"][0]:
            if s.cash_since is None:
                s.cash_since, s.built[0] = now, True
            if s.building >= 0 and now >= s.build_end:
                s.built[s.building], s.building = True, -1
            hours = min(P["cash_hours"], now - s.cash_since)
            rooms = sum(s.built)
            sq = {1: 0.1, 2: 0.2, 3: 0.3, 4: 0.5}.get(s.squirrel_level(), 0)
            cash = int((P["cash_base"] + P["cash_per_room"] * rooms) * (1 + sq) * hours)
            s.earn(cash, "касса")
            if cash and s.a["ads"] and s.r.random() < 0.5:
                s.ads += 1
                s.earn(cash, "касса ×2")
            s.cash_since = now
        # сезонные витрины
        if P.get("late_sink") and s.lvl() >= 150:
            if s.late_next is None or now >= s.late_next:
                s.late_next = (now if s.late_next is None else s.late_next) + 14 * 24
                s.late_left = list(P["late_sink_cost"])
        # покупки по важности: ремонт (двигает уровни) → комната → декор дешевле → витрина
        while True:
            items = s.open_items()
            order = {"reno": 0, "room": 1, "decor": 2, "late": 3}
            items.sort(key=lambda x: (order[x[0]], x[2]))
            done = False
            for kind, key, price in items:
                reserve = 0 if kind == "reno" else s.reno_need()
                if s.coins - reserve >= price:
                    s.take(kind, key, price, now)
                    done = True
                    break
            if not done:
                break
        if s.building >= 0:
            left = (s.build_end - now) * 60
            while s.a["ads"] and s.build_ads < 3 and left > 0:
                s.build_ads += 1
                s.ads += 1
                s.build_end -= 0.5
                left -= 30
            c = skip_cost(left)
            if left > 0 and left >= (240 if s.a["payer"] else 1440) and s.gems >= c:
                s.gems -= c
                s.build_end = now
            if now >= s.build_end:
                s.built[s.building], s.building = True, -1

    def reno_need(s):
        for kind, key, price in s.open_items():
            if kind == "reno":
                return price
        return 0

    def take(s, kind, key, price, now):
        if kind == "reno":
            s.spend(price, "ремонт")
            s.bought[key] += 1
            if s.stage_done(key):
                s.earn(s.P["stage_bonus"], "ремонт готов")
        elif kind == "room":
            s.spend(price, "дом: стройка")
            s.building, s.build_end, s.build_ads = key, now + s.P["house_minutes"][key] / 60, 0
        elif kind == "decor":
            s.spend(price, "дом: декор")
            s.decor_bought.add(key)
            if s.squirrel_level() >= 4 and s.squirrel4 is None:
                s.squirrel4 = s.lvl()
        else:
            s.spend(price, "сезонные витрины")
            s.late_left.pop(0)

    def squirrel_level(s):
        if not s.built[0]:
            return 0
        n = sum(1 for (r, i) in s.decor_bought if r == 0)
        return min(4, 1 + n // 2)

    # ---- уровень
    def play(s, lv, replay=False):
        P, a, m = s.P, s.a, s.mode
        hard = lv["diff"] in ("hard", "superhard") or lv["rev"] or m > 0
        p = min(0.97, V2.WIN_P.get(lv["diff"], 0.8) + a["skill"] - (0.1 if lv["rev"] else 0) - WIN_PENALTY[m])
        t = 0.0
        if P["prelevel_booster"] and hard and not replay and lv["id"] >= 15:
            price = P["prelevel_booster"]
            if s.coins - s.reno_need() >= 2 * price and s.r.random() < a["booster"]:
                s.spend(price, "бустеры")
                p = min(0.98, p + 0.10)
        first_try, cont_n, rewind = True, 0, P["free_rewind"]
        while True:
            t += (2.0 + 0.12 * lv["d"] + (0.6 if lv["diff"] in ("hard", "superhard") else 0)) * TIME_MULT[m]
            if s.r.random() < p:
                break
            if rewind:
                rewind -= 1
                t += 0.6
                if s.r.random() < 0.55:
                    break
            took = False
            if not replay and cont_n < len(P["continue_coins"]):
                cc = P["continue_coins"][cont_n]
                if a["ads"] and cont_n == 0 and s.r.random() < 0.6:
                    s.ads += 1
                    took = True
                elif s.coins - s.reno_need() >= cc and s.r.random() < a["spend"] / (1 + cont_n):
                    s.spend(cc, "продолжения")
                    took = True
            if took:
                cont_n += 1
                t += 0.5
                if s.r.random() < 0.75:
                    break
            first_try = False
            if not replay:
                s.streak = 0
        u = s.r.random()
        p3 = min(0.95, V2.P3.get(lv["diff"], 0.6) + a["skill"] - WIN_PENALTY[m])
        stars = 3 if u < p3 else 2 if u < p3 + 0.3 else 1
        coins = V2.win_coins(lv, stars) * P["mode_coin_mult"][m]
        if not replay and P.get("streak_pct"):
            coins *= 1 + P["streak_pct"][min(s.streak, len(P["streak_pct"]) - 1)] / 100
        if replay:
            coins = coins * P["replay_pct"] / 100
        s.earn(coins, "уровни (повтор)" if replay else "уровни")
        if not replay and s.r.random() < a["x2"]:
            s.ads += 1
            s.earn(coins * ((P["x2_from100"] if lv["id"] >= 100 else 2) - 1), "×2 за видео")
        s.stars[(m, lv["id"])] = max(s.stars.get((m, lv["id"]), 0), stars)
        if not replay:
            s.reached[m] = lv["id"]
            s.streak += 1
            if s.streak in (5, 10):
                s.earn(P["streak_chest"][0 if s.streak == 5 else 1], "серия побед")
            if s.streak >= 10:
                s.streak = 5
            if lv["id"] % 10 == 0 and P["mode_district_gems"][m]:
                s.gems += P["mode_district_gems"][m]
        st = sum(s.stars.values())
        if st > s.star_claimed:
            s.earn(P["star_path_coins_total"] * (st - s.star_claimed) / 1350, "звёздный путь")
            s.star_claimed = st
        return t

    def log_segment(s):
        """Метрики конца захода — в отрезок «Лёгкого» по 10 уровней."""
        seg = min(14, s.lvl() // 10) if s.mode == 0 else 15 + s.mode
        d = s.seg.setdefault(seg, {"coins": [], "idle": [], "choice": [], "blocked": 0, "sess": 0})
        items = s.open_items()
        d["coins"].append(s.coins)
        d["choice"].append(len(items))
        d["sess"] += 1
        if not items:
            d["idle"].append(s.coins)   # купить нечего — всё на руках лишнее
        if s.mode == 0 and not s.can_next() and s.lvl() < 150:
            d["blocked"] += 1


def simulate(P, aname, seed, days):
    a = CFG["players"][aname]
    s = Player(P, a, random.Random(seed))
    login = 0
    tips_at = -99.0
    week_q = month_q = 0
    for day in range(1, days + 1):
        login += 1
        s.earn(P["daily_login"][(login - 1) % 7], "ежедневка")
        if a["payer"] and day == 2:   # стартовый набор
            s.rub += P["starter"]["rub"]
            s.buys.append((s.lvl(), P["starter"]["rub"]))
            s.earn(P["starter"]["coins"], "покупки")
        if s.lvl() >= P["challenge_at"]:
            s.earn(P["challenge_coins"], "испытание дня")
            if day % 7 == 0:
                s.earn(P["challenge_week_coins"], "испытание дня")
        if s.lvl() >= P["quests_at"]:
            n = sum(1 for _ in range(3) if s.r.random() < a["quests"])
            s.earn(P["quest_coins"] * n, "задания")
            week_q += n
            month_q += n
            if day % 7 == 0:
                if week_q >= 17:
                    s.earn(P["week_coins"], "задания")
                week_q = 0
            if day % 30 == 0:
                if month_q >= 70:
                    s.earn(P["month_coins"], "задания")
                month_q = 0
        ad_coins = 0
        for sess in range(a["sessions"]):
            now = (day - 1) * 24 + 9 + sess * 9
            budget = a["minutes"] / a["sessions"]
            if now - tips_at >= 4 and s.lvl() >= 10:
                tips_at = now
                s.earn(P["tips"], "чаевые")
            s.buy_all(now)
            used = 0.0
            while used < budget:
                if s.mode == 2 and s.reached[2] >= 150:
                    s.done.setdefault("всё", day)
                    break
                if s.can_next():
                    lv = LEVELS[s.reached[s.mode]]
                    used += s.play(lv)
                    s.blocked_since = None
                    if s.reached[s.mode] >= 150:
                        s.done.setdefault(["Лёгкий", "Средний", "Сложный"][s.mode], day)
                        if s.mode < 2:
                            s.mode += 1
                    s.buy_all(now + used / 60)
                    continue
                # упёрся в ремонт
                if s.blocked_since is None:
                    s.blocked_since = now
                if a["payer"] and now - s.blocked_since >= P["payer_patience_h"]:
                    pk = P["payer_pack"]
                    s.rub += pk["rub"]
                    s.buys.append((s.lvl(), pk["rub"]))
                    s.earn(pk["coins"], "покупки")
                    s.blocked_since = now
                    s.buy_all(now + used / 60)
                    continue
                if a["ads"] and ad_coins < P["ad_coins_per_day"]:
                    ad_coins += 1
                    s.ads += 1
                    s.earn(P["ad_coins_base"] + P["ad_coins_per_district"] * s.dist(s.lvl() + 1), "видео за монеты")
                    used += 0.6
                    s.buy_all(now + used / 60)
                    continue
                cand = [l for l in LEVELS[: s.lvl()] if s.stars.get((0, l["id"]), 0) < 3] or LEVELS[: s.lvl()]
                t = s.play(s.r.choice(cand), replay=True)
                used += t
                s.grind += t
                s.buy_all(now + used / 60)
            s.minutes += used
            s.log_segment()
        if all(s.built):
            s.done.setdefault("дом построен", day)
        if all(s.built) and len(s.decor_bought) == 72:
            s.done.setdefault("дом обставлен", day)
    return s


def med(vals):
    vals = [v for v in vals if v is not None]
    return int(statistics.median(vals)) if vals else None


def table(P):
    reno, decor = build_prices(P)
    print("Ремонт по районам:", ", ".join(f"{d}: {sum(reno[d])}" for d in range(1, 16)), "= всего", sum(sum(v) for v in reno.values()))
    print("Комнаты:", ", ".join(f"{i}: {P['house_build'][i]} (ур. {P['house_unlock'][i]})" for i in range(12)),
          "= всего", sum(P["house_build"]))
    for r in range(12):
        row = [(ul, price) for (rr, i, ul, price) in decor if rr == r]
        print(f"  комната {r:2d}: " + " · ".join(f"{p} (ур.{u})" for u, p in row) + f"  = {sum(p for _, p in row)}")
    print("Декор всего:", sum(p for *_, p in decor))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preset", default="both")
    ap.add_argument("--runs", type=int, default=8)
    ap.add_argument("--days", type=int, default=150)
    ap.add_argument("--only", default="")
    ap.add_argument("--table", action="store_true")
    ap.add_argument("-v", action="store_true")
    args = ap.parse_args()
    presets = ["build", "v6"] if args.preset == "both" else [args.preset]
    if args.table:
        for pn in presets:
            print(f"######## {pn}")
            table(CFG[pn])
        return
    for pn in presets:
        P = CFG[pn]
        print(f"\n######## {pn} ########")
        for aname in CFG["players"]:
            if args.only and args.only not in aname:
                continue
            runs = [simulate(P, aname, 2000 + i, args.days) for i in range(args.runs)]
            dd = lambda k: med([r.done.get(k) for r in runs]) or "—"
            print(f"== {aname}")
            print(f"   день: Лёгкий {dd('Лёгкий')}, дом построен {dd('дом построен')}, обставлен {dd('дом обставлен')}, "
                  f"Средний {dd('Средний')}, Сложный {dd('Сложный')}")
            sq = med([r.squirrel4 for r in runs])
            tot_in = med([sum(r.inc.values()) for r in runs])
            cons = med([r.out.get("продолжения", 0) + r.out.get("бустеры", 0) for r in runs])
            print(f"   белка 4★ на уровне {sq if sq else '—'}; гринд {med([100 * r.grind / max(1, r.minutes) for r in runs])} %; "
                  f"видео/день {med([r.ads for r in runs]) / args.days:.1f}; ₽ {med([r.rub for r in runs])}; "
                  f"расходники {int(100 * cons / max(1, tot_in))} % дохода; остаток {med([r.coins for r in runs])}")
            hdr = "   отрезок   монет(мед) избыток(мед) выбор(мед) упор%"
            print(hdr)
            for seg in sorted({k for r in runs for k in r.seg}):
                ds = [r.seg[seg] for r in runs if seg in r.seg]
                coins = med([statistics.median(d["coins"]) for d in ds])
                idle = [statistics.median(d["idle"]) for d in ds if d["idle"]]
                idle_share = sum(len(d["idle"]) for d in ds) / max(1, sum(d["sess"] for d in ds))
                ch = med([statistics.median(d["choice"]) for d in ds])
                bl = int(100 * sum(d["blocked"] for d in ds) / max(1, sum(d["sess"] for d in ds)))
                name = f"ур.{seg * 10 + 1}-{seg * 10 + 10}" if seg < 15 else ["", "Средний", "Сложный"][seg - 15]
                print(f"   {name:10s} {coins:8d} {('%d (%d%% заходов)' % (med(idle), 100 * idle_share)) if idle else '—':>18s} {ch:6d} {bl:6d}")
            if args.v:
                r = runs[0]
                ti = sum(r.inc.values())
                print("   доход: " + ", ".join(f"{k} {100 * v // ti}%" for k, v in sorted(r.inc.items(), key=lambda x: -x[1])))
                print("   траты: " + ", ".join(f"{k} {v}" for k, v in sorted(r.out.items(), key=lambda x: -x[1])))
                if r.buys:
                    print("   покупки (уровень, ₽):", r.buys)


if __name__ == "__main__":
    main()

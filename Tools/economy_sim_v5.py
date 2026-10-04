# -*- coding: utf-8 -*-
"""Симулятор экономики v5 «Всё по полкам!» (04.10.2026).

python Tools/economy_sim_v5.py                  — сравнить current (как сейчас) и v5 (предложение), 8 прогонов
python Tools/economy_sim_v5.py --preset v5 -v   — один набор, подробно (доход и траты по источникам)
python Tools/economy_sim_v5.py --runs 20 --days 150

Отличие от economy_sim_v4.py: считает БЕСКОНЕЧНЫЕ траты — продолжения после поражения (лестница цен),
бустеры перед уровнем и в уровне. Главный вопрос v5: остаются ли у игрока «лишние» монеты.
Числа — Tools/economy_v5.json (два набора: current и v5). Уровни и формула монет за уровень — из кода
(levels.json, Economy.WinCoins через economy_sim.py).

Модель игрока: N минут в день, 1–2 захода. В заходе: касса, «Чаевые», ежедневка, Испытание дня, задания;
потом уровни по порядку. Поражение → бесплатная «отмотка» (1 раз за уровень) → платное продолжение
(монеты, видео или алмазы — по типу игрока) → иначе заново (серия сгорает). Перед трудным уровнем игрок
с запасом монет иногда берёт бустер. Ремонт закрывает следующий район «Лёгкого»; когда не хватает —
переигровки и видео за монеты (это гринд). Дом строится по таймерам. После «Лёгкого» — «Средний» и
«Сложный»; в v5 там есть поздняя трата (сезонные витрины, раз в 2 недели).
"""
import argparse
import io
import json
import os
import random
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import economy_sim as V2  # noqa: E402  уровни и формула монет за уровень

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CFG = json.load(io.open(os.path.join(ROOT, "Tools", "economy_v5.json"), encoding="utf-8"))
GF0 = CFG["gems_free"]
GF = GF0
WIN_PENALTY = [0.0, 0.08, 0.15]
TIME_MULT = [1.0, 1.15, 1.3]
SKIP = [(0, 0), (5, 1), (30, 3), (60, 5), (240, 15), (1440, 60), (10080, 300)]


def skip_cost(minutes):
    if minutes <= 0:
        return 0
    for (m0, c0), (m1, c1) in zip(SKIP, SKIP[1:]):
        if minutes <= m1:
            return max(1, int(round(c0 + (c1 - c0) * (minutes - m0) / (m1 - m0))))
    return SKIP[-1][1]


def reno_costs(P):
    _, _, items_per, _, first = V2.load_code()
    out = {}
    for d in range(1, 16):
        n = items_per.get(d, 9)
        tot = P["stage_cost"][d]
        w = [0.7 + 0.6 * i / max(1, n - 1) for i in range(n)]
        ws, acc, cs = sum(w), 0, []
        for i in range(n):
            c = tot - acc if i == n - 1 else int(round(tot * w[i] / ws / 5.0) * 5)
            acc += c
            cs.append(c)
        out[d] = cs
    out[1][0] = first or 30
    return out


class Player:
    def __init__(s, P, a, levels, costs, r):
        s.P, s.a, s.lv, s.costs, s.r = P, a, levels, costs, r
        s.coins = s.gems = 0
        s.inc, s.out, s.gin, s.gout = {}, {}, {}, {}
        s.mode = 0
        s.reached = [0, 0, 0]
        s.stars = {}
        s.bought = {d: 0 for d in range(1, 16)}
        s.streak = 0
        s.ads = 0
        s.rub = 0
        s.grind = s.minutes = 0.0
        s.built = [False] * 12
        s.decor = [0] * 12
        s.building, s.build_end, s.build_ads = -1, 0.0, 0
        s.cash_since = None
        s.star_claimed = 0
        s.piggy = 0
        s.late_bought, s.late_next = 0, None
        s.skins = 0
        s.balance_log = {}
        s.done = {}
        s.ad_coins_today = 0
        s.tips_at = -99.0
        s.login_day = 0
        s.quest_pts_w = s.quest_pts_m = 0

    # ---- кошелёк
    def earn(s, n, why):
        n = int(n)
        if n <= 0:
            return
        s.coins += n
        s.inc[why] = s.inc.get(why, 0) + n
        if why in ("уровни", "×2 за видео", "касса", "касса ×2"):
            s.piggy = min(4000, s.piggy + n // 20)

    def spend(s, n, why):
        s.coins -= n
        s.out[why] = s.out.get(why, 0) + n

    def gem(s, n, why):
        s.gems += n
        s.gin[why] = s.gin.get(why, 0) + n

    def spend_gems(s, n, why):
        s.gems -= n
        s.gout[why] = s.gout.get(why, 0) + n

    # ---- прогресс
    @staticmethod
    def dist(lid):
        return (lid - 1) // 10 + 1

    def stage_done(s, d):
        return s.bought[d] >= len(s.costs[d])

    def can_next(s):
        n = s.reached[s.mode] + 1
        if n > 150:
            return False
        if s.mode > 0:
            return True
        d = s.dist(n)
        return d == 1 or s.stage_done(d - 1)

    def reno_reserve(s):
        if s.mode > 0:
            return 0
        d = s.dist(min(150, s.reached[0] + 1))
        for x in range(1, d + 1):
            if not s.stage_done(x):
                return s.costs[x][s.bought[x]]
        return 0

    # ---- уровень
    def play(s, lv, replay=False):
        P, a, m = s.P, s.a, s.mode
        hard = lv["diff"] in ("hard", "superhard") or lv["rev"] or m > 0
        p = min(0.97, V2.WIN_P.get(lv["diff"], 0.8) + a["skill"] - (0.1 if lv["rev"] else 0) - WIN_PENALTY[m]
                + (0.04 if s.streak >= 3 else 0))
        t = 0.0
        # бустер перед уровнем (v5): на трудном уровне, если монет с запасом
        if P["prelevel_booster"] and hard and not replay:
            price = P["prelevel_booster"]
            if s.coins - s.reno_reserve() >= 2 * price and s.r.random() < a["booster"]:
                s.spend(price, "бустер перед уровнем")
                p = min(0.98, p + 0.10)
        first_try, won, cont_n, rewind = True, False, 0, P["free_rewind"]
        while not won:
            t += (2.0 + 0.12 * lv["d"] + (0.6 if lv["diff"] in ("hard", "superhard") else 0)) * TIME_MULT[m]
            if s.r.random() < p:
                won = True
                break
            # поражение: бесплатная отмотка
            if rewind > 0:
                rewind -= 1
                t += 0.6
                if s.r.random() < 0.55:
                    won = True
                    break
            # помощники в уровне (пачка отмен/подсказок) — иногда, если монеты есть
            if s.coins >= P["undo_pack"] and s.r.random() < 0.2 * a["spend"] / 0.4:
                s.spend(P["undo_pack"], "отмены/подсказки")
                if s.r.random() < 0.5:
                    won = True
                    break
            # платное продолжение по лестнице
            if cont_n < len(P["continue_coins"]) and not replay:
                cc, cg = P["continue_coins"][cont_n], P["continue_gems"][cont_n]
                took = False
                if a["ads"] and cont_n == 0 and s.r.random() < 0.6:
                    s.ads += 1
                    took = True
                elif s.coins >= cc and s.r.random() < a["spend"] / (1 + cont_n):
                    s.spend(cc, "продолжения")
                    took = True
                elif a["payer"] and s.gems >= cg and s.r.random() < 0.5:
                    s.spend_gems(cg, "продолжения")
                    took = True
                if took:
                    cont_n += 1
                    t += 0.5
                    if s.r.random() < 0.75:
                        won = True
                        break
            first_try = False
            if not replay and m == 0:
                s.streak = 0
        u = s.r.random()
        p3 = min(0.95, V2.P3.get(lv["diff"], 0.6) + a["skill"] - WIN_PENALTY[m])
        stars = 3 if u < p3 else 2 if u < p3 + 0.3 else 1
        coins = V2.win_coins(lv, stars) * P["mode_coin_mult"][m]
        if replay:
            coins = coins * P["replay_pct"] / 100
        s.earn(coins, "уровни (повтор)" if replay else "уровни")
        if not replay and s.r.random() < a["x2"]:
            mult = P["x2_mult_from100"] if lv["id"] >= 100 else P["x2_mult"]
            s.ads += 1
            s.earn(coins * (mult - 1), "×2 за видео")
        key = (m, lv["id"])
        s.stars[key] = max(s.stars.get(key, 0), stars)
        if not replay:
            s.reached[m] = lv["id"]
            if first_try or cont_n:
                s.streak += 1
                if s.streak in (5, 10):
                    k = 0 if s.streak == 5 else 1
                    s.earn(P["streak_chest"][k], "серия побед")
                    s.gem(GF["streak_chest"][k], "серия побед")
                if s.streak >= 10:
                    s.streak = 5
            if lv["id"] % 10 == 0 and P["mode_district_gems"][m]:
                s.gem(P["mode_district_gems"][m], "район в режиме")
        s.claim_path()
        return t

    def claim_path(s):
        st = sum(s.stars.values())
        new = st - s.star_claimed
        if new <= 0:
            return
        s.star_claimed = st
        k = new / 1350
        s.earn(s.P["star_path_coins_total"] * k, "звёздный путь")
        s.gacc = getattr(s, "gacc", 0.0) + GF["star_path_total"] * k
        while s.gacc >= 1:
            s.gacc -= 1
            s.gem(1, "звёздный путь")

    # ---- траты меты
    def buy_meta(s, now):
        P = s.P
        # ремонт
        if s.mode == 0:
            cur = s.dist(min(150, s.reached[0] + 1))
            for d in range(1, cur + 1):
                while not s.stage_done(d):
                    c = s.costs[d][s.bought[d]]
                    if s.coins < c:
                        break
                    s.spend(c, "ремонт")
                    s.bought[d] += 1
                    if s.stage_done(d):
                        s.earn(P["stage_bonus"], "ремонт готов")
                if not s.stage_done(d):
                    break
        # дом
        if max(s.reached) >= P["house_unlock"][0]:
            if s.cash_since is None:
                s.cash_since = now
                s.built[0] = True
            if s.building >= 0 and now >= s.build_end:
                s.built[s.building], s.building = True, -1
            hours = min(P["cash_hours"], now - s.cash_since)
            rate = (P["cash_base"] + P["cash_per_room"] * sum(s.built)) * 1.2
            cash = int(rate * hours)
            if cash > 0:
                s.earn(cash, "касса")
                if s.a["ads"] and s.r.random() < 0.5:
                    s.ads += 1
                    s.earn(cash, "касса ×2")
            s.cash_since = now
            if s.building < 0:
                for i in range(1, 12):
                    if s.built[i] or max(s.reached) < P["house_unlock"][i]:
                        continue
                    if s.coins - s.reno_reserve() < P["house_build"][i]:
                        break
                    s.spend(P["house_build"][i], "дом: стройка")
                    s.building, s.build_end, s.build_ads = i, now + P["house_minutes"][i] / 60, 0
                    break
            if s.building >= 0:
                left = (s.build_end - now) * 60
                while s.a["ads"] and s.build_ads < 3 and left > 0:
                    s.build_ads += 1
                    s.ads += 1
                    s.build_end -= 0.5
                    left -= 30
                if left > 0:
                    c = skip_cost(left)
                    want = left >= (240 if s.a["payer"] else 1440)
                    if want and s.gems >= c:
                        s.spend_gems(c, "ускорение стройки")
                        s.build_end = now
                if now >= s.build_end:
                    s.built[s.building], s.building = True, -1
            while True:
                best = None
                for i in range(12):
                    if not s.built[i] or s.decor[i] >= 6:
                        continue
                    c = int(P["decor_base"][s.decor[i]] * P["floor_mult"][i // 3])
                    if i == 0 and s.decor[i] == 0:
                        c = 0
                    if best is None or c < best[1]:
                        best = (i, c)
                if best is None or s.coins - s.reno_reserve() - 300 < best[1]:
                    break
                s.spend(best[1], "дом: декор")
                s.decor[best[0]] += 1
        # поздняя трата v5: сезонные витрины раз в 2 недели после «Лёгкого»
        if P.get("late_sink") and s.reached[0] >= 150:
            if s.late_next is None:
                s.late_next = now
            if now >= s.late_next:
                s.late_next += 14 * 24
                s.late_left = list(P["late_sink_cost"])
            while getattr(s, "late_left", None) and s.coins - 500 >= s.late_left[0]:
                s.spend(s.late_left.pop(0), "сезонные витрины")
        # алмазы: скин за алмазы (v5)
        if P["skin_gems"] and s.gems >= P["skin_gems"] + 30:
            s.spend_gems(P["skin_gems"], "скины за алмазы")
            s.skins += 1

    def house_done(s):
        return all(s.built)

    def house_full(s):
        return all(s.built) and all(d >= 6 for d in s.decor)


def simulate(P, aname, seed, days):
    global GF
    GF = dict(GF0, **P.get("gems_free", {}))
    a = CFG["players"][aname]
    levels = V2.load_levels()
    s = Player(P, a, levels, reno_costs(P), random.Random(seed))
    for day in range(1, days + 1):
        s.ad_coins_today = 0
        # ежедневные награды
        s.login_day += 1
        s.earn(P["daily_login"][(s.login_day - 1) % 7], "ежедневка")
        if s.reached[0] >= 8:
            s.earn(P["challenge_coins"], "испытание дня")
            s.gem(GF["challenge_day"], "испытание дня")
            if day % 7 == 0:
                s.earn(P["challenge_week_coins"], "испытание дня")
                s.gem(GF["challenge_week"], "испытание дня")
            done = sum(1 for _ in range(3) if s.r.random() < a["quests"])
            s.earn(P["quest_coins"] * done, "задания дня")
            s.quest_pts_w += 10 * done
            s.quest_pts_m += 10 * done
            if day % 7 == 0:
                if s.quest_pts_w >= 175:
                    s.earn(P["week_coins"], "задания: неделя")
                    s.gem(GF["week_track"], "задания: неделя")
                s.quest_pts_w = 0
            if day % 30 == 0:
                if s.quest_pts_m >= 700:
                    s.earn(P["month_coins"], "задания: месяц")
                    s.gem(GF["month_track"], "задания: месяц")
                s.quest_pts_m = 0
            if day % 7 == 0:
                s.gem(GF["league_week"], "рейтинг")
        # покупки платящего
        if a["payer"]:
            if day == 2:
                s.rub += 99
                s.earn(1500, "покупки")
            if day == 6:
                s.rub += 299
            if s.piggy >= 1500 and day % 10 == 0:
                s.rub += 99
                s.earn(s.piggy, "копилка")
                s.piggy = 0
            if day in (14, 40):
                s.rub += 149
                s.gem(180, "покупка алмазов")
        for sess in range(a["sessions"]):
            now = (day - 1) * 24 + 9 + sess * 9
            budget = a["minutes"] / a["sessions"]
            if now - s.tips_at >= 4 and s.reached[0] >= 10:
                s.tips_at = now
                s.earn(P["tips"], "чаевые")
            s.buy_meta(now)
            used = 0.0
            while used < budget:
                if s.mode == 2 and s.reached[2] >= 150:
                    s.done.setdefault("весь контент", day)
                    break
                if s.can_next():
                    lv = levels[s.reached[s.mode]]
                    used += s.play(lv)
                    s.buy_meta(now + used / 60)
                    if s.reached[s.mode] >= 150:
                        s.done.setdefault(["Лёгкий", "Средний", "Сложный"][s.mode], day)
                        if s.mode < 2:
                            s.mode += 1
                        else:
                            break
                else:
                    if s.mode == 0 and a["ads"] and s.ad_coins_today < P["ad_coins_per_day"]:
                        s.ad_coins_today += 1
                        s.ads += 1
                        s.earn(P["ad_coins_base"] + P["ad_coins_per_district"] * s.dist(s.reached[0] + 1), "видео за монеты")
                        used += 0.6
                        s.buy_meta(now + used / 60)
                        continue
                    cand = [l for l in levels[: s.reached[0]] if s.stars.get((0, l["id"]), 0) < 3] or levels[: s.reached[0]]
                    t = s.play(s.r.choice(cand), replay=True)
                    used += t
                    s.grind += t
                    s.buy_meta(now + used / 60)
            s.minutes += used
        if s.house_done():
            s.done.setdefault("дом построен", day)
        if s.house_full():
            s.done.setdefault("дом обставлен", day)
        if day in (3, 7, 14, 30, 45, 60, 90, 120):
            s.balance_log[day] = s.coins
    return s


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preset", choices=["current", "v5", "both"], default="both")
    ap.add_argument("--runs", type=int, default=8)
    ap.add_argument("--days", type=int, default=120)
    ap.add_argument("--only", default="")
    ap.add_argument("-v", action="store_true")
    args = ap.parse_args()
    presets = ["current", "v5"] if args.preset == "both" else [args.preset]
    for pn in presets:
        P = CFG[pn]
        print(f"\n######## {pn} ########")
        for aname in CFG["players"]:
            if args.only and args.only not in aname:
                continue
            runs = [simulate(P, aname, 1000 + i, args.days) for i in range(args.runs)]

            def med(f):
                vals = [f(r) for r in runs]
                vals = [v for v in vals if v is not None]
                return int(statistics.median(vals)) if vals else None

            def dd(k):
                v = med(lambda r: r.done.get(k))
                return "—" if v is None else str(v)

            tot_in = med(lambda r: sum(r.inc.values()))
            cons = med(lambda r: r.out.get("продолжения", 0) + r.out.get("отмены/подсказки", 0) + r.out.get("бустер перед уровнем", 0))
            meta = med(lambda r: sum(v for k, v in r.out.items() if k.startswith(("ремонт", "дом", "сезон"))))
            print(f"== {aname}")
            print(f"   день: Лёгкий {dd('Лёгкий')}, дом построен {dd('дом построен')}, обставлен {dd('дом обставлен')}, "
                  f"Средний {dd('Средний')}, Сложный {dd('Сложный')}, всё пройдено {dd('весь контент')}")
            bl = {d: med(lambda r, d=d: r.balance_log.get(d)) for d in (7, 14, 30, 60, 90, 120) if d <= args.days}
            print(f"   монеты на руках по дням: " + ", ".join(f"д{d} {v}" for d, v in bl.items()))
            print(f"   за {args.days} дн: заработано {tot_in}, мета {meta}, расходники {cons} "
                  f"({int(100 * cons / max(1, tot_in))} % дохода), остаток {med(lambda r: r.coins)}; "
                  f"гринд {int(100 * med(lambda r: r.grind) / max(1, med(lambda r: r.minutes)))} %, "
                  f"видео в день {med(lambda r: r.ads) / args.days:.1f}, ₽ {med(lambda r: r.rub)}")
            print(f"   алмазы: получено {med(lambda r: sum(r.gin.values()))}, потрачено {med(lambda r: sum(r.gout.values()))}, "
                  f"скинов за алмазы {med(lambda r: r.skins)}")
            if args.v:
                r = runs[0]
                ti = sum(r.inc.values())
                print("   доход: " + ", ".join(f"{k} {100 * v // ti}%" for k, v in sorted(r.inc.items(), key=lambda x: -x[1])))
                print("   траты: " + ", ".join(f"{k} {v}" for k, v in sorted(r.out.items(), key=lambda x: -x[1])))
                print("   алмазы: откуда", r.gin, "куда", r.gout)


if __name__ == "__main__":
    main()

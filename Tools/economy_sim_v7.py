# -*- coding: utf-8 -*-
"""Симулятор экономики v7 «Всё по полкам!» — финал после живого прохождения билда 24 (04–06.10.2026).

python Tools/economy_sim_v7.py                  — build (билд 24, сверен с живой игрой) против v7, 8 прогонов
python Tools/economy_sim_v7.py --preset v7 -v   — подробно: откуда монеты, на что ушли, покупки
python Tools/economy_sim_v7.py --check          — сверка модели build с живым прохождением (уровни 11–41)
python Tools/economy_sim_v7.py --table          — все цены v7 (ремонт, комнаты, декор, помощь в уровне)

Чем отличается от economy_sim_v6.py (v6 недосчитывал доход билда примерно в полтора раза):
  * множители монет за уровень, которые есть в билде: серия до +25 %, Белка на смене до +25 %, альбом +2 % за отдел.
    Живая игра: на уровнях 21–40 монеты за уровень = формула × 1,51–1,57 (серия ×1,25 · Белка ×1,25);
  * сундуки серии после 10 побед — каждые 5 побед (150 монет + 3 💎), как в билде;
  * задания недели и месяца билда: 3 × 100 и 3 × 300 монет + сундуки шкал; закрываются за 2–3 дня;
  * участие в рейтинге недели (+40), повторы наклеек (+10), «Чаевые» — только за видео;
  * Ёж на смене спасает переполненную тележку (в билде — каждый уровень, бесплатно);
  * алмазы: откуда и куда; «молоток» за 25 💎 заканчивает любую стройку (в билде);
  * «моменты оплаты»: игрок упёрся в ремонт ИЛИ проиграл, а продолжение не по карману, — здесь и продаётся
    самый дешёвый пак. Платящий покупает его в такой момент.
Числа — Tools/economy_v7.json. Уровни и формула монет — из кода (levels.json, Economy.WinCoins).
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
import economy_sim_v6 as V6  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CFG = json.load(io.open(os.path.join(ROOT, "Tools", "economy_v7.json"), encoding="utf-8"))
LEVELS = V2.load_levels()
W = V6.W
r5 = V6.r5
skip_cost = V6.skip_cost
WIN_PENALTY = V6.WIN_PENALTY
TIME_MULT = V6.TIME_MULT

# живое прохождение билда 24 (Docs/Экономика_v7_…, раздел 2): уровень → (монет за уровень в окне победы, звёзд)
LIVE = {11: (65, 3), 12: (86, 2), 13: (67, 3), 14: (94, 2), 15: (72, 3), 16: (86, 2), 17: (90, 3), 18: (108, 2),
        19: (99, 2), 20: (127, 2), 21: (99, 3), 22: (103, 3), 23: (99, 3), 24: (100, 3), 25: (148, 2), 26: (100, 3),
        27: (98, 3), 28: (128, 2), 29: (103, 3), 30: (131, 2), 31: (113, 3), 32: (103, 3), 33: (120, 3), 34: (145, 2),
        35: (115, 3), 36: (110, 3), 37: (115, 3), 38: (140, 2), 39: (115, 3), 40: (149, 2), 41: (128, 3)}


def help_price(P, key, level_id, n=0):
    """Цена помощи в уровне: в билде — фиксированная, в v7 — от дохода за уровень (W)."""
    v = P[key]
    if isinstance(v, dict) and "by_district" in v:   # таблица по районам: [[1-е, 2-е, 3-е], …]
        row = v["by_district"][min(14, (level_id - 1) // 10)]
        return row[min(n, len(row) - 1)]
    if isinstance(v, dict):   # {"w": [3, 7, 14]} — множители W
        k = v["w"][min(n, len(v["w"]) - 1)]
        return r5(k * W(level_id))
    return v[min(n, len(v) - 1)] if isinstance(v, list) else v


class Player(V6.Player):
    def __init__(s, P, a, r):
        super().__init__(P, a, r)
        s.gems = P.get("start_gems", 0)
        s.gem_in, s.gem_out = {}, {}
        s.depts = 0
        s.wins_first = 0
        s.packs = 0
        s.pay_moments = {}      # отрезок → [упор в ремонт, проигрыш без денег на продолжение]
        s.near_loss = 0
        s.hedge_charge = 0.0
        s.shortage_bought = 0
        s.deficits = []      # (уровень, сколько монет не хватало на остаток ремонта в момент упора)

    # ---- алмазы
    def gem(s, n, why):
        if n > 0:
            s.gems += n
            s.gem_in[why] = s.gem_in.get(why, 0) + n

    def gem_spend(s, n, why):
        s.gems -= n
        s.gem_out[why] = s.gem_out.get(why, 0) + n

    def staff_level(s, room):
        if not s.built[room]:
            return 0
        n = sum(1 for (r, i) in s.decor_bought if r == room)
        return min(4, 1 + n // 2)

    def seg_key(s):
        return min(14, s.lvl() // 10) if s.mode == 0 else 15 + s.mode

    def moment(s, kind):
        d = s.pay_moments.setdefault(s.seg_key(), [0, 0])
        d[kind] += 1

    # ---- дом: касса с умением Белки, стройка, молоток
    def buy_all(s, now):
        P = s.P
        if s.lvl() >= P["house_unlock"][0]:
            if s.cash_since is None:
                s.cash_since, s.built[0] = now, True
            if s.building >= 0 and now >= s.build_end:
                s.built[s.building], s.building = True, -1
            hours = min(P["cash_hours"], now - s.cash_since)
            rooms = sum(s.built)
            sq = P["squirrel_cash_pct"][s.staff_level(0)] / 100
            cash = int((P["cash_base"] + P["cash_per_room"] * rooms) * (1 + sq) * hours)
            s.earn(cash, "касса")
            if cash and s.a["ads"] and s.r.random() < 0.5:
                s.ads += 1
                s.earn(cash, "касса ×2")
            s.cash_since = now
        if P.get("late_sink") and s.lvl() >= 150:
            if s.late_next is None or now >= s.late_next:
                s.late_next = (now if s.late_next is None else s.late_next) + 14 * 24
                s.late_left = list(P["late_sink_cost"])
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
            if left > 0:
                if P["hammer_gems"] and left > 60 and s.gems >= P["hammer_gems"]:
                    s.gem_spend(P["hammer_gems"], "молоток")
                    s.build_end = now
                else:
                    c = skip_cost(left)
                    if left >= (240 if s.a["payer"] else 1440) and s.gems >= c:
                        s.gem_spend(c, "ускорение стройки")
                        s.build_end = now
            if now >= s.build_end:
                s.built[s.building], s.building = True, -1

    # ---- уровень
    def play(s, lv, replay=False):
        P, a, m = s.P, s.a, s.mode
        hard = lv["diff"] in ("hard", "superhard") or lv["rev"] or m > 0
        p = min(0.97, V2.WIN_P.get(lv["diff"], 0.8) + a["skill"] - (0.1 if lv["rev"] else 0) - WIN_PENALTY[m])
        t = 0.0
        if hard and not replay and lv["id"] >= 15:
            price = help_price(P, "prelevel_booster", lv["id"])
            if price and s.coins - s.reno_need() >= 2 * price and s.r.random() < a["booster"]:
                s.spend(price, "бустеры")
                p = min(0.98, p + 0.10)
        # Ёж на смене: бесплатно спасает переполненную тележку (заряд копится за уровни)
        # Ёж помогает, только если он на смене: второе место открывают комнаты slot2_rooms
        hl = s.staff_level(1) if all(s.built[i] for i in P["slot2_rooms"]) else 0
        hedge = 0
        if hl and not replay:
            s.hedge_charge += P["hedgehog_per_level"][hl]
            if s.hedge_charge >= 1:
                hedge = int(s.hedge_charge)
                s.hedge_charge -= hedge
        cont_n, rewind = 0, P["free_rewind"]
        while True:
            t += (2.0 + 0.12 * lv["d"] + (0.6 if lv["diff"] in ("hard", "superhard") else 0)) * TIME_MULT[m]
            if s.r.random() < p:
                break
            if hedge:
                hedge -= 1
                if s.r.random() < 0.6:
                    break
            if rewind:
                rewind -= 1
                t += 0.6
                if s.r.random() < 0.55:
                    break
            took = False
            if not replay and cont_n < P["continue_steps"]:
                cc = help_price(P, "continue_coins", lv["id"], cont_n)
                if a["ads"] and cont_n == 0 and s.r.random() < 0.6:
                    s.ads += 1
                    took = True
                elif s.coins - s.reno_need() >= cc and s.r.random() < a["spend"] / (1 + cont_n):
                    s.spend(cc, "продолжения")
                    took = True
                elif P.get("continue_gems") and s.gems >= P["continue_gems"][cont_n] \
                        and s.r.random() < a["spend"] / (1 + cont_n):
                    s.gem_spend(P["continue_gems"][cont_n], "продолжения")
                    took = True
                elif s.coins < cc and cont_n == 0:
                    s.moment(1)   # хотел бы продолжить, но не на что — момент оплаты
                    if a["payer"] and s.r.random() < 0.5:
                        s.buy_pack("проигрыш")
                        s.spend(cc, "продолжения")
                        took = True
            if took:
                cont_n += 1
                t += 0.5
                if s.r.random() < 0.75:
                    break
            if not replay:
                s.streak = 0
        u = s.r.random()
        p3 = min(0.95, V2.P3.get(lv["diff"], 0.6) + a["skill"] - WIN_PENALTY[m])
        stars = 3 if u < p3 else 2 if u < p3 + 0.3 else 1
        coins = V2.win_coins(lv, stars) * P["mode_coin_mult"][m]
        mult = 1.0
        if not replay and P.get("streak_pct"):
            mult *= 1 + P["streak_pct"][min(s.streak, len(P["streak_pct"]) - 1)] / 100
        mult *= 1 + P["squirrel_coin_pct"][s.staff_level(0)] / 100   # Белка на смене (в билде её ставят всегда)
        mult *= 1 + P["album_dept_pct"] * s.depts / 100
        coins *= mult
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
            sc = P["streak_chests"]   # {"5": [монет, алмазов], "10": [...], "every": 5 или 10}
            if str(s.streak) in sc:
                c, g = sc[str(s.streak)]
                s.earn(c, "сундуки серии")
                s.gem(g, "сундуки серии")
            if s.streak >= 10:
                s.streak = 10 - sc["every"]
            if lv["id"] % 10 == 0 and P["mode_district_gems"][m]:
                s.gem(P["mode_district_gems"][m], "районы режимов")
            if m == 0:
                s.wins_first += 1
                if s.wins_first % 3 == 0:
                    s.open_pack()
                if lv["id"] >= 20 and lv["id"] % P["album_dept_every"] == 0 and s.depts < 10:
                    s.depts += 1
                    s.earn(P["album_dept_coins"], "альбом")
                    s.gem(P["album_dept_gems"], "альбом")
        st = sum(s.stars.values())
        if st > s.star_claimed:
            s.earn(P["star_path_coins_total"] * (st - s.star_claimed) / 1350, "звёздный путь")
            s.gem(P["star_path_gems_total"] * (st - s.star_claimed) / 1350, "звёздный путь")
            s.star_claimed = st
        return t

    def open_pack(s):
        s.packs += 1
        dup = min(3, max(0, int(3 * (s.packs / 40.0))))   # чем полнее альбом, тем больше повторов
        s.earn(dup * s.P["dup_coins"], "повторы наклеек")

    def buy_pack(s, why):
        pk = s.P["payer_pack"]
        s.rub += pk["rub"]
        s.buys.append((s.lvl(), pk["rub"], why))
        s.earn(pk["coins"], "покупки")


def simulate(P, aname, seed, days):
    a = CFG["players"][aname]
    s = Player(P, a, random.Random(seed))
    login = 0
    tips_at = -99.0
    for day in range(1, days + 1):
        login += 1
        s.earn(P["daily_login"][(login - 1) % 7], "ежедневка")
        if a["payer"] and day == 2:
            s.rub += P["starter"]["rub"]
            s.buys.append((s.lvl(), P["starter"]["rub"], "стартовый"))
            s.earn(P["starter"]["coins"], "покупки")
            s.gem(P["starter"].get("gems", 0), "покупки")
        if s.lvl() >= P["challenge_at"]:
            s.earn(P["challenge_coins"], "испытание дня")
            s.gem(P["challenge_gems"], "испытание дня")
            if day % 7 == 0:
                s.earn(P["challenge_week_coins"], "испытание дня")
                s.gem(P["challenge_week_gems"], "испытание дня")
        if s.lvl() >= P["quests_at"]:
            n = sum(1 for _ in range(3) if s.r.random() < a["quests"])
            s.earn(P["quest_coins"] * n, "задания")
            # неделя: 3 задания + шкала из 5 сундуков; месяц: 3 задания + шкала (как в билде)
            if day % 7 == 1:
                q = P["week"]
                k = sum(1 for _ in range(3) if s.r.random() < q["done_p"] * a["quests"])
                s.earn(q["task_coins"] * k + q["chest_coins"], "задания недели")
                s.gem(q["chest_gems"], "задания недели")
            if day % 30 == 1:
                q = P["month"]
                k = sum(1 for _ in range(3) if s.r.random() < q["done_p"] * a["quests"])
                s.earn(q["task_coins"] * k + q["chest_coins"], "задания месяца")
                s.gem(q["chest_gems"], "задания месяца")
        if day % 7 == 1 and day > 1:
            s.earn(P["league_week"], "рейтинг")
        if s.lvl() >= 12:
            s.earn(P["rush_coins"], "час пик")
        ad_coins = 0
        for sess in range(a["sessions"]):
            now = (day - 1) * 24 + 9 + sess * 9
            budget = a["minutes"] / a["sessions"]
            if a["ads"] and now - tips_at >= 4 and s.lvl() >= 10:
                tips_at = now
                s.ads += 1
                s.earn(P["tips"], "чаевые (видео)")
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
                if s.blocked_since is None:
                    s.blocked_since = now
                    s.moment(0)
                    s.deficits.append((s.lvl(), max(0, sum(s.reno[s.dist(s.lvl())][s.bought[s.dist(s.lvl())]:]) - s.coins)))
                if a["payer"] and now - s.blocked_since >= P["payer_patience_h"]:
                    s.buy_pack("ремонт")
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
            s.seg[s.seg_key()].setdefault("gems", []).append(s.gems)
        if all(s.built):
            s.done.setdefault("дом построен", day)
        if all(s.built) and len(s.decor_bought) == 72:
            s.done.setdefault("дом обставлен", day)
    return s


def check():
    """Сверка: монеты за уровень в модели build против живого прохождения билда."""
    P = CFG["build"]
    print("уровень  живая игра  формула×(серия·Белка)  разница")
    tot_live = tot_mod = 0
    for lid, (live, stars) in LIVE.items():
        lv = LEVELS[lid - 1]
        streak = min(10, lid - 7)            # серия шла с 4-й победы на 11-м уровне
        sq = 1 if lid < 12 else 2 if lid < 17 else 3 if lid < 21 else 4
        m = (1 + P["streak_pct"][min(streak, len(P["streak_pct"]) - 1)] / 100) * (1 + P["squirrel_coin_pct"][sq] / 100)
        mod = int(V2.win_coins(lv, stars) * m)
        tot_live += live
        tot_mod += mod
        print(f"{lid:7d} {live:11d} {mod:22d} {mod - live:+8d}")
    print(f"итого {tot_live} против {tot_mod} ({100 * (tot_mod - tot_live) / tot_live:+.1f} %)")


def table(P):
    V6.table(P)
    print("Помощь в уровне (цена на уровнях 20 / 60 / 100 / 140):")
    for key, n in (("continue_coins", 0), ("continue_coins", 1), ("continue_coins", 2), ("prelevel_booster", 0)):
        print(f"  {key}[{n}]: " + " / ".join(str(help_price(P, key, l, n)) for l in (20, 60, 100, 140)))


def report(pn, P, runs_n, days, only, verbose):
    print(f"\n######## {pn} ########")
    for aname in CFG["players"]:
        if only and only not in aname:
            continue
        runs = [simulate(P, aname, 3000 + i, days) for i in range(runs_n)]
        med = V6.med
        dd = lambda k: med([r.done.get(k) for r in runs]) or "—"
        print(f"== {aname}")
        print(f"   день: Лёгкий {dd('Лёгкий')}, дом построен {dd('дом построен')}, обставлен {dd('дом обставлен')}, "
              f"Средний {dd('Средний')}, Сложный {dd('Сложный')}")
        tot_in = med([sum(r.inc.values()) for r in runs])
        cons = med([r.out.get("продолжения", 0) + r.out.get("бустеры", 0) for r in runs])
        print(f"   белка 4★ на уровне {med([r.squirrel4 for r in runs]) or '—'}; гринд {med([100 * r.grind / max(1, r.minutes) for r in runs])} %; "
              f"видео/день {med([r.ads for r in runs]) / days:.1f}; ₽ {med([r.rub for r in runs])}; "
              f"расходники {int(100 * cons / max(1, tot_in))} % дохода; остаток {med([r.coins for r in runs])}; "
              f"алмазы: получено {med([sum(r.gem_in.values()) for r in runs])}, на руках {med([r.gems for r in runs])}")
        print("   отрезок    монет(мед) избыток(мед)        выбор упор%  моменты оплаты (ремонт/проигрыш) на игрока  💎")
        for seg in sorted({k for r in runs for k in r.seg}):
            ds = [r.seg[seg] for r in runs if seg in r.seg]
            coins = med([statistics.median(d["coins"]) for d in ds])
            idle = [statistics.median(d["idle"]) for d in ds if d["idle"]]
            idle_share = sum(len(d["idle"]) for d in ds) / max(1, sum(d["sess"] for d in ds))
            ch = med([statistics.median(d["choice"]) for d in ds])
            bl = int(100 * sum(d["blocked"] for d in ds) / max(1, sum(d["sess"] for d in ds)))
            pm = [r.pay_moments.get(seg, [0, 0]) for r in runs]
            pm0 = sum(x[0] for x in pm) / len(runs)
            pm1 = sum(x[1] for x in pm) / len(runs)
            gm = med([statistics.median(d.get("gems", [0])) for d in ds])
            name = f"ур.{seg * 10 + 1}-{seg * 10 + 10}" if seg < 15 else ["", "Средний", "Сложный"][seg - 15]
            idle_s = ('%d (%d%% заходов)' % (med(idle), 100 * idle_share)) if idle else '—'
            print(f"   {name:10s} {coins:8d} {idle_s:>20s} {ch:6d} {bl:5d}   {pm0:5.1f} / {pm1:5.1f}{gm:20d}")
        dfs = [d for r in runs for (l, d) in r.deficits]
        if dfs:
            q = sorted(dfs)
            print(f"   упоров на игрока {len(dfs) / len(runs):.1f}; не хватало монет (медиана / 80-й процентиль): {q[len(q) // 2]} / {q[int(len(q) * 0.8)]}")
        if verbose:
            r = runs[0]
            ti = sum(r.inc.values())
            print("   доход: " + ", ".join(f"{k} {100 * v / ti:.0f}%" for k, v in sorted(r.inc.items(), key=lambda x: -x[1])))
            print("   траты: " + ", ".join(f"{k} {v}" for k, v in sorted(r.out.items(), key=lambda x: -x[1])))
            print("   алмазы: откуда " + ", ".join(f"{k} {int(v)}" for k, v in r.gem_in.items())
                  + "; куда " + ", ".join(f"{k} {v}" for k, v in r.gem_out.items()))
            if r.buys:
                print("   покупки (уровень, ₽, повод):", r.buys[:12], "…" if len(r.buys) > 12 else "")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preset", default="both")
    ap.add_argument("--runs", type=int, default=8)
    ap.add_argument("--days", type=int, default=150)
    ap.add_argument("--only", default="")
    ap.add_argument("--table", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("-v", action="store_true")
    args = ap.parse_args()
    if args.check:
        check()
        return
    presets = ["build", "v7"] if args.preset == "both" else [args.preset]
    for pn in presets:
        if args.table:
            print(f"######## {pn}")
            table(CFG[pn])
        else:
            report(pn, CFG[pn], args.runs, args.days, args.only, args.v)


if __name__ == "__main__":
    main()

# -*- coding: utf-8 -*-
"""Симулятор экономики v4 «Всё по полкам!» (03.10.2026).

python Tools/economy_sim_v4.py              — все типы игроков, 6 прогонов, сводка
python Tools/economy_sim_v4.py -v           — подробно: доход по источникам, когда что закончено
python Tools/economy_sim_v4.py --only реклам — один тип игрока

Чем отличается от economy_sim.py (v2): 150 уровней (районы 12–15 — по образцу 7–11), три режима сложности
по тем же уровням, Торговый дом (стройка по таймеру до 7 дней, декор, касса), алмазы (покупка, задания,
серия, Испытание дня, путь), задания дня со шкалами недели и месяца, серия побед, бонусы альбома,
Золотой путь. Числа — Tools/economy_v4.json (проект; при внедрении переедут в код).

Время идёт по часам: у игрока 1–2 захода в день, в каждом он забирает кассу, ставит стройку, покупает
ремонт и декор, играет уровни. Гринд — переигровки и видео за монеты, когда уровни закрыты ремонтом.
Цель: без доната и рекламы ~2 месяца на «Лёгкий» + дом + «Средний»; с рекламой ~35–45 дней; платящий ~25–30
(быстрее не выйдет — уровни надо играть руками).
"""
import argparse
import io
import json
import os
import random
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import economy_sim as V2  # noqa: E402  формулы уровня и данные из кода

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CFG = json.load(io.open(os.path.join(ROOT, "Tools", "economy_v4.json"), encoding="utf-8"))

ARCH = {
    # минуты в день, заходов, «×2» после победы, видео за монеты/ускорение, мастерство, платит
    "без рекламы и доната": dict(minutes=20, sessions=1, x2=0.0, ads=False, skill=0.0, payer=False, quests=0.85),
    "смотрит рекламу": dict(minutes=20, sessions=2, x2=0.7, ads=True, skill=0.0, payer=False, quests=0.9),
    "казуал (12 мин)": dict(minutes=12, sessions=1, x2=0.3, ads=True, skill=-0.06, payer=False, quests=0.6),
    "хардкор (50 мин)": dict(minutes=50, sessions=2, x2=0.5, ads=True, skill=0.07, payer=False, quests=1.0),
    "платящий": dict(minutes=25, sessions=2, x2=0.3, ads=False, skill=0.0, payer=True, quests=0.9),
}


def skip_cost(minutes):
    """Цена ускорения в алмазах за оставшиеся минуты (кусочно-линейно по skip_curve)."""
    pts = CFG["gems"]["skip_curve"]
    if minutes <= 0:
        return 0
    for (m0, c0), (m1, c1) in zip(pts, pts[1:]):
        if minutes <= m1:
            return max(1, int(round(c0 + (c1 - c0) * (minutes - m0) / (m1 - m0))))
    m0, c0 = pts[-2]
    m1, c1 = pts[-1]
    return int(round(c1 + (c1 - c0) * (minutes - m1) / (m1 - m0)))


def levels150():
    lv = V2.load_levels()
    out = list(lv)
    for i in range(len(lv) + 1, CFG["levels"] + 1):   # районы 12–15: по образцу уровней на 40 раньше
        src = dict(lv[i - 41 - 1])
        src.update(id=i, d=(i - 1) // 10 + 1, sets=src["sets"] + 1)
        out.append(src)
    return out


def reno_costs(items_per, first):
    st = CFG["stage_cost"]
    out = {}
    for d in range(1, CFG["districts"] + 1):
        n = items_per.get(d, 9)
        w = [0.7 + 0.6 * i / max(1, n - 1) for i in range(n)]
        ws, acc, cs = sum(w), 0, []
        for i in range(n):
            c = st[d] - acc if i == n - 1 else int(round(st[d] * w[i] / ws / 5.0) * 5)
            acc += c
            cs.append(c)
        out[d] = cs
    if first:
        out[1][0] = first
    return out


class P:
    def __init__(s, a, E, costs, levels, r):
        s.a, s.E, s.costs, s.lv, s.r = a, E, costs, levels, r
        s.coins = 0
        s.gems = 0
        s.gems_in, s.gems_out = {}, {}
        s.mode = 0
        s.reached = [0, 0, 0]          # последний пройденный уровень в режиме
        s.stars = {}
        s.bought = {d: 0 for d in range(1, CFG["districts"] + 1)}
        s.income = {}
        s.minutes = s.grind = 0.0
        s.rub, s.buys = 0, []
        s.ads = 0
        s.streak = 0
        s.depts = 0
        s.pack_wins = 0
        s.packs = 0
        s.stickers = 0
        # дом
        H = CFG["house"]
        s.rooms = H["rooms"]
        s.built = [False] * len(s.rooms)
        s.decor = [0] * len(s.rooms)
        s.building = -1
        s.build_end = 0.0
        s.build_ads = 0
        s.cash_since = None
        s.hammers = 0
        s.piggy = 0
        s.gold = False
        s.star_claimed = 0
        s.quest_week = s.quest_month = 0
        s.challenge_run = 0
        s.done = {}

    # ---- кошелёк
    def earn(s, n, why):
        s.coins += n
        s.income[why] = s.income.get(why, 0) + n

    def gem(s, n, why):
        s.gems += n
        s.gems_in[why] = s.gems_in.get(why, 0) + n

    def spend_gems(s, n, why):
        s.gems -= n
        s.gems_out[why] = s.gems_out.get(why, 0) + n

    def buy(s, rub, what):
        s.rub += rub
        s.buys.append(what)

    # ---- уровни
    def district(s, lid):
        return (lid - 1) // 10 + 1

    def stage_done(s, d):
        return s.bought[d] >= len(s.costs[d])

    def can_play_next(s):
        n = s.reached[s.mode] + 1
        if n > CFG["levels"]:
            return False
        if s.mode > 0:
            return True
        d = s.district(n)
        return d == 1 or s.stage_done(d - 1)

    def total_stars(s):
        return sum(s.stars.values())

    def play(s, lv, replay=False):
        M = CFG["modes"]
        m = s.mode
        p = min(0.98, V2.WIN_P.get(lv["diff"], 0.8) + s.a["skill"] - (0.1 if lv["rev"] else 0) - M["win_penalty"][m]
                + (0.04 if s.streak >= 3 else 0))
        t = 0.0
        first_try = True
        while True:
            t += (2.0 + 0.12 * lv["d"] + (0.6 if lv["diff"] in ("hard", "superhard") else 0)) * M["time_mult"][m]
            if s.r.random() < p:
                break
            first_try = False
            if not replay:
                s.streak = 0
            if s.coins >= s.E["LoaderPrice"] and s.r.random() < 0.25:
                s.coins -= s.E["LoaderPrice"]
                t += 0.5
                break
        p3 = min(0.95, V2.P3.get(lv["diff"], 0.6) + s.a["skill"] - M["win_penalty"][m])
        u = s.r.random()
        stars = 3 if u < p3 else 2 if u < p3 + 0.3 else 1
        coins = V2.win_coins(lv, stars) * M["coin_mult"][m] * (1 + CFG["album"]["dept_bonus_pct"] * s.depts / 100)
        coins = int(coins * (s.E["ReplayPct"] / 100 if replay else 1))
        if s.r.random() < s.a["x2"]:
            s.ads += 1
            s.earn(coins * (V2.reward_mult(lv["id"]) - 1), "реклама ×2")
        s.earn(coins, "уровни (повтор)" if replay else "уровни")
        key = (m, lv["id"])
        s.stars[key] = max(s.stars.get(key, 0), stars)
        if not replay:
            s.reached[m] = lv["id"]
            s.piggy = min(4000, s.piggy + 20 + coins // 20)
            if first_try:
                s.streak += 1
                st = CFG["streak"]
                if s.streak in st["chest_at"]:
                    k = st["chest_at"].index(s.streak)
                    s.earn(st["chest_coins"][k], "серия побед")
                    s.gem(st["chest_gems"][k], "серия побед")
                if s.streak >= st["chest_at"][-1]:
                    s.streak = 0
            s.pack_wins += 1
            if s.pack_wins >= s.E["WinsPerPack"]:
                s.pack_wins = 0
                s.packs += 1
        s.claim_path()
        return t

    def claim_path(s):
        SP = CFG["star_path"]
        st = s.total_stars()
        new = st - s.star_claimed
        if new <= 0:
            return
        s.star_claimed = st
        k = new / SP["max_stars"]
        s.earn(int(6000 * k), "звёздный путь")
        s.free_gems_acc = getattr(s, "free_gems_acc", 0.0) + CFG["gems"]["free"]["star_path_free"] * k
        while s.free_gems_acc >= 1:
            s.free_gems_acc -= 1
            s.gem(1, "звёздный путь")
        s.packs_acc = getattr(s, "packs_acc", 0.0) + 30 * k
        while s.packs_acc >= 1:
            s.packs_acc -= 1
            s.packs += 1
        if s.gold:
            s.earn(int(SP["gold_coins_total"] * k), "Золотой путь")
            s.gold_acc = getattr(s, "gold_acc", 0.0) + SP["gold_gems_total"] * k
            while s.gold_acc >= 1:
                s.gold_acc -= 1
                s.gem(1, "Золотой путь")
            s.ham_acc = getattr(s, "ham_acc", 0.0) + SP["gold_hammers"] * k
            while s.ham_acc >= 1:
                s.ham_acc -= 1
                s.hammers += 1

    def open_packs(s):
        if s.reached[0] < s.E["AlbumAt"]:
            return
        while s.packs > 0:
            s.packs -= 1
            s.stickers += 3
            # 90 наклеек (10 отделов × 9); дубли растут к концу — отдел в среднем за 3× свою длину пачек
            got = min(10, int(s.stickers / 27))
            while s.depts < got:
                s.depts += 1
                s.earn(CFG["album"]["dept_coins"], "альбом")
                if s.depts == 10:
                    s.gem(CFG["gems"]["free"]["album_full"], "альбом")
            s.earn(6, "альбом")   # дубли

    # ---- траты: ремонт → стройка дома → декор
    def buy_reno(s):
        if s.mode > 0:
            return
        cur = s.district(min(CFG["levels"], s.reached[0] + 1))
        for d in range(1, CFG["districts"] + 1):
            if d > cur:
                break
            while not s.stage_done(d):
                c = s.costs[d][s.bought[d]]
                if s.coins < c:
                    return
                s.coins -= c
                s.bought[d] += 1
                if s.stage_done(d):
                    s.earn(s.E["StageBonus"], "ремонт готов")
                    s.packs += 1

    def reno_reserve(s):
        """Сколько нужно на следующее улучшение ремонта, если оно открыто (дом не трогает эти деньги)."""
        if s.mode > 0:
            return 0
        d = s.district(min(CFG["levels"], s.reached[0] + 1))
        for x in range(1, d + 1):
            if not s.stage_done(x):
                return s.costs[x][s.bought[x]]
        return 0

    def house(s, now):
        H = CFG["house"]
        if s.reached[0] < s.rooms[0][1]:
            return
        if s.cash_since is None:
            s.cash_since = now
        # стройка закончилась
        if s.building >= 0 and now >= s.build_end:
            s.built[s.building] = True
            s.building = -1
        # касса
        if s.built[0]:
            hours = min(H["cash_hours"], now - s.cash_since)
            rate = (H["cash_base"] + H["cash_per_room"] * sum(s.built)) * 1.25
            cash = int(rate * hours)
            if cash > 0:
                s.earn(cash, "касса")
                if s.a["ads"] and s.r.random() < 0.5:
                    s.earn(cash, "касса ×2")
                    s.ads += 1
            s.cash_since = now
        # новая стройка
        if s.building < 0:
            for i, (name, at, cost, hrs) in enumerate(s.rooms):
                if s.built[i] or max(s.reached) < at:
                    continue
                prev_ok = i == 0 or all(s.decor[j] >= 2 for j in range(i) if s.built[j])
                if not prev_ok or s.coins - s.reno_reserve() < cost:
                    break
                s.coins -= cost
                s.building, s.build_end, s.build_ads = i, now + hrs, 0
                if hrs == 0:
                    s.built[i] = True
                    s.building = -1
                    continue
                break
        # ускорить: видео, молоток, алмазы
        if s.building >= 0:
            left = (s.build_end - now) * 60
            while s.a["ads"] and s.build_ads < H["speed_ad_max"] and left > 0:
                s.build_ads += 1
                s.ads += 1
                s.build_end -= H["speed_ad_minutes"] / 60
                left -= H["speed_ad_minutes"]
            if left > 0 and s.hammers > 0 and left >= 12 * 60:
                s.hammers -= 1
                s.build_end = now
                left = 0
            if left > 0:
                c = skip_cost(left)
                # бесплатные алмазы тратит только на длинную стройку, платящий — на всё дольше 4 ч
                want = (left >= 24 * 60) if not s.a["payer"] else (left >= 4 * 60)
                if s.a["payer"] and want and s.gems < c and left >= 24 * 60 and s.buys.count("алмазы 400") < 4:
                    s.buy(299, "алмазы 400")
                    s.gem(400, "покупка")
                if want and s.gems >= c:
                    s.spend_gems(c, "ускорение стройки")
                    s.build_end = now
            if now >= s.build_end:
                s.built[s.building] = True
                s.building = -1
        # декор: самый дешёвый следующий предмет в построенных комнатах
        while True:
            best = None
            for i, (name, at, cost, hrs) in enumerate(s.rooms):
                if not s.built[i] or s.decor[i] >= 6:
                    continue
                floor = i // 3
                c = int(H["decor_base"][s.decor[i]] * H["floor_mult"][floor])
                if i == 0 and s.decor[i] == 0:
                    c = 0
                if best is None or c < best[1]:
                    best = (i, c)
            if best is None or s.coins - s.reno_reserve() - 200 < best[1]:
                break
            s.coins -= best[1]
            s.decor[best[0]] += 1

    def house_built(s):
        return all(s.built)

    def house_full(s):
        return all(s.built) and all(d >= 6 for d in s.decor)


def simulate(name, E, costs, levels, seed, max_days=240):
    a = ARCH[name]
    r = random.Random(seed)
    p = P(a, E, costs, levels, r)
    Q, G = CFG["quests"], CFG["gems"]["free"]
    for day in range(1, max_days + 1):
        day_budget = a["minutes"] * r.uniform(0.7, 1.3)
        # платящий: Золотой путь на 3-й день, стартовый набор после 6-го уровня, копилка
        if a["payer"]:
            if not p.gold and day >= 3:
                p.gold = True
                p.buy(CFG["star_path"]["gold_path_rub"], "Золотой путь")
                p.star_claimed_gold_backfill = True
                tot = p.total_stars()
                p.star_claimed = 0
                p.claim_path()           # награды золотой строки за уже набранные звёзды
                p.coins -= int(6000 * tot / CFG["star_path"]["max_stars"])   # бесплатные за них уже получены
            if "стартовый" not in p.buys and p.reached[0] >= E.get("StarterOfferAt", 6):
                p.buy(CFG["iap"]["starter_rub"], "стартовый")
                p.earn(CFG["iap"]["starter_coins"], "покупки")
            if p.piggy >= 3000 and p.buys.count("копилка") < 6:
                p.buy(CFG["iap"]["piggy_rub"], "копилка")
                p.earn(p.piggy, "копилка")
                p.piggy = 0
        # ежедневки, чаевые
        if p.reached[0] >= E["DailyAt"]:
            i = (day - 1) % 7
            p.earn(E["DailyStreakCoins"][i] + (30 if a["ads"] else 0), "ежедневка")
            if i in (2, 6):
                p.packs += 1
            # задания дня
            done = sum(1 for _ in range(Q["per_day"]) if r.random() < a["quests"])
            p.earn(done * Q["coins_each"], "задания дня")
            p.quest_week += done * Q["points_each"]
            p.quest_month += done * Q["points_each"]
            if i == 6:
                steps = min(Q["week_steps"], p.quest_week // Q["week_step_points"])
                p.earn(int(Q["week_coins"] * steps / Q["week_steps"]), "шкала недели")
                if steps >= Q["week_steps"]:
                    p.gem(G["weekly_track"], "шкала недели")
                p.gem(G["league_week"] if r.random() < 0.3 else 0, "рейтинг")
                p.quest_week = 0
            if day % 30 == 0:
                steps = min(Q["month_steps"], p.quest_month // Q["month_step_points"])
                p.earn(int(Q["month_coins"] * steps / Q["month_steps"]), "шкала месяца")
                if steps >= Q["month_steps"]:
                    p.gem(G["monthly_track"], "шкала месяца")
                p.quest_month = 0
            # Испытание дня
            if r.random() < a["quests"]:
                C = CFG["challenge"]
                p.earn(C["coins"], "испытание дня")
                p.gem(G["challenge_day"], "испытание дня")
                p.packs += 1
                p.challenge_run += 1
                day_budget -= C["minutes"]
                if p.challenge_run % 7 == 0:
                    p.earn(C["week_coins"], "испытание дня")
                    p.gem(G["challenge_week"], "испытание дня")
            else:
                p.challenge_run = 0
        if p.reached[0] >= E["TipsAt"]:
            p.earn(E["TipsReward"] * a["sessions"], "чаевые")
        coin_ads = E["AdCoinsPerDay"] if a["ads"] else 0
        rush = E["RushDailyRuns"] if p.reached[0] >= E["RushAt"] else 0
        for sess in range(a["sessions"]):
            now = (day - 1) * 24 + (12 if a["sessions"] == 1 else [10, 20][sess])
            budget = day_budget / a["sessions"]
            used = 0.0
            p.house(now)
            while used < budget:
                p.open_packs()
                p.buy_reno()
                p.house(now + used / 60)
                if p.can_play_next():
                    used += p.play(levels[p.reached[p.mode]])
                    continue
                if p.mode < 2 and p.reached[p.mode] >= CFG["levels"] and (p.mode > 0 or p.stage_done(CFG["districts"])):
                    p.done[CFG["modes"]["names"][p.mode]] = day
                    p.mode += 1
                    continue
                if p.mode == 2 and p.reached[2] >= CFG["levels"]:
                    p.done.setdefault("Сложный", day)
                    # всё пройдено — заходит только за домом
                    break
                # упёрся в ремонт: гринд
                t = 0.0
                if p.a["payer"] and p.buys.count("мешок монет") < 3:
                    p.buy(CFG["iap"]["coin_bag_rub"], "мешок монет")
                    p.earn(CFG["iap"]["coin_bag"], "покупки")
                    continue
                d = p.district(p.reached[0] + 1) - 1
                if coin_ads > 0:
                    coin_ads -= 1
                    p.ads += 1
                    p.earn(E["AdCoinsBase"] + E["AdCoinsPerDistrict"] * d, "видео за монеты")
                    t = 0.6
                elif rush > 0:
                    rush -= 1
                    p.earn(min(E["RushMaxCoins"], int(r.uniform(30, 110))), "час пик")
                    t = 1.8
                else:
                    cand = [l for l in levels[:p.reached[0]] if p.stars.get((0, l["id"]), 0) < 3]
                    lv = cand[-1] if cand else levels[p.reached[0] - 1 - r.randrange(min(10, p.reached[0]))]
                    t = p.play(lv, replay=True)
                used += t
                p.grind += t
            p.minutes += used
            p.house(now + used / 60)
        if "дом построен" not in p.done and p.house_built():
            p.done["дом построен"] = day
        if "дом обставлен" not in p.done and p.house_full():
            p.done["дом обставлен"] = day
        if "Сложный" in p.done and p.house_full():
            return p, day
    return p, max_days


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=6)
    ap.add_argument("--only", default="")
    ap.add_argument("-v", action="store_true")
    args = ap.parse_args()
    E, _stage, items_per, _track, first = V2.load_code()
    costs = reno_costs(items_per, first)
    levels = levels150()
    H = CFG["house"]
    decor = sum(int(b * H["floor_mult"][i // 3]) for i in range(len(H["rooms"])) for b in H["decor_base"])
    print(f"Ремонт 15 районов: {sum(CFG['stage_cost'])}; дом: стройка {sum(r[2] for r in H['rooms'])} + декор {decor}; "
          f"таймеры дома: {sum(r[3] for r in H['rooms']) / 24:.1f} сут")
    print("Цена ускорения, алмазы: 5 мин", skip_cost(5), "· 1 ч", skip_cost(60), "· 4 ч", skip_cost(240),
          "· 1 сут", skip_cost(1440), "· 7 сут", skip_cost(10080))
    print()
    for name in ARCH:
        if args.only and args.only not in name:
            continue
        res = [simulate(name, E, costs, levels, 500 + k) for k in range(args.runs)]
        def avg(key):
            v = [p.done.get(key) for p, _ in res if p.done.get(key)]
            return f"{statistics.mean(v):.0f}" if v else "—"
        print(f"== {name}")
        print(f"   день: Лёгкий 150 — {avg('Лёгкий')}, дом построен — {avg('дом построен')}, обставлен — {avg('дом обставлен')}, "
              f"Средний — {avg('Средний')}, Сложный — {avg('Сложный')}")
        hrs = statistics.mean(p.minutes for p, _ in res) / 60
        gr = statistics.mean(p.grind / max(1, p.minutes) for p, _ in res) * 100
        print(f"   часов игры {hrs:.0f}, гринд {gr:.0f} %, видео {statistics.mean(p.ads for p, _ in res):.0f}, "
              f"покупки {statistics.mean(p.rub for p, _ in res):.0f} ₽, остаток монет {statistics.mean(p.coins for p, _ in res):.0f}")
        gi = statistics.mean(sum(p.gems_in.values()) for p, _ in res)
        go = statistics.mean(sum(p.gems_out.values()) for p, _ in res)
        print(f"   алмазы: получено {gi:.0f}, потрачено {go:.0f}")
        if args.v:
            p = res[0][0]
            tot = sum(p.income.values())
            print("   доход:", ", ".join(f"{k} {v * 100 // tot}%" for k, v in sorted(p.income.items(), key=lambda x: -x[1])))
            print("   алмазы откуда:", p.gems_in, "куда:", p.gems_out)
            print("   покупки:", p.buys)
            print("   готово по дням:", p.done)
        print()


if __name__ == "__main__":
    main()

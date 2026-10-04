# -*- coding: utf-8 -*-
"""Симулятор экономики «Всё по полкам!» (30.09.2026).

python Tools/economy_sim.py                 — все типы игроков, 5 прогонов каждого, сводка
python Tools/economy_sim.py --runs 20 -v    — больше прогонов и подробности по районам
python Tools/economy_sim.py --stage 0,400,800,...   — проверить другие цены ремонта, не трогая код

Цены и награды читаются прямо из кода (Economy.cs, MetaCatalog.cs), уровни — из levels.json,
поэтому после правки цен достаточно запустить скрипт ещё раз. Формулы монет повторяют
Economy.WinCoins / ReplayCoins / RewardMultiplier и GameApp.RegisterWin — при их правке поправить и здесь.

Модель игрока: каждый день у него есть N минут; он забирает ежедневные награды, «Чаевые», «Час пик»,
играет уровни по порядку и сразу покупает улучшения ремонта, как только хватает монет. Если уровни района
пройдены, а ремонт не закончен (следующий район закрыт) — он «гриндит»: смотрит видео за монеты (если
смотрит рекламу) и переигрывает уровни на три звезды (30 % монет). Время гринда — главный показатель:
«немного погриндить» = 10–25 % времени у игрока без рекламы, почти ноль у того, кто смотрит «×2».
"""
import argparse
import io
import json
import os
import random
import re
import statistics
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SCR = os.path.join(ROOT, "Assets", "AllOnShelves", "Scripts")


def read(rel):
    return io.open(os.path.join(SCR, rel), encoding="utf-8-sig").read()


# ------------------------------------------------------------------ данные из кода
def load_code():
    eco = read("Core/Economy.cs")
    E = {}
    for decl in re.findall(r"public const int ([^;]+);", eco):   # бывает «A = 3, B = 6, …» в одной строке
        for k, v in re.findall(r"(\w+) = (-?\d+)", decl):
            E[k] = int(v)
    rc = re.search(r"ReplayCoins\(int winCoins\) => \(int\)\(winCoins \* ([\d.]+)f\)", eco)
    E["ReplayPct"] = int(round(float(rc.group(1)) * 100)) if rc else 30
    E["DailyStreakCoins"] = [int(x) for x in re.search(r"DailyStreakCoins = \{([^}]*)\}", eco).group(1).split(",")]
    meta = read("Services/MetaCatalog.cs")
    stage = [int(x) for x in re.search(r"StageCost = \{([^}]*)\}", meta).group(1).split(",")]
    raw = re.findall(r"\((\d+), \"(\w+)\", \"[^\"]*\", \"[^\"]*\"\)", meta)
    items_per = {}
    for d, _ in raw:
        items_per[int(d)] = items_per.get(int(d), 0) + 1
    track = [(int(s), k, int(a)) for s, k, a in re.findall(r"new StarReward\((\d+), \"(\w+)\", (\d+)", meta)]
    reno_bonus = re.search(r"AddCoins\((\d+), \"stage\"\)", read("Services/GameApp.cs"))
    E["StageBonus"] = int(reno_bonus.group(1)) if reno_bonus else 0
    first = re.search(r"_items\[0\]\.Cost = (\d+);", meta)
    return E, stage, items_per, track, int(first.group(1)) if first else 0


def item_costs(stage, items_per, first_cost):
    """Как MetaCatalog.Items: веса 0.7..1.3, округление до 5, последний добирает остаток."""
    out = {}
    for d in range(1, 12):
        n = items_per.get(d, 0)
        total = stage[d]
        w = [0.7 + 0.6 * i / max(1, n - 1) for i in range(n)]
        ws = sum(w)
        acc, costs = 0, []
        for i in range(n):
            c = total - acc if i == n - 1 else int(round(total * w[i] / ws / 5.0) * 5)
            acc += c
            costs.append(c)
        out[d] = costs
    if first_cost:
        out[1][0] = first_cost
    return out


def load_levels():
    d = json.load(io.open(os.path.join(ROOT, "Assets", "AllOnShelves", "Resources", "Levels", "levels.json"), encoding="utf-8-sig"))
    out = []
    for l in sorted(d["levels"], key=lambda x: x["id"]):
        out.append(dict(
            id=l["id"], d=l["district"], diff=l["difficulty"], rev=l["revision"],
            sets=sum(g["sets"] for g in l["goals"]),
            sale=sum(1 for s in l["sections"] if s["kind"] == "sale"),
            cust=len(l["customers"]),
        ))
    return out


# ------------------------------------------------------------------ формулы монет (как в Economy.cs)
def base(d):
    return 20 + 5 * d


def diff_mult(lv):
    if lv["rev"]:
        return 2.0
    return {"hard": 1.5, "superhard": 2.0}.get(lv["diff"], 1.0)


def star_bonus(s):
    return 10 if s >= 3 else 5 if s == 2 else 0


def win_coins(lv, stars):
    tips = lv["cust"] * 3  # чаевые покупателей: около одного обслуженного на покупателя
    return int(base(lv["d"]) * diff_mult(lv)) + lv["sets"] + lv["sale"] * 2 + star_bonus(stars) + tips


def reward_mult(level_id):
    return 3 if level_id >= 100 else 2


DEPTS_OPEN_AT = [1, 1, 1, 1, 1, 2, 2, 4, 2, 3]  # MetaCatalog.DepartmentDistrict


# ------------------------------------------------------------------ типы игроков
ARCHETYPES = {
    # имя: минуты в день, шанс смотреть «×2», смотрит видео за монеты, мастерство, покупает бустеры за монеты, платит
    "без рекламы": dict(minutes=30, x2=0.0, coin_ads=False, skill=0.0, booster=0.15, daily_level=True, rush=True, payer=False),
    "смотрит рекламу": dict(minutes=30, x2=0.75, coin_ads=True, skill=0.0, booster=0.3, daily_level=True, rush=True, payer=False),
    "казуал (15 мин)": dict(minutes=15, x2=0.35, coin_ads=True, skill=-0.08, booster=0.1, daily_level=False, rush=False, payer=False),
    "хардкор (60 мин)": dict(minutes=60, x2=0.5, coin_ads=True, skill=0.07, booster=0.3, daily_level=True, rush=True, payer=False),
    "платящий": dict(minutes=30, x2=0.4, coin_ads=False, skill=0.0, booster=0.5, daily_level=True, rush=True, payer=True),
}

WIN_P = {"tutorial": 1.0, "easy": 0.95, "medium": 0.85, "hard": 0.7, "superhard": 0.55}
P3 = {"tutorial": 0.9, "easy": 0.75, "medium": 0.6, "hard": 0.45, "superhard": 0.35}


class Player:
    def __init__(self, arch, E, costs, track, rng, stage):
        self.a, self.E, self.costs, self.track, self.r, self.stage = arch, E, costs, track, rng, stage
        self.coins = 0
        self.max_reached = 0
        self.stars = {}
        self.bought = {d: 0 for d in range(1, 12)}  # сколько улучшений района куплено (по порядку)
        self.claimed_track = set()
        self.stickers = set()
        self.depts_done = set()
        self.wins_since_pack = 0
        self.pending_packs = 0
        self.minutes = 0.0
        self.grind_minutes = 0.0
        self.grind_by_d = {}
        self.blocked_events = 0
        self.booster_spent = 0
        self.iap_rub = 0
        self.iap_list = []
        self.ad_views = 0
        self.income = {}
        self.day_done = {}
        self.minutes_done = {}
        self.streak = 0
        self.starter = False
        self.helpers = 0

    # --- состояние
    def district_of(self, lid):
        return (lid - 1) // 10 + 1

    def stage_complete(self, d):
        return self.bought[d] >= len(self.costs[d])

    def can_play(self, lid):
        if lid > 110 or lid > self.max_reached + 1:
            return False
        d = self.district_of(lid)
        return d == 1 or self.stage_complete(d - 1) or lid <= self.max_reached

    def current_district(self):
        return self.district_of(min(110, self.max_reached + 1))

    def stars_total(self):
        return sum(self.stars.values())

    def earn(self, n, why):
        self.coins += n
        self.income[why] = self.income.get(why, 0) + n

    # --- покупки ремонта: сразу, как только хватает (игрок хочет открыть следующий район)
    def buy_reno(self):
        cur = self.current_district()
        for d in range(1, 12):
            open_ = d <= cur or (d == cur + 1 and self.max_reached >= d * 10 - 10)
            if not open_:
                break
            while not self.stage_complete(d):
                c = self.costs[d][self.bought[d]]
                if self.coins < c:
                    return
                self.coins -= c
                self.bought[d] += 1
                if self.stage_complete(d):
                    self.earn(self.E["StageBonus"], "ремонт готов")
                    self.pending_packs += 1

    # --- «Звёздный путь» и пачки
    def claim_track(self):
        st = self.stars_total()
        for i, (s, kind, amount) in enumerate(self.track):
            if st >= s and i not in self.claimed_track:
                self.claimed_track.add(i)
                if kind == "coins":
                    self.earn(amount, "звёздный путь")
                elif kind == "pack":
                    self.pending_packs += amount

    def open_packs(self):
        if self.max_reached < self.E["AlbumAt"]:
            return
        cur = self.current_district()
        open_depts = [i for i in range(10) if DEPTS_OPEN_AT[i] <= cur]
        while self.pending_packs > 0:
            self.pending_packs -= 1
            coins = 0
            for _ in range(3):
                dept = self.r.choice(open_depts)
                if self.r.random() < 0.03:
                    coins += self.E["DuplicateGoldSticker"] if ("g", dept) in self.stickers else 0
                    self.stickers.add(("g", dept))
                    continue
                missing = [k for k in range(9) if (dept, k) not in self.stickers]
                k = self.r.choice(missing) if missing and self.r.random() < 0.6 else self.r.randrange(9)
                if (dept, k) in self.stickers:
                    coins += self.E["DuplicateSticker"]
                self.stickers.add((dept, k))
            for dept in range(10):
                if dept not in self.depts_done and all((dept, k) in self.stickers for k in range(9)):
                    self.depts_done.add(dept)
                    coins += self.E["DepartmentReward"]
            if coins:
                self.earn(coins, "альбом")

    # --- уровень
    def level_minutes(self, lv):
        return 2.0 + 0.12 * lv["d"] + (0.6 if lv["diff"] in ("hard", "superhard") else 0)

    def play(self, lv, replay=False):
        """Одна попытка + повторы до победы (FreeRewind и «Заново»). Возвращает минуты."""
        t = 0.0
        p = min(0.99, WIN_P.get(lv["diff"], 0.8) + self.a["skill"] - (0.1 if lv["rev"] else 0))
        while True:
            t += self.level_minutes(lv)
            if self.r.random() < p:
                break
            # поражение: часть игроков тратит монеты на грузчика / отмены
            if self.r.random() < self.a["booster"] and self.coins >= self.E["LoaderPrice"]:
                self.coins -= self.E["LoaderPrice"]
                self.booster_spent += self.E["LoaderPrice"]
                t += 0.5
                break
            t *= 1.0  # переигровка
        p3 = min(0.95, P3.get(lv["diff"], 0.6) + self.a["skill"])
        u = self.r.random()
        stars = 3 if u < p3 else 2 if u < p3 + 0.3 else 1
        # в трудных уровнях иногда берут подсказки за монеты
        if lv["diff"] in ("hard", "superhard") and self.r.random() < self.a["booster"] * 0.3 and self.coins >= self.E["HintPackPrice"]:
            self.coins -= self.E["HintPackPrice"]
            self.booster_spent += self.E["HintPackPrice"]
        coins = win_coins(lv, stars)
        first = not replay and lv["id"] > self.max_reached
        if replay:
            coins = int(coins * self.E["ReplayPct"] / 100)
        mult = reward_mult(lv["id"]) if self.r.random() < self.a["x2"] else 1
        if mult > 1:
            self.ad_views += 1
        self.earn(coins, "уровни (повтор)" if replay else "уровни")
        if mult > 1:
            self.earn(coins * (mult - 1), "реклама ×2")
        self.stars[lv["id"]] = max(self.stars.get(lv["id"], 0), stars)
        if first:
            self.max_reached = lv["id"]
            self.wins_since_pack += 1
            if self.wins_since_pack >= self.E["WinsPerPack"]:
                self.wins_since_pack = 0
                self.pending_packs += 1
            if lv["rev"]:
                self.pending_packs += 1
        self.claim_track()
        return t


def simulate(arch_name, E, costs, track, levels, seed, max_days=200, stage=None):
    a = ARCHETYPES[arch_name]
    r = random.Random(seed)
    P = Player(a, E, costs, track, r, stage)
    ad_coins = E.get("AdCoinsPerDay", 0)
    for day in range(1, max_days + 1):
        budget = a["minutes"] * r.uniform(0.7, 1.3)
        used = 0.0
        # ежедневные награды — с уровня DailyAt
        if P.max_reached >= E["DailyAt"]:
            i = P.streak % 7
            P.earn(E["DailyStreakCoins"][i], "ежедневка")
            if i == 2:
                P.pending_packs += 1
            if i == 6:
                P.pending_packs += 1
            P.streak += 1
            if a["x2"] > 0.3:
                P.earn(int(E["DailyStreakCoins"][i] * 0.5) if E["DailyStreakCoins"][i] else 30, "ежедневка (видео)")
                P.ad_views += 1
        if P.max_reached >= E["TipsAt"]:
            P.earn(E["TipsReward"] * (2 if a["minutes"] >= 30 else 1), "чаевые")
        coin_ads_left = ad_coins if a["coin_ads"] else 0
        rush_left = E["RushDailyRuns"] if a["rush"] and P.max_reached >= E["RushAt"] else 0
        daily_level = a["daily_level"] and P.max_reached >= E["DailyAt"]
        if daily_level:
            P.earn(E["DailyLevelCoins"] + 12, "завоз дня")
            P.pending_packs += 1
            used += 3.0
        # платящий: стартовый набор, когда его покажут, и мешок монет, если застрял
        if a["payer"] and not P.starter and P.max_reached >= E.get("StarterOfferAt", 10):
            P.starter = True
            P.iap_rub += E.get("_starter_rub", 99)
            P.iap_list.append("стартовый")
            P.earn(E.get("_starter_coins", 1000), "покупки")
        while used < budget:
            P.open_packs()
            P.buy_reno()
            nxt = P.max_reached + 1
            if nxt > 110:
                if all(P.stage_complete(d) for d in range(1, 12)):
                    break
            if nxt <= 110 and P.can_play(nxt):
                used += P.play(levels[nxt - 1])
                d = P.district_of(nxt)
                if nxt % 10 == 0 and d not in P.day_done:
                    pass
                continue
            # застрял: уровни района пройдены, ремонт не закрыт
            d = P.current_district() if nxt <= 110 else 11
            d_block = P.district_of(nxt) - 1 if nxt <= 110 else 11
            if d_block not in P.grind_by_d:
                P.blocked_events += 1
                P.grind_by_d[d_block] = 0.0
            if a["payer"] and P.iap_list.count("мешок") < 3:
                P.iap_rub += 129
                P.iap_list.append("мешок")
                P.earn(2000, "покупки")
                continue
            t = 0.0
            if coin_ads_left > 0:
                coin_ads_left -= 1
                P.ad_views += 1
                P.earn(E.get("AdCoinsBase", 0) + E.get("AdCoinsPerDistrict", 0) * d_block, "видео за монеты")
                t = 0.6
            elif rush_left > 0:
                rush_left -= 1
                P.earn(min(E["RushMaxCoins"], int(r.uniform(30, 110))), "час пик")
                t = 1.8
            else:
                # переигровка: сначала уровни без трёх звёзд, иначе последние уровни
                cands = [l for l in levels if l["id"] <= P.max_reached and P.stars.get(l["id"], 0) < 3]
                lv = min(cands, key=lambda l: -l["id"]) if cands else levels[P.max_reached - 1 - r.randrange(min(10, P.max_reached))]
                t = P.play(lv, replay=True)
            used += t
            P.grind_minutes += t
            P.grind_by_d[d_block] += t
        P.minutes += used
        for dd in range(1, 12):
            if dd not in P.day_done and P.max_reached >= dd * 10 and P.stage_complete(dd):
                P.day_done[dd] = day
                P.minutes_done[dd] = P.minutes
        if P.max_reached >= 110 and all(P.stage_complete(x) for x in range(1, 12)):
            return P, day
    return P, max_days


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=5)
    ap.add_argument("--stage", type=str, default="")
    ap.add_argument("-v", action="store_true")
    ap.add_argument("--only", type=str, default="")
    ap.add_argument("--set", action="append", default=[], help="Константа=значение поверх Economy.cs, напр. DepartmentReward=150")
    args = ap.parse_args()
    E, stage, items_per, track, first = load_code()
    if args.stage:
        stage = [int(x) for x in args.stage.split(",")]
    E.setdefault("_starter_rub", 99)
    for kv in args.set:
        k, v = kv.split("=")
        E[k] = int(v)
    costs = item_costs(stage, items_per, first)
    levels = load_levels()

    print("Цены ремонта:", stage[1:], "итого", sum(stage))
    inc1 = {}
    for lv in levels:
        inc1[lv["d"]] = inc1.get(lv["d"], 0) + win_coins(lv, 3)
    print("Доход района за первое прохождение на 3★ без рекламы:", [inc1[d] for d in range(1, 12)])
    print("Ремонт / доход:", [round(stage[d] / inc1[d], 2) for d in range(1, 12)])
    print()
    for name in ARCHETYPES:
        if args.only and args.only not in name:
            continue
        res = []
        for run in range(args.runs):
            P, days = simulate(name, E, costs, track, levels, seed=1000 + run)
            res.append((P, days))
        days = [d for _, d in res]
        grind = [P.grind_minutes / max(1, P.minutes) * 100 for P, _ in res]
        hours = [P.minutes / 60 for P, _ in res]
        print(f"== {name}: {args.runs} прогонов")
        print(f"   дней до конца игры: {statistics.mean(days):.0f} (от {min(days)} до {max(days)}), часов игры: {statistics.mean(hours):.1f}")
        print(f"   гринд: {statistics.mean(grind):.0f} % времени (от {min(grind):.0f} до {max(grind):.0f}); "
              f"упёрся в ремонт: {statistics.mean(P.blocked_events for P, _ in res):.1f} раз из 10")
        print(f"   на бустеры потрачено: {statistics.mean(P.booster_spent for P, _ in res):.0f} монет; "
              f"просмотров рекламы за награду: {statistics.mean(P.ad_views for P, _ in res):.0f}; "
              f"покупки: {statistics.mean(P.iap_rub for P, _ in res):.0f} ₽; остаток монет: {statistics.mean(P.coins for P, _ in res):.0f}")
        if args.v:
            P = res[0][0]
            tot = sum(P.income.values())
            print("   доход (1-й прогон):", ", ".join(f"{k} {v * 100 // tot}%" for k, v in sorted(P.income.items(), key=lambda x: -x[1])))
            print("   гринд по районам, мин (1-й прогон):", {d: round(t) for d, t in sorted(P.grind_by_d.items())})
            print("   район готов на день:", P.day_done)
        print()


if __name__ == "__main__":
    main()

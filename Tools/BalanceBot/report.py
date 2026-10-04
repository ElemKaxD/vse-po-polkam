# -*- coding: utf-8 -*-
"""Инфографика по данным бота: Temp/balancebot/report.html (графики matplotlib + выводы)."""
import base64
import glob
import io
import json
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Temp", "balancebot")
RUN = os.path.join(OUT, "run.json")
ECON = os.path.join(OUT, "econ.json")
LEVELS = os.path.join(ROOT, "Assets", "AllOnShelves", "Resources", "Levels", "levels.json")

plt.rcParams.update({"figure.dpi": 110, "font.size": 9, "axes.grid": True,
                     "grid.alpha": 0.3, "axes.titlesize": 11})


def fig64(fig):
    buf = io.BytesIO()
    fig.savefig(buf, format="png", bbox_inches="tight")
    plt.close(fig)
    return base64.b64encode(buf.getvalue()).decode()


def load():
    run = json.load(open(RUN, encoding="utf-8")) if os.path.exists(RUN) else {}
    econ = json.load(open(ECON, encoding="utf-8")) if os.path.exists(ECON) else {}
    lv = json.load(open(LEVELS, encoding="utf-8"))["levels"]
    return run, econ, {l["id"]: l for l in lv}


def district_of(lid):
    return (lid - 1) // 10 + 1


def build():
    run, econ, levels = load()
    ids = sorted(int(k) for k in run if not k.startswith("_"))
    cards = []
    conclusions, warnings, todo = [], [], []

    # ---------- 1. Проходимость и эффективность ходов ----------
    if ids:
        # «?» = авто-игрок встал в тупик (тележка полна) — уровень решаем руками, это не провал
        stuck_lv = [i for i in ids if run[str(i)].get("result") == "?"]
        eff = {i: (levels[i]["metrics"]["moves"] / run[str(i)]["moves"])
               for i in ids if run[str(i)].get("result") == "Win" and run[str(i)]["moves"] > 0}
        wins = [i for i in ids if run[str(i)].get("result") == "Win"]
        fails = [i for i in ids if run[str(i)].get("result") not in ("Win", "?")]
        fig, ax = plt.subplots(figsize=(10, 3.6))
        xs = sorted(eff)
        ax.bar(xs, [eff[i] for i in xs], color=["#d44" if eff[i] < 0.55 else "#2a8" if eff[i] > 0.9 else "#48c" for i in xs])
        for i in stuck_lv:
            ax.axvline(i, color="#c92", lw=2, alpha=0.35)
        ax.set_xlabel("уровень (оранжевые полосы — тупик авто-игрока)"); ax.set_ylabel("оптимум солвера / ходы бота")
        ax.set_title("Эффективность ходов (1.0 = бот идёт по оптимальному решению; ниже — сложнее)")
        ax.axhline(0.7, color="k", ls="--", lw=0.8)
        cards.append(("Проходимость", fig64(fig), ""))
        wr = len(wins) / max(1, len(ids) - len(stuck_lv))
        cards.append(("Итог прогона", "", f"Побед: <b>{len(wins)}/{len(ids) - len(stuck_lv)}</b> ({wr:.0%}). "
                      + (f"Тупик авто-игрока (решается руками): {stuck_lv}." if stuck_lv else "")
                      + (f" Не пройдены: {fails}" if fails else "")))
        tight = [i for i in xs if eff[i] < 0.55]
        easy = [i for i in xs if eff[i] > 0.9]
        if tight:
            warnings.append(f"Очень плотные уровни (ходов бота почти вдвое больше оптимума): {tight} — проверить честность.")
        if easy:
            conclusions.append(f"Лёгкие уровни (эффективность &gt;0.9): {len(easy)} шт. — запас сложности не используется.")
        stuck = [(i, run[str(i)]["stuck"]) for i in ids if run[str(i)].get("stuck", 0) >= 3 and run[str(i)].get("result") != "?"]
        if stuck:
            warnings.append(f"Тупики автоплея у решённых уровней: {stuck}.")
        if stuck_lv:
            conclusions.append(f"Тупик авто-игрока на {len(stuck_lv)} уровнях {stuck_lv} — по ГДД решается руками "
                               "(кнопка «вынести из тележки»/ходы в запасе); стоит проверить руками играбельность.")

    # ---------- 2. Сложность по районам ----------
    if ids:
        fig, ax = plt.subplots(figsize=(10, 3.2))
        ds = sorted({district_of(i) for i in ids})
        means = []
        for d in ds:
            e = [levels[i]["metrics"]["moves"] / run[str(i)]["moves"]
                 for i in ids if district_of(i) == d and run[str(i)].get("result") == "Win" and run[str(i)]["moves"] > 0]
            means.append(sum(e) / len(e) if e else 0)
        ax.plot(ds, means, "o-", color="#48c")
        ax.set_xticks(ds); ax.set_xlabel("район"); ax.set_ylabel("средняя эффективность")
        ax.set_title("Сложность по районам (ниже = тяжелее уровни)")
        cards.append(("Сложность по районам", fig64(fig), ""))

    # ---------- 3. Механики ----------
    mech_intro, mech_count = {}, {}
    for lid, l in sorted(levels.items()):
        m = l.get("mechanic") or ""
        if m:
            mech_intro.setdefault(m, lid)
            mech_count[m] = mech_count.get(m, 0) + 1
    if mech_intro:
        fig, ax = plt.subplots(figsize=(10, 3.2))
        ms = sorted(mech_intro, key=lambda m: mech_intro[m])
        ax.barh(ms, [mech_count[m] for m in ms], color="#c96")
        for k, m in enumerate(ms):
            ax.text(0.3, k, f"с {mech_intro[m]} ур.", va="center", fontsize=8)
        ax.set_xlabel("число уровней"); ax.set_title("Покрытие механик (когда вводится и сколько уровней использует)")
        cards.append(("Механики", fig64(fig), ""))
        late = [m for m in ms if mech_count[m] <= 1]
        if late:
            todo.append(f"Механики всего в 1 уровне: {late} — после ввода не повторяются, игрок их забудет.")
        todo.append("Не реализованы по ГДД (LiveOps после релиза): золотая карта, сезонные события.")

    # ---------- 4. Экономика ----------
    if econ:
        eco = econ.get("economy", {})
        reno = econ.get("reno", [])
        cost_by_d = {}
        for it in reno:
            cost_by_d[it["district"]] = cost_by_d.get(it["district"], 0) + it["cost"]
        stage = [0, 280, 520, 620, 930, 1050, 1140, 1250, 1340, 1430, 1520, 1620][:econ.get("planner", {}).get("districts", 11) + 1]
        base = 20 + 5 * 1
        income = []
        acc = 0
        for lid in sorted(levels):
            d = levels[lid]["district"]
            b = 20 + 5 * d
            mult = 2 if levels[lid].get("revision") else {"hard": 1.5, "superhard": 2}.get(levels[lid].get("difficulty"), 1)
            acc += int(b * mult) + eco.get("DepartmentReward", 0) / 110 + eco.get("StarChestCoins", 150) / 40
            income.append(acc)
        fig, ax = plt.subplots(figsize=(10, 3.6))
        xs = sorted(levels)
        ax.plot(xs, income, label="накопленный доход (3★, без рекламы/повторов)", color="#2a8")
        cx = 0
        for d in sorted(cost_by_d):
            cx += cost_by_d[d] + stage[min(d, len(stage) - 1)]
            ax.axvline(d * 10 - 0.5, color="#999", lw=0.6)
            ax.plot(d * 10 - 0.5, cx, "ro", ms=5)
        ax.set_xlabel("уровень"); ax.set_ylabel("монеты")
        ax.set_title("Экономика: доход прогресса против расходов на ремонт (красное — накопленные траты к концу района)")
        ax.legend(fontsize=8)
        cards.append(("Экономика", fig64(fig), ""))
        # выводы по экономике
        for d in sorted(cost_by_d):
            district_income = income[d * 10 - 1] - (income[d * 10 - 11] if d > 1 else 0)
            spend = cost_by_d[d] + stage[min(d, len(stage) - 1)]
            if spend > district_income:
                warnings.append(f"Район {d}: ремонт ({spend:.0f} монет) дороже дохода района ({district_income:.0f}) — игрок встанет в grind.")
        conclusions.append(f"Товары магазина: {len(econ.get('shop', []))} позиций; предметов ремонта: {len(reno)} ("
                           f"{sum(i['cost'] for i in reno)} монет всего).")

    # ---------- 5. Визуальный контроль ----------
    shots = sorted(glob.glob(os.path.join(OUT, "shots", "*.png")))
    if shots:
        img_html = "".join(
            f"<figure><img src='data:image/png;base64,{base64.b64encode(open(p, 'rb').read()).decode()}'/>"
            f"<figcaption>{os.path.basename(p)}</figcaption></figure>"
            for p in shots)
        cards.append(("Скриншоты прогона", "", img_html))

    # ---------- 6. Чек-лист «чего не хватает / чего много» ----------
    if run.get("_console_errors"):
        warnings.append("Ошибки консоли за прогон: " + "; ".join(run["_console_errors"][:5]))
    else:
        conclusions.append("Ошибок консоли за прогон нет.")
    checklist = "<ul>" + "".join(f"<li>⚠ {t}</li>" for t in warnings) + "".join(f"<li>✅ {c}</li>" for c in conclusions) \
                + "".join(f"<li>📋 {t}</li>" for t in todo) + "</ul>"
    cards.append(("Выводы бота", "", checklist))

    html = f"""<!doctype html><html><head><meta charset='utf-8'><title>Баланс «Всё по полкам!»</title>
<style>
body{{font-family:'Segoe UI',Arial,sans-serif;background:#171717;color:#eee;margin:24px}}
h1{{font-size:22px}} .card{{background:#222;border:1px solid #333;border-radius:10px;padding:14px;margin:14px 0}}
.card h2{{font-size:15px;margin:0 0 10px;color:#ffd479}} img{{max-width:100%;border-radius:6px}}
figure{{display:inline-block;margin:6px}} figcaption{{font-size:11px;color:#999;text-align:center}}
li{{margin:4px 0}}
</style></head><body><h1>🤖 Отчёт бота-тестировщика — «Всё по полкам!»</h1>
<p>Прогон: {len(ids)} уровней · источник: Temp/balancebot (bot.py)</p>"""
    for title, img, body in cards:
        html += f"<div class='card'><h2>{title}</h2>"
        if img:
            html += f"<img src='data:image/png;base64,{img}'/>"
        html += body + "</div>"
    html += "</body></html>"
    path = os.path.join(OUT, "report.html")
    open(path, "w", encoding="utf-8").write(html)
    print("report ->", path)


if __name__ == "__main__":
    build()

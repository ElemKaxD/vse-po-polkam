using System;
using System.Collections.Generic;
using System.Linq;

namespace AllOnShelves.Core
{
    /// <summary>Параметры генерации одного уровня (ГДД 6.1, 6.5).</summary>
    public sealed class GenParams
    {
        public int Id;
        public int District;
        public string Difficulty = "easy";
        public int Sets = 6;
        public int Types = 4;
        public int Sections = 3;
        public int Window = 3;
        public int Visible = 8;
        public int Cart = 5;
        public int Extras;
        public int Locks;
        public int LockSets = 2;
        public int BoxPercent;
        public int Pallets;
        public int BundlePercent;
        public bool Customers;
        public int PerishTimer;
        public bool Freezer;
        public bool Big;
        public bool Sale;
        public bool TwoBelts;
        public bool Door;
        public bool Night;
        public bool Revision;
        public string Mechanic = "";
        public string Tutorial = "";
        /// <summary>Доступные товары (индексы каталога) — по прогрессу районов.</summary>
        public int[] Pool;
    }

    /// <summary>Генератор последовательностей (ГДД 6.6). Проходимость и сложность проверяет LevelEvaluator.</summary>
    public static class LevelGenerator
    {
        public static LevelData Generate(GenParams p, int seed)
        {
            var rng = new Random(seed);
            var pool = p.Pool.ToList();

            // ---- типы заказа
            var ordinary = pool.Where(t => !ItemCatalog.IsFrozen(t) && !ItemCatalog.IsBig(t)).ToList();
            var goalTypes = new List<int>();
            if (p.Freezer)
            {
                var frozen = pool.Where(ItemCatalog.IsFrozen).ToList();
                if (frozen.Count > 0) goalTypes.Add(frozen[rng.Next(frozen.Count)]);
            }
            if (p.Big)
            {
                var big = pool.Where(ItemCatalog.IsBig).ToList();
                if (big.Count > 0) goalTypes.Add(big[rng.Next(big.Count)]);
            }
            Shuffle(ordinary, rng);
            // свежие товары района — в приоритете
            ordinary = ordinary.OrderByDescending(t => ItemCatalog.Get(t).District == p.District ? 1 : 0).ThenBy(_ => rng.Next()).ToList();
            foreach (var t in ordinary)
            {
                if (goalTypes.Count >= p.Types) break;
                goalTypes.Add(t);
            }
            // покупатель просит товар НЕ из заказа. В первом районе всего 6 видов, и на 7-м уровне
            // (где енот знакомит с покупателем) все шесть уходили в заказ — покупателей не было вовсе
            // (обход 29.09.2026). Тогда один обычный вид отдаём под «лишний» товар покупателя
            if (p.Customers && goalTypes.Count > 2 && ordinary.All(goalTypes.Contains))
            {
                int last = goalTypes.FindLastIndex(t => ordinary.Contains(t));
                if (last >= 0) goalTypes.RemoveAt(last);
            }

            int types = goalTypes.Count;
            int sets = Math.Max(p.Sets, types);
            var setsPer = new int[types];
            for (int i = 0; i < types; i++) setsPer[i] = 1;
            for (int k = types; k < sets; k++) setsPer[rng.Next(types)]++;

            var tokens = new List<string>();
            for (int i = 0; i < types; i++)
            {
                int t = goalTypes[i];
                int n = setsPer[i] * ItemCatalog.SetSize(t);
                // запасных товаров на порчу и таяние больше нет: испорченный довозится (Rules.Restock),
                // а запасной оставался на полке после победы (02.10.2026)
                for (int k = 0; k < n; k++) tokens.Add(ItemCatalog.Get(t).Id);
            }

            // ---- лишние товары
            var extraTypes = ordinary.Where(t => !goalTypes.Contains(t)).Take(Math.Max(1, Math.Min(3, (p.Extras + 2) / 3))).ToList();
            for (int k = 0; k < p.Extras && extraTypes.Count > 0; k++) tokens.Add(ItemCatalog.Get(extraTypes[k % extraTypes.Count]).Id);

            // ---- ревизия: часть товаров уже на полках / в тележке
            var sections = new List<SectionData>();
            for (int i = 0; i < p.Sections; i++) sections.Add(new SectionData());
            var cartStart = new List<string>();
            if (p.Revision)
            {
                int prefills = Math.Min(2, p.Sections - 1);
                var candidates = goalTypes.Where(t => !ItemCatalog.IsFrozen(t) && !ItemCatalog.IsBig(t)).ToList();
                Shuffle(candidates, rng);
                for (int i = 0; i < prefills && i < candidates.Count; i++)
                {
                    int t = candidates[i];
                    int cnt = 1 + rng.Next(2);
                    sections[i].prefillType = ItemCatalog.Get(t).Id;
                    sections[i].prefillCount = cnt;
                    for (int k = 0; k < cnt; k++) tokens.Remove(ItemCatalog.Get(t).Id);
                }
                if (candidates.Count > 2)
                {
                    int t = candidates[2];
                    cartStart.Add(ItemCatalog.Get(t).Id);
                    tokens.Remove(ItemCatalog.Get(t).Id);
                }
            }

            Shuffle(tokens, rng);
            // лишний товар — не в хвосте ленты: последним едет товар заказа, иначе уровень кончался, а лишнее
            // ещё лежало на ленте (02.10.2026). Хвост — последняя четверть, но не меньше 5 позиций
            {
                var extraIds = new HashSet<string>(extraTypes.Select(t => ItemCatalog.Get(t).Id));
                int tail = Math.Max(5, tokens.Count / 4), from = Math.Max(0, tokens.Count - tail);
                for (int i = tokens.Count - 1; i >= from; i--)
                {
                    if (!extraIds.Contains(tokens[i])) continue;
                    for (int tries = 0; tries < 20; tries++)
                    {
                        int j = rng.Next(Math.Max(1, from));
                        if (extraIds.Contains(tokens[j])) continue;
                        (tokens[i], tokens[j]) = (tokens[j], tokens[i]);
                        break;
                    }
                }
            }

            // ---- паллеты
            for (int n = 0; n < p.Pallets; n++)
            {
                var counts = tokens.Where(x => !x.Contains(":")).GroupBy(x => x)
                    .Where(g => g.Count() >= 3 && !ItemCatalog.IsBig(ItemCatalog.IndexOf(g.Key)) && !ItemCatalog.IsFrozen(ItemCatalog.IndexOf(g.Key))
                                && !extraTypes.Contains(ItemCatalog.IndexOf(g.Key)))   // паллету лишнего товара некуда поставить
                    .Select(g => g.Key).ToList();
                if (counts.Count == 0) break;
                string id = counts[rng.Next(counts.Count)];
                int first = tokens.IndexOf(id);
                for (int k = 0; k < 3; k++) tokens.Remove(id);
                tokens.Insert(Math.Min(first, tokens.Count), "pallet:" + id);
            }

            // ---- коробки
            if (p.BoxPercent > 0)
            {
                for (int i = p.Window; i < tokens.Count; i++)
                    if (!tokens[i].Contains(":") && rng.Next(100) < p.BoxPercent) tokens[i] = "box:" + tokens[i];
            }

            // ---- связки
            if (p.BundlePercent > 0)
            {
                for (int i = 0; i + 1 < tokens.Count; i++)
                {
                    string a = tokens[i], b = tokens[i + 1];
                    if (a.Contains(":") || b.Contains(":") || a == b) continue;
                    if (ItemCatalog.IsBig(ItemCatalog.IndexOf(a)) || ItemCatalog.IsBig(ItemCatalog.IndexOf(b))) continue;
                    if (rng.Next(100) >= p.BundlePercent) continue;
                    tokens[i] = "bundle:" + a + "+" + b;
                    tokens.RemoveAt(i + 1);
                }
            }

            // ---- ленты
            var belts = new List<BeltData>();
            if (p.TwoBelts)
            {
                var a = new List<string>(); var b = new List<string>();
                for (int i = 0; i < tokens.Count; i++) (i % 2 == 0 ? a : b).Add(tokens[i]);
                belts.Add(new BeltData { tokens = a.ToArray() });
                belts.Add(new BeltData { tokens = b.ToArray() });
            }
            else belts.Add(new BeltData { tokens = tokens.ToArray() });

            // ---- секции: замки, акция, морозилка
            for (int i = 0; i < p.Locks && i < sections.Count - 1; i++)
                sections[sections.Count - 1 - i].lockSets = p.LockSets + i;
            if (p.Sale)
            {
                int idx = rng.Next(sections.Count);
                if (sections[idx].lockSets == 0 && string.IsNullOrEmpty(sections[idx].prefillType)) sections[idx].kind = "sale";
            }
            if (p.Freezer) sections.Add(new SectionData { kind = "freezer" });

            // ---- покупатели: по одному на КАЖДЫЙ лишний товар (02.10.2026). Покупатель приходит ЗАРАНЕЕ и ждёт:
            // его товар доезжает до края ленты через 1–3 хода после прихода, пока горят сердечки (просьба 03.10.2026).
            // До этого он приходил уже после товара: игрок успевал положить товар в тележку, и покупатель,
            // едва выйдя, сразу забирал его — ждать было нечего
            var customers = new List<CustomerData>();
            if (extraTypes.Count > 0)
            {
                var extraIds = new HashSet<string>(extraTypes.Select(t => ItemCatalog.Get(t).Id));
                // когда товар становится доступен: из ленты берут первые Window штук, каждый ход ленты убирает одну
                // (на двух лентах ходы делятся между ними)
                int win = Math.Max(1, p.TwoBelts ? 2 : p.Window);
                var order = new List<(int reach, string id)>();
                foreach (var b in belts)
                    for (int i = 0; i < b.tokens.Length; i++)
                        foreach (var id in TokenItems(b.tokens[i]))
                            if (extraIds.Contains(id)) order.Add((Math.Max(0, i - win + 1) * belts.Count, id));
                order.Sort((x, y) => x.reach.CompareTo(y.reach));
                int total = belts.Sum(b => b.tokens.Length), at = 0;
                foreach (var (reach, id) in order)
                {
                    int patience = 4 + rng.Next(3);
                    int lead = 1 + rng.Next(Math.Max(1, patience - 2));      // товар доедет через 1…(терпение−2) хода
                    // следующий — не раньше, чем ушёл прежний (правила и так ждут, но так расписание честное)
                    int earliest = at == 0 ? 1 : at + 2;
                    at = Math.Max(earliest, reach - lead);
                    at = Math.Min(at, Math.Max(1, total - 3));
                    customers.Add(new CustomerData { atMove = at, type = id, patience = patience, look = rng.Next(6) });
                }
            }

            var goals = new List<GoalData>();
            for (int i = 0; i < types; i++) goals.Add(new GoalData { type = ItemCatalog.Get(goalTypes[i]).Id, sets = setsPer[i] });

            return new LevelData
            {
                id = p.Id,
                district = p.District,
                difficulty = p.Difficulty,
                cart = p.Cart,
                window = p.Window,
                visible = p.Night ? p.Window + 1 : p.Visible,
                night = p.Night,
                revision = p.Revision,
                perishTimer = p.PerishTimer,
                doorOpen = p.Door ? 2 : 0,
                doorClosed = p.Door ? 1 : 0,
                sections = sections.ToArray(),
                goals = goals.ToArray(),
                belts = belts.ToArray(),
                customers = customers.ToArray(),
                cartStart = cartStart.ToArray(),
                mechanic = p.Mechanic,
                tutorial = p.Tutorial,
            };
        }

        /// <summary>Товары позиции ленты: «box:x», «pallet:x», «bundle:a+b» или просто «x».</summary>
        static IEnumerable<string> TokenItems(string token)
        {
            string body = token.Contains(":") ? token.Substring(token.IndexOf(':') + 1) : token;
            return body.Split('+');
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Замер сложности уровня ботами и солвером (ГДД 6.2).</summary>
    public static class LevelEvaluator
    {
        public static bool Measure(LevelData d, int solverNodes = 250000, int randomRuns = 80)
        {
            var s = LevelState.FromData(d);
            var solver = new Solver { NodeLimit = solverNodes };
            var r = solver.SolveMinPeak(s);
            d.metrics = d.metrics ?? new LevelMetrics();
            if (!r.Solved)
            {
                d.metrics.minPeakCart = -1;
                return false;
            }
            d.metrics.minPeakCart = r.MinPeak;
            d.metrics.moves = r.Moves.Count;
            d.metrics.greedyWin = Solver.GreedyWins(s);
            d.metrics.randomWinRate = Solver.RandomWinRate(s, randomRuns, d.id * 7919);
            d.solution = r.Moves.Select(m => m.ToData()).ToArray();
            return true;
        }

        /// <summary>Класс сложности по метрикам.</summary>
        public static string Classify(LevelData d)
        {
            var m = d.metrics;
            int c = d.cart;
            if (m.minPeakCart < 0) return "unsolvable";
            if (m.greedyWin && m.minPeakCart <= c - 2) return "easy";
            if (m.minPeakCart >= c && m.randomWinRate < 0.05f) return "superhard";
            if (m.minPeakCart >= c - 1 && m.randomWinRate < 0.25f) return "hard";
            return "medium";
        }

        static readonly string[] Order = { "easy", "medium", "hard", "superhard" };

        /// <summary>Насколько класс далёк от целевого (0 — совпадает).</summary>
        public static int Distance(string actual, string target)
        {
            if (target == "tutorial") target = "easy";
            int a = Array.IndexOf(Order, actual), t = Array.IndexOf(Order, target);
            if (a < 0 || t < 0) return 99;
            return Math.Abs(a - t);
        }
    }
}

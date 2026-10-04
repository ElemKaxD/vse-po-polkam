using System;
using System.Collections.Generic;
using System.Linq;

namespace AllOnShelves.Core
{
    /// <summary>
    /// План 200 уровней по ГДД: районы (5.1), график механик (5.3), пила сложности (6.3),
    /// первые 20 уровней (6.4), параметры районов (6.5), ревизии (4.14).
    /// </summary>
    public static class LevelPlanner
    {
        public const int LevelCount = 150; // по 10 уровней в каждом из 15 районов (v4, 03.10.2026: было 110)

        /// <summary>Первый уровень каждого района (район = индекс + 1).</summary>
        public static readonly int[] DistrictStart = { 1, 11, 21, 31, 41, 51, 61, 71, 81, 91, 101, 111, 121, 131, 141 };
        public static int DistrictCount => DistrictStart.Length;

        public static int DistrictOf(int id)
        {
            for (int d = DistrictStart.Length - 1; d >= 0; d--) if (id >= DistrictStart[d]) return d + 1;
            return 1;
        }

        public static int DistrictEnd(int district) =>
            district >= DistrictStart.Length ? LevelCount : DistrictStart[district] - 1;

        public static bool IsRevision(int id) => id % 25 == 0;

        // ------------------------------------------------------------------ режимы сложности (v4, 03.10.2026)
        // Лёгкий (0) — основной путь со всеми знакомствами. Средний (1) и Сложный (2) — те же 150 уровней заново:
        // все механики уже знакомы, видов товара на 1 / 2 больше, цели на ступень труднее, свои сиды.
        // Наборы готовятся заранее: Tools/LevelTool <from> <to> <attempts> <mode> → levels_medium / levels_hard.
        public const int ModeCount = 3;
        public static readonly string[] ModeFiles = { "levels", "levels_medium", "levels_hard" };

        static readonly string[] Ladder = { "tutorial", "easy", "medium", "hard", "superhard" };

        public static string DifficultyOf(int id, int mode)
        {
            string d = DifficultyOf(id);
            if (mode <= 0) return d;
            int i = Math.Max(1, Array.IndexOf(Ladder, d));
            // цель — на ступень выше. Средний: не выше «сложного», суперсложные — только боссы;
            // Сложный: сложные становятся суперсложными. Остальную трудность дают +1 / +2 вида товара
            // и параметры районов на 1 / 2 шага старше (Params)
            int cap = mode == 1 ? (d == "superhard" ? 4 : 3) : 4;
            return Ladder[Math.Min(cap, i + 1)];
        }

        /// <summary>
        /// Уровень, на котором вводится механика. Темп v2 (22.09.2026): средняя сессия на Яндексе 4–10 минут,
        /// поэтому в первых 20 уровнях новое — через уровень, дальше — раз в 3–5 уровней.
        /// Порядок ключей не менять: по нему считаются биты «механика показана» в сейве.
        /// </summary>
        public static readonly Dictionary<string, int> MechanicIntro = new Dictionary<string, int>
        {
            { "lock", 5 }, { "box", 9 }, { "extra", 11 }, { "customer", 7 }, { "perish", 13 },
            { "freezer", 15 }, { "big", 21 }, { "pallet", 24 }, { "sale", 17 }, { "bundle", 27 },
            { "belt2", 31 }, { "door", 36 }, { "night", 41 },
        };

        public static string MechanicIntroducedAt(int id)
        {
            foreach (var kv in MechanicIntro) if (kv.Value == id) return kv.Key;
            return "";
        }

        // первые 20 уровней — вручную: знакомство с механикой легче, между ними — средние, «боссы» на 10 и 20
        static readonly string[] EarlyDifficulty =
        {
            "", "tutorial", "tutorial", "easy", "easy", "medium", "medium", "medium", "hard", "medium", "hard",
            "medium", "hard", "medium", "hard", "medium", "hard", "medium", "hard", "hard", "superhard",
        };

        public static string DifficultyOf(int id)
        {
            if (id < EarlyDifficulty.Length) return EarlyDifficulty[id];
            if (IsRevision(id)) return "hard";
            int d = DistrictOf(id);
            if (id == DistrictEnd(d)) return id >= 60 && id % 20 == 0 ? "superhard" : "hard";
            // знакомство с механикой — средний уровень: сама новинка уже добавляет сложности
            if (!string.IsNullOrEmpty(MechanicIntroducedAt(id))) return "medium";
            // пила: отдых (easy) раз в 4–5 уровней, остальное — средние и сложные
            switch ((id - 1) % 10)
            {
                case 1: case 5: return "easy";
                case 3: case 7: return "hard";
                default: return "medium";
            }
        }

        public static int[] PoolFor(int district) =>
            ItemCatalog.All.Where(i => i.District <= district).Select(i => i.Index).ToArray();

        // ------------------------------------------------------------------ ручные уровни 1–3

        public static LevelData Handmade(int id)
        {
            switch (id)
            {
                case 1:
                    return new LevelData
                    {
                        id = 1, district = 1, difficulty = "tutorial", cart = 5, window = 1, visible = 6, tutorial = "tut_tap",
                        sections = Sections(2),
                        goals = new[] { G("apple", 1), G("milk", 1) },
                        belts = new[] { new BeltData { tokens = new[] { "apple", "apple", "milk", "apple", "milk", "milk" } } },
                    };
                // уровень 2: три товара на две секции — без тележки уже не пройти
                case 2:
                    return new LevelData
                    {
                        id = 2, district = 1, difficulty = "tutorial", cart = 5, window = 1, visible = 6, tutorial = "tut_goal",
                        sections = Sections(2),
                        goals = new[] { G("apple", 2), G("milk", 1), G("bread", 1) },
                        belts = new[] { new BeltData { tokens = new[]
                            { "apple", "milk", "apple", "bread", "apple", "milk", "apple", "bread", "milk", "apple", "apple", "bread" } } },
                    };
                // уровень 3: четыре товара на две секции — тележку надо держать свободной
                case 3:
                    return new LevelData
                    {
                        id = 3, district = 1, difficulty = "easy", cart = 5, window = 1, visible = 6, tutorial = "tut_cart",
                        sections = Sections(2),
                        goals = new[] { G("apple", 1), G("milk", 1), G("bread", 1), G("carrot", 1) },
                        // порядок нарочно такой: кто просто кликает подряд — забьёт тележку и встанет,
                        // а кто отложит яблоко с молоком в тележку — пройдёт (просьба 23.09.2026)
                        belts = new[] { new BeltData { tokens = new[]
                            { "apple", "milk", "bread", "bread", "bread", "carrot", "carrot", "carrot", "apple", "apple", "milk", "milk" } } },
                    };
            }
            return null;
        }

        static GoalData G(string t, int n) => new GoalData { type = t, sets = n };

        static SectionData[] Sections(int n)
        {
            var a = new SectionData[n];
            for (int i = 0; i < n; i++) a[i] = new SectionData();
            return a;
        }

        // ------------------------------------------------------------------ параметры генерации

        // уровни 4–20 (темп v2): G, T, S, K, V, lockSets(0 — нет), обучение. Механики добавляет общий блок ниже.
        // Просьба 23.09.2026: у конкурентов думать надо с первых уровней. Разница «товаров минус секций»
        // (сколько видов приходится держать в тележке) растёт со 2-го уровня: 1 → 2 → 3 → 4.
        // В районе 1 всего 6 видов товара (ItemCatalog), больше взять неоткуда.
        static readonly (int g, int t, int s, int k, int v, int lk, string tut)[] Early =
        {
            (0,0,0,0,0,0,""), (0,0,0,0,0,0,""), (0,0,0,0,0,0,""), (0,0,0,0,0,0,""),
            (6,4,2,2,7,0,"tut_window"),   // 4  окно из двух товаров, 4 вида на 2 секции
            (7,5,2,2,7,2,""),             // 5  замок
            (8,5,2,2,7,0,"tut_hint"),     // 6  подсказка
            (8,6,3,2,7,0,""),             // 7  покупатель
            (9,6,2,3,8,0,"tut_window3"),  // 8  окно из трёх, 6 видов на 2 секции
            (9,6,3,2,7,0,""),             // 9  коробка
            (11,6,3,2,7,3,""),            // 10 босс района
            (10,6,3,2,7,0,""),            // 11 лишний товар
            (11,7,4,2,7,2,""),            // 12
            (10,7,3,2,7,0,""),            // 13 скоропорт (полок 4→3: 02.10.2026, без запасного товара стало легче)
            (12,8,4,2,7,0,""),            // 14
            (11,7,3,3,7,0,""),            // 15 морозилка
            (12,8,4,2,7,3,""),            // 16
            (11,8,4,3,7,0,""),            // 17 акция
            (13,9,4,2,7,0,""),            // 18
            (12,8,4,3,7,2,""),            // 19
            (14,9,5,2,7,3,""),            // 20 босс района
        };

        // видов товара +1 со 2-го района (02.10.2026): лишний товар больше не встаёт на полку, порча довозится —
        // уровни стали легче, давление возвращаем разнообразием товара и одной полкой меньше в районах 3–4 (s1 = 4)
        struct Tier { public int g0, g1, t0, t1, s0, s1, k0, k1, v0, v1, e0, e1, deficit; }

        static Tier TierOf(int district)
        {
            switch (district)
            {
                case 1: return new Tier { g0 = 6, g1 = 10, t0 = 4, t1 = 6, s0 = 3, s1 = 4, k0 = 2, k1 = 3, v0 = 7, v1 = 8, e0 = 0, e1 = 0, deficit = 1 };
                case 2: return new Tier { g0 = 9, g1 = 13, t0 = 7, t1 = 8, s0 = 4, s1 = 5, k0 = 2, k1 = 3, v0 = 7, v1 = 7, e0 = 1, e1 = 3, deficit = 1 };
                // просьба 26.09.2026: много полок = легко. Полок держим мало, сложность даём
                // разнообразием товара, второй лентой и «видами минус секции» (deficit)
                case 3: return new Tier { g0 = 11, g1 = 14, t0 = 8, t1 = 10, s0 = 3, s1 = 4, k0 = 2, k1 = 3, v0 = 7, v1 = 7, e0 = 1, e1 = 4, deficit = 4 };
                case 4: return new Tier { g0 = 12, g1 = 15, t0 = 8, t1 = 10, s0 = 3, s1 = 4, k0 = 2, k1 = 3, v0 = 6, v1 = 7, e0 = 2, e1 = 5, deficit = 4 };
                case 5: return new Tier { g0 = 13, g1 = 16, t0 = 9, t1 = 11, s0 = 3, s1 = 5, k0 = 2, k1 = 3, v0 = 6, v1 = 7, e0 = 2, e1 = 6, deficit = 4 };
                case 6: return new Tier { g0 = 13, g1 = 17, t0 = 9, t1 = 11, s0 = 3, s1 = 5, k0 = 2, k1 = 3, v0 = 6, v1 = 7, e0 = 3, e1 = 6, deficit = 5 };
                case 7:
                case 8: return new Tier { g0 = 13, g1 = 18, t0 = 9, t1 = 12, s0 = 3, s1 = 5, k0 = 2, k1 = 3, v0 = 6, v1 = 6, e0 = 3, e1 = 7, deficit = 5 };
                case 9:
                case 10:
                case 11:
                case 12: return new Tier { g0 = 15, g1 = 22, t0 = 10, t1 = 13, s0 = 3, s1 = 5, k0 = 2, k1 = 3, v0 = 5, v1 = 6, e0 = 3, e1 = 9, deficit = 6 };
                // районы 13–15 (v4): ещё на один вид товара больше, полок столько же
                default: return new Tier { g0 = 16, g1 = 23, t0 = 11, t1 = 14, s0 = 3, s1 = 5, k0 = 2, k1 = 3, v0 = 5, v1 = 6, e0 = 3, e1 = 9, deficit = 6 };
            }
        }

        static int Lerp(int a, int b, float t) => a + (int)Math.Round((b - a) * Math.Max(0f, Math.Min(1f, t)));

        static float DiffT(string diff) =>
            diff == "tutorial" ? 0f : diff == "easy" ? 0.15f : diff == "medium" ? 0.5f : diff == "hard" ? 0.8f : 1f;

        /// <summary>Механика активна (введена) к этому уровню.</summary>
        public static bool Has(string mech, int id) => id >= MechanicIntro[mech];

        /// <summary>Уровни сразу после введения механики используют её чаще.
        /// В Среднем и Сложном механики уже знакомы: доступны с половины пути (Средний) или сразу (Сложный).</summary>
        static bool Use(string mech, int id, int mode, Random rng, double chance)
        {
            int intro = MechanicIntro[mech];
            if (mode > 0)
            {
                if (mode == 1 && id < intro / 2) return false;
                return rng.NextDouble() < Math.Min(0.7, chance * (mode == 1 ? 1.15 : 1.35));
            }
            if (id < intro) return false;
            if (id == intro) return true;
            if (id <= intro + 4) return rng.NextDouble() < 0.75;
            return rng.NextDouble() < chance;
        }

        public static GenParams Params(int id, int seed, int mode = 0)
        {
            var rng = new Random(seed * 31 + id);
            int d = DistrictOf(id);
            string diff = DifficultyOf(id, mode);
            var p = new GenParams
            {
                Id = id, District = d, Difficulty = diff, Cart = 5, Revision = IsRevision(id),
                // в режимах товар уже весь знаком — берём ассортимент на несколько районов вперёд
                Pool = PoolFor(mode > 0 ? Math.Min(DistrictCount, d + 3 * mode) : d),
                Mechanic = mode > 0 ? "" : MechanicIntroducedAt(id),
            };

            // режимы: параметры района на ступень-две старше (Средний — +1 район, Сложный — +2), но не дальше последнего
            var tier = TierOf(Math.Min(DistrictCount, d + mode));
            float t = DiffT(diff);
            float jitter = (float)rng.NextDouble() * 0.2f - 0.1f;
            bool early = mode == 0 && id < Early.Length && Early[id].g > 0;
            if (early)
            {
                // первые 20 — по таблице; механики ниже добавляются так же, как на остальных уровнях
                var e = Early[id];
                p.Sets = e.g; p.Types = e.t; p.Sections = e.s; p.Window = e.k; p.Visible = e.v;
                p.Locks = e.lk > 0 ? 1 : 0; p.LockSets = e.lk; p.Tutorial = e.tut;
                p.Extras = Has("extra", id) ? 1 + rng.Next(2) : 0;
            }
            else
            {
                p.Sets = Lerp(tier.g0, tier.g1, t + jitter);
                p.Types = Lerp(tier.t0, tier.t1, t + 0.2f + jitter);
                int deficit = tier.deficit + (diff == "easy" || diff == "tutorial" ? -1 : diff == "superhard" ? 1 : 0);
                p.Sections = Math.Max(tier.s0, Math.Min(tier.s1, p.Types - Math.Max(0, deficit)));
                p.Window = diff == "hard" || diff == "superhard" || (diff == "medium" && rng.NextDouble() < 0.5) ? tier.k0 : tier.k1;
                p.Visible = Lerp(tier.v1, tier.v0, t);
                p.Extras = Has("extra", id) || mode > 0 ? Lerp(tier.e0, tier.e1, t + jitter) : 0;
                // режимы: видов товара на 1 / 2 больше при тех же полках («запас» тележки меньше)
                if (mode > 0) p.Types = Math.Min(p.Pool.Length, p.Types + mode);
                if (mode > 0) p.Sets = Math.Max(p.Sets, p.Types + 1);
                if (diff == "superhard" && (id >= 60 || mode == 2)) p.Cart = 4;
                if (Use("lock", id, mode, rng, 0.3)) { p.Locks = 1 + (d >= 5 && rng.NextDouble() < 0.3 ? 1 : 0); p.LockSets = 2 + rng.Next(3); }
            }

            if (Use("box", id, mode, rng, 0.35)) p.BoxPercent = 10 + rng.Next(15);
            if (id == MechanicIntro["extra"]) p.Extras = Math.Max(p.Extras, 3);
            if (Use("customer", id, mode, rng, 0.45)) { p.Customers = true; p.Extras = Math.Max(p.Extras, 3); }
            // лишний товар без покупателя некому забрать — он оставался после победы (02.10.2026):
            // где есть лишний товар, всегда приходят покупатели (по одному на каждый лишний)
            if (p.Extras > 0) p.Customers = true;
            if (Use("perish", id, mode, rng, 0.4)) p.PerishTimer = d >= 7 || mode == 2 ? 3 : 4;
            if (Use("freezer", id, mode, rng, 0.4)) p.Freezer = true;
            if (Use("big", id, mode, rng, 0.35)) p.Big = true;
            if (Use("pallet", id, mode, rng, 0.35)) p.Pallets = 1 + rng.Next(2);
            if (Use("sale", id, mode, rng, 0.4)) p.Sale = true;
            if (Use("bundle", id, mode, rng, 0.35)) p.BundlePercent = 12 + rng.Next(10);
            // вторая лента — частый способ усложнить, не добавляя полок (просьба 26.09.2026)
            if (Use("belt2", id, mode, rng, d >= 6 ? 0.5 : 0.35)) { p.TwoBelts = true; p.Window = 2; }
            if (Use("door", id, mode, rng, 0.5)) { p.Freezer = true; p.Door = true; }
            if (Use("night", id, mode, rng, 0.35)) p.Night = true;

            // первый уровень механики — облегчённый (в первых 20 облегчение уже заложено в таблицу)
            if (!early && !string.IsNullOrEmpty(p.Mechanic))
            {
                p.Sets = Math.Max(tier.g0 - 1, p.Sets - 2);
                p.Sections = Math.Min(tier.s1, p.Sections + 1);
            }
            return p;
        }

        // ------------------------------------------------------------------ сборка уровня

        /// <summary>
        /// Штраф за уровень «в обрез». Решение, которое занимает всю тележку, не прощает ни одной
        /// ошибки: игрок встаёт в тупик и упирается в рекламу за полку. Просьба 26.09.2026 —
        /// сложность должна заставлять думать, но выход из положения обязан оставаться.
        /// </summary>
        static int TightPenalty(LevelData lvl, int id)
        {
            var m = lvl.metrics;
            int slack = lvl.cart - m.minPeakCart;     // сколько мест в тележке остаётся про запас
            int p = 0;
            if (slack < 1) p += id <= 40 ? 30 : 12;   // ни одного свободного места — не берём
            else if (slack < 2 && id <= 20) p += 6;   // в первом районе хотим запас побольше
            if (id <= 40 && m.randomWinRate < 0.02f) p += 8;  // «игольное ушко»: один-единственный путь
            return p;
        }

        /// <summary>Генерирует уровень, подбирая сид под целевой класс сложности.</summary>
        public static LevelData Build(int id, int attempts = 30, int solverNodes = 250000, int mode = 0)
        {
            var hand = mode > 0 ? null : Handmade(id);
            if (hand != null)
            {
                LevelEvaluator.Measure(hand, solverNodes);
                return hand;
            }

            LevelData best = null;
            int bestDist = int.MaxValue;
            string target = DifficultyOf(id, mode);
            for (int a = 0; a < attempts; a++)
            {
                int seed = id * 1000 + a + mode * 1000000;
                var p = Params(id, seed, mode);
                if (a >= attempts / 2) p.Sets = Math.Max(p.Types, p.Sets - 1 - (a - attempts / 2) / 5); // облегчаем, если не выходит
                var lvl = LevelGenerator.Generate(p, seed);
                if (!LevelEvaluator.Measure(lvl, solverNodes)) continue;
                lvl.metrics.seed = seed;
                int dist = LevelEvaluator.Distance(LevelEvaluator.Classify(lvl), target) * 10;
                // уровень, который выигрывает «жадный» бот, проходится спамом мышкой — с 4-го такие не берём
                // (просьба 23.09.2026: «по-прежнему по спаму ЛКМ проходятся первые 10–20 уровней»)
                if (id >= 4 && lvl.metrics.greedyWin) dist += 25;
                // «сложно, но пройти можно без полки за рекламу» (просьба 26.09.2026)
                dist += TightPenalty(lvl, id);
                if (dist < bestDist) { best = lvl; bestDist = dist; }
                if (dist == 0) break;
            }
            return best;
        }
    }
}

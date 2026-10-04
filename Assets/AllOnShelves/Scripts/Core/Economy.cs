namespace AllOnShelves.Core
{
    /// <summary>
    /// Формулы экономики (ГДД 9.2–9.4). Экономика v2 (30.09.2026): ремонт дороже дохода района примерно
    /// вдвое к концу игры — без рекламы игрок немного гриндит (переигровки на 3★, «Час пик», ежедневки),
    /// с рекламой «×2» проходит ровно, лишние монеты уходят на бустеры. Проверка — Tools/economy_sim.py.
    /// Скины за монеты не продаются: только наградой или наборами за деньги (MetaCatalog.Products).
    /// Подробно — Docs/claude/economy.md (цены и симулятор Tools/economy_sim.py).
    /// </summary>
    public static class Economy
    {
        // с какого пройденного уровня открывается (темп v2, 22.09.2026: всё основное — за первую сессию)
        public const int RenoAt = 3, AlbumAt = 6, HintAt = 6, DailyAt = 8, TipsAt = 10, LeadersAt = 10, RushAt = 12,
                         HouseAt = 11;   // Торговый дом: с 11-го уровня (просьба 03.10.2026, было 14)

        public const int StartUndo = 5;
        public const int StartHint = 3;
        public const int UndoPackPrice = 90;   // 3 отмены (окно бустера в уровне, 29.09.2026)
        public const int ShelfPrice = 200;     // +1 полка за монеты (или за рекламу)
        public const int HintPackPrice = 120;  // 3 подсказки
        public const int LoaderPrice = 150;    // второе продолжение
        public const int TipsReward = 60;      // «Чаевые от енота»
        public const int TipsCooldownHours = 4;
        public const int StarChestEvery = 40;
        public const int StarChestCoins = 150;
        public const int DuplicateSticker = 10;
        public const int DuplicateGoldSticker = 50;
        public const int DepartmentReward = 150;   // было 300: альбом давал пятую часть всех монет
        // бонусы коллекции (v4, 03.10.2026): каждый собранный отдел — +2 % монет за уровни навсегда; весь альбом — алмазы
        public const int AlbumDeptBonusPct = 2, AlbumFullGems = 100;
        public const int DailyLevelCoins = 50;
        public const int RushDailyRuns = 3;
        public const int RushMaxCoins = 100;
        public const int WinsPerPack = 3;
        // «+1 полка за рекламу» (просьба 23.09.2026): помощь там, где секций специально мало
        public const int ExtraShelfFrom = 4;   // с какого уровня предлагаем
        public const int ExtraShelfMax = 2;    // сколько раз за уровень
        public const int ExtraShelfMaxSections = 8;
        public const int RewardedPacksPerDay = 3;
        // «Не хватает монет» (окно в ремонте и бустерах): монеты за видео, не больше AdCoinsPerDay раз в день
        public const int AdCoinsBase = 40, AdCoinsPerDistrict = 10, AdCoinsPerDay = 5;
        public const int StarterOfferAt = 6;
        // магазин (01.10.2026): подсказка и отмена за видео — каждой не больше AdHelpPerDay раз в день
        public const int AdHelpPerDay = 3;   // стартовый набор предлагаем после 6-го уровня (затем 20 и 40)

        public static int AdCoins(int district) => AdCoinsBase + AdCoinsPerDistrict * district;

        public static int Base(int district) => 20 + 5 * district;

        public static float DifficultyMult(string difficulty, bool revision)
        {
            if (revision) return 2f;
            switch (difficulty)
            {
                case "hard": return 1.5f;
                case "superhard": return 2f;
                default: return 1f;
            }
        }

        public static int StarBonus(int stars) => stars >= 3 ? 10 : stars == 2 ? 5 : 0;

        public static int WinCoins(int district, string difficulty, bool revision, int setCoins, int stars, int tips) =>
            (int)(Base(district) * DifficultyMult(difficulty, revision)) + setCoins + StarBonus(stars) + tips;

        public static int RewardMultiplier(int levelId) => levelId >= 100 ? 3 : 2;

        // повтор пройденного — 40 % (было 30 %): это главный гринд игрока без рекламы
        public static int ReplayCoins(int winCoins) => (int)(winCoins * 0.4f);

        public static readonly int[] DailyStreakCoins = { 40, 40, 0, 80, 0, 120, 250 };

        // ------------------------------------------------------------------ алмазы (экономика v4, 03.10.2026)
        // Числа — Tools/economy_v4.json, проверка — Tools/economy_sim_v4.py. Покупка + понемногу в игре
        // (Испытание дня, шкалы заданий, серия, путь, рейтинг). Скины за монеты по-прежнему не продаются.
        public const int GemsAt = HouseAt;           // счётчик алмазов появляется вместе с домом
        public const int GemHammerPrice = 25;        // молоток — стройка готова сразу
        public const int GemUndo5 = 10, GemHint3 = 12, GemCartSlot = 8, GemStreakSave = 8;

        /// <summary>Ускорители стройки (предметы): минуты, цена в алмазах, значок, подпись.</summary>
        public static readonly int[] BoostMinutes = { 5, 30, 60, 240, 1440 };
        public static readonly int[] BoostPrice = { 1, 3, 5, 15, 60 };
        public static readonly string[] BoostIcons = { "icon_boost_5m", "icon_boost_30m", "icon_boost_1h", "icon_boost_4h", "icon_boost_1d" };

        // цена ускорения за оставшееся время: плавно между точками (минуты → алмазы), форма как у игр с таймерами,
        // но в 4 раза дешевле — алмазов у игрока мало и они в основном бесплатные
        static readonly int[] SkipMin = { 0, 5, 30, 60, 240, 1440, 10080 };
        static readonly int[] SkipGems = { 0, 1, 3, 5, 15, 60, 300 };

        public static int GemSkipCost(double minutes)
        {
            if (minutes <= 0) return 0;
            for (int i = 1; i < SkipMin.Length; i++)
                if (minutes <= SkipMin[i])
                {
                    double k = (minutes - SkipMin[i - 1]) / (SkipMin[i] - SkipMin[i - 1]);
                    return System.Math.Max(1, (int)System.Math.Round(SkipGems[i - 1] + (SkipGems[i] - SkipGems[i - 1]) * k));
                }
            return SkipGems[SkipGems.Length - 1];
        }

        /// <summary>Длительность для подписи: «15 мин», «3 ч», «1,5 сут», «7 сут».</summary>
        public static string Dur(int minutes)
        {
            if (minutes < 60) return minutes + " мин";
            if (minutes < 1440) return (minutes % 60 == 0 ? (minutes / 60).ToString() : (minutes / 60f).ToString("0.#")) + " ч";
            float d = minutes / 1440f;
            return (System.Math.Abs(d - System.Math.Round(d)) < 0.01 ? ((int)System.Math.Round(d)).ToString() : d.ToString("0.#")) + " сут";
        }

        /// <summary>Остаток времени для таймера: «2 д 4 ч», «3:05:12», «4:59».</summary>
        public static string Left(long seconds)
        {
            if (seconds <= 0) return "0:00";
            long d = seconds / 86400, h = seconds / 3600 % 24, m = seconds / 60 % 60, s = seconds % 60;
            if (d > 0) return $"{d} д {h} ч";
            return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        }
    }
}

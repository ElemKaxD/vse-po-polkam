using System;
using System.Collections.Generic;

namespace AllOnShelves
{
    /// <summary>Сейв игрока (ГДД 14.4). Хранится внутри YG2.saves (облако Яндекса + локально).</summary>
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;

        // прогресс уровней
        public int maxReached;              // последний пройденный уровень
        public int[] stars = new int[0];    // лучшие звёзды по уровням (индекс = id) — режим «Лёгкий»
        // режимы сложности (v4, 03.10.2026): после 150 уровней — Средний и Сложный по тем же уровням.
        // Прогресс и звёзды у каждого режима свои; maxReached и stars — это «Лёгкий» (по ним открывается всё остальное)
        public int diffMode;                           // 0 Лёгкий, 1 Средний, 2 Сложный
        public int[] modeReached = new int[3];         // пройдено в Среднем [1] и Сложном [2]
        public int[] starsMedium = new int[0], starsHard = new int[0];
        public int diffShown;                          // биты: окно «Новая сложность открыта» уже показано (1, 2)
        public int attemptsLevel;           // уровень, для которого считаются попытки
        public int attempts;
        public int failsInRow;

        // кошелёк
        public int coins;
        public int undo = Core.Economy.StartUndo;
        public int hint = 0;
        public long undoUnlimitedUntil;

        // флаги
        public bool noAds;
        public bool starterBought;
        public long tutorialBits;
        public long mechanicsSeen;
        public bool hintGranted;

        // ремонт
        public int renovationDistrict = 1;  // район, этап которого сейчас ремонтируется
        public List<string> boughtItems = new List<string>();
        public int starsChestClaimed;       // (устарело: сундук заменён «Звёздным путём»)
        public List<int> starRewards = new List<int>();  // «Звёздный путь»: полученные награды (индексы)
        public List<int> goldRewards = new List<int>();  // Золотой путь: полученные награды (те же индексы шагов)
        public int starTrackSeen;           // сколько наград игрок уже видел открытыми (для окна «новая награда»)

        // альбом
        public List<string> stickers = new List<string>();
        public List<string> goldStickers = new List<string>();
        public List<int> departmentsRewarded = new List<int>();
        public bool albumFullRewarded;                 // весь альбом собран — приз выдан (v4)
        public int winsSincePack;
        public int packsOpenedToday;
        public string packsDate = "";
        public int pendingPacks;
        public int pendingGoldPacks;

        // ежедневки
        public int streakIndex;             // 0..6 — какой день цепочки следующий
        public int streakDays;
        public string lastClaimDate = "";
        public string chest2Date = "";
        public string dailyLevelDate = "";
        public bool dailyLevelWon;

        // час пик
        public int rushBest;
        public int rushRunsToday;
        public string rushDate = "";

        // реклама и офферы
        public long lastTipsTime;
        public int starterShownAtLevel;
        public long starterExpires;
        public long noAdsLastShown;
        public long helperOfferDay;
        public string adCoinsDate = "";     // «Не хватает монет»: сколько раз сегодня брали монеты за видео
        public int adCoinsToday;
        public string adHelpDate = "";     // магазин: подсказки и отмены за видео сегодня (01.10.2026)
        public int adHintToday, adUndoToday;

        // рейтинг «Сегодня / Неделя» (01.10.2026, League.cs): номер периода, очки, последнее известное место
        public int lbDay, lbDayPts, lbDayRank;
        public int lbWeek, lbWeekPts, lbWeekRank;
        // итоги прошлого периода, которые игрок ещё не забрал (место 0 — ничего нет)
        public int lbPendDay, lbPendDayRank, lbPendDayPts;
        public int lbPendWeek, lbPendWeekRank, lbPendWeekPts;

        // темы (устарело 30.09.2026: темы стали наборами скинов, см. cosmetics)
        public List<string> themesOwned = new List<string>();
        public string activeTheme = "";

        // косметика за звёзды: что получено и что надето (ореол, скин енота, оформление магазина)
        public List<string> cosmetics = new List<string>();
        public string wearHalo = "", wearRaccoon = "", wearScene = "", wearCart = "";
        public string wearBelt = "", wearShelf = "", wearItems = "";   // скины конвейера, стеллажей, товаров (30.09.2026)
        public string wearAvatar = "", wearFrame = "";                 // аватарка и рамка в рейтинге (01.10.2026)

        // Торговый дом (03.10.2026, House.cs)
        public int houseBuilt;                         // построенные комнаты (биты 0..11)
        public int[] houseDecor = new int[12];         // сколько предметов декора куплено в комнате
        public int houseBuilding = -1;                 // какая комната строится (-1 — никакая)
        public long houseBuildEnd;                     // когда достроится (unix)
        public int houseBuildAds;                      // сколько раз стройку ускоряли за видео
        public long houseCashSince;                    // с какого момента копится касса
        public int houseFloorsSeen;                    // сколько этажей игрок видел открытыми (леса падают один раз)
        public int hammers;                            // «Молотки бригадира»: мгновенно достроить
        public int gems;                               // алмазы (экономика v4, 03.10.2026)
        public int streak, streakBest;                 // серия побед «Лучший продавец» (Services/Streak.cs)
        public bool streakVideoUsed;                   // «Сохранить серию» за видео — раз за серию
        public int challengeRun;                       // Испытание дня: сколько дней подряд (Services/Challenge.cs)
        public string challengeLast = "";
        // задания дня и шкалы недели / месяца (Services/Quests.cs)
        public int questDay, questClaimed, questWeek, questWeekPts, questWeekClaimed, questMonth, questMonthPts, questMonthClaimed;
        public string[] questIds = new string[3];
        public int[] questProgress = new int[3];
        public int[] boosts = new int[5];              // ускорители стройки 5 мин / 30 мин / 1 ч / 4 ч / 1 день
        public bool goldPath;                          // Золотой путь куплен (с ним приходит Кот-управляющий)
        public int piggyCoins;                         // копилка енота (Services/Piggy, 03.10.2026)
        public bool piggyFullShown;                    // «Копилка полна!» уже показали (самовсплывающее — один раз)
        public int piggyBroken;                        // сколько раз разбивали

        // настройки
        public float music = 0.7f;
        public string playerName = "";     // ник, который игрок задал сам (профиль)
        public float sfx = 1f;
        public string lang = "";

        // статистика
        public int wins, losses, rewardedWatched, interstitials;

        /// <summary>Все звёзды всех режимов — для «Звёздного пути» (до 1350) и рейтинга.</summary>
        public int StarsTotal => StarsIn(0) + StarsIn(1) + StarsIn(2);

        public int StarsIn(int mode)
        {
            int s = 0;
            var a = StarsArr(mode);
            if (a != null) foreach (var x in a) s += x;
            return s;
        }

        int[] StarsArr(int mode) => mode == 1 ? starsMedium : mode == 2 ? starsHard : stars;

        public int Mode => diffMode < 0 || diffMode > 2 ? 0 : diffMode;

        /// <summary>Звёзды уровня в текущем режиме.</summary>
        public int GetStars(int id) => GetStars(id, Mode);

        public int GetStars(int id, int mode)
        {
            var a = StarsArr(mode);
            return a != null && id < a.Length ? a[id] : 0;
        }

        public void SetStars(int id, int value)
        {
            int m = Mode;
            var a = StarsArr(m) ?? new int[0];
            if (id >= a.Length) Array.Resize(ref a, Math.Max(id + 1, 201));
            if (value > a[id]) a[id] = value;
            if (m == 1) starsMedium = a; else if (m == 2) starsHard = a; else stars = a;
        }

        /// <summary>Пройдено уровней в режиме.</summary>
        public int Reached(int mode)
        {
            if (mode <= 0) return maxReached;
            return modeReached != null && modeReached.Length > mode ? modeReached[mode] : 0;
        }

        public void SetReached(int mode, int id)
        {
            if (mode <= 0) { maxReached = id; return; }
            if (modeReached == null || modeReached.Length < 3) Array.Resize(ref modeReached, 3);
            modeReached[mode] = id;
        }

        public bool Tutorial(int bit) => (tutorialBits & (1L << bit)) != 0;
        public void SetTutorial(int bit) => tutorialBits |= 1L << bit;
        public bool MechanicSeen(int bit) => (mechanicsSeen & (1L << bit)) != 0;
        public void SetMechanicSeen(int bit) => mechanicsSeen |= 1L << bit;
    }
}

namespace YG
{
    public partial class SavesYG
    {
        public AllOnShelves.SaveData aos = new AllOnShelves.SaveData();
    }
}

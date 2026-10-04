using System;
using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;
using Random = System.Random;

namespace AllOnShelves
{
    public enum PlayMode { Career, Replay, Daily, Rush }

    /// <summary>Итог последнего уровня — для анимаций на карте.</summary>
    public sealed class LevelOutcome
    {
        public int LevelId;
        public bool Won;
        public int Stars;
        public int Coins;
        public PlayMode Mode;
        public bool DistrictFinished;
        public int Piggy;                // сколько упало в копилку (0 — нет копилки или она полна)
        public int StreakStep = -1;      // какую ступень серии зажгла победа (−1 — никакую)
        public bool ChallengeBig;        // Испытание дня: 7 дней подряд — большой сундук
        public int ModeOpened;           // эта победа открыла режим сложности (1 Средний, 2 Сложный; 0 — нет)
    }

    /// <summary>
    /// Центральный сервис игры (живёт между сценами, создаётся в сцене Boot).
    /// Прогресс, кошелёк, ремонт, альбом, ежедневки, «Час пик» (ГДД 5, 8, 9).
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        /// <summary>
        /// true — прогресс не сохраняется, каждый запуск с обучения (так было 20.09–03.10.2026).
        /// Сохранения вернули 03.10.2026; начать заново — меню «Всё по полкам/Обнулить прогресс».
        /// </summary>
        public const bool FreshStart = false;

        public const string SceneBoot = "Boot";
        public const string SceneHub = "Hub";
        public const string SceneGame = "Game";

        public LevelData[] Levels { get; private set; } = new LevelData[0];
        public SaveData Save => YG2.saves.aos ?? (YG2.saves.aos = new SaveData());

        public PlayMode PendingMode { get; set; } = PlayMode.Career;
        public int PendingLevel { get; set; } = 1;
        public LevelData PendingLevelData { get; set; }
        public bool PendingCartBonus { get; set; }
        public LevelOutcome LastOutcome { get; set; }
        /// <summary>Прибавка к монетам следующей победы от смены Торгового дома (Белка, Кот), в процентах.</summary>
        public int ShiftCoinPercent { get; set; }

        public float SessionStart { get; private set; }

        public static event Action WalletChanged;
        public static event Action ProgressChanged;

        float _saveDirtyAt = -1f;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            SessionStart = Time.realtimeSinceStartup;
            Application.targetFrameRate = 60;
            LoadLevels();
            if (FreshStart)
            {
                // сброс здесь, а не в Boot: Play в редакторе часто жмут из сцены Hub или Game,
                // и тогда Boot не запускается — прогресс брался из SavesEditorYG2.json
                ResetProgress();
                // SDK может дочитать сейв позже и подменить YG2.saves — сбрасываем и после этого
                if (!YG2.isSDKEnabled) YG2.onGetSDKData += ResetAfterSdk;
            }
        }

        void OnDestroy() { YG2.onGetSDKData -= ResetAfterSdk; }

        void ResetAfterSdk()
        {
            YG2.onGetSDKData -= ResetAfterSdk;
            ResetProgress();
        }

        /// <summary>Режим FreshStart: чистый сейв — обучение и уровень 1.</summary>
        static void ResetProgress()
        {
            YG2.saves.aos = new SaveData();
            Debug.Log("[AllOnShelves] FreshStart: прогресс сброшен, начинаем с обучения");
        }

        /// <summary>Обнулить прогресс и сразу записать пустой сейв (меню редактора «Обнулить прогресс»).</summary>
        public void ResetAllProgress()
        {
            YG2.saves.aos = new SaveData();
            _saveDirtyAt = -1f;
            try { YG2.SaveProgress(); } catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
            Debug.Log("[AllOnShelves] Прогресс обнулён");
        }

        void Update()
        {
            if (_saveDirtyAt > 0 && Time.realtimeSinceStartup - _saveDirtyAt > 3f) Flush();
            Platform.Tick();
        }

        void OnApplicationPause(bool pause) { if (pause) Flush(); }

        /// <summary>Уровни режимов сложности: [0] — Лёгкий (= Levels), [1] Средний, [2] Сложный (null — набора нет).</summary>
        public LevelData[][] ModeLevels = new LevelData[LevelPlanner.ModeCount][];

        public void LoadLevels()
        {
            for (int m = 0; m < LevelPlanner.ModeCount; m++)
            {
                var ta = Resources.Load<TextAsset>("Levels/" + LevelPlanner.ModeFiles[m]);
                if (ta == null) { if (m == 0) Debug.LogError("levels.json not found"); continue; }
                var set = JsonUtility.FromJson<LevelSet>(ta.text);
                ModeLevels[m] = set.levels.OrderBy(l => l.id).ToArray();
            }
            Levels = ModeLevels[0];
        }

        /// <summary>Уровень текущего режима сложности (нет набора режима — уровень «Лёгкого»).</summary>
        public LevelData Level(int id)
        {
            var set = ModeLevels[Mode] ?? Levels;
            return id >= 1 && id <= set.Length ? set[id - 1] : null;
        }
        public int LevelCount => Levels.Length;

        // ------------------------------------------------------------------ режимы сложности (v4, 03.10.2026)

        public static readonly string[] ModeNames = { "Лёгкий", "Средний", "Сложный" };
        public static readonly string[] ModeIcons = { "icon_diff_easy", "icon_diff_medium", "icon_diff_hard" };
        public static readonly float[] ModeCoinMult = { 1f, 1.25f, 1.5f };

        public int Mode => Save.Mode;

        /// <summary>Режим открыт: все предыдущие пройдены до конца (и набор уровней режима есть в сборке).</summary>
        public bool ModeUnlocked(int mode) =>
            mode <= 0 || (mode < LevelPlanner.ModeCount && ModeLevels[mode] != null && Save.Reached(mode - 1) >= LevelCount && ModeUnlocked(mode - 1));

        /// <summary>Есть из чего выбирать — переключатель режима на карте.</summary>
        public bool ModesAvailable => ModeUnlocked(1);

        public void SetMode(int mode)
        {
            if (!ModeUnlocked(mode) || mode == Mode) return;
            Save.diffMode = mode;
            MarkDirty();
            Platform.Metrica("diff_mode", new Dictionary<string, object> { { "mode", mode } });
        }

        /// <summary>Открытый, но ещё не объявленный режим (окно «Новая сложность открыта!»), 0 — нет.</summary>
        public int ModeToAnnounce
        {
            get
            {
                for (int m = 1; m < LevelPlanner.ModeCount; m++)
                    if (ModeUnlocked(m) && (Save.diffShown & (1 << m)) == 0) return m;
                return 0;
            }
        }

        public void MarkModeAnnounced(int mode) { Save.diffShown |= 1 << mode; MarkDirty(); }

        // ------------------------------------------------------------------ сохранение

        public void MarkDirty() { if (_saveDirtyAt < 0) _saveDirtyAt = Time.realtimeSinceStartup; }

        public void Flush()
        {
            _saveDirtyAt = -1f;
            if (FreshStart) return;
            try { YG2.SaveProgress(); } catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        // ------------------------------------------------------------------ прогресс

        /// <summary>Следующий уровень в текущем режиме сложности.</summary>
        public int CurrentLevel => Mathf.Clamp(Save.Reached(Mode) + 1, 1, Math.Max(1, LevelCount));
        public bool AllLevelsDone => Save.Reached(Mode) >= LevelCount;
        /// <summary>Район «Лёгкого» (по нему — ремонт, открытия); режимы сложности на ремонт не влияют.</summary>
        public int CurrentDistrict => LevelPlanner.DistrictOf(Mathf.Clamp(Save.maxReached + 1, 1, Math.Max(1, LevelCount)));

        /// <summary>Район завершён по уровням (последний уровень пройден).</summary>
        public bool DistrictLevelsDone(int d) => Save.maxReached >= LevelPlanner.DistrictEnd(d);

        public bool StageComplete(int d) => MetaCatalog.ItemsOf(d).All(i => Save.boughtItems.Contains(i.Id));

        /// <summary>Уровень доступен: пройдены предыдущие и отремонтирован предыдущий район (ГДД 5.1).</summary>
        public bool CanPlay(int id)
        {
            if (id < 1 || id > LevelCount) return false;
            // Средний и Сложный: районы уже отремонтированы — только по порядку
            if (Mode > 0) return id <= Save.Reached(Mode) + 1;
            if (id > Save.maxReached + 1) return false;
            int d = LevelPlanner.DistrictOf(id);
            return d == 1 || StageComplete(d - 1) || id <= Save.maxReached;
        }

        /// <summary>Следующий уровень заблокирован ремонтом.</summary>
        public bool BlockedByRenovation => Mode == 0 && !AllLevelsDone && !CanPlay(CurrentLevel);

        // ------------------------------------------------------------------ кошелёк

        public int Coins => Save.coins;
        public bool UndoUnlimited => Save.undoUnlimitedUntil > NowUnix;

        public void AddCoins(int amount, string reason = "")
        {
            if (amount == 0) return;
            Save.coins = Math.Max(0, Save.coins + amount);
            MarkDirty();
            WalletChanged?.Invoke();
        }

        public bool TrySpend(int amount)
        {
            if (Save.coins < amount) return false;
            Save.coins -= amount;
            MarkDirty();
            WalletChanged?.Invoke();
            // траты монет звучат одинаково во всей игре (пак «Cute & Cozy», 03.10.2026)
            if (amount > 0) AudioService.Play("sfx_coins_spend");
            return true;
        }

        // ------------------------------------------------------------------ алмазы (v4, 03.10.2026)

        public int Gems => Save.gems;

        public void AddGems(int amount, string reason = "")
        {
            if (amount == 0) return;
            Save.gems = Math.Max(0, Save.gems + amount);
            MarkDirty();
            WalletChanged?.Invoke();
        }

        public bool TrySpendGems(int amount)
        {
            if (Save.gems < amount) return false;
            Save.gems -= amount;
            MarkDirty();
            WalletChanged?.Invoke();
            if (amount > 0) AudioService.Play("sfx_coins_spend");
            return true;
        }

        public int Boosts(int kind)
        {
            if (Save.boosts == null || Save.boosts.Length < Economy.BoostMinutes.Length) Array.Resize(ref Save.boosts, Economy.BoostMinutes.Length);
            return Save.boosts[kind];
        }

        public void AddBoost(int kind, int n) { Boosts(kind); Save.boosts[kind] += n; MarkDirty(); WalletChanged?.Invoke(); }

        public bool ConsumeBoost(int kind)
        {
            if (Boosts(kind) <= 0) return false;
            Save.boosts[kind]--; MarkDirty(); WalletChanged?.Invoke();
            return true;
        }

        public void AddHammers(int n) { Save.hammers += n; MarkDirty(); WalletChanged?.Invoke(); }

        public void AddUndo(int n) { Save.undo += n; MarkDirty(); WalletChanged?.Invoke(); }
        public void AddHint(int n) { Save.hint += n; MarkDirty(); WalletChanged?.Invoke(); }

        public bool ConsumeUndo()
        {
            if (UndoUnlimited) return true;
            if (Save.undo <= 0) return false;
            Save.undo--; MarkDirty(); WalletChanged?.Invoke();
            return true;
        }

        public bool ConsumeHint()
        {
            if (Save.hint <= 0) return false;
            Save.hint--; MarkDirty(); WalletChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ итог уровня

        /// <summary>Засчитывает победу, возвращает монеты (без множителя рекламы).</summary>
        public LevelOutcome RegisterWin(LevelData lvl, LevelState st, PlayMode mode)
        {
            int stars = Rules.Stars(st, lvl.cart);
            int coins = Economy.WinCoins(lvl.district, lvl.difficulty, lvl.revision, st.SetCoins, stars, st.Tips);
            int dm = Mode;
            bool firstClear = mode == PlayMode.Career && lvl.id > Save.Reached(dm);
            // режимы сложности: монет за уровень ×1,25 / ×1,5
            if ((mode == PlayMode.Career || mode == PlayMode.Replay) && dm > 0) coins = Mathf.RoundToInt(coins * ModeCoinMult[dm]);
            if (mode == PlayMode.Replay) coins = Economy.ReplayCoins(coins);
            bool bigChest = false;
            if (mode == PlayMode.Daily) coins = Challenge.Win(out bigChest) + st.SetCoins;   // Испытание дня: сундук

            var o = new LevelOutcome { LevelId = lvl.id, Won = true, Stars = stars, Coins = coins, Mode = mode, ChallengeBig = bigChest };
            if (mode == PlayMode.Career) { o.StreakStep = Streak.Win(); Quests.Max("streak", Streak.Wins); }
            Quests.Add("levels");
            Quests.Add("stars", stars);
            Quests.Add("items", st.Moves);
            Quests.Add("customers", st.CustomersServed);
            if (mode == PlayMode.Career || mode == PlayMode.Replay)
            {
                Save.SetStars(lvl.id, stars);
                if (firstClear)
                {
                    bool wasOpen = ModeUnlocked(dm + 1);
                    Save.SetReached(dm, lvl.id);
                    Save.winsSincePack++;
                    if (Save.winsSincePack >= Economy.WinsPerPack) { Save.winsSincePack = 0; Save.pendingPacks++; }
                    if (lvl.revision) Save.pendingPacks++;
                    if (dm == 0)
                    {
                        if (lvl.id >= Economy.HintAt && !Save.hintGranted) { Save.hintGranted = true; Save.hint += Economy.StartHint; }
                        o.DistrictFinished = lvl.id == LevelPlanner.DistrictEnd(lvl.district);
                        Platform.SetLeaderboard("levels", Save.maxReached);
                    }
                    if (!wasOpen && ModeUnlocked(dm + 1)) o.ModeOpened = dm + 1;
                }
                Platform.SetLeaderboard("stars", Save.StarsTotal);
            }
            // рейтинг «Сегодня / Неделя»: новый уровень 10 + 5 за звезду (+20 финал района), переигровка 5 + 5 за звезду
            League.AddPoints(mode == PlayMode.Daily ? 30
                : firstClear ? 10 + 5 * stars + (o.DistrictFinished ? 20 : 0)
                : mode == PlayMode.Replay || mode == PlayMode.Career ? 5 + 5 * stars : 0);
            if (Save.attemptsLevel == lvl.id) Save.attempts = 0;
            Save.failsInRow = 0;
            Save.wins++;
            if (ShiftCoinPercent > 0 && mode != PlayMode.Rush) { coins += (coins * ShiftCoinPercent + 50) / 100; o.Coins = coins; }
            ShiftCoinPercent = 0;
            if (AlbumBonusPct > 0 && mode != PlayMode.Rush) { coins += (coins * AlbumBonusPct + 50) / 100; o.Coins = coins; }
            AddCoins(coins, "win");
            o.Piggy = AllOnShelves.Piggy.AddFromWin(coins);
            LastOutcome = o;
            MarkDirty();
            ProgressChanged?.Invoke();
            Platform.Metrica("level_win", new Dictionary<string, object>
                { { "level", lvl.id }, { "stars", stars }, { "peak_cart", st.PeakCart }, { "continue", st.ContinueUsed }, { "mode", mode.ToString() } });
            return o;
        }

        public void RegisterLose(LevelData lvl, LevelState st, PlayMode mode, bool keepStreak = false)
        {
            Save.losses++;
            Save.failsInRow++;
            // сдался в карьере, сделав хоть один ход, — серия сгорела (выход без ходов не считается)
            if (mode == PlayMode.Career && st.Moves > 0 && !keepStreak) Streak.Lose();
            if (Save.attemptsLevel != lvl.id) { Save.attemptsLevel = lvl.id; Save.attempts = 0; }
            Save.attempts++;
            AddCoins(st.SetCoins, "lose");
            MarkDirty();
            Platform.Metrica("level_lose", new Dictionary<string, object> { { "level", lvl.id }, { "reason", st.Reason.ToString() }, { "moves", st.Moves } });
        }

        public int AttemptsOn(int levelId) => Save.attemptsLevel == levelId ? Save.attempts : 0;

        // ------------------------------------------------------------------ ремонт

        public int StageDistrict
        {
            get
            {
                for (int d = 1; d <= LevelPlanner.DistrictCount; d++)
                    if (!StageComplete(d)) return d;
                return LevelPlanner.DistrictCount;
            }
        }

        /// <summary>Доступен ли этап: нельзя ремонтировать район дальше текущего прогресса.</summary>
        public bool StageOpen(int d) => d <= CurrentDistrict || (d == CurrentDistrict + 1 && DistrictLevelsDone(d - 1));

        public bool BuyItem(RenovationItem item)
        {
            if (Save.boughtItems.Contains(item.Id) || !StageOpen(item.District)) return false;
            if (!TrySpend(item.Cost)) return false;
            Save.boughtItems.Add(item.Id);
            Quests.Add("build");
            if (StageComplete(item.District))
            {
                Save.pendingPacks++;
                AddCoins(100, "stage");
                // награда за ремонт района — скин (конвейер-леденец, кондитерские стеллажи…)
                var reward = StageReward(item.District);
                if (reward != null) GrantCosmetic(reward.Id, true);
            }
            MarkDirty();
            ProgressChanged?.Invoke();
            Platform.Metrica("meta_buy", new Dictionary<string, object> { { "item", item.Id }, { "coins_left", Save.coins } });
            return true;
        }

        /// <summary>Скин за законченный ремонт района (null — за этот район скина нет или нет картинки).</summary>
        public Cosmetic StageReward(int d)
        {
            if (!MetaCatalog.RenoRewards.TryGetValue(d, out var id)) return null;
            var c = MetaCatalog.Cosmetic(id);
            return c;
        }

        public int StageProgress(int d, out int total)
        {
            var items = MetaCatalog.ItemsOf(d).ToList();
            total = items.Count;
            return items.Count(i => Save.boughtItems.Contains(i.Id));
        }

        public int CheapestMissing(int d)
        {
            var left = MetaCatalog.ItemsOf(d).Where(i => !Save.boughtItems.Contains(i.Id)).ToList();
            return left.Count == 0 ? 0 : left.Min(i => i.Cost);
        }

        // ------------------------------------------------------------------ альбом

        public bool DepartmentOpen(int dept) => MetaCatalog.DepartmentDistrict[dept] <= Math.Max(1, CurrentDistrict);

        public sealed class PackResult
        {
            public List<string> Stickers = new List<string>();
            public List<bool> IsNew = new List<bool>();
            public int DuplicateCoins;
            public List<int> DepartmentsCompleted = new List<int>();
            public bool AlbumFull;
        }

        /// <summary>Бонус коллекции: +2 % монет за уровни за каждый собранный отдел альбома.</summary>
        public int AlbumBonusPct => Save.departmentsRewarded.Count * Economy.AlbumDeptBonusPct;

        public PackResult OpenPack(string source, bool guaranteedGold = false)
        {
            var rng = new Random(Environment.TickCount);
            var r = new PackResult();
            Quests.Add("album");
            var open = Enumerable.Range(0, MetaCatalog.DepartmentCount).Where(DepartmentOpen).ToList();
            for (int k = 0; k < 3; k++)
            {
                int dept = open[rng.Next(open.Count)];
                bool gold = (guaranteedGold && k == 0) || rng.NextDouble() < 0.03;
                string id;
                if (gold) id = MetaCatalog.GoldId(dept);
                else
                {
                    var list = MetaCatalog.StickersOf(dept);
                    // новые выпадают чаще: 60% шанс взять недостающую
                    var missing = list.Where(x => !Save.stickers.Contains(x)).ToList();
                    id = missing.Count > 0 && rng.NextDouble() < 0.6 ? missing[rng.Next(missing.Count)] : list[rng.Next(list.Length)];
                }
                bool isNew;
                if (gold)
                {
                    isNew = !Save.goldStickers.Contains(id);
                    if (isNew) Save.goldStickers.Add(id); else r.DuplicateCoins += Economy.DuplicateGoldSticker;
                }
                else
                {
                    isNew = !Save.stickers.Contains(id);
                    if (isNew) Save.stickers.Add(id); else r.DuplicateCoins += Economy.DuplicateSticker;
                }
                r.Stickers.Add(id);
                r.IsNew.Add(isNew);
            }
            for (int d = 0; d < MetaCatalog.DepartmentCount; d++)
            {
                if (Save.departmentsRewarded.Contains(d)) continue;
                if (MetaCatalog.StickersOf(d).All(Save.stickers.Contains))
                {
                    Save.departmentsRewarded.Add(d);
                    r.DepartmentsCompleted.Add(d);
                    r.DuplicateCoins += Economy.DepartmentReward;
                }
            }
            if (r.DuplicateCoins > 0) AddCoins(r.DuplicateCoins, "album");
            // весь альбом собран — большой приз (v4, 03.10.2026)
            if (!Save.albumFullRewarded && Save.departmentsRewarded.Count >= MetaCatalog.DepartmentCount)
            {
                Save.albumFullRewarded = true;
                AddGems(Economy.AlbumFullGems, "album_full");
                r.AlbumFull = true;
            }
            MarkDirty();
            Platform.Metrica("album_pack", new Dictionary<string, object> { { "source", source } });
            return r;
        }

        public int StickersOwned(int dept) => MetaCatalog.StickersOf(dept).Count(Save.stickers.Contains);

        public bool CanWatchPackAd
        {
            get
            {
                if (Save.packsDate != Today) { Save.packsDate = Today; Save.packsOpenedToday = 0; }
                return Save.packsOpenedToday < Economy.RewardedPacksPerDay;
            }
        }

        // ------------------------------------------------------------------ «Звёздный путь»

        public bool StarRewardReached(int i) => Save.StarsTotal >= MetaCatalog.StarTrack[i].Stars;
        public bool StarRewardClaimed(int i) => Save.starRewards.Contains(i);
        public bool StarRewardReady(int i) => StarRewardReached(i) && !StarRewardClaimed(i);

        /// <summary>Сколько наград можно забрать прямо сейчас.</summary>
        public int StarRewardsReady
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MetaCatalog.StarTrack.Length; i++) if (StarRewardReady(i)) n++;
                for (int i = 0; i < MetaCatalog.GoldTrack.Length; i++) if (GoldRewardReady(i)) n++;
                return n;
            }
        }

        /// <summary>Сколько наград открыто по звёздам (забранных и нет).</summary>
        public int StarRewardsReachedCount
        {
            get { int n = 0; while (n < MetaCatalog.StarTrack.Length && StarRewardReached(n)) n++; return n; }
        }

        /// <summary>Индекс следующей ещё не открытой награды (-1 — открыты все).</summary>
        public int NextStarReward => StarRewardsReachedCount < MetaCatalog.StarTrack.Length ? StarRewardsReachedCount : -1;

        public bool ClaimStarReward(int i)
        {
            if (i < 0 || i >= MetaCatalog.StarTrack.Length || !StarRewardReady(i)) return false;
            var r = MetaCatalog.StarTrack[i];
            Save.starRewards.Add(i);
            GrantTrackReward(r);
            MarkDirty();
            Platform.Metrica("star_reward", new Dictionary<string, object> { { "index", i }, { "stars", Save.StarsTotal } });
            ProgressChanged?.Invoke();
            return true;
        }

        // Золотой путь (экономика v4, 03.10.2026): вторая строка на тех же шагах, только с покупкой gold_path
        public bool GoldRewardClaimed(int i) => Save.goldRewards != null && Save.goldRewards.Contains(i);
        public bool GoldRewardReady(int i) => Save.goldPath && StarRewardReached(i) && !GoldRewardClaimed(i);

        public bool ClaimGoldReward(int i)
        {
            if (i < 0 || i >= MetaCatalog.GoldTrack.Length || !GoldRewardReady(i)) return false;
            if (Save.goldRewards == null) Save.goldRewards = new List<int>();
            Save.goldRewards.Add(i);
            GrantTrackReward(MetaCatalog.GoldTrack[i]);
            MarkDirty();
            Platform.Metrica("gold_reward", new Dictionary<string, object> { { "index", i }, { "stars", Save.StarsTotal } });
            ProgressChanged?.Invoke();
            return true;
        }

        /// <summary>Выдать награду пути или шкалы заданий (монеты, подсказки, алмазы, молотки, скины, пачки).</summary>
        public void GrantTrackReward(StarReward r)
        {
            switch (r.Kind)
            {
                case "coins": AddCoins(r.Amount, "star_track"); break;
                case "hint": Save.hintGranted = true; AddHint(r.Amount); break;
                case "undo": AddUndo(r.Amount); break;
                case "gems": case "staff": AddGems(r.Amount, "star_track"); break;   // Кот приходит с самой покупкой
                case "hammer": AddHammers(r.Amount); break;
                case "boost": AddBoost(2, r.Amount); break;
                case "cosmetic":
                    if (!Save.cosmetics.Contains(r.CosmeticId)) Save.cosmetics.Add(r.CosmeticId);
                    Wear(r.CosmeticId);     // новую вещь сразу надеваем — иначе награду не видно
                    break;
                default: Save.pendingPacks += r.Amount; break;
            }
        }

        // ------------------------------------------------------------------ косметика

        public bool HasCosmetic(string id)
        {
            if (Save.cosmetics.Contains(id)) return true;
            // рамки альбома — по собранным отделам (так они есть и у тех, кто собрал отделы до v4)
            int dept = Array.IndexOf(MetaCatalog.DeptFrames, id);
            if (dept >= 0) return Save.departmentsRewarded.Contains(dept);
            if (id == MetaCatalog.AlbumMasterFrame) return Save.departmentsRewarded.Count >= MetaCatalog.DepartmentCount;
            // мордочка в скине доступна, когда есть сам скин; прочие аватарки — бесплатные
            var skin = MetaCatalog.SkinOfAvatar(id);
            if (skin != null) return Save.cosmetics.Contains(skin);
            return MetaCatalog.Cosmetic(id)?.Kind == "avatar";
        }

        /// <summary>
        /// Мордочка игрока на карте и в профиле (03.10.2026): выбранная в «Гардеробе» аватарка, а не надетый скин —
        /// скин во весь рост показывается только в уровнях. На карте — только енот (зверьки рейтинга там чужие).
        /// </summary>
        public Sprite FaceSprite(bool raccoonOnly)
        {
            string id = string.IsNullOrEmpty(Save.wearAvatar) ? "av_raccoon" : Save.wearAvatar;
            if (!HasCosmetic(id) || (raccoonOnly && id != "av_raccoon" && MetaCatalog.SkinOfAvatar(id) == null)) id = "av_raccoon";
            var c = MetaCatalog.Cosmetic(id);
            return ArtLibrary.S(c != null ? c.Sprite : "map_avatar") ?? ArtLibrary.S("map_avatar");
        }

        /// <summary>
        /// Выдать скин (награда или покупка набора). За монеты скины не продаются (30.09.2026).
        /// wear — сразу надеть, если картинка есть: иначе награду не видно.
        /// </summary>
        public void GrantCosmetic(string id, bool wear)
        {
            var c = MetaCatalog.Cosmetic(id);
            if (c == null) return;
            if (!Save.cosmetics.Contains(id)) Save.cosmetics.Add(id);
            if (wear && c.ArtReady && Worn(c.Kind) != id) Wear(id);
            MarkDirty();
        }

        /// <summary>Все ли скины набора уже есть у игрока.</summary>
        public bool OwnsAll(ProductInfo p) => p?.Cosmetics != null && p.Cosmetics.Length > 0 && p.Cosmetics.All(HasCosmetic);

        // ------------------------------------------------------------------ монеты за видео («Не хватает монет»)

        public bool CanWatchCoinsAd
        {
            get
            {
                if (Save.adCoinsDate != Today) { Save.adCoinsDate = Today; Save.adCoinsToday = 0; }
                return Save.adCoinsToday < Economy.AdCoinsPerDay;
            }
        }

        /// <summary>Подсказка или отмена за видео в магазине (01.10.2026): каждой — не больше AdHelpPerDay в день.</summary>
        public bool CanWatchHelpAd(bool hint)
        {
            if (Save.adHelpDate != Today) { Save.adHelpDate = Today; Save.adHintToday = 0; Save.adUndoToday = 0; }
            return (hint ? Save.adHintToday : Save.adUndoToday) < Economy.AdHelpPerDay;
        }

        public void ClaimHelpAd(bool hint)
        {
            if (!CanWatchHelpAd(hint)) return;
            if (hint) { Save.adHintToday++; Save.hintGranted = true; AddHint(1); }
            else { Save.adUndoToday++; AddUndo(1); }
        }

        public int AdCoinsAmount => Economy.AdCoins(Math.Max(1, CurrentDistrict));

        public void ClaimAdCoins()
        {
            if (!CanWatchCoinsAd) return;
            Save.adCoinsToday++;
            AddCoins(AdCoinsAmount, "ad_coins");
            MarkDirty();
        }

        /// <summary>Надетая вещь этого вида (пусто — обычный вид).</summary>
        public string Worn(string kind)
        {
            switch (kind)
            {
                case "halo": return Save.wearHalo;
                case "raccoon": return Save.wearRaccoon;
                case "cart": return Save.wearCart;
                case "belt": return Save.wearBelt;
                case "shelf": return Save.wearShelf;
                case "items": return Save.wearItems;
                case "avatar": return string.IsNullOrEmpty(Save.wearAvatar) ? "av_raccoon" : Save.wearAvatar;
                case "frame": return Save.wearFrame;
                default: return Save.wearScene;
            }
        }

        public void Wear(string id)
        {
            var c = MetaCatalog.Cosmetic(id);
            if (c == null) return;
            string Next(string cur) => cur == id ? "" : id;
            switch (c.Kind)
            {
                case "halo": Save.wearHalo = Next(Save.wearHalo); break;
                case "raccoon": Save.wearRaccoon = Next(Save.wearRaccoon); break;
                case "cart": Save.wearCart = Next(Save.wearCart); break;
                case "belt": Save.wearBelt = Next(Save.wearBelt); break;
                case "shelf": Save.wearShelf = Next(Save.wearShelf); break;
                case "items": Save.wearItems = Next(Save.wearItems); break;
                case "avatar": Save.wearAvatar = id == "av_raccoon" ? "" : id; break;   // аватарка есть всегда — не снимается
                case "frame": Save.wearFrame = Next(Save.wearFrame); break;
                default: Save.wearScene = Next(Save.wearScene); break;
            }
            // в рейтинге у всех видна новая аватарка и рамка — если игрок уже есть в таблицах
            if (c.Kind == "avatar" || c.Kind == "frame") League.Push();
            if (c.Kind == "raccoon") RaccoonSkin.Refresh();   // енот на всех экранах надевает костюм
            MarkDirty();
            ProgressChanged?.Invoke();
        }

        /// <summary>Картинка надетой вещи (или null, если ничего не надето или картинки ещё нет).</summary>
        public Sprite WornSprite(string kind)
        {
            var c = MetaCatalog.Cosmetic(Worn(kind));
            return c != null ? ArtLibrary.S(c.Sprite) : null;
        }

        // ------------------------------------------------------------------ ежедневки (ГДД 8.4)

        public static string Today => DateTime.Now.ToString("yyyy-MM-dd");
        public static long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public bool DailyUnlocked => Save.maxReached >= Economy.DailyAt;
        public bool CanClaimDaily => DailyUnlocked && Save.lastClaimDate != Today && string.CompareOrdinal(Save.lastClaimDate, Today) < 0;
        public bool CanClaimChest2 => DailyUnlocked && Save.lastClaimDate == Today && Save.chest2Date != Today;

        public string DailyRewardText(int index)
        {
            switch (index)
            {
                case 0: return "40 монет";
                case 1: return "1 отмена + 40 монет";
                case 2: return "Пачка наклеек";
                case 3: return "80 монет";
                case 4: return "1 подсказка + 1 отмена";
                case 5: return "120 монет";
                default: return "250 монет + золотая наклейка";
            }
        }

        /// <summary>Выдаёт награду дня. mult=0.5 — второй сундук за рекламу.</summary>
        public void ClaimDaily(float mult = 1f)
        {
            int i = Save.streakIndex;
            int coins = Economy.DailyStreakCoins[i];
            Quests.Add("daily");
            if (coins > 0) AddCoins(Mathf.RoundToInt(coins * mult), "daily");
            if (mult >= 1f)
            {
                if (i == 1 || i == 4) Save.undo++;
                if (i == 4) Save.hint++;
                if (i == 2) Save.pendingPacks++;
                if (i == 6) { Save.pendingPacks++; Save.pendingGoldPacks++; }
                // серия дней подряд
                string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                Save.streakDays = Save.lastClaimDate == yesterday ? Save.streakDays + 1 : 1;
                Save.lastClaimDate = Today;
                Save.streakIndex = (Save.streakIndex + 1) % 7;
            }
            else
            {
                Save.chest2Date = Today;
                if (coins == 0) AddCoins(30, "daily2");
            }
            MarkDirty();
        }

        public bool DailyLevelAvailable => DailyUnlocked && !(Save.dailyLevelDate == Today && Save.dailyLevelWon);

        /// <summary>«Завоз дня»: генерируется из даты, одинаков для всех на одном пресете.</summary>
        public LevelData BuildDailyLevel()
        {
            int preset = Save.maxReached < 40 ? 0 : Save.maxReached < 120 ? 1 : 2;
            int district = preset == 0 ? 2 : preset == 1 ? 5 : 8;
            int dateSeed = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            // берём параметры «среднего» уровня района
            int baseId = LevelPlanner.DistrictStart[district - 1] + 2;
            for (int a = 0; a < 6; a++)
            {
                var p = LevelPlanner.Params(baseId, dateSeed + a);
                p.Id = 900 + preset; p.Difficulty = "medium"; p.Mechanic = ""; p.Tutorial = ""; p.Revision = false;
                var lvl = LevelGenerator.Generate(p, dateSeed * 3 + a);
                var st = LevelState.FromData(lvl);
                // решение ищем под правило дня: «Тележка меньше» и «3 звезды» должны быть проходимы
                var r = new Solver { NodeLimit = 40000, TimeLimitMs = 400 }.Solve(st, Challenge.PeakLimit(Challenge.Today, st.CartCapacity));
                if (r.Solved)
                {
                    lvl.solution = r.Moves.Select(m => m.ToData()).ToArray();
                    return lvl;
                }
            }
            // запасной вариант — один из готовых средних уровней
            var pool = Levels.Where(l => l.difficulty == "medium" && l.district <= district).ToArray();
            var copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(pool[dateSeed % pool.Length]));
            copy.id = 900 + preset;
            return copy;
        }

        // ------------------------------------------------------------------ «Час пик»

        public bool RushUnlocked => Save.maxReached >= Economy.RushAt;

        public int RushCoinRunsLeft
        {
            get
            {
                if (Save.rushDate != Today) { Save.rushDate = Today; Save.rushRunsToday = 0; }
                return Math.Max(0, Economy.RushDailyRuns - Save.rushRunsToday);
            }
        }

        public int RegisterRush(int score)
        {
            Quests.Add("rush");
            int coins = 0;
            if (RushCoinRunsLeft > 0)
            {
                coins = Math.Min(Economy.RushMaxCoins, score / 20);
                Save.rushRunsToday++;
            }
            if (score > Save.rushBest)
            {
                Save.rushBest = score;
                Platform.SetLeaderboard("rush", score);
            }
            if (coins > 0) AddCoins(coins, "rush");
            League.AddPoints(score / 50);
            MarkDirty();
            return coins;
        }

        // ------------------------------------------------------------------ «Чаевые от енота»

        public bool TipsAvailable => NowUnix - Save.lastTipsTime >= Economy.TipsCooldownHours * 3600L;

        public void ClaimTips()
        {
            Save.lastTipsTime = NowUnix;
            AddCoins(Economy.TipsReward, "tips");
        }

        // ------------------------------------------------------------------ покупки

        /// <summary>Сколько монет дала последняя разбитая копилка (для анимации окна).</summary>
        public int LastPiggyBreak { get; private set; }

        public void GrantProduct(ProductInfo p)
        {
            if (p.NoAds) Save.noAds = true;
            if (p.Id == "starter_pack") Save.starterBought = true;
            if (p.Id == AllOnShelves.Piggy.ProductId) LastPiggyBreak = AllOnShelves.Piggy.Break();
            // наборы оформления и эксклюзивы: скины сразу надеваем — игрок видит покупку в первом же уровне
            if (p.Cosmetics != null) foreach (var c in p.Cosmetics) GrantCosmetic(c, true);
            if (p.Undo > 0) Save.undo += p.Undo;
            if (p.Hint > 0) Save.hint += p.Hint;
            if (p.UndoUnlimited24h) Save.undoUnlimitedUntil = Math.Max(Save.undoUnlimitedUntil, NowUnix) + 24 * 3600;
            if (p.Coins > 0) Save.coins += p.Coins;
            if (p.Gems > 0) Save.gems += p.Gems;
            if (p.Hammers > 0) Save.hammers += p.Hammers;
            if (p.GoldPath) Save.goldPath = true;
            Save.pendingPacks += p.Packs;
            Save.pendingGoldPacks += p.GoldPacks;
            MarkDirty();
            Flush();
            WalletChanged?.Invoke();
        }

        /// <summary>Постоянные покупки восстанавливаются из списка платформы (ГДД 11.3).</summary>
        public void ApplyPermanent(ProductInfo p)
        {
            bool changed = false;
            if (p.NoAds && !Save.noAds) { Save.noAds = true; changed = true; }
            if (p.Id == "starter_pack" && !Save.starterBought) { Save.starterBought = true; changed = true; }
            if (p.GoldPath && !Save.goldPath) { Save.goldPath = true; changed = true; }
            if (p.Cosmetics != null)
                foreach (var c in p.Cosmetics)
                    if (!HasCosmetic(c)) { GrantCosmetic(c, false); changed = true; }
            if (changed) { MarkDirty(); WalletChanged?.Invoke(); }
        }

        // ------------------------------------------------------------------ переходы

        public void PlayLevel(int id, PlayMode mode = PlayMode.Career, bool cartBonus = false)
        {
            PendingMode = mode;
            PendingLevel = id;
            PendingLevelData = mode == PlayMode.Daily ? BuildDailyLevel() : Level(id);
            PendingCartBonus = cartBonus;
            SceneManager.LoadScene(SceneGame);
        }

        public void PlayRush()
        {
            PendingMode = PlayMode.Rush;
            PendingLevelData = null;
            SceneManager.LoadScene(SceneGame);
        }

        public void GoHub()
        {
            Flush();
            SceneManager.LoadScene(SceneHub);
        }
    }
}

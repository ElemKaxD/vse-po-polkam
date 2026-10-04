using System;
using AllOnShelves.Core;

namespace AllOnShelves
{
    /// <summary>Сотрудник Торгового дома: живёт в своей комнате, на смене помогает в уровнях.</summary>
    public sealed class StaffInfo
    {
        public string Id;          // squirrel → картинки staff_squirrel / staff_squirrel_face
        public string Name;        // «Белка-кассир»
        public string Mechanic;    // против какой механики (для смены); "" — помогает везде
        public string[] Skill;     // умение на уровнях 1..4
        public bool Gold;          // приходит только с Золотым путём
    }

    /// <summary>Комната Торгового дома (v3.1, 03.10.2026).</summary>
    public sealed class RoomInfo
    {
        public int Index;          // 0..11, снизу вверх и слева направо
        public string Name;
        public string Staff;       // id жильца, null — общая комната
        public string Note;        // что даёт общая комната
        public int UnlockLevel;    // открывается вместе с сотрудником после этого уровня
        public int BuildCost;
        public int BuildMinutes;   // v4 (03.10.2026): от 15 минут до 7 суток (было BuildHours 2…12)
        public int Floor => Index / 3 + 1;
    }

    public enum RoomState { Locked, Available, Building, Ready, Built }

    /// <summary>
    /// Торговый дом (ГДД v3 + PROMPTS_v3.1/v3.2, 03.10.2026). Снаружи — дом в лесах, этажи открываются
    /// вместе с первой комнатой этажа. Комнаты открываются постепенно (замок «Уровень N»), строятся за монеты
    /// по таймеру (одна стройка за раз), потом в комнату заселяется сотрудник. Шесть предметов декора
    /// в комнате прокачивают жильца: каждые два — уровень (1..4).
    /// </summary>
    public static class House
    {
        public const int Rooms = 12, Floors = 4, Decor = 6, MaxLevel = 4;
        public const int SpeedAdMinutes = 30, SpeedAdMax = 3;
        public const int CashBase = 20, CashPerRoom = 3, CashHours = 8;   // v4: +3 за комнату (было 4) — касса давала треть дохода

        public static readonly string[] FloorNames = { "Лавка", "Зал", "Склад", "Мансарда" };

        public static readonly StaffInfo[] Staff =
        {
            new StaffInfo { Id = "squirrel", Name = "Белка-кассир", Mechanic = "",
                Skill = new[] { "+5 % монет за победу, касса +10 %", "+10 % монет за победу, касса +20 %",
                                "+15 % монет за победу, касса +30 %", "+25 % монет за победу, касса +50 %" } },
            new StaffInfo { Id = "hedgehog", Name = "Ёж-грузчик", Mechanic = "",
                Skill = new[] { "Тележка переполнилась — освобождает место (1 раз за уровень)", "Освобождает место 2 раза за уровень",
                                "Освобождает место 3 раза за уровень", "Освобождает место 4 раза за уровень" } },
            new StaffInfo { Id = "beaver", Name = "Бобр-плотник", Mechanic = "lock",
                Skill = new[] { "Замки открываются на 1 набор раньше", "Замки — на 2 набора раньше",
                                "Замки — на 3 набора раньше", "Замки — на 4 набора раньше" } },
            new StaffInfo { Id = "rabbit", Name = "Кролик-зазывала", Mechanic = "customer",
                Skill = new[] { "Покупатели ждут на 1 ход дольше", "Ждут на 2 хода дольше",
                                "Ждут на 2 хода дольше, чаевые ×1,5", "Ждут на 3 хода дольше, чаевые ×2" } },
            new StaffInfo { Id = "hamster", Name = "Хомяк-кладовщик", Mechanic = "perish",
                Skill = new[] { "Скоропорт держится на 2 хода дольше", "На 3 хода дольше",
                                "На 4 хода дольше", "На 6 ходов дольше" } },
            new StaffInfo { Id = "penguin", Name = "Пингвин-холодильщик", Mechanic = "freezer",
                Skill = new[] { "Заморозка тает на 2 хода позже", "На 3 хода позже",
                                "На 4 хода позже", "На 6 ходов позже" } },
            new StaffInfo { Id = "fox", Name = "Лиса-маркетолог", Mechanic = "sale",
                Skill = new[] { "Акционная полка даёт ×3 монет", "Акция ×4", "Акция ×5", "Акция ×6" } },
            new StaffInfo { Id = "owl", Name = "Сова-сторож", Mechanic = "night",
                Skill = new[] { "Ночью видно на 1 товар дальше", "Видно на 2 товара дальше",
                                "Видно на 3 товара дальше", "Видно на 4 товара дальше" } },
            new StaffInfo { Id = "bear", Name = "Медведь-силач", Mechanic = "big",
                Skill = new[] { "С крупным товаром тележка больше на 1 место", "Тележка +1 место, если есть крупный товар",
                                "С крупным товаром тележка больше на 2 места", "Тележка +2 места, если есть крупный товар" } },
            new StaffInfo { Id = "cat", Name = "Кот-управляющий", Mechanic = "", Gold = true,
                Skill = new[] { "+1 отмена в начале уровня", "+1 отмена и +5 % монет",
                                "+2 отмены, +1 подсказка и +5 % монет", "+2 отмены, +1 подсказка и +10 % монет" } },
        };

        // открываются по прогрессу — вместе с сотрудником; первая комната этажа открывает и сам этаж
        public static readonly RoomInfo[] All =
        {
            // v4 (03.10.2026, Docs «Экономика v4»): стройка растёт от минут до суток и недели, цены этажей 2–3 ниже.
            // верхний этаж — на уровнях 100/115/130 (v4: уровней стало 150, 03.10.2026)
            new RoomInfo { Index = 0,  Name = "Касса",              Staff = "squirrel", UnlockLevel = Economy.HouseAt, BuildCost = 0,    BuildMinutes = 0 },
            new RoomInfo { Index = 1,  Name = "Подсобка",           Staff = "hedgehog", UnlockLevel = 18,  BuildCost = 600,  BuildMinutes = 15 },
            new RoomInfo { Index = 2,  Name = "Мастерская",         Staff = "beaver",   UnlockLevel = 24,  BuildCost = 600,  BuildMinutes = 60 },
            new RoomInfo { Index = 3,  Name = "Стойка у входа",     Staff = "rabbit",   UnlockLevel = 31,  BuildCost = 1200, BuildMinutes = 180 },
            new RoomInfo { Index = 4,  Name = "Кладовая",           Staff = "hamster",  UnlockLevel = 36,  BuildCost = 1200, BuildMinutes = 360 },
            new RoomInfo { Index = 5,  Name = "Холодильный цех",    Staff = "penguin",  UnlockLevel = 42,  BuildCost = 1200, BuildMinutes = 600 },
            new RoomInfo { Index = 6,  Name = "Рекламный отдел",    Staff = "fox",      UnlockLevel = 51,  BuildCost = 2500, BuildMinutes = 960 },
            new RoomInfo { Index = 7,  Name = "Ночной пост",        Staff = "owl",      UnlockLevel = 57,  BuildCost = 2500, BuildMinutes = 1440 },
            new RoomInfo { Index = 8,  Name = "Грузовая рампа",     Staff = "bear",     UnlockLevel = 64,  BuildCost = 2500, BuildMinutes = 2160 },
            new RoomInfo { Index = 9,  Name = "Буфет",              Staff = null,       UnlockLevel = 100,  BuildCost = 4000, BuildMinutes = 3600,
                           Note = "Общая комната. Каждые 2 предмета — касса +5 %" },
            new RoomInfo { Index = 10, Name = "Кабинет управляющего", Staff = "cat",    UnlockLevel = 115,  BuildCost = 4000, BuildMinutes = 5760 },
            new RoomInfo { Index = 11, Name = "Терраса",            Staff = null,       UnlockLevel = 130,  BuildCost = 4000, BuildMinutes = 10080,
                           Note = "Финал дома. Каждые 2 предмета — касса копит на 1 час дольше" },
        };

        // цена декора: шесть предметов от дешёвого к «гордости комнаты», этажи дороже
        static readonly int[] DecorBase = { 120, 150, 200, 250, 300, 450 };
        static readonly float[] FloorMult = { 1f, 2f, 3f, 4f };   // v4: было 1 / 2 / 3,5 / 5 — декор всего дома 44 тыс.

        public static StaffInfo StaffOf(string id) => Array.Find(Staff, s => s.Id == id);
        public static StaffInfo StaffOf(int room) => All[room].Staff == null ? null : StaffOf(All[room].Staff);
        public static int RoomOf(string staffId) => Array.FindIndex(All, r => r.Staff == staffId);

        public static int DecorPrice(int room, int item) =>
            (int)Math.Round(DecorBase[Math.Min(item, Decor - 1)] * FloorMult[All[room].Floor - 1] / 10f) * 10;

        // ------------------------------------------------------------------ состояние

        static SaveData S
        {
            get
            {
                var s = GameApp.I.Save;
                if (s.houseDecor == null || s.houseDecor.Length < Rooms) Array.Resize(ref s.houseDecor, Rooms);
                return s;
            }
        }

        static long Now => GameApp.NowUnix;

        public static bool Unlocked => GameApp.I != null && GameApp.I.Save.maxReached >= Economy.HouseAt;
        public static int FloorUnlockLevel(int floor) => All[(floor - 1) * 3].UnlockLevel;
        public static bool FloorOpen(int floor) => S.maxReached >= FloorUnlockLevel(floor);
        public static int FloorsOpen { get { int n = 0; for (int f = 1; f <= Floors; f++) if (FloorOpen(f)) n = f; return n; } }

        public static bool IsBuilt(int room) => (S.houseBuilt & (1 << room)) != 0;
        public static int BuiltCount { get { int n = 0; for (int i = 0; i < Rooms; i++) if (IsBuilt(i)) n++; return n; } }

        public static RoomState State(int room)
        {
            var s = S;
            if (IsBuilt(room)) return RoomState.Built;
            if (s.maxReached < All[room].UnlockLevel) return RoomState.Locked;
            if (s.houseBuilding == room) return Now >= s.houseBuildEnd ? RoomState.Ready : RoomState.Building;
            return RoomState.Available;
        }

        /// <summary>Сколько секунд осталось стройке (0 — готово).</summary>
        public static long BuildLeft(int room) => S.houseBuilding == room ? Math.Max(0, S.houseBuildEnd - Now) : 0;

        /// <summary>Доля готовности стройки 0..1.</summary>
        public static float BuildProgress(int room)
        {
            long total = All[room].BuildMinutes * 60L;
            return total <= 0 ? 1f : 1f - BuildLeft(room) / (float)total;
        }

        /// <summary>Строится ли сейчас другая комната (одна стройка за раз — повод зайти позже).</summary>
        public static int BusyRoom => S.houseBuilding >= 0 && !IsBuilt(S.houseBuilding) ? S.houseBuilding : -1;

        public static bool StartBuild(int room)
        {
            var s = S;
            if (State(room) != RoomState.Available || BusyRoom >= 0) return false;
            if (!GameApp.I.TrySpend(All[room].BuildCost)) return false;
            s.houseBuilding = room;
            s.houseBuildEnd = Now + All[room].BuildMinutes * 60L;
            s.houseBuildAds = 0;
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static bool CanSpeedUpAd(int room) => State(room) == RoomState.Building && S.houseBuildAds < SpeedAdMax;

        public static void SpeedUpAd(int room)
        {
            if (!CanSpeedUpAd(room)) return;
            S.houseBuildEnd -= SpeedAdMinutes * 60L;
            S.houseBuildAds++;
            GameApp.I.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>Цена «Готово сейчас» в алмазах за оставшееся время стройки.</summary>
        public static int GemFinishCost(int room) => Economy.GemSkipCost(BuildLeft(room) / 60.0);

        /// <summary>Достроить за алмазы (v4, 03.10.2026).</summary>
        public static bool FinishForGems(int room)
        {
            if (State(room) != RoomState.Building) return false;
            if (!GameApp.I.TrySpendGems(GemFinishCost(room))) return false;
            S.houseBuildEnd = Now;
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Ускоритель-предмет: стройка короче на его минуты.</summary>
        public static bool UseBoost(int room, int kind)
        {
            if (State(room) != RoomState.Building || !GameApp.I.ConsumeBoost(kind)) return false;
            S.houseBuildEnd = Math.Max(Now, S.houseBuildEnd - Economy.BoostMinutes[kind] * 60L);
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static bool UseHammer(int room)
        {
            if (State(room) != RoomState.Building || S.hammers <= 0) return false;
            S.hammers--;
            S.houseBuildEnd = Now;
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Стройка готова — «Заселить!»: комната построена, сотрудник въехал (1 уровень).</summary>
        public static bool MoveIn(int room)
        {
            if (State(room) != RoomState.Ready) return false;
            Built(room);
            return true;
        }

        static void Built(int room)
        {
            var s = S;
            s.houseBuilt |= 1 << room;
            if (s.houseBuilding == room) s.houseBuilding = -1;
            if (room == 0 && s.houseCashSince == 0) s.houseCashSince = Now;
            GameApp.I.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>Первый вход в дом: этаж 1 открыт, Касса уже построена и в ней Белка (обучение).</summary>
        public static void EnsureStarted()
        {
            if (!Unlocked || IsBuilt(0)) return;
            Built(0);
        }

        public static int DecorCount(int room) => Math.Min(Decor, S.houseDecor[room]);

        /// <summary>Уровень жильца: въехал — 1, каждые 2 предмета декора — ещё уровень (до 4).</summary>
        public static int Level(int room) => IsBuilt(room) ? Math.Min(MaxLevel, 1 + DecorCount(room) / 2) : 0;

        /// <summary>Уровень сотрудника по id (0 — не работает: комната не построена или не пришёл).</summary>
        public static int LevelOf(string staffId)
        {
            int r = RoomOf(staffId);
            if (r < 0) return 0;
            var st = StaffOf(staffId);
            if (st != null && st.Gold && !GameApp.I.Save.goldPath) return 0;
            return Level(r);
        }

        /// <summary>Сколько предметов ещё купить до следующего уровня (0 — максимум).</summary>
        public static int DecorToNextLevel(int room)
        {
            int lvl = Level(room);
            if (lvl >= MaxLevel || !IsBuilt(room)) return 0;
            return lvl * 2 - DecorCount(room);
        }

        public static int NextDecorPrice(int room) => DecorCount(room) >= Decor ? 0 : DecorPrice(room, DecorCount(room));

        /// <summary>Покупка следующего предмета декора. free — подарок обучения.</summary>
        public static bool BuyDecor(int room, bool free = false)
        {
            if (!IsBuilt(room) || DecorCount(room) >= Decor) return false;
            if (!free && !GameApp.I.TrySpend(NextDecorPrice(room))) return false;
            S.houseDecor[room]++;
            if (!free) Quests.Add("build");
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static bool AllDone { get { for (int i = 0; i < Rooms; i++) if (!IsBuilt(i) || DecorCount(i) < Decor) return false; return true; } }

        // ------------------------------------------------------------------ касса

        /// <summary>Монет в час: 20 на старте, +4 за каждую построенную комнату, Белка и Буфет добавляют проценты.</summary>
        public static float CashPerHour
        {
            get
            {
                if (!IsBuilt(0)) return 0f;
                float k = 1f + SquirrelCash(Level(0)) + 0.05f * (DecorCount(9) / 2);
                return (CashBase + CashPerRoom * (BuiltCount - 1)) * k;
            }
        }

        static float SquirrelCash(int lvl) => lvl switch { 1 => 0.1f, 2 => 0.2f, 3 => 0.3f, 4 => 0.5f, _ => 0f };

        public static int CashCapHours => CashHours + DecorCount(11) / 2;
        public static int CashCap => (int)(CashPerHour * CashCapHours);

        public static int CashReady
        {
            get
            {
                if (!IsBuilt(0) || S.houseCashSince <= 0) return 0;
                float hours = Math.Min(CashCapHours, (Now - S.houseCashSince) / 3600f);
                return (int)(hours * CashPerHour);
            }
        }

        public static bool CashFull => CashReady >= CashCap && CashCap > 0;

        public static int CollectCash(bool doubled)
        {
            int c = CashReady;
            if (c <= 0) return 0;
            if (doubled) c *= 2;
            S.houseCashSince = Now;
            GameApp.I.AddCoins(c, "house_cash");
            Changed?.Invoke();
            return c;
        }

        /// <summary>Есть что сделать в доме: забрать кассу, заселить, открылась комната, хватает на декор.</summary>
        public static bool HasNews
        {
            get
            {
                if (!Unlocked) return false;
                if (!IsBuilt(0) || CashReady >= 30) return true;
                for (int i = 0; i < Rooms; i++)
                {
                    var st = State(i);
                    if (st == RoomState.Ready) return true;
                    if (st == RoomState.Available && BusyRoom < 0 && GameApp.I.Coins >= All[i].BuildCost) return true;
                    if (st == RoomState.Built && DecorCount(i) < Decor && GameApp.I.Coins >= NextDecorPrice(i)) return true;
                }
                return false;
            }
        }

        public static string Time(long seconds)
        {
            if (seconds <= 0) return "готово";
            long h = seconds / 3600, m = seconds % 3600 / 60, sec = seconds % 60;
            if (h >= 24) return $"{h / 24} д {h % 24} ч";   // v4: стройка до 7 суток
            return h > 0 ? $"{h} ч {m:00} мин" : m > 0 ? $"{m} мин {sec:00} с" : $"{sec} с";
        }

        public static event Action Changed;
    }
}

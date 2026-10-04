using System;
using System.Collections.Generic;
using AllOnShelves.Core;

namespace AllOnShelves
{
    /// <summary>
    /// Задания дня со шкалами недели и месяца (экономика v4, 03.10.2026). Каждый день — 3 задания из списка
    /// (только из того, что игроку уже открыто); выполнил — «Забрать»: 30 монет и 10 листиков-очков. Листики
    /// заполняют шкалу недели (5 сундуков, сброс в ночь на понедельник по Москве) и шкалу месяца (4 награды и
    /// большой приз, сброс 1-го числа). Числа — Tools/economy_v4.json, раздел quests.
    /// </summary>
    public static class Quests
    {
        public const int PerDay = 3, CoinsEach = 30, PointsEach = 10;
        public const int UnlockAt = 8;   // вместе с «Завозом дня»

        public sealed class Def
        {
            public string Id, Icon, Text;
            public int Target;
            public Func<SaveData, bool> Open;
        }

        static readonly Def[] Pool =
        {
            new Def { Id = "levels", Icon = "icon_q_levels", Text = "Пройди {0} уровня", Target = 3, Open = s => true },
            new Def { Id = "stars", Icon = "icon_q_stars", Text = "Набери {0} звёзд", Target = 6, Open = s => true },
            new Def { Id = "items", Icon = "icon_q_items", Text = "Разложи {0} товаров", Target = 60, Open = s => true },
            new Def { Id = "no_undo", Icon = "icon_q_no_undo", Text = "Пройди уровень без отмен", Target = 1, Open = s => true },
            new Def { Id = "customers", Icon = "icon_q_customers", Text = "Обслужи {0} покупателей", Target = 4, Open = s => s.maxReached >= 9 },
            new Def { Id = "daily", Icon = "icon_q_daily", Text = "Забери «Завоз дня»", Target = 1, Open = s => s.maxReached >= Economy.DailyAt },
            new Def { Id = "album", Icon = "icon_q_album", Text = "Открой пачку наклеек", Target = 1, Open = s => s.maxReached >= Economy.AlbumAt },
            new Def { Id = "build", Icon = "icon_q_build", Text = "Купи улучшение в ремонте или доме", Target = 1, Open = s => s.maxReached >= Economy.RenoAt },
            new Def { Id = "rush", Icon = "icon_q_rush", Text = "Сыграй «Час пик»", Target = 1, Open = s => s.maxReached >= Economy.RushAt },
            new Def { Id = "streak", Icon = "icon_q_streak", Text = "Выиграй {0} уровня подряд", Target = 3, Open = s => s.maxReached >= Streak.ShowAt },
            new Def { Id = "cash", Icon = "icon_q_cash", Text = "Забери выручку кассы", Target = 1, Open = s => (s.houseBuilt & 1) != 0 },
            new Def { Id = "challenge", Icon = "icon_q_challenge", Text = "Пройди Испытание дня", Target = 1, Open = s => Challenge.Unlocked },
        };

        // шкала недели: 5 сундуков; месяца: 4 награды и большой приз (виды наград — как у «Звёздного пути»)
        public static readonly int[] WeekPoints = { 30, 60, 100, 140, 175 };
        public static readonly StarReward[] WeekRewards =
        {
            new StarReward(30, "coins", 40), new StarReward(60, "hint", 2), new StarReward(100, "pack", 1),
            new StarReward(140, "coins", 80), new StarReward(175, "gems", 15),
        };
        public static readonly string[] WeekChests = { "chest_week_1", "chest_week_1", "chest_week_2", "chest_week_2", "chest_week_3" };
        public const int WeekGoldCoins = 130;   // золотой сундук недели: 💎 15 + 130 монет

        public static readonly int[] MonthPoints = { 140, 280, 420, 560, 700 };
        public static readonly StarReward[] MonthRewards =
        {
            new StarReward(140, "coins", 150), new StarReward(280, "pack", 2), new StarReward(420, "boost", 2),
            new StarReward(560, "coins", 250), new StarReward(700, "gems", 60),
        };
        public const int MonthPrizeCoins = 400;  // большой приз месяца: 💎 60 + 400 монет

        public static event Action Changed;

        static SaveData S => GameApp.I.Save;
        public static bool Unlocked => GameApp.I != null && S.maxReached >= UnlockAt;
        static int MonthNumber => League.Now.Year * 12 + League.Now.Month;

        public static Def Get(int slot)
        {
            Roll();
            return Array.Find(Pool, d => d.Id == S.questIds[slot]);
        }

        public static int Progress(int slot) => Math.Min(S.questProgress[slot], Get(slot)?.Target ?? 1);
        public static bool Done(int slot) { var d = Get(slot); return d != null && S.questProgress[slot] >= d.Target; }
        public static bool Claimed(int slot) => (S.questClaimed & (1 << slot)) != 0;
        public static string TextOf(int slot) { var d = Get(slot); return d == null ? "" : string.Format(d.Text, d.Target); }

        /// <summary>Сколько готовых к «Забрать» (заданий и сундуков) — для точки на кнопке.</summary>
        public static int ReadyCount
        {
            get
            {
                if (!Unlocked) return 0;
                Roll();
                int n = 0;
                for (int i = 0; i < PerDay; i++) if (Done(i) && !Claimed(i)) n++;
                for (int i = 0; i < WeekPoints.Length; i++) if (WeekReady(i)) n++;
                for (int i = 0; i < MonthPoints.Length; i++) if (MonthReady(i)) n++;
                return n;
            }
        }

        public static bool WeekReady(int i) => S.questWeekPts >= WeekPoints[i] && (S.questWeekClaimed & (1 << i)) == 0;
        public static bool WeekClaimed(int i) => (S.questWeekClaimed & (1 << i)) != 0;
        public static bool MonthReady(int i) => S.questMonthPts >= MonthPoints[i] && (S.questMonthClaimed & (1 << i)) == 0;
        public static bool MonthClaimed(int i) => (S.questMonthClaimed & (1 << i)) != 0;
        public static int WeekPts => S.questWeekPts;
        public static int MonthPts => S.questMonthPts;

        /// <summary>Новый день — новые задания; новая неделя / месяц — шкалы с нуля.</summary>
        public static void Roll()
        {
            if (GameApp.I == null) return;
            var s = S;
            if (s.questIds == null || s.questIds.Length != PerDay) s.questIds = new string[PerDay];
            if (s.questProgress == null || s.questProgress.Length != PerDay) s.questProgress = new int[PerDay];
            bool dirty = false;
            if (s.questWeek != League.WeekNumber) { s.questWeek = League.WeekNumber; s.questWeekPts = 0; s.questWeekClaimed = 0; dirty = true; }
            if (s.questMonth != MonthNumber) { s.questMonth = MonthNumber; s.questMonthPts = 0; s.questMonthClaimed = 0; dirty = true; }
            if (s.questDay != League.DayNumber || string.IsNullOrEmpty(s.questIds[0]))
            {
                s.questDay = League.DayNumber;
                var open = new List<Def>();
                foreach (var d in Pool) if (d.Open(s)) open.Add(d);
                var rng = new Random(League.DayNumber * 7919 + 13);
                for (int i = 0; i < PerDay; i++)
                {
                    int k = rng.Next(open.Count);
                    s.questIds[i] = open[k].Id;
                    open.RemoveAt(k);
                    s.questProgress[i] = 0;
                }
                s.questClaimed = 0;
                dirty = true;
            }
            if (dirty) GameApp.I.MarkDirty();
        }

        /// <summary>Что-то сделано: +n к заданиям этого вида. Выполнено — сообщение.</summary>
        public static void Add(string id, int n = 1)
        {
            if (!Unlocked || n <= 0) return;
            Roll();
            var s = S;
            for (int i = 0; i < PerDay; i++)
            {
                if (s.questIds[i] != id) continue;
                var d = Get(i);
                bool was = s.questProgress[i] >= d.Target;
                s.questProgress[i] = Math.Min(d.Target, s.questProgress[i] + n);
                GameApp.I.MarkDirty();
                if (!was && s.questProgress[i] >= d.Target) Toast.Show("Задание выполнено: " + TextOf(i), d.Icon);
            }
            Changed?.Invoke();
        }

        /// <summary>Задание «держи значение» (серия): прогресс — наибольшее за день.</summary>
        public static void Max(string id, int value)
        {
            if (!Unlocked) return;
            Roll();
            for (int i = 0; i < PerDay; i++)
                if (S.questIds[i] == id && value > S.questProgress[i]) Add(id, value - S.questProgress[i]);
        }

        public static bool Claim(int slot)
        {
            if (!Done(slot) || Claimed(slot)) return false;
            var s = S;
            s.questClaimed |= 1 << slot;
            s.questWeekPts += PointsEach;
            s.questMonthPts += PointsEach;
            GameApp.I.AddCoins(CoinsEach, "quest");
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static bool ClaimWeek(int i)
        {
            if (!WeekReady(i)) return false;
            S.questWeekClaimed |= 1 << i;
            GameApp.I.GrantTrackReward(WeekRewards[i]);
            if (i == WeekRewards.Length - 1) GameApp.I.AddCoins(WeekGoldCoins, "quest_week");
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static bool ClaimMonth(int i)
        {
            if (!MonthReady(i)) return false;
            S.questMonthClaimed |= 1 << i;
            GameApp.I.GrantTrackReward(MonthRewards[i]);
            if (i == MonthRewards.Length - 1) GameApp.I.AddCoins(MonthPrizeCoins, "quest_month");
            GameApp.I.MarkDirty();
            Changed?.Invoke();
            return true;
        }
    }
}

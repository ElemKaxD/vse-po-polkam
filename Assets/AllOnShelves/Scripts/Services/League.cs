using System;
using System.Collections.Generic;
using UnityEngine;
using YG;
using YG.Utils.LB;

namespace AllOnShelves
{
    /// <summary>Награда за место в рейтинге «Сегодня» или «Неделя».</summary>
    public sealed class LeagueReward
    {
        public int Coins, Packs;
        public string Frame;      // рамка аватарки (только недельные награды)
        public int Gift;          // картинка подарка lb_gift_N (1..6)
        public string Places;     // «1», «4–10», «участие»

        public bool Empty => Coins == 0 && Packs == 0 && string.IsNullOrEmpty(Frame);
    }

    /// <summary>
    /// Рейтинг «Сегодня / Неделя» (01.10.2026, дизайн — Docs/PROMPTS_Рейтинг_ВсёПоПолкам_01-10.txt).
    /// У лидербордов Яндекса нет сброса по времени, поэтому номер периода «вшит» в очки:
    /// day = номер_дня × 100 000 + очки, week = номер_недели × 1 000 000 + очки. Записи текущего периода
    /// всегда выше прошлых, место среди них — настоящее; прошлые записи экран просто не показывает.
    /// Время — московское у всех игроков, чтобы день у всех кончался одновременно.
    /// Итог периода — последнее место, которое игра видела (после побед и при входе в хаб).
    /// </summary>
    public static class League
    {
        public const string DayBoard = "day", WeekBoard = "week", WorldBoard = "stars";
        public const int DayBase = 100000, WeekBase = 1000000;
        public const int ParticipationPoints = 100;   // за неделю — хотя бы столько очков, чтобы получить «за участие»

        static readonly DateTime Epoch = new DateTime(2025, 12, 29);   // понедельник

        public static DateTime Now => DateTime.UtcNow.AddHours(3);
        public static int DayNumber => (int)(Now.Date - Epoch).TotalDays + 1;
        public static int WeekNumber => (int)(Now.Date - Epoch).TotalDays / 7 + 1;

        /// <summary>Сколько осталось до конца дня / недели.</summary>
        public static TimeSpan DayLeft => Now.Date.AddDays(1) - Now;
        public static TimeSpan WeekLeft => Epoch.AddDays(WeekNumber * 7) - Now;

        public static string Left(TimeSpan t) =>
            t.TotalHours >= 24 ? $"{(int)t.TotalDays} д {t.Hours} ч" : t.TotalMinutes >= 60 ? $"{(int)t.TotalHours} ч {t.Minutes} мин" : $"{Math.Max(1, t.Minutes)} мин";

        // ------------------------------------------------------------------ награды за места

        public static LeagueReward DayReward(int rank)
        {
            if (rank == 1) return new LeagueReward { Coins = 250, Packs = 1, Gift = 1, Places = "1" };
            if (rank == 2) return new LeagueReward { Coins = 150, Packs = 1, Gift = 2, Places = "2" };
            if (rank == 3) return new LeagueReward { Coins = 100, Packs = 1, Gift = 3, Places = "3" };
            if (rank >= 4 && rank <= 10) return new LeagueReward { Coins = 60, Gift = 4, Places = "4–10" };
            if (rank >= 11 && rank <= 50) return new LeagueReward { Coins = 30, Gift = 5, Places = "11–50" };
            if (rank >= 51 && rank <= 100) return new LeagueReward { Coins = 15, Gift = 6, Places = "51–100" };
            return new LeagueReward { Places = "" };
        }

        /// <summary>points — очки недели: ниже 100-го места награда «за участие», если набрано 100 очков.</summary>
        public static LeagueReward WeekReward(int rank, int points)
        {
            if (rank == 1) return new LeagueReward { Coins = 1500, Packs = 3, Frame = "frame_crown", Gift = 1, Places = "1" };
            if (rank == 2) return new LeagueReward { Coins = 1000, Packs = 2, Frame = "frame_silver", Gift = 2, Places = "2" };
            if (rank == 3) return new LeagueReward { Coins = 700, Packs = 2, Frame = "frame_bronze", Gift = 3, Places = "3" };
            if (rank >= 4 && rank <= 10) return new LeagueReward { Coins = 400, Packs = 1, Frame = "frame_laurel", Gift = 4, Places = "4–10" };
            if (rank >= 11 && rank <= 50) return new LeagueReward { Coins = 200, Packs = 1, Gift = 5, Places = "11–50" };
            if (rank >= 51 && rank <= 100) return new LeagueReward { Coins = 100, Gift = 6, Places = "51–100" };
            if (points >= ParticipationPoints) return new LeagueReward { Coins = 40, Gift = 6, Places = "участие" };
            return new LeagueReward { Places = "" };
        }

        /// <summary>Строки таблицы наград (страница «Награды»): первое место каждой ступени.</summary>
        public static readonly int[] TierRanks = { 1, 2, 3, 4, 11, 51 };

        /// <summary>За какое место недели дают рамку (для подписи в «Гардеробе»).</summary>
        public static int FramePlace(string frame) =>
            frame == "frame_crown" ? 1 : frame == "frame_silver" ? 2 : frame == "frame_bronze" ? 3 : frame == "frame_laurel" ? 4 : 0;

        public static string Describe(LeagueReward r)
        {
            var parts = new List<string>();   // через «·»: строка должна помещаться в маленькое окно
            if (r.Coins > 0) parts.Add($"+{r.Coins} монет");
            if (r.Packs > 0) parts.Add(r.Packs == 1 ? "пачка" : $"{r.Packs} пачки");
            if (!string.IsNullOrEmpty(r.Frame)) parts.Add("рамка");   // какая — видно в «Гардеробе», строка короткая
            return string.Join(" · ", parts);
        }

        // ------------------------------------------------------------------ очки

        static SaveData S => GameApp.I.Save;

        /// <summary>Сменился день или неделя — запомнить итог прошлого периода как награду «забрать».</summary>
        public static void Roll()
        {
            if (GameApp.I == null) return;
            var s = S;
            int day = DayNumber, week = WeekNumber;
            if (s.lbDay != day)
            {
                if (s.lbDay > 0 && s.lbDayPts > 0 && s.lbDayRank > 0 && !DayReward(s.lbDayRank).Empty)
                {
                    if (s.lbPendDayRank > 0) Grant(false, true);   // прошлую не забрал — отдаём молча, чтобы не пропала
                    s.lbPendDay = s.lbDay; s.lbPendDayRank = s.lbDayRank; s.lbPendDayPts = s.lbDayPts;
                }
                s.lbDay = day; s.lbDayPts = 0; s.lbDayRank = 0;
                GameApp.I.MarkDirty();
            }
            if (s.lbWeek != week)
            {
                if (s.lbWeek > 0 && s.lbWeekPts > 0 && !WeekReward(s.lbWeekRank > 0 ? s.lbWeekRank : int.MaxValue, s.lbWeekPts).Empty)
                {
                    if (s.lbPendWeekRank != 0) Grant(true, true);
                    s.lbPendWeek = s.lbWeek; s.lbPendWeekRank = s.lbWeekRank > 0 ? s.lbWeekRank : -1; s.lbPendWeekPts = s.lbWeekPts;
                }
                s.lbWeek = week; s.lbWeekPts = 0; s.lbWeekRank = 0;
                GameApp.I.MarkDirty();
            }
        }

        /// <summary>
        /// Очки за игру: новый уровень 10 + 5 за звезду (+20 за финал района), переигровка 5 + 5 за звезду,
        /// «Заказ дня» 30, «Час пик» — очки забега / 50.
        /// </summary>
        public static void AddPoints(int points)
        {
            if (GameApp.I == null || points <= 0) return;
            Roll();
            var s = S;
            s.lbDayPts = Math.Min(DayBase - 1, s.lbDayPts + points);
            s.lbWeekPts = Math.Min(WeekBase - 1, s.lbWeekPts + points);
            GameApp.I.MarkDirty();
            Push();
        }

        /// <summary>Отправить текущие очки (и аватарку с рамкой в extraData) в таблицы day и week.</summary>
        public static void Push()
        {
            var s = S;
            string extra = Extra();
            if (s.lbDayPts > 0) Platform.SetLeaderboard(DayBoard, s.lbDay * DayBase + s.lbDayPts, extra);
            if (s.lbWeekPts > 0) Platform.SetLeaderboard(WeekBoard, s.lbWeek * WeekBase + s.lbWeekPts, extra);
            // место узнаём после записи: запрос встаёт в ту же очередь, что и запись
            if (s.lbDayPts > 0) Platform.RequestLeaderboard(DayBoard, 1, 1);
            if (s.lbWeekPts > 0) Platform.RequestLeaderboard(WeekBoard, 1, 1);
        }

        /// <summary>Вход в хаб: обновить места, чтобы итог периода был свежим.</summary>
        public static void RefreshRanks()
        {
            Roll();
            var s = S;
            if (s.lbDayPts > 0) Platform.RequestLeaderboard(DayBoard, 1, 1);
            if (s.lbWeekPts > 0) Platform.RequestLeaderboard(WeekBoard, 1, 1);
        }

        /// <summary>Пришла таблица (из любого места игры): запоминаем своё место в текущем периоде.</summary>
        public static void OnData(LBData data)
        {
            if (GameApp.I == null || data == null || data.currentPlayer == null || data.currentPlayer.rank <= 0) return;
            var s = S;
            if (data.technoName == DayBoard && data.currentPlayer.score / DayBase == s.lbDay)
            {
                s.lbDayRank = data.currentPlayer.rank;
                s.lbDayPts = Math.Max(s.lbDayPts, data.currentPlayer.score % DayBase);
                GameApp.I.MarkDirty();
            }
            else if (data.technoName == WeekBoard && data.currentPlayer.score / WeekBase == s.lbWeek)
            {
                s.lbWeekRank = data.currentPlayer.rank;
                s.lbWeekPts = Math.Max(s.lbWeekPts, data.currentPlayer.score % WeekBase);
                GameApp.I.MarkDirty();
            }
        }

        // ------------------------------------------------------------------ итоги

        public static bool HasPending => GameApp.I != null && (S.lbPendDayRank > 0 || S.lbPendWeekRank != 0);

        public static LeagueReward PendingDay => S.lbPendDayRank > 0 ? DayReward(S.lbPendDayRank) : null;
        public static LeagueReward PendingWeek => S.lbPendWeekRank != 0 ? WeekReward(S.lbPendWeekRank > 0 ? S.lbPendWeekRank : int.MaxValue, S.lbPendWeekPts) : null;

        /// <summary>Заголовок окна итогов: «Итоги недели: 7-е место!».</summary>
        public static string PendingTitle()
        {
            var s = S;
            if (s.lbPendWeekRank != 0)
                return s.lbPendWeekRank > 0 && s.lbPendWeekRank <= 100 ? $"Итоги недели: {s.lbPendWeekRank}-е место!" : "Итоги недели";
            return $"Итоги дня: {s.lbPendDayRank}-е место!";
        }

        public static string PendingText()
        {
            var lines = new List<string>();
            // окно маленькое: не больше двух коротких строк
            if (S.lbPendWeekRank != 0) lines.Add(Describe(PendingWeek));
            if (S.lbPendDayRank > 0) lines.Add((S.lbPendWeekRank != 0 ? "и за день: " : "") + Describe(PendingDay));
            return string.Join("\n", lines);
        }

        /// <summary>Забрать все итоги. Возвращает монеты (для полёта к счётчику).</summary>
        public static int ClaimAll()
        {
            int coins = 0;
            if (S.lbPendWeekRank != 0) coins += Grant(true, false);
            if (S.lbPendDayRank > 0) coins += Grant(false, false);
            return coins;
        }

        static int Grant(bool week, bool silent)
        {
            var s = S;
            var app = GameApp.I;
            var r = week ? PendingWeek : PendingDay;
            if (week) { s.lbPendWeek = 0; s.lbPendWeekRank = 0; s.lbPendWeekPts = 0; }
            else { s.lbPendDay = 0; s.lbPendDayRank = 0; s.lbPendDayPts = 0; }
            if (r == null) return 0;
            if (r.Coins > 0) app.AddCoins(r.Coins, week ? "league_week" : "league_day");
            s.pendingPacks += r.Packs;
            if (!string.IsNullOrEmpty(r.Frame)) app.GrantCosmetic(r.Frame, true);
            app.MarkDirty();
            Platform.Metrica("league_reward", new Dictionary<string, object> { { "period", week ? "week" : "day" }, { "places", r.Places }, { "silent", silent } });
            return r.Coins;
        }

        // ------------------------------------------------------------------ аватарка, рамка, ник в таблице

        /// <summary>extraData записи: «a:bear|f:crown|n:Ник». Ник — свой из профиля, если задан.</summary>
        public static string Extra()
        {
            var s = S;
            string av = string.IsNullOrEmpty(s.wearAvatar) ? "raccoon" : s.wearAvatar.Replace("av_", "");
            string fr = string.IsNullOrEmpty(s.wearFrame) ? "" : s.wearFrame.Replace("frame_", "");
            string nick = (s.playerName ?? "").Replace("|", " ").Trim();
            if (nick.Length > 18) nick = nick.Substring(0, 18);
            return $"a:{av}|f:{fr}|n:{nick}";
        }

        static readonly string[] AvatarIds =
            { "raccoon", "cat", "corgi", "bear", "bunny", "penguin", "fox", "panda", "hedgehog", "owl", "frog", "pig" };

        /// <summary>Разбор extraData; у игрока без неё зверёк выбирается по id (всегда один и тот же).</summary>
        public static void Parse(string extra, string uniqueId, out string avatarSprite, out string frameSprite, out string nick)
        {
            string a = "", f = "";
            nick = "";
            if (!string.IsNullOrEmpty(extra))
                foreach (var part in extra.Split('|'))
                {
                    if (part.StartsWith("a:")) a = part.Substring(2);
                    else if (part.StartsWith("f:")) f = part.Substring(2);
                    else if (part.StartsWith("n:")) nick = part.Substring(2).Trim();
                }
            if (Array.IndexOf(AvatarIds, a) < 0)
            {
                int h = 0;
                foreach (char c in uniqueId ?? "") h = h * 31 + c;
                a = AvatarIds[(h & 0x7fffffff) % AvatarIds.Length];
            }
            avatarSprite = AvatarSprite("av_" + a);
            frameSprite = string.IsNullOrEmpty(f) || ArtLibrary.S("frame_" + f) == null ? "frame_basic" : "frame_" + f;
        }

        /// <summary>Картинка аватарки по id косметики (енот — его надетый скин).</summary>
        public static string AvatarSprite(string id)
        {
            var c = MetaCatalog.Cosmetic(id);
            return c != null ? c.Sprite : "map_avatar";
        }

        public static string MyAvatarSprite => AvatarSprite(string.IsNullOrEmpty(S.wearAvatar) ? "av_raccoon" : S.wearAvatar);
        public static string MyFrameSprite => string.IsNullOrEmpty(S.wearFrame) ? "frame_basic" : S.wearFrame;

        /// <summary>Имя в таблице: свой ник из extraData → имя Яндекса → «Гость».</summary>
        public static string NameOf(string yandexName, string nick)
        {
            if (!string.IsNullOrWhiteSpace(nick)) return nick;
            if (string.IsNullOrWhiteSpace(yandexName) || yandexName == "anonymous" || yandexName == "no data") return "Гость";
            return yandexName.Trim();
        }
    }
}
// сборка 01.10.2026: магазин и рейтинг по концепту
// сборка 02.10.2026: лишний товар, довоз, «Час пик», скины ленты

// build 03.10.2026

// build 03.10.2026 b: окно смены, обучение дома в фокусе, сохранения

// build 03.10.2026 c: копилка, покупатели очередью

// build 03.10.2026 vfx
// build 03.10.2026 vfx 2

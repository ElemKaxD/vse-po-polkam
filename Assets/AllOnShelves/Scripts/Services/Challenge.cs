using System;
using AllOnShelves.Core;

namespace AllOnShelves
{
    /// <summary>
    /// Испытание дня (v4, 03.10.2026, вместо «Уровня дня»): уровень дня (GameApp.BuildDailyLevel) с особым правилом —
    /// без отмен, без подсказок, ограничение ходов или тележка на место меньше. Правило меняется каждый день.
    /// Победа — сундук (80 монет, 💎 2, пачка); 7 дней подряд — большой сундук (400 монет, 💎 15).
    /// Числа — Tools/economy_v4.json, раздел challenge.
    /// </summary>
    public static class Challenge
    {
        public const int Coins = 80, Gems = 2, WeekCoins = 400, WeekGems = 15, RunDays = 7;
        public const int MovesSlack = 8;   // «ограничение ходов»: ходов решения + запас
        public const float SecondsPerMove = 3f, TimeSlack = 40f;   // «на время»: 3 с на ход решения + 40 с запаса

        // v4.1 (04.10.2026): + «На время» и «Только на 3 звезды» — под значки icon_rule_timer / icon_rule_three_stars
        public enum Rule { NoUndo, NoHint, Moves, SmallCart, Timer, ThreeStars }

        public static readonly string[] Icons = { "icon_rule_no_undo", "icon_rule_no_hint", "icon_rule_moves", "icon_rule_full_cart",
                                                  "icon_rule_timer", "icon_rule_three_stars" };
        public static readonly string[] Titles = { "Без отмен", "Без подсказок", "Ходов в обрез", "Тележка меньше", "На время", "Только на 3 звезды" };
        public static readonly string[] Texts =
        {
            "Отменять ходы нельзя — думай перед каждым.",
            "Подсказок не будет — только своя голова.",
            "Ходов ровно столько, сколько нужно, и чуть-чуть в запас.",
            "В тележке на одно место меньше.",
            "Успей собрать заказ, пока не кончилось время.",
            "Тележка не должна заполняться больше, чем на три звезды.",
        };

        /// <summary>Цифра в пустом кольце значка (у icon_rule_three_stars середина пустая — туда «3»), иначе null.</summary>
        public static string IconNumber(Rule r) => r == Rule.ThreeStars ? "3" : null;

        /// <summary>Сколько мест тележки можно занять по правилу дня (решение генератора ищется с этим пределом).</summary>
        public static int PeakLimit(Rule r, int cart) => r == Rule.SmallCart ? cart - 1 : r == Rule.ThreeStars ? cart - 2 : cart;

        static SaveData S => GameApp.I.Save;

        public static bool Unlocked => GameApp.I != null && GameApp.I.DailyUnlocked;

        /// <summary>Правило сегодняшнего дня (одно для всех).</summary>
        public static Rule Today => (Rule)(League.DayNumber % Icons.Length);

        public static bool WonToday => S.dailyLevelDate == GameApp.Today && S.dailyLevelWon;

        /// <summary>Сколько дней подряд пройдено (с учётом, что вчерашний пропуск обнуляет).</summary>
        public static int Run
        {
            get
            {
                var s = S;
                if (string.IsNullOrEmpty(s.challengeLast)) return 0;
                string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                return s.challengeLast == GameApp.Today || s.challengeLast == yesterday ? s.challengeRun : 0;
            }
        }

        /// <summary>Победа в Испытании: сундук, счёт дней подряд, на 7-м — большой сундук. Возвращает монеты для окна победы.</summary>
        public static int Win(out bool bigChest)
        {
            var s = S;
            bigChest = false;
            if (WonToday) return 0;
            int run = Run + 1;
            s.challengeRun = run;
            s.challengeLast = GameApp.Today;
            s.dailyLevelWon = true;
            s.dailyLevelDate = GameApp.Today;
            s.pendingPacks++;
            int coins = Coins;
            GameApp.I.AddGems(Gems, "challenge");
            if (run % RunDays == 0)
            {
                bigChest = true;
                coins += WeekCoins;
                GameApp.I.AddGems(WeekGems, "challenge_week");
            }
            Quests.Add("challenge");
            GameApp.I.MarkDirty();
            return coins;
        }
    }
}

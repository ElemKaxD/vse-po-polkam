using System;

namespace AllOnShelves
{
    /// <summary>
    /// Копилка енота (ГДД v3, п. 2.5; 03.10.2026). С каждой победы сверху дохода в копилку падает +20 монет и 5 %
    /// от выигрыша. Копит до 4000; полная — больше не растёт. Разбить — покупка «piggy» (99 ₽): все монеты копилки
    /// идут в кошелёк, копилка снова пустая. Разбить можно, когда накопилось хотя бы 1500.
    /// Живёт в Торговом доме (на площади и в Кассе), видна строкой в окне победы. Сама всплывает один раз —
    /// когда впервые наполнилась.
    /// </summary>
    public static class Piggy
    {
        public const string ProductId = "piggy";
        public const int Cap = 4000, MinBreak = 1500, PerWin = 20, Percent = 5;

        static SaveData S => GameApp.I.Save;

        /// <summary>Копилка открывается вместе с Торговым домом.</summary>
        public static bool Unlocked => House.Unlocked;
        public static int Coins => GameApp.I == null ? 0 : S.piggyCoins;
        public static bool Full => Coins >= Cap;
        public static bool CanBreak => Coins >= MinBreak;
        public static float Fill => Math.Min(1f, Coins / (float)Cap);

        /// <summary>Картинка по наполнению: пустая — до половины, дальше — полная.</summary>
        public static string Sprite => Coins >= Cap / 2 ? "piggy_full" : "piggy_empty";

        /// <summary>Победа: сколько монет упало в копилку (0 — копилка закрыта или полна).</summary>
        public static int AddFromWin(int winCoins)
        {
            if (!Unlocked || Full) return 0;
            int add = PerWin + Math.Max(0, winCoins) * Percent / 100;
            add = Math.Min(add, Cap - S.piggyCoins);
            S.piggyCoins += add;
            return add;
        }

        /// <summary>Покупка прошла: монеты копилки — в кошелёк. Возвращает, сколько.</summary>
        public static int Break()
        {
            int c = S.piggyCoins;
            S.piggyCoins = 0;
            S.piggyBroken++;
            S.coins += c;
            return c;
        }

        /// <summary>Пора один раз показать «Копилка полна!».</summary>
        public static bool ShouldAnnounceFull => Unlocked && Full && !S.piggyFullShown;

        public static void MarkAnnounced()
        {
            S.piggyFullShown = true;
            GameApp.I.MarkDirty();
        }
    }
}

using AllOnShelves.Core;

namespace AllOnShelves
{
    /// <summary>
    /// Серия побед «Лучший продавец» (экономика v4, 03.10.2026). Победы в карьере подряд дают бонусы в начале
    /// уровня: 1 — бесплатная подсказка, 2 — +1 место в тележке, 3 — +1 место на смене; на 5 и 10 победах — сундук.
    /// После 10 счёт возвращается к 5 (бонусы остаются), следующий сундук — снова на 10.
    /// Серия сгорает, когда игрок сдаётся («Заново» / «На карту» после поражения или из паузы с ходами);
    /// продолжение уровня (отмотать, грузчик) серию сохраняет — это и есть повод продолжить.
    /// </summary>
    public static class Streak
    {
        public static readonly int[] Steps = { 1, 2, 3, 5, 10 };
        public static readonly string[] Icons = { "icon_hint", "icon_bonus_cart_slot", "icon_bonus_staff_slot", "icon_coin_pile", "chest_week_3" };
        public static readonly string[] Names =
            { "Подсказка в начале уровня", "+1 место в тележке", "+1 место на смене", "Сундук: 60 монет и 1 алмаз", "Большой сундук: 150 монет и 3 алмаза" };
        public const int ChestCoins5 = 60, ChestGems5 = 1, ChestCoins10 = 150, ChestGems10 = 3;
        public const int ShowAt = 3;   // плашка на карте — после 3-го уровня

        static SaveData S => GameApp.I.Save;

        public static int Wins => GameApp.I != null ? S.streak : 0;
        public static bool FreeHint => Wins >= Steps[0];
        public static bool CartSlot => Wins >= Steps[1];
        public static bool StaffSlot => Wins >= Steps[2];

        /// <summary>Сколько ступеней горит (0…5).</summary>
        public static int Lit { get { int n = 0; foreach (var s in Steps) if (Wins >= s) n++; return n; } }

        /// <summary>Победа в карьере: +1 к серии; сундук — сразу в кошелёк. Возвращает номер ступени, которую зажгли (или −1).</summary>
        public static int Win()
        {
            var s = S;
            s.streak++;
            s.streakBest = System.Math.Max(s.streakBest, s.streak);
            int step = System.Array.IndexOf(Steps, s.streak);
            if (s.streak == 5) { GameApp.I.AddCoins(ChestCoins5, "streak"); GameApp.I.AddGems(ChestGems5, "streak"); }
            if (s.streak == 10)
            {
                GameApp.I.AddCoins(ChestCoins10, "streak");
                GameApp.I.AddGems(ChestGems10, "streak");
                s.streak = 5;   // бонусы остаются, следующий большой сундук — через 5 побед
            }
            GameApp.I.MarkDirty();
            return step;
        }

        /// <summary>Игрок сдался — серия сгорела.</summary>
        /// <summary>«Сохранить серию» (экономика v4): за алмазы — всегда, за видео — раз за серию.</summary>
        public const int KeepAt = 2;   // кнопку показываем, когда есть что терять: от 2 побед
        public static bool CanKeepByVideo => !S.streakVideoUsed;

        public static void KeptByVideo() { S.streakVideoUsed = true; GameApp.I.MarkDirty(); }

        public static void Lose()
        {
            if (S.streak == 0) return;
            S.streak = 0;
            S.streakVideoUsed = false;   // новая серия — снова можно спасти за видео
            GameApp.I.MarkDirty();
        }

        /// <summary>Что даёт серия в этом уровне — одной строкой для сообщения в начале.</summary>
        public static string BonusLine()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (FreeHint) parts.Add("подсказка");
            if (CartSlot) parts.Add("+1 место в тележке");
            if (StaffSlot && House.Unlocked) parts.Add("+1 место на смене");
            return parts.Count == 0 ? "" : $"Серия ×{Wins}: " + string.Join(", ", parts);
        }
    }
}

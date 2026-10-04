using System;
using System.Collections.Generic;

namespace AllOnShelves.Core
{
    /// <summary>«Час пик» (ГДД 8.6): лента едет сама, несобранный товар падает в тележку, при полной — пробка.</summary>
    public static class RushRules
    {
        public const int Sections = 5;
        // темп (02.10.2026, «слишком быстро тикает»): старт 4 с на товар, каждый набор ускоряет ленту на 2,5 %
        // (раньше −0,05 с за набор от 3 с — к 40-му набору уже 1,1 с). 10 наборов — 3,1 с, 20 — 2,4 с,
        // 30 — 1,9 с, дальше не быстрее 1,5 с
        public const float StartInterval = 4.0f;
        public const float MinInterval = 1.5f;
        public const float SpeedUp = 0.975f;

        public static LevelState Create(int seed)
        {
            var d = new LevelData
            {
                id = 0, district = 1, cart = 5, window = 3, visible = 8,
                sections = new SectionData[Sections],
                goals = new GoalData[0],
                belts = new[] { new BeltData() },
            };
            for (int i = 0; i < Sections; i++) d.sections[i] = new SectionData();
            var s = LevelState.FromData(d);
            s.Endless = true;
            Refill(s, new Random(seed), null);
            return s;
        }

        public static int ActiveTypes(LevelState s) => Math.Min(8, 4 + s.SetsSold / 12);

        public static float Interval(LevelState s) => Math.Max(MinInterval, StartInterval * (float)Math.Pow(SpeedUp, s.SetsSold));

        static readonly int[] RushPool = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 10 };

        public static void Refill(LevelState s, Random rng, List<GameEvent> ev)
        {
            var belt = s.Belts[0];
            int types = ActiveTypes(s);
            while (belt.Count < s.Visible + 3)
            {
                int type = RushPool[rng.Next(types)];
                belt.Add(new Token { Kind = TokenKind.Item, Type = type, Type2 = -1, Uid = s.NextUid++, Revealed = true });
            }
        }

        /// <summary>Автосдвиг: крайний товар падает в тележку; если места нет — пробка +1.</summary>
        public static void Tick(LevelState s, Random rng, List<GameEvent> ev)
        {
            if (s.Result != GameResult.Playing) return;
            var belt = s.Belts[0];
            if (belt.Count > 0)
            {
                var t = belt[0];
                belt.RemoveAt(0);
                if (Rules.CartFits(s, t.Type)) Rules.AddToCart(s, t.Uid, t.Type, ev);
                else
                {
                    s.Jam++;
                    ev?.Add(new GameEvent { Kind = EventKind.CartCleared, Uids = new[] { t.Uid } });
                }
                ev?.Add(new GameEvent { Kind = EventKind.BeltAdvanced, A = 0 });
            }
            Refill(s, rng, ev);
            if (s.CartUsed > s.PeakCart) s.PeakCart = s.CartUsed;
            Rules.Evaluate(s, ev);
        }

        /// <summary>Каждые 3 набора подряд без падений снимают деление пробки.</summary>
        public static void RelieveJam(LevelState s)
        {
            if (s.Jam > 0) s.Jam--;
        }
    }
}

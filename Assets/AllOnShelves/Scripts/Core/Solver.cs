using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AllOnShelves.Core
{
    public struct SolveResult
    {
        public bool Solved;
        public bool LimitHit;
        public int MinPeak;
        public List<Move> Moves;
        public int Nodes;
    }

    /// <summary>
    /// Солвер (ГДД 6.6): DFS с таблицей посещённых состояний. Ищет решение с минимальным пиком тележки.
    /// Используется генератором уровней и подсказкой в игре (с ограничением по времени).
    /// </summary>
    public sealed class Solver
    {
        public int NodeLimit = 300000;
        public long TimeLimitMs = 0;

        int _nodes;
        bool _limitHit;
        HashSet<ulong> _visited;
        Stopwatch _sw;
        readonly List<Move> _tmp = new List<Move>(64);

        public static bool HasTimedMechanics(LevelState s)
        {
            if (s.PerishTimer > 0 || s.DoorOpen > 0 || s.CustomerPlans.Length > 0) return true;
            foreach (var x in s.Sections) if (x.Kind == SectionKind.Freezer) return true;
            return false;
        }

        /// <summary>Решение с минимальным пиком тележки.</summary>
        public SolveResult SolveMinPeak(LevelState start)
        {
            int total = 0;
            for (int limit = Math.Max(start.CartUsed, 0); limit <= start.CartCapacity; limit++)
            {
                var r = Solve(start, limit);
                total += r.Nodes;
                if (r.Solved) { r.MinPeak = limit; r.Nodes = total; return r; }
                if (r.LimitHit && limit == start.CartCapacity) { r.Nodes = total; return r; }
            }
            return new SolveResult { Solved = false, MinPeak = -1, Nodes = total };
        }

        /// <summary>Есть ли решение, при котором в тележке не больше peakLimit мест.</summary>
        public SolveResult Solve(LevelState start, int peakLimit)
        {
            _nodes = 0;
            _limitHit = false;
            _visited = new HashSet<ulong>();
            _sw = Stopwatch.StartNew();
            var s = start.Clone();
            s.CartCapacity = Math.Min(s.CartCapacity, peakLimit);
            var path = new List<Move>();
            bool timed = HasTimedMechanics(s);
            bool ok = ApplyForced(s, path, timed) && Dfs(s, path, timed);
            if (s.Result == GameResult.Win) ok = true;
            return new SolveResult { Solved = ok, LimitHit = _limitHit, Moves = ok ? path : null, Nodes = _nodes, MinPeak = ok ? peakLimit : -1 };
        }

        bool Dfs(LevelState s, List<Move> path, bool timed)
        {
            if (s.Result == GameResult.Win) return true;
            if (s.Result == GameResult.Lose) return false;
            if (++_nodes > NodeLimit || (TimeLimitMs > 0 && _sw.ElapsedMilliseconds > TimeLimitMs)) { _limitHit = true; return false; }
            if (!_visited.Add(s.Hash())) return false;

            var moves = Ordered(s);
            foreach (var m in moves)
            {
                var c = s.Clone();
                if (!Rules.Apply(c, m, null)) continue;
                int mark = path.Count;
                path.Add(m);
                if (ApplyForced(c, path, timed) && Dfs(c, path, timed)) return true;
                path.RemoveRange(mark, path.Count - mark);
                if (_limitHit) return false;
            }
            return false;
        }

        /// <summary>Доминирующие ходы: товар из тележки в свою секцию; без таймеров — ещё и с ленты в свою секцию.</summary>
        static bool ApplyForced(LevelState s, List<Move> path, bool timed)
        {
            bool changed = true;
            int guard = 0;
            while (changed && s.Result == GameResult.Playing && guard++ < 200)
            {
                changed = false;
                for (int i = 0; i < s.Cart.Count; i++)
                {
                    var c = s.Cart[i];
                    if (c.Spoiled) continue;
                    int own = s.SectionOfType(c.Type);
                    if (own < 0) continue;
                    var m = Move.FromCart(i, TargetKind.Section, own);
                    if (Rules.Apply(s, m, null)) { path.Add(m); changed = true; break; }
                }
                if (changed || timed) continue;
                for (int b = 0; b < s.Belts.Length && !changed; b++)
                {
                    int n = Rules.SourceCount(s, SourceKind.Belt, b);
                    for (int i = 0; i < n; i++)
                    {
                        var t = s.Belts[b][i];
                        if (t.Kind != TokenKind.Item) continue;
                        int own = s.SectionOfType(t.Type);
                        if (own < 0) continue;
                        var m = Move.FromBelt(b, i, TargetKind.Section, own);
                        if (Rules.Apply(s, m, null)) { path.Add(m); changed = true; break; }
                    }
                }
            }
            return s.Result != GameResult.Lose;
        }

        List<Move> Ordered(LevelState s)
        {
            var list = new List<Move>(Rules.LegalMoves(s, reduce: true, into: _tmp));
            list.Sort((a, b) => Priority(s, a).CompareTo(Priority(s, b)));
            return list;
        }

        public static int Priority(LevelState s, Move m)
        {
            Rules.TryGetSource(s, m, out var tok);
            if (m.Dst == TargetKind.Section && s.Sections[m.DstIndex].Type == tok.Type) return m.Src == SourceKind.Cart ? 0 : 1;
            if (m.Dst == TargetKind.Section) return m.Src == SourceKind.Cart ? 2 : 3;
            return 4;
        }

        // ------------------------------------------------------------------ боты

        /// <summary>Жадный бот: всегда самый «очевидный» ход.</summary>
        public static bool GreedyWins(LevelState start, int maxMoves = 500)
        {
            var s = start.Clone();
            var tmp = new List<Move>();
            for (int k = 0; k < maxMoves && s.Result == GameResult.Playing; k++)
            {
                var moves = Rules.LegalMoves(s, reduce: true, into: tmp);
                if (moves.Count == 0) break;
                Move best = moves[0];
                int bp = int.MaxValue;
                foreach (var m in moves)
                {
                    int p = Priority(s, m) * 100 + (m.Src == SourceKind.Belt ? m.SrcIndex : 0);
                    if (p < bp) { bp = p; best = m; }
                }
                Rules.Apply(s, best, null);
            }
            return s.Result == GameResult.Win;
        }

        /// <summary>Доля побед случайного бота (с доминирующими ходами).</summary>
        public static float RandomWinRate(LevelState start, int runs, int seed)
        {
            var rng = new Random(seed);
            var tmp = new List<Move>();
            var path = new List<Move>();
            bool timed = HasTimedMechanics(start);
            int wins = 0;
            for (int r = 0; r < runs; r++)
            {
                var s = start.Clone();
                for (int k = 0; k < 500 && s.Result == GameResult.Playing; k++)
                {
                    path.Clear();
                    ApplyForced(s, path, timed);
                    if (s.Result != GameResult.Playing) break;
                    var moves = Rules.LegalMoves(s, reduce: true, into: tmp);
                    if (moves.Count == 0) break;
                    Rules.Apply(s, moves[rng.Next(moves.Count)], null);
                }
                if (s.Result == GameResult.Win) wins++;
            }
            return wins / (float)runs;
        }

        // ------------------------------------------------------------------ подсказка в игре

        /// <summary>Лучший следующий ход за ограниченное время. null — спасения нет (предложить отмену).</summary>
        public static Move? Hint(LevelState s, long timeMs = 150, List<Move> line = null)
        {
            var solver = new Solver { NodeLimit = 60000, TimeLimitMs = timeMs };
            var r = solver.Solve(s, s.CartCapacity);
            if (r.Solved && r.Moves != null && r.Moves.Count > 0)
            {
                line?.AddRange(r.Moves);   // весь найденный путь — чтобы следующие подсказки не искали заново
                return r.Moves[0];
            }
            if (r.LimitHit)
            {
                // не успели — показываем эвристически лучший ход
                var moves = Rules.LegalMoves(s, reduce: true);
                if (moves.Count == 0) return null;
                moves.Sort((a, b) => Priority(s, a).CompareTo(Priority(s, b)));
                return moves[0];
            }
            return null;
        }
    }
}

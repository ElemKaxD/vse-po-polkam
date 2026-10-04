using System.Collections.Generic;

namespace AllOnShelves.Core
{
    public enum SourceKind : byte { Belt, Cart }
    public enum TargetKind : byte { Section, Cart, Auto }

    public struct Move
    {
        public SourceKind Src;
        public int SrcBelt;
        public int SrcIndex;
        public TargetKind Dst;
        public int DstIndex;

        public static Move FromBelt(int belt, int index, TargetKind dst, int dstIndex = -1) =>
            new Move { Src = SourceKind.Belt, SrcBelt = belt, SrcIndex = index, Dst = dst, DstIndex = dstIndex };

        public static Move FromCart(int index, TargetKind dst, int dstIndex = -1) =>
            new Move { Src = SourceKind.Cart, SrcIndex = index, Dst = dst, DstIndex = dstIndex };

        public MoveData ToData() => new MoveData { sk = (int)Src, sb = SrcBelt, si = SrcIndex, dk = (int)Dst, di = DstIndex };
        public static Move FromData(MoveData m) => new Move
            { Src = (SourceKind)m.sk, SrcBelt = m.sb, SrcIndex = m.si, Dst = (TargetKind)m.dk, DstIndex = m.di };

        public override string ToString() =>
            $"{Src}{(Src == SourceKind.Belt ? SrcBelt.ToString() : "")}[{SrcIndex}]->{Dst}{(Dst == TargetKind.Section ? DstIndex.ToString() : "")}";
    }

    public enum EventKind
    {
        ItemToSection,   // A=uid, B=section, C=type
        ItemToCart,      // A=uid, C=type
        BeltAdvanced,    // A=belt
        BoxRevealed,     // A=uid, C=type
        PalletUnpacked,  // A=pallet uid (новые uid — ItemToSection/ItemToCart следом)
        SetSold,         // B=section, C=type, D=coins; Uids — проданные товары
        SectionUnlocked, // B=section
        LockProgress,    // B=section, D=left
        Spoiled,         // A=uid
        SpoiledRemoved,  // A=uid
        DoorChanged,     // D=1 open / 0 closed
        CustomerArrived, // C=type, D=patience
        CustomerServed,  // A=uid, C=type, D=tips
        CustomerLeft,
        CustomerPatience,// D=left
        Win,
        Lose,            // D=(int)LoseReason
        CartCleared,     // Uids — унесены грузчиком
    }

    public struct GameEvent
    {
        public EventKind Kind;
        public int A, B, C, D;
        public int[] Uids;
        public override string ToString() => $"{Kind}({A},{B},{C},{D})";
    }

    /// <summary>Правила хода (ГДД 2.2, 2.7) и механики (ГДД 4).</summary>
    public static class Rules
    {
        public const int TipCoins = 3;
        public const int FrozenCartTimer = 3;
        public const int SpoilLinger = 2;

        // ------------------------------------------------------------------ проверки

        public static bool CanPlaceInSection(LevelState s, int type, int sec)
        {
            if (sec < 0 || sec >= s.Sections.Length) return false;
            var x = s.Sections[sec];
            if (x.Locked) return false;
            // лишний товар (его нет в заказе) на полку не встаёт — только в тележку, его заберёт покупатель
            // (02.10.2026: иначе он навсегда занимал секцию и оставался на стеллаже после победы)
            if (!s.Endless && s.GoalTypes.Length > 0 && s.GoalIndex(type) < 0) return false;
            bool frozen = ItemCatalog.IsFrozen(type);
            if (x.Kind == SectionKind.Freezer)
            {
                if (!frozen || !s.DoorIsOpen) return false;
            }
            else if (frozen) return false;

            if (x.Type == type) return x.Uids.Count < x.Capacity;
            if (x.Type >= 0) return false;
            // пустая секция: правило одной секции на тип
            return s.SectionOfType(type) < 0;
        }

        static bool CanPlacePallet(LevelState s, int type, int sec) => CanPlaceInSection(s, type, sec);

        public static bool CartFits(LevelState s, int type, int count = 1) =>
            s.CartFree >= ItemCatalog.SlotSize(type) * count;

        public static int SourceCount(LevelState s, SourceKind kind, int belt)
        {
            if (kind == SourceKind.Cart) return s.Cart.Count;
            if (belt < 0 || belt >= s.Belts.Length) return 0;
            return System.Math.Min(s.Window, s.Belts[belt].Count);
        }

        /// <summary>Тип товара у источника (для связки — первый).</summary>
        public static bool TryGetSource(LevelState s, Move m, out Token tok)
        {
            tok = default;
            if (m.Src == SourceKind.Cart)
            {
                if (m.SrcIndex < 0 || m.SrcIndex >= s.Cart.Count) return false;
                var c = s.Cart[m.SrcIndex];
                if (c.Spoiled) return false;
                tok = new Token { Kind = TokenKind.Item, Type = c.Type, Uid = c.Uid, Type2 = -1, Revealed = true };
                return true;
            }
            if (m.SrcBelt < 0 || m.SrcBelt >= s.Belts.Length) return false;
            var belt = s.Belts[m.SrcBelt];
            if (m.SrcIndex < 0 || m.SrcIndex >= System.Math.Min(s.Window, belt.Count)) return false;
            tok = belt[m.SrcIndex];
            return true;
        }

        /// <summary>Автоцель быстрого хода (ГДД 2.3): своя секция → пустая → тележка.</summary>
        public static Move ResolveAuto(LevelState s, Move m)
        {
            if (m.Dst != TargetKind.Auto) return m;
            if (!TryGetSource(s, m, out var tok)) return m;
            int type = tok.Type;
            // покупатель ждёт этот товар (и он не нужен для заказа) — нажатие отдаёт товар ему через тележку,
            // а не кладёт на полку (01.10.2026: игрок жал на нужный товар, а тот уезжал на стеллаж)
            if (m.Src == SourceKind.Belt && tok.Kind == TokenKind.Item && s.Customer.Active && s.Customer.Type == type
                && s.GoalIndex(type) < 0 && CartFits(s, type))
            {
                m.Dst = TargetKind.Cart; m.DstIndex = -1;
                return m;
            }
            int own = s.SectionOfType(type);
            if (own >= 0 && CanPlaceInSection(s, type, own)) { m.Dst = TargetKind.Section; m.DstIndex = own; if (IsLegal(s, m)) return m; }
            for (int i = 0; i < s.Sections.Length; i++)
            {
                if (s.Sections[i].IsEmpty && CanPlaceInSection(s, type, i))
                {
                    m.Dst = TargetKind.Section; m.DstIndex = i;
                    if (IsLegal(s, m)) return m;
                }
            }
            if (m.Src == SourceKind.Belt)
            {
                m.Dst = TargetKind.Cart; m.DstIndex = -1;
                if (IsLegal(s, m)) return m;
            }
            m.Dst = TargetKind.Auto;
            return m;
        }

        public static bool IsLegal(LevelState s, Move m)
        {
            if (s.Result != GameResult.Playing) return false;
            if (m.Dst == TargetKind.Auto) m = ResolveAuto(s, m);
            if (m.Dst == TargetKind.Auto) return false;
            if (!TryGetSource(s, m, out var tok)) return false;
            if (m.Src == SourceKind.Cart && m.Dst == TargetKind.Cart) return false;

            switch (tok.Kind)
            {
                case TokenKind.Pallet:
                    if (m.Dst == TargetKind.Cart) return CartFits(s, tok.Type, 3);
                    return CanPlacePallet(s, tok.Type, m.DstIndex);
                case TokenKind.Bundle:
                {
                    // первый — в выбранную цель, второй — автоцелью после этого
                    var probe = s.Clone();
                    probe.Result = GameResult.Playing;
                    return PlaceBundle(probe, tok, m, null, simulateOnly: true);
                }
                default:
                    if (m.Dst == TargetKind.Cart) return CartFits(s, tok.Type);
                    return CanPlaceInSection(s, tok.Type, m.DstIndex);
            }
        }

        /// <summary>Все допустимые ходы. reduce=true — без эквивалентных (для солвера).</summary>
        public static List<Move> LegalMoves(LevelState s, bool reduce = false, List<Move> into = null)
        {
            var list = into ?? new List<Move>();
            list.Clear();
            if (s.Result != GameResult.Playing) return list;

            for (int b = 0; b < s.Belts.Length; b++)
            {
                int n = SourceCount(s, SourceKind.Belt, b);
                for (int i = 0; i < n; i++) AddMovesFor(s, Move.FromBelt(b, i, TargetKind.Auto), reduce, list);
            }
            for (int i = 0; i < s.Cart.Count; i++)
            {
                if (s.Cart[i].Spoiled) continue;
                if (reduce)
                {
                    bool dup = false;
                    for (int j = 0; j < i; j++)
                        if (!s.Cart[j].Spoiled && s.Cart[j].Type == s.Cart[i].Type && s.Cart[j].Timer == s.Cart[i].Timer) { dup = true; break; }
                    if (dup) continue;
                }
                AddMovesFor(s, Move.FromCart(i, TargetKind.Auto), reduce, list);
            }
            return list;
        }

        static void AddMovesFor(LevelState s, Move baseMove, bool reduce, List<Move> list)
        {
            bool normalEmptyAdded = false, saleEmptyAdded = false, freezerEmptyAdded = false;
            for (int j = 0; j < s.Sections.Length; j++)
            {
                var m = baseMove; m.Dst = TargetKind.Section; m.DstIndex = j;
                if (!IsLegal(s, m)) continue;
                if (reduce && s.Sections[j].IsEmpty)
                {
                    var k = s.Sections[j].Kind;
                    if (k == SectionKind.Normal) { if (normalEmptyAdded) continue; normalEmptyAdded = true; }
                    else if (k == SectionKind.Sale) { if (saleEmptyAdded) continue; saleEmptyAdded = true; }
                    else { if (freezerEmptyAdded) continue; freezerEmptyAdded = true; }
                }
                list.Add(m);
            }
            if (baseMove.Src == SourceKind.Belt)
            {
                var m = baseMove; m.Dst = TargetKind.Cart; m.DstIndex = -1;
                if (IsLegal(s, m)) list.Add(m);
            }
        }

        public static bool HasAnyLegalMove(LevelState s)
        {
            for (int b = 0; b < s.Belts.Length; b++)
            {
                int n = SourceCount(s, SourceKind.Belt, b);
                for (int i = 0; i < n; i++) if (ResolveAuto(s, Move.FromBelt(b, i, TargetKind.Auto)).Dst != TargetKind.Auto) return true;
            }
            for (int i = 0; i < s.Cart.Count; i++)
                if (!s.Cart[i].Spoiled && ResolveAuto(s, Move.FromCart(i, TargetKind.Auto)).Dst != TargetKind.Auto) return true;
            return false;
        }

        // ------------------------------------------------------------------ применение хода

        /// <summary>Применяет ход. Возвращает false, если ход недопустим (состояние не меняется).</summary>
        public static bool Apply(LevelState s, Move m, List<GameEvent> ev)
        {
            m = ResolveAuto(s, m);
            if (!IsLegal(s, m)) return false;
            TryGetSource(s, m, out var tok);

            bool fromBelt = m.Src == SourceKind.Belt;
            if (fromBelt)
            {
                s.Belts[m.SrcBelt].RemoveAt(m.SrcIndex);
            }
            else
            {
                s.Cart.RemoveAt(m.SrcIndex);
            }

            switch (tok.Kind)
            {
                case TokenKind.Pallet:
                    ev?.Add(new GameEvent { Kind = EventKind.PalletUnpacked, A = tok.Uid, C = tok.Type });
                    for (int k = 0; k < 3; k++)
                    {
                        int uid = s.NextUid++;
                        if (m.Dst == TargetKind.Cart) AddToCart(s, uid, tok.Type, ev);
                        else PlaceInSection(s, uid, tok.Type, m.DstIndex, ev);
                    }
                    break;
                case TokenKind.Bundle:
                    PlaceBundle(s, tok, m, ev, simulateOnly: false);
                    break;
                default:
                    if (m.Dst == TargetKind.Cart) AddToCart(s, tok.Uid, tok.Type, ev);
                    else PlaceInSection(s, tok.Uid, tok.Type, m.DstIndex, ev);
                    break;
            }

            s.Moves++;
            // сначала покупатель забирает свой товар, потом тикает терпение: товар, принесённый на последнем
            // сердечке, успевает к нему (раньше покупатель уходил грустным, а товар доставался следующему)
            ServeCustomer(s, ev);
            if (fromBelt)
            {
                ev?.Add(new GameEvent { Kind = EventKind.BeltAdvanced, A = m.SrcBelt });
                RevealWindow(s, ev);
                Tick(s, ev);
            }
            ServeCustomer(s, ev);   // только что пришедший покупатель может сразу найти свой товар в тележке
            if (s.CartUsed > s.PeakCart) s.PeakCart = s.CartUsed;
            Evaluate(s, ev);
            return true;
        }

        static bool PlaceBundle(LevelState s, Token tok, Move m, List<GameEvent> ev, bool simulateOnly)
        {
            // первый товар
            if (m.Dst == TargetKind.Cart)
            {
                if (!CartFits(s, tok.Type)) return false;
                if (simulateOnly) s.Cart.Add(new CartItem { Uid = tok.Uid, Type = tok.Type, Timer = -1 });
                else AddToCart(s, tok.Uid, tok.Type, ev);
            }
            else
            {
                if (!CanPlaceInSection(s, tok.Type, m.DstIndex)) return false;
                if (simulateOnly) SimPlace(s, tok.Type, m.DstIndex);
                else PlaceInSection(s, tok.Uid, tok.Type, m.DstIndex, ev);
            }
            // второй товар — автоцелью: своя секция → пустая → тележка
            int t2 = tok.Type2;
            int target = -1;
            int own = s.SectionOfType(t2);
            if (own >= 0 && CanPlaceInSection(s, t2, own)) target = own;
            else
                for (int i = 0; i < s.Sections.Length; i++)
                    if (s.Sections[i].IsEmpty && CanPlaceInSection(s, t2, i)) { target = i; break; }
            if (target >= 0)
            {
                if (simulateOnly) SimPlace(s, t2, target);
                else PlaceInSection(s, tok.Uid2, t2, target, ev);
                return true;
            }
            if (CartFits(s, t2))
            {
                if (!simulateOnly) AddToCart(s, tok.Uid2, t2, ev);
                return true;
            }
            return false;
        }

        static void SimPlace(LevelState s, int type, int sec)
        {
            var x = s.Sections[sec];
            x.Type = type;
            x.Uids.Add(-1);
            if (x.Uids.Count >= x.Capacity) { x.Uids.Clear(); x.Type = -1; }
        }

        public static void AddToCart(LevelState s, int uid, int type, List<GameEvent> ev)
        {
            int timer = -1;
            if (ItemCatalog.IsFrozen(type) && HasFreezer(s)) timer = s.FrozenTimer;
            else if (s.PerishTimer > 0 && ItemCatalog.IsPerishable(type)) timer = s.PerishTimer;
            s.Cart.Add(new CartItem { Uid = uid, Type = type, Timer = timer });
            ev?.Add(new GameEvent { Kind = EventKind.ItemToCart, A = uid, C = type });
        }

        static bool HasFreezer(LevelState s)
        {
            foreach (var x in s.Sections) if (x.Kind == SectionKind.Freezer) return true;
            return false;
        }

        static void PlaceInSection(LevelState s, int uid, int type, int sec, List<GameEvent> ev)
        {
            var x = s.Sections[sec];
            x.Type = type;
            x.Uids.Add(uid);
            ev?.Add(new GameEvent { Kind = EventKind.ItemToSection, A = uid, B = sec, C = type });
            if (x.Uids.Count >= x.Capacity) SellSection(s, sec, ev);
        }

        static void SellSection(LevelState s, int sec, List<GameEvent> ev)
        {
            var x = s.Sections[sec];
            int type = x.Type;
            int coins = x.Kind == SectionKind.Sale ? s.SaleCoins : 1;
            var sold = x.Uids.ToArray();
            x.Uids.Clear();
            x.Type = -1;
            s.SetsSold++;
            s.SetCoins += coins;
            int g = s.GoalIndex(type);
            if (g >= 0) s.GoalDone[g]++;
            ev?.Add(new GameEvent { Kind = EventKind.SetSold, B = sec, C = type, D = coins, Uids = sold });

            for (int i = 0; i < s.Sections.Length; i++)
            {
                var l = s.Sections[i];
                if (l.LockSets <= 0) continue;
                l.LockSets--;
                ev?.Add(l.LockSets == 0
                    ? new GameEvent { Kind = EventKind.SectionUnlocked, B = i }
                    : new GameEvent { Kind = EventKind.LockProgress, B = i, D = l.LockSets });
            }
        }

        public static void RevealWindow(LevelState s, List<GameEvent> ev)
        {
            foreach (var belt in s.Belts)
            {
                int n = System.Math.Min(s.Window, belt.Count);
                for (int i = 0; i < n; i++)
                {
                    var t = belt[i];
                    if (t.Kind == TokenKind.Box && !t.Revealed)
                    {
                        t.Revealed = true;
                        t.Kind = TokenKind.Item;
                        belt[i] = t;
                        ev?.Add(new GameEvent { Kind = EventKind.BoxRevealed, A = t.Uid, C = t.Type });
                    }
                }
            }
        }

        /// <summary>Тик механик — только после хода с ленты.</summary>
        static void Tick(LevelState s, List<GameEvent> ev)
        {
            bool doorBefore = s.DoorIsOpen;
            s.BeltMoves++;
            if (s.DoorOpen > 0 && s.DoorClosed > 0 && doorBefore != s.DoorIsOpen)
                ev?.Add(new GameEvent { Kind = EventKind.DoorChanged, D = s.DoorIsOpen ? 1 : 0 });

            // испорченные уходят, свежие портятся
            for (int i = s.Cart.Count - 1; i >= 0; i--)
            {
                var c = s.Cart[i];
                if (c.Spoiled)
                {
                    c.SpoilLeft--;
                    if (c.SpoilLeft <= 0)
                    {
                        s.Cart.RemoveAt(i);
                        ev?.Add(new GameEvent { Kind = EventKind.SpoiledRemoved, A = c.Uid });
                        continue;
                    }
                    s.Cart[i] = c;
                }
                else if (c.Timer > 0)
                {
                    c.Timer--;
                    if (c.Timer == 0)
                    {
                        c.Spoiled = true;
                        c.SpoilLeft = SpoilLinger;
                        s.SpoiledCount++;
                        ev?.Add(new GameEvent { Kind = EventKind.Spoiled, A = c.Uid, C = c.Type });
                        // поставщик довозит свежий товар в конец ленты (02.10.2026): раньше генератор клал
                        // запасной товар заранее, и после победы он оставался на полке по одной штуке
                        if (!s.Endless) Restock(s, c.Type);
                    }
                    s.Cart[i] = c;
                }
            }

            // покупатели
            if (s.Customer.Active)
            {
                s.Customer.Patience--;
                if (s.Customer.Patience <= 0)
                {
                    s.Customer.Active = false;
                    ev?.Add(new GameEvent { Kind = EventKind.CustomerLeft });
                }
                else ev?.Add(new GameEvent { Kind = EventKind.CustomerPatience, D = s.Customer.Patience });
            }
            if (!s.Customer.Active && s.NextCustomer < s.CustomerPlans.Length && s.CustomerPlans[s.NextCustomer].AtMove <= s.BeltMoves)
            {
                var p = s.CustomerPlans[s.NextCustomer++];
                s.Customer = new Customer { Active = true, Type = p.Type, Patience = p.Patience, MaxPatience = p.Patience, Look = p.Look };
                ev?.Add(new GameEvent { Kind = EventKind.CustomerArrived, C = p.Type, D = p.Patience, B = p.Look });
            }
        }

        /// <summary>
        /// Свежий товар взамен испорченного — в конец самой короткой ленты. Только если без него заказ
        /// не собрать: лишний довоз оставался на ленте после победы.
        /// </summary>
        public const int MaxRestocks = 12;  // без предела решатель уходил в цикл «испортился — довезли»

        static void Restock(LevelState s, int type)
        {
            int g = s.GoalIndex(type);
            if (g < 0 || s.Restocks >= MaxRestocks) return;
            int need = (s.GoalNeed[g] - s.GoalDone[g]) * ItemCatalog.SetSize(type), have = 0;
            foreach (var x in s.Sections) if (x.Type == type) have += x.Uids.Count;
            foreach (var c in s.Cart) if (!c.Spoiled && c.Type == type) have++;
            foreach (var b in s.Belts)
                foreach (var t in b)
                {
                    if (t.Type == type) have += t.Kind == TokenKind.Pallet ? 3 : 1;
                    if (t.Kind == TokenKind.Bundle && t.Type2 == type) have++;
                }
            if (have >= need) return;
            s.Restocks++;
            int best = 0;
            for (int b = 1; b < s.Belts.Length; b++) if (s.Belts[b].Count < s.Belts[best].Count) best = b;
            s.Belts[best].Add(new Token { Kind = TokenKind.Item, Type = type, Type2 = -1, Uid = s.NextUid++, Revealed = true });
        }

        static void ServeCustomer(LevelState s, List<GameEvent> ev)
        {
            if (!s.Customer.Active) return;
            for (int i = 0; i < s.Cart.Count; i++)
            {
                var c = s.Cart[i];
                if (c.Spoiled || c.Type != s.Customer.Type) continue;
                s.Cart.RemoveAt(i);
                s.Tips += TipCoins;
                s.CustomersServed++;
                s.Customer.Active = false;
                ev?.Add(new GameEvent { Kind = EventKind.CustomerServed, A = c.Uid, C = c.Type, D = TipCoins });
                return;
            }
        }

        public static void Evaluate(LevelState s, List<GameEvent> ev)
        {
            if (s.Result != GameResult.Playing) return;
            if (s.Endless)
            {
                if (s.Jam >= s.JamMax)
                {
                    s.Result = GameResult.Lose;
                    s.Reason = LoseReason.Jam;
                    ev?.Add(new GameEvent { Kind = EventKind.Lose, D = (int)s.Reason });
                }
                return;
            }
            if (s.AllGoalsDone)
            {
                s.Result = GameResult.Win;
                ev?.Add(new GameEvent { Kind = EventKind.Win });
                return;
            }
            if (!HasAnyLegalMove(s))
            {
                s.Result = GameResult.Lose;
                s.Reason = s.BeltsEmpty && s.SpoiledCount > 0 ? LoseReason.OutOfStock : LoseReason.Deadlock;
                ev?.Add(new GameEvent { Kind = EventKind.Lose, D = (int)s.Reason });
            }
        }

        // ------------------------------------------------------------------ продолжение и звёзды

        public const int LoaderSlots = 2;

        /// <summary>
        /// «Грузчик» (ГДД 2.6, вариант кадра 07): приносит ящики — +2 места в тележке до конца уровня.
        /// В отличие от выноса товаров никогда не делает заказ невыполнимым.
        /// </summary>
        public static void ApplyLoader(LevelState s, List<GameEvent> ev)
        {
            s.CartCapacity += LoaderSlots;
            s.ContinueUsed = true;
            s.Result = GameResult.Playing;
            s.Reason = LoseReason.None;
            Evaluate(s, ev);
        }

        /// <summary>Поможет ли «Грузчик»: после добавления мест должен появиться ход.</summary>
        public static bool LoaderCanHelp(LevelState s)
        {
            var probe = s.Clone();
            ApplyLoader(probe, null);
            return probe.Result == GameResult.Playing;
        }

        /// <summary>Звёзды (ГДД 2.4).</summary>
        public static int Stars(LevelState s, int baseCart)
        {
            if (s.Result != GameResult.Win) return 0;
            if (s.ContinueUsed) return 1;
            int stars;
            if (s.PeakCart <= baseCart - 2 && s.SpoiledCount == 0) stars = 3;
            else if (s.PeakCart <= baseCart - 1 || (s.PeakCart <= baseCart - 2 && s.SpoiledCount == 1)) stars = 2;
            else stars = 1;
            if (s.CartBonusUsed) stars = System.Math.Min(stars, 2);
            return stars;
        }
    }
}

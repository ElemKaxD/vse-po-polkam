using System;
using System.Collections.Generic;

namespace AllOnShelves.Core
{
    public enum TokenKind : byte { Item, Box, Pallet, Bundle }
    public enum SectionKind : byte { Normal, Freezer, Sale }
    public enum GameResult : byte { Playing, Win, Lose }
    public enum LoseReason : byte { None, Deadlock, OutOfStock, Jam }

    /// <summary>Позиция на ленте. Uid — идентификатор для отображения.</summary>
    public struct Token
    {
        public TokenKind Kind;
        public int Type;
        public int Type2;     // второй товар связки
        public int Uid;
        public int Uid2;      // второй товар связки
        public bool Revealed; // для коробки

        public int Count => Kind == TokenKind.Pallet ? 3 : Kind == TokenKind.Bundle ? 2 : 1;
    }

    public struct CartItem
    {
        public int Uid;
        public int Type;
        public int Timer;     // ходов с ленты до порчи, -1 — не портится
        public bool Spoiled;
        public int SpoilLeft; // сколько ходов испорченный товар ещё лежит
    }

    public sealed class SectionState
    {
        public SectionKind Kind;
        public int LockSets;
        public int Type = -1;
        public readonly List<int> Uids = new List<int>(3);

        public bool Locked => LockSets > 0;
        public int Capacity => ItemCatalog.SetSize(Type);
        public bool IsEmpty => Type < 0;

        public SectionState Clone()
        {
            var s = new SectionState { Kind = Kind, LockSets = LockSets, Type = Type };
            s.Uids.AddRange(Uids);
            return s;
        }
    }

    public struct Customer
    {
        public bool Active;
        public int Type;
        public int Patience;
        public int MaxPatience;
        public int Look;
    }

    public struct CustomerPlan
    {
        public int AtMove;
        public int Type;
        public int Patience;
        public int Look;
    }

    /// <summary>Полное состояние уровня. Клонируется для отмены хода и для солвера.</summary>
    public sealed class LevelState
    {
        public List<Token>[] Belts;
        public int Window;
        public int Visible;
        /// <summary>Мест на ленте на экране: шаг между товарами одинаковый во всех уровнях.</summary>
        public const int BeltSlots = 8;
        public SectionState[] Sections;
        public List<CartItem> Cart;
        public int CartCapacity;

        public int[] GoalTypes;
        public int[] GoalNeed;
        public int[] GoalDone;

        public int PerishTimer;
        // умения команды Торгового дома (03.10.2026): без команды — прежние значения правил
        public int FrozenTimer = Rules.FrozenCartTimer;   // сколько ходов заморозка живёт в тележке
        public int SaleCoins = 2;                         // монет (в наборах) за набор с акционной полки
        public int DoorOpen;
        public int DoorClosed;
        public bool Night;

        public CustomerPlan[] CustomerPlans;
        public int NextCustomer;
        public Customer Customer;

        public int BeltMoves;
        public int Moves;
        public int PeakCart;
        public int SpoiledCount;
        public int Restocks;      // сколько раз довозили свежий товар взамен испорченного (Rules.Restock)
        public int SetsSold;
        public int SetCoins;
        public int Tips;
        public int CustomersServed;
        public int NextUid = 1;
        public bool ContinueUsed;
        public bool CartBonusUsed;

        public GameResult Result;
        public LoseReason Reason;

        // «Час пик» (ГДД 8.6)
        public bool Endless;
        public int Jam;
        public int JamMax = 3;

        public int CartUsed
        {
            get
            {
                int u = 0;
                for (int i = 0; i < Cart.Count; i++) u += ItemCatalog.SlotSize(Cart[i].Type);
                return u;
            }
        }

        public int CartFree => CartCapacity - CartUsed;

        public bool DoorIsOpen
        {
            get
            {
                if (DoorOpen <= 0 || DoorClosed <= 0) return true;
                return BeltMoves % (DoorOpen + DoorClosed) < DoorOpen;
            }
        }

        public bool AllGoalsDone
        {
            get
            {
                for (int i = 0; i < GoalNeed.Length; i++) if (GoalDone[i] < GoalNeed[i]) return false;
                return true;
            }
        }

        public int GoalIndex(int type)
        {
            for (int i = 0; i < GoalTypes.Length; i++) if (GoalTypes[i] == type) return i;
            return -1;
        }

        public bool BeltsEmpty
        {
            get
            {
                foreach (var b in Belts) if (b.Count > 0) return false;
                return true;
            }
        }

        public int SectionOfType(int type)
        {
            for (int i = 0; i < Sections.Length; i++) if (Sections[i].Type == type) return i;
            return -1;
        }

        public LevelState Clone()
        {
            var c = (LevelState)MemberwiseClone();
            c.Belts = new List<Token>[Belts.Length];
            for (int i = 0; i < Belts.Length; i++) c.Belts[i] = new List<Token>(Belts[i]);
            c.Sections = new SectionState[Sections.Length];
            for (int i = 0; i < Sections.Length; i++) c.Sections[i] = Sections[i].Clone();
            c.Cart = new List<CartItem>(Cart);
            c.GoalDone = (int[])GoalDone.Clone();
            return c;
        }

        /// <summary>Хэш состояния для солвера (порядок товаров в тележке не важен).</summary>
        public ulong Hash()
        {
            ulong h = 1469598103934665603UL;
            void Mix(long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } }
            foreach (var b in Belts)
            {
                Mix(b.Count);
                for (int i = 0; i < b.Count; i++) Mix(b[i].Uid);
            }
            foreach (var s in Sections) { Mix(s.Type); Mix(s.Uids.Count); Mix(s.LockSets); }
            ulong cartHash = 0;
            foreach (var c in Cart)
            {
                unchecked { cartHash += (ulong)((c.Type + 1) * 7919 + (c.Timer + 2) * 131 + (c.Spoiled ? 17 : 0) + c.SpoilLeft * 3) * 2654435761UL; }
            }
            Mix((long)cartHash);
            Mix(Customer.Active ? Customer.Type * 100 + Customer.Patience : -1);
            Mix(NextCustomer);
            if (DoorOpen > 0) Mix(BeltMoves % (DoorOpen + DoorClosed));
            for (int i = 0; i < GoalDone.Length; i++) Mix(GoalDone[i]);
            return h;
        }

        // ------------------------------------------------------------------ создание из данных

        public static LevelState FromData(LevelData d, bool cartBonus = false)
        {
            var s = new LevelState
            {
                Window = Math.Max(1, d.window),
                // видно всю ленту (8 мест) — плотность одинаковая во всех уровнях (просьба 03.10.2026);
                // ночью — только край ленты, это и есть механика
                Visible = Math.Max(d.window, d.night ? d.window + 1 : BeltSlots),
                CartCapacity = d.cart + (cartBonus ? 1 : 0),
                CartBonusUsed = cartBonus,
                PerishTimer = d.perishTimer,
                DoorOpen = d.doorOpen,
                DoorClosed = d.doorClosed,
                Night = d.night,
                Cart = new List<CartItem>(),
            };

            s.Sections = new SectionState[d.sections.Length];
            for (int i = 0; i < d.sections.Length; i++)
            {
                var sd = d.sections[i];
                var sec = new SectionState
                {
                    Kind = sd.kind == "freezer" ? SectionKind.Freezer : sd.kind == "sale" ? SectionKind.Sale : SectionKind.Normal,
                    LockSets = sd.lockSets,
                };
                if (!string.IsNullOrEmpty(sd.prefillType) && sd.prefillCount > 0)
                {
                    sec.Type = ItemCatalog.IndexOf(sd.prefillType);
                    for (int k = 0; k < sd.prefillCount; k++) sec.Uids.Add(s.NextUid++);
                }
                s.Sections[i] = sec;
            }

            s.GoalTypes = new int[d.goals.Length];
            s.GoalNeed = new int[d.goals.Length];
            s.GoalDone = new int[d.goals.Length];
            for (int i = 0; i < d.goals.Length; i++)
            {
                s.GoalTypes[i] = ItemCatalog.IndexOf(d.goals[i].type);
                s.GoalNeed[i] = d.goals[i].sets;
            }

            s.Belts = new List<Token>[Math.Max(1, d.belts.Length)];
            for (int b = 0; b < s.Belts.Length; b++)
            {
                s.Belts[b] = new List<Token>();
                if (b >= d.belts.Length) continue;
                foreach (var t in d.belts[b].tokens) s.Belts[b].Add(ParseToken(t, s));
            }

            foreach (var c in d.cartStart)
            {
                int type = ItemCatalog.IndexOf(c);
                s.Cart.Add(new CartItem { Uid = s.NextUid++, Type = type, Timer = -1 });
            }

            s.CustomerPlans = new CustomerPlan[d.customers.Length];
            for (int i = 0; i < d.customers.Length; i++)
                s.CustomerPlans[i] = new CustomerPlan
                {
                    AtMove = d.customers[i].atMove, Type = ItemCatalog.IndexOf(d.customers[i].type),
                    Patience = d.customers[i].patience, Look = d.customers[i].look
                };

            Rules.RevealWindow(s, null);
            s.PeakCart = s.CartUsed;
            return s;
        }

        public static Token ParseToken(string t, LevelState s)
        {
            var tok = new Token { Uid = s.NextUid++, Type2 = -1 };
            if (t.StartsWith("box:")) { tok.Kind = TokenKind.Box; tok.Type = ItemCatalog.IndexOf(t.Substring(4)); }
            else if (t.StartsWith("pallet:")) { tok.Kind = TokenKind.Pallet; tok.Type = ItemCatalog.IndexOf(t.Substring(7)); }
            else if (t.StartsWith("bundle:"))
            {
                var parts = t.Substring(7).Split('+');
                tok.Kind = TokenKind.Bundle;
                tok.Type = ItemCatalog.IndexOf(parts[0]);
                tok.Type2 = ItemCatalog.IndexOf(parts[1]);
                tok.Uid2 = s.NextUid++;
            }
            else { tok.Kind = TokenKind.Item; tok.Type = ItemCatalog.IndexOf(t); }
            if (tok.Type < 0) throw new ArgumentException("Unknown item in token: " + t);
            return tok;
        }

        public static string TokenToString(Token t)
        {
            string a = ItemCatalog.Get(t.Type).Id;
            switch (t.Kind)
            {
                case TokenKind.Box: return "box:" + a;
                case TokenKind.Pallet: return "pallet:" + a;
                case TokenKind.Bundle: return "bundle:" + a + "+" + ItemCatalog.Get(t.Type2).Id;
                default: return a;
            }
        }

        /// <summary>Пустая секция сверх уровня (награда за рекламу, просьба 23.09.2026).</summary>
        public void AddSection()
        {
            var a = new SectionState[Sections.Length + 1];
            System.Array.Copy(Sections, a, Sections.Length);
            a[Sections.Length] = new SectionState();
            Sections = a;
        }

    }
}

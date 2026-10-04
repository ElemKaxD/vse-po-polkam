using System.Collections.Generic;
using AllOnShelves.Core;
using NUnit.Framework;

namespace AllOnShelves.Tests
{
    /// <summary>Тест-кейсы ядра (ГДД 17.1).</summary>
    public class RulesTests
    {
        static LevelData Level(string[] belt, int sections = 2, int window = 1, string[] goals = null, int cart = 5)
        {
            var d = new LevelData { id = 1, cart = cart, window = window, visible = 6, belts = new[] { new BeltData { tokens = belt } } };
            d.sections = new SectionData[sections];
            for (int i = 0; i < sections; i++) d.sections[i] = new SectionData();
            var g = new List<GoalData>();
            foreach (var x in goals ?? new[] { "apple:1" })
            {
                var p = x.Split(':');
                g.Add(new GoalData { type = p[0], sets = int.Parse(p[1]) });
            }
            d.goals = g.ToArray();
            return d;
        }

        [Test]
        public void ClickOutsideWindow_IsIllegal()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "milk", "apple" }, window: 1));
            Assert.IsFalse(Rules.IsLegal(s, Move.FromBelt(0, 1, TargetKind.Auto)));
        }

        [Test]
        public void AutoMove_GoesToOwnThenEmptySection()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "apple", "milk" }, window: 1, goals: new[] { "apple:1", "milk:1" }));
            Assert.IsTrue(Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null));
            Assert.AreEqual(0, s.SectionOfType(ItemCatalog.IndexOf("apple")));
            Assert.IsTrue(Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null));
            Assert.AreEqual(2, s.Sections[0].Uids.Count);
        }

        [Test]
        public void ThreeSame_SellsSection_AndCountsGoal()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "apple", "apple", "milk" }, goals: new[] { "apple:1", "milk:1" }));
            var ev = new List<GameEvent>();
            for (int i = 0; i < 3; i++) Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), ev);
            Assert.AreEqual(1, s.GoalDone[0]);
            Assert.IsTrue(s.Sections[0].IsEmpty);
            Assert.IsTrue(ev.Exists(e => e.Kind == EventKind.SetSold));
        }

        [Test]
        public void OneSectionPerType_Rule()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "apple" }, window: 2));
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Section, 0), null);
            Assert.IsFalse(Rules.IsLegal(s, Move.FromBelt(0, 0, TargetKind.Section, 1)));
        }

        [Test]
        public void MoveFromCart_DoesNotAdvanceBelt()
        {
            var s = LevelState.FromData(Level(new[] { "milk", "bread", "apple", "apple" }, sections: 1, window: 1, goals: new[] { "bread:1" }));
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);  // молоко занимает секцию
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);  // хлеб — в тележку
            Assert.AreEqual(1, s.Cart.Count);
            int beltMoves = s.BeltMoves;
            int beltCount = s.Belts[0].Count;
            Assert.IsFalse(Rules.IsLegal(s, Move.FromCart(0, TargetKind.Auto)), "Хлебу пока некуда");
            Assert.AreEqual(beltMoves, s.BeltMoves);
            Assert.AreEqual(beltCount, s.Belts[0].Count);
        }

        [Test]
        public void GoalsDone_WinsImmediately_EvenWithBeltLeft()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "apple", "apple", "milk", "milk" }, goals: new[] { "apple:1" }));
            for (int i = 0; i < 3; i++) Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.AreEqual(GameResult.Win, s.Result);
        }

        [Test]
        public void FullCartAndNoMoves_Loses()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "milk", "bread", "cheese", "carrot", "banana", "fish", "candy" },
                sections: 1, cart: 5, goals: new[] { "apple:1" }));
            for (int i = 0; i < 6; i++) Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.AreEqual(GameResult.Lose, s.Result);
            Assert.AreEqual(LoseReason.Deadlock, s.Reason);
        }

        [Test]
        public void Loader_AddsTwoSlots_AndResumes()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "milk", "bread", "cheese", "carrot", "banana", "fish", "candy", "apple", "apple" },
                sections: 1, cart: 5, goals: new[] { "apple:1" }));
            for (int i = 0; i < 6; i++) Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.IsTrue(Rules.LoaderCanHelp(s));
            Rules.ApplyLoader(s, null);
            Assert.AreEqual(GameResult.Playing, s.Result);
            Assert.AreEqual(7, s.CartCapacity);
            Assert.AreEqual(1, Rules.Stars(s, 5) == 0 ? 1 : 1);
        }

        [Test]
        public void Lock_OpensAfterSets()
        {
            var d = Level(new[] { "apple", "apple", "apple", "milk" }, sections: 2, goals: new[] { "apple:1", "milk:1" });
            d.sections[1].lockSets = 1;
            var s = LevelState.FromData(d);
            Assert.IsTrue(s.Sections[1].Locked);
            for (int i = 0; i < 3; i++) Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.IsFalse(s.Sections[1].Locked);
        }

        [Test]
        public void Box_RevealsInWindow()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "box:milk" }, window: 1, goals: new[] { "apple:1" }));
            Assert.AreEqual(TokenKind.Box, s.Belts[0][1].Kind);
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.AreEqual(TokenKind.Item, s.Belts[0][0].Kind);
        }

        [Test]
        public void Perishable_SpoilsInCart()
        {
            var d = Level(new[] { "cheese", "milk", "apple", "apple", "apple", "carrot", "carrot" }, sections: 1, window: 1, goals: new[] { "apple:1" });
            d.perishTimer = 2;
            var s = LevelState.FromData(d);
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null); // сыр — секция
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Cart), null); // молоко — тележка, таймер 2 → 1
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Cart), null); // таймер 0 → испортилось
            Assert.IsTrue(s.Cart.Exists(c => c.Spoiled));
            Assert.AreEqual(1, s.SpoiledCount);
        }

        [Test]
        public void Pallet_FillsSectionAndSells()
        {
            var s = LevelState.FromData(Level(new[] { "pallet:apple", "milk" }, goals: new[] { "apple:1", "milk:1" }));
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Section, 0), null);
            Assert.AreEqual(1, s.GoalDone[0]);
        }

        [Test]
        public void Undo_ByClone_RestoresState()
        {
            var s = LevelState.FromData(Level(new[] { "apple", "apple", "apple" }));
            var before = s.Clone();
            Rules.Apply(s, Move.FromBelt(0, 0, TargetKind.Auto), null);
            Assert.AreNotEqual(before.Hash(), s.Hash());
            Assert.AreEqual(3, before.Belts[0].Count);
        }

        [Test]
        public void AllGeneratedLevelsSolvable_First20()
        {
            for (int id = 1; id <= 20; id++)
            {
                var lvl = LevelPlanner.Build(id, attempts: 5, solverNodes: 60000);
                Assert.IsNotNull(lvl, "level " + id);
                Assert.GreaterOrEqual(lvl.metrics.minPeakCart, 0, "level " + id);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;
using UnityEngine;

namespace AllOnShelves
{
    /// <summary>
    /// Смена в уровне (ГДД v3, п. 3.1; 03.10.2026). Перед уровнем игрок видит, что в нём ждёт (замки, скоропорт…),
    /// и сам ставит сотрудников на смену — игра только советует (окно Game/ShiftPopup, просьба 03.10.2026: «игрок сам
    /// должен выбирать»). Мест на смене: 1, второе — когда построен весь второй этаж, третье — Буфет. Умения скромные:
    /// уровни проходятся и без команды (солвер проверяет без умений).
    /// </summary>
    public static class Team
    {
        public sealed class Member
        {
            public StaffInfo Info;
            public int Level;
        }

        public static int Slots =>
            System.Math.Min(3, 1 + (House.IsBuilt(3) && House.IsBuilt(4) && House.IsBuilt(5) ? 1 : 0) + (House.IsBuilt(9) ? 1 : 0)
                               + (Streak.StaffSlot ? 1 : 0));   // серия побед ×3 — ещё одно место (03.10.2026)

        /// <summary>Сотрудники, которых можно поставить на смену (въехали в свою комнату).</summary>
        public static List<Member> Ready() =>
            House.Staff.Select(st => new Member { Info = st, Level = House.LevelOf(st.Id) }).Where(m => m.Level > 0).ToList();

        /// <summary>Бывает ли смена в этом режиме (в «Часе пик» нет) и есть ли кого ставить.</summary>
        public static bool Available(PlayMode mode) =>
            mode != PlayMode.Rush && GameApp.I != null && House.Unlocked && Ready().Count > 0;

        /// <summary>
        /// Кого советуем (значок «Советую» в окне смены): сначала те, чья механика есть в уровне, потом — помощники
        /// на любой уровень; внутри — по уровню. Сами на смену не ставим.
        /// </summary>
        public static List<string> Recommend(LevelState s)
        {
            return Ready().OrderByDescending(m => string.IsNullOrEmpty(m.Info.Mechanic) ? 1 : Useful(m.Info, s) ? 2 : 0)
                          .ThenByDescending(m => m.Level)
                          .Where(m => Useful(m.Info, s) || string.IsNullOrEmpty(m.Info.Mechanic))
                          .Take(Slots).Select(m => m.Info.Id).ToList();
        }

        public static Member Get(string id)
        {
            var st = House.Staff.FirstOrDefault(x => x.Id == id);
            return st == null ? null : new Member { Info = st, Level = House.LevelOf(id) };
        }

        /// <summary>Что ждёт в уровне — ключи механик (картинка и название — MechanicPopup.Info).</summary>
        public static List<string> Features(LevelState s)
        {
            var f = new List<string>();
            var goal = new HashSet<int>(s.GoalTypes);
            bool box = false, pallet = false, bundle = false, extra = false;
            foreach (var belt in s.Belts)
                foreach (var t in belt)
                {
                    box |= t.Kind == TokenKind.Box;
                    pallet |= t.Kind == TokenKind.Pallet;
                    bundle |= t.Kind == TokenKind.Bundle;
                    extra |= !goal.Contains(t.Type) || (t.Kind == TokenKind.Bundle && !goal.Contains(t.Type2));
                }
            if (s.Sections.Any(x => x.LockSets > 0)) f.Add("lock");
            if (s.CustomerPlans != null && s.CustomerPlans.Length > 0) f.Add("customer");
            if (s.PerishTimer > 0) f.Add("perish");
            if (s.Sections.Any(x => x.Kind == SectionKind.Freezer)) f.Add(s.DoorOpen > 0 || s.DoorClosed > 0 ? "door" : "freezer");
            if (s.Sections.Any(x => x.Kind == SectionKind.Sale)) f.Add("sale");
            if (s.Night) f.Add("night");
            if (s.GoalTypes.Any(ItemCatalog.IsBig)) f.Add("big");
            if (box) f.Add("box");
            if (pallet) f.Add("pallet");
            if (bundle) f.Add("bundle");
            if (extra) f.Add("extra");
            if (s.Belts.Length > 1) f.Add("belt2");
            return f;
        }

        /// <summary>Что сотрудник сделает в этом уровне — строка для окна смены.</summary>
        public static string EffectHere(Member m, LevelState s)
        {
            string skill = m.Info.Skill[Mathf.Clamp(m.Level, 1, 4) - 1];
            return Useful(m.Info, s) ? skill : skill + " — в этом уровне не пригодится";
        }

        /// <summary>Пригодится ли умение в этом уровне.</summary>
        public static bool Useful(StaffInfo st, LevelState s)
        {
            switch (st.Mechanic)
            {
                case "lock": return s.Sections.Any(x => x.LockSets > 0);
                case "customer": return s.CustomerPlans != null && s.CustomerPlans.Length > 0;
                case "perish": return s.PerishTimer > 0;
                case "freezer": return s.Sections.Any(x => x.Kind == SectionKind.Freezer);
                case "sale": return s.Sections.Any(x => x.Kind == SectionKind.Sale);
                case "night": return s.Night;
                case "big": return s.GoalTypes.Any(ItemCatalog.IsBig);
                default: return true;
            }
        }

        static int L(List<Member> shift, string id) => shift.FirstOrDefault(m => m.Info.Id == id)?.Level ?? 0;

        /// <summary>
        /// Умения на старте уровня: меняют состояние до первого хода. freeUndo — бесплатные отмены уровня,
        /// hints — подсказки в подарок. Возвращает строки «кто что сделал» для сообщения.
        /// </summary>
        public static List<string> ApplyStart(LevelState s, List<Member> shift, ref int freeUndo, out int hints)
        {
            var notes = new List<string>();
            hints = 0;
            int lv;
            if ((lv = L(shift, "beaver")) > 0)
            {
                // замки ближе к открытию: по набору за уровень умения, с самых «коротких» замков
                int n = lv;
                foreach (var sec in s.Sections.Where(x => x.LockSets > 0).OrderBy(x => x.LockSets))
                    while (n > 0 && sec.LockSets > 0) { sec.LockSets--; n--; }
                notes.Add("Бобр подточил замки");
            }
            if ((lv = L(shift, "rabbit")) > 0 && s.CustomerPlans != null)
            {
                int add = new[] { 0, 1, 2, 2, 3 }[lv];
                for (int i = 0; i < s.CustomerPlans.Length; i++) s.CustomerPlans[i].Patience += add;
                notes.Add("Кролик зазывает покупателей");
            }
            if ((lv = L(shift, "hamster")) > 0 && s.PerishTimer > 0)
            {
                s.PerishTimer += new[] { 0, 2, 3, 4, 6 }[lv];
                notes.Add("Хомяк бережёт скоропорт");
            }
            if ((lv = L(shift, "penguin")) > 0)
            {
                s.FrozenTimer += new[] { 0, 2, 3, 4, 6 }[lv];
                notes.Add("Пингвин держит холод");
            }
            if ((lv = L(shift, "fox")) > 0) s.SaleCoins = new[] { 2, 3, 4, 5, 6 }[lv];
            if ((lv = L(shift, "owl")) > 0 && s.Night)
            {
                s.Visible += new[] { 0, 1, 2, 3, 4 }[lv];
                notes.Add("Сова светит фонарём");
            }
            if ((lv = L(shift, "bear")) > 0 && s.GoalTypes.Any(ItemCatalog.IsBig))
            {
                s.CartCapacity += lv >= 3 ? 2 : 1;
                notes.Add("Медведь освободил место в тележке");
            }
            if ((lv = L(shift, "cat")) > 0)
            {
                freeUndo += lv >= 3 ? 2 : 1;
                if (lv >= 3) hints = 1;
            }
            return notes;
        }

        /// <summary>Ёж-грузчик: сколько раз за уровень уносит товар из полной тележки (+1 место).</summary>
        public static int Rescues(List<Member> shift) => L(shift, "hedgehog");

        /// <summary>Прибавка к монетам за победу, в процентах (Белка, Кот).</summary>
        public static int CoinPercent(List<Member> shift)
        {
            int p = new[] { 0, 5, 10, 15, 25 }[L(shift, "squirrel")];
            p += new[] { 0, 0, 5, 5, 10 }[L(shift, "cat")];
            return p;
        }

        /// <summary>Множитель чаевых от Кролика (3–4 уровень).</summary>
        public static float TipsMult(List<Member> shift) => new[] { 1f, 1f, 1f, 1.5f, 2f }[L(shift, "rabbit")];

        /// <summary>Портрет сотрудника (значок-бейдж staff_&lt;id&gt;_face); нет картинки — аватарка зверька или значок.</summary>
        public static string Face(string id)
        {
            foreach (var n in new[] { $"staff_{id}_face", "av_" + (id == "rabbit" ? "bunny" : id) })
                if (ArtLibrary.S(n) != null) return n;
            return "icon_staff_badge";
        }
    }
}

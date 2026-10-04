using System.Collections.Generic;
using System.Linq;

namespace AllOnShelves.Core
{
    public enum Department { Fruits, Vegetables, Dairy, Bakery, Deli, Seafood, Sweets, Drinks, Frozen, Seasonal }

    public sealed class ItemInfo
    {
        public readonly int Index;
        public readonly string Id;
        public readonly string NameRu;
        public readonly Department Department;
        public readonly bool Perishable;
        public readonly bool Frozen;
        public readonly bool Big;
        /// <summary>Район, с которого товар появляется в уровнях (темп v2: все товары — к 6-му району).</summary>
        public readonly int District;

        public ItemInfo(int index, string id, string nameRu, Department dep, int district,
                        bool perishable = false, bool frozen = false, bool big = false)
        {
            Index = index; Id = id; NameRu = nameRu; Department = dep; District = district;
            Perishable = perishable; Frozen = frozen; Big = big;
        }
    }

    /// <summary>28 игровых товаров. Индекс = тип товара в модели.</summary>
    public static class ItemCatalog
    {
        public static readonly ItemInfo[] All =
        {
            new ItemInfo(0, "apple", "Яблоко", Department.Fruits, 1),
            new ItemInfo(1, "milk", "Молоко", Department.Dairy, 1, perishable: true),
            new ItemInfo(2, "bread", "Хлеб", Department.Bakery, 1),
            new ItemInfo(3, "cheese", "Сыр", Department.Deli, 1),
            new ItemInfo(4, "carrot", "Морковь", Department.Vegetables, 1),
            new ItemInfo(5, "banana", "Банан", Department.Fruits, 1),
            new ItemInfo(6, "fish", "Рыба", Department.Seafood, 2, perishable: true),
            new ItemInfo(7, "candy", "Конфета", Department.Sweets, 2),
            new ItemInfo(8, "yogurt", "Йогурт", Department.Dairy, 2, perishable: true),
            new ItemInfo(9, "eggs", "Яйца", Department.Dairy, 2),
            new ItemInfo(10, "cucumber", "Огурец", Department.Vegetables, 3),
            new ItemInfo(11, "ice_cream", "Мороженое", Department.Frozen, 2, frozen: true),
            new ItemInfo(12, "dumplings", "Пельмени", Department.Frozen, 2, frozen: true),
            new ItemInfo(13, "baguette", "Багет", Department.Bakery, 3),
            new ItemInfo(14, "watermelon", "Арбуз", Department.Seasonal, 3, big: true),
            new ItemInfo(15, "cabbage", "Капуста", Department.Vegetables, 3),
            new ItemInfo(16, "tomato", "Помидор", Department.Vegetables, 3),
            new ItemInfo(17, "croissant", "Круассан", Department.Bakery, 4),
            new ItemInfo(18, "cake", "Торт", Department.Sweets, 4),
            new ItemInfo(19, "juice", "Сок", Department.Drinks, 4),
            new ItemInfo(20, "shrimp", "Креветки", Department.Seafood, 4, perishable: true),
            new ItemInfo(21, "sausage", "Колбаса", Department.Deli, 4, perishable: true),
            new ItemInfo(22, "water", "Вода", Department.Drinks, 5),
            new ItemInfo(23, "chocolate", "Шоколад", Department.Sweets, 5),
            new ItemInfo(24, "pumpkin", "Тыква", Department.Seasonal, 5, big: true),
            new ItemInfo(25, "pear", "Груша", Department.Fruits, 5),
            new ItemInfo(26, "orange", "Апельсин", Department.Fruits, 6),
            new ItemInfo(27, "pizza", "Пицца", Department.Frozen, 6, frozen: true),
        };

        // строится сразу, а не лениво: генератор уровней зовёт IndexOf из многих потоков —
        // ленивое заполнение давало «Unknown item» в соседнем потоке (03.10.2026)
        static readonly Dictionary<string, int> _byId = All.ToDictionary(i => i.Id, i => i.Index);

        public static int Count => All.Length;

        public static int IndexOf(string id) => _byId.TryGetValue(id, out var idx) ? idx : -1;

        public static ItemInfo Get(int type) => All[type];
        public static bool IsBig(int type) => type >= 0 && All[type].Big;
        public static bool IsFrozen(int type) => type >= 0 && All[type].Frozen;
        public static bool IsPerishable(int type) => type >= 0 && All[type].Perishable;
        public static int SlotSize(int type) => IsBig(type) ? 2 : 1;
        public static int SetSize(int type) => IsBig(type) ? 2 : 3;
    }
}

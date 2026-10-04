using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;

namespace AllOnShelves
{
    public sealed class RenovationItem
    {
        public string Id;       // meta_d01_sign
        public string Name;
        public int Cost;
        public int District;
        public string Unlocks;  // подпись «Открывает: …»
    }

    public sealed class StoreInfo
    {
        public string Id;       // kiosk / corner / minimarket / supermarket / hypermarket
        public string Name;
        public int[] Districts;
    }

    public sealed class StickerInfo
    {
        public string Id;       // sprite name: item_apple / sticker_grapes / gold_fruits
        public string Name;
        public int Department;
        public bool Gold;
    }

    public enum ProductType { Permanent, Consumable }

    public sealed class ProductInfo
    {
        public string Id;
        public string Title;
        public string Description;
        public string Icon;
        public ProductType Type;
        public int Coins, Undo, Hint;
        public int Packs, GoldPacks;     // пачки наклеек (набор коллекционера, 01.10.2026)
        public int Gems, Hammers;        // алмазы и молотки (экономика v4, 03.10.2026)
        public bool GoldPath;            // Золотой путь — вторая строка «Звёздного пути», навсегда
        public bool NoAds;
        public string Theme;             // у наборов оформления — место карточки в магазине и превью (night/winter/farm)
        public string[] Cosmetics;       // скины, которые даёт покупка (наборы оформления, стартовый, золотой)
        public bool UndoUnlimited24h;
        public string FallbackPrice;
    }

    /// <summary>Данные меты (ГДД 8.2–8.4, 9.3, 11.1). Правятся здесь.</summary>
    /// <summary>Косметика: ореол вокруг енота на карте, скин енота, оформление магазина в уровне.</summary>
    public sealed class Cosmetic
    {
        public readonly string Id;
        public readonly string Kind;     // halo | raccoon | scene | cart | belt | shelf | items | avatar | frame
        public readonly string Title;
        public readonly string Sprite;   // картинка; пока её нет — вещь не показываем
        public readonly string IconSprite; // значок в «Гардеробе», если сама картинка для кружка не годится (лента — узкая полоса)

        // Скины за монеты НЕ продаются (решение 30.09.2026): только наградой («Звёздный путь», ремонт района)
        // или в наборах за деньги (MetaCatalog.Products[].Cosmetics). Откуда вещь — MetaCatalog.SourceOf.
        public Cosmetic(string id, string kind, string title, string sprite, string icon = null)
        {
            Id = id; Kind = kind; Title = title; Sprite = sprite; IconSprite = icon ?? sprite;
        }

        /// <summary>Вид товаров («items») готов, только когда нарисованы ВСЕ товары: иначе на полке смесь стилей.</summary>
        public bool ArtReady => Kind == "items"
            ? System.Linq.Enumerable.All(Core.ItemCatalog.All, i => ArtLibrary.S($"item_{i.Id}_{Suffix}") != null)
            : ArtLibrary.S(Sprite) != null;

        /// <summary>Для вида товаров: окончание имени картинки (item_apple_gift → gift).</summary>
        public string Suffix => Kind == "items" && Sprite.LastIndexOf('_') > 0 ? Sprite.Substring(Sprite.LastIndexOf('_') + 1) : "";
    }

    /// <summary>Награда «Звёздного пути»: открывается, когда всего звёзд набрано не меньше Stars.</summary>
    public sealed class StarReward
    {
        public readonly int Stars;
        public readonly string Kind;    // coins | hint | undo | pack | cosmetic | gems | hammer | boost (1 ч) | staff (Кот + алмазы)
        public readonly int Amount;
        public readonly string CosmeticId;

        public StarReward(int stars, string kind, int amount, string cosmetic = null)
        {
            Stars = stars; Kind = kind; Amount = amount; CosmeticId = cosmetic;
        }

        public Cosmetic Cosmetic => Kind == "cosmetic" ? MetaCatalog.Cosmetic(CosmeticId) : null;

        public string Icon
        {
            get
            {
                if (Kind == "cosmetic")
                {
                    var c = Cosmetic;
                    return c != null && c.ArtReady ? c.Sprite : "icon_gift";
                }
                switch (Kind)
                {
                    case "coins": return "icon_coin";
                    case "hint": return "icon_hint";
                    case "undo": return "icon_undo";
                    case "gems": return "icon_gem";
                    case "hammer": return "icon_hammer_gold";
                    case "boost": return "icon_boost_1h";
                    case "staff": return Team.Face("cat");
                    default: return "icon_sticker_pack";
                }
            }
        }

        /// <summary>Подпись под картинкой награды.</summary>
        public string Caption =>
            // подпись — внутри кружка награды (v4): одно короткое слово, полное название — в сообщении при получении
            Kind == "cosmetic" ? (Cosmetic == null ? "Скин" : Cosmetic.Kind == "halo" ? "Ореол" : Cosmetic.Kind == "frame" ? "Рамка"
                                  : Cosmetic.Kind == "scene" ? "Фон" : "Скин")
            : Kind == "pack" ? (Amount > 1 ? $"{Amount} пачки" : "Пачка")
            : Kind == "staff" ? "Кот" : Kind == "boost" ? "−1 ч ×" + Amount : Kind == "hammer" ? "×" + Amount : "+" + Amount;

        /// <summary>Полное название — для сообщений.</summary>
        public string Title
        {
            get
            {
                switch (Kind)
                {
                    case "cosmetic": return Cosmetic != null ? Cosmetic.Title : "скин";
                    case "coins": return $"{Amount} монет";
                    case "hint": return Amount == 1 ? "подсказка" : $"{Amount} подсказки";
                    case "undo": return Amount >= 5 ? $"{Amount} отмен" : $"{Amount} отмены";
                    case "gems": return $"{Amount} алмазов";
                    case "hammer": return Amount > 1 ? $"{Amount} молотка" : "молоток";
                    case "boost": return $"ускоритель «1 час» ×{Amount}";
                    case "staff": return $"Кот-управляющий и {Amount} алмазов";
                    default: return Amount > 1 ? $"{Amount} пачки наклеек" : "пачка наклеек";
                }
            }
        }
    }

    public static class MetaCatalog
    {
        // ------------------------------------------------------------------ «Звёздный путь» (темп v2, 22.09.2026)

        /// <summary>
        /// Награды за общее число звёзд (Лёгкий режим — 450 = 150 уровней × 3, с режимами — 1350). Первые — часто, чтобы игрок
        /// с первых уровней видел, куда идут звёзды, и переигрывал ради трёх звёзд.
        /// </summary>
        public static readonly StarReward[] StarTrack =
        {
            new StarReward(3, "coins", 25), new StarReward(7, "undo", 2),
            new StarReward(12, "cosmetic", 0, "halo_leaf"),
            new StarReward(17, "hint", 2), new StarReward(22, "pack", 1),
            new StarReward(28, "cosmetic", 0, "rac_chef"),
            new StarReward(34, "undo", 3), new StarReward(40, "coins", 80),
            new StarReward(47, "cosmetic", 0, "halo_hearts"),
            new StarReward(54, "hint", 3), new StarReward(62, "pack", 2),
            new StarReward(70, "cosmetic", 0, "scene_sunset"),
            new StarReward(80, "undo", 5), new StarReward(90, "coins", 150),
            new StarReward(100, "cosmetic", 0, "halo_gold"),
            new StarReward(112, "hint", 5), new StarReward(125, "pack", 3),
            new StarReward(140, "cosmetic", 0, "rac_winter"),
            new StarReward(150, "cosmetic", 0, "scene_winter_fair"),
            new StarReward(162, "undo", 5), new StarReward(175, "coins", 300),
            new StarReward(190, "cosmetic", 0, "halo_snow"),
            new StarReward(205, "hint", 5),
            new StarReward(220, "coins", 250),       // тележка в цветах ушла в стартовый набор (30.09.2026)
            new StarReward(235, "pack", 4),
            // скины енота (ГДД v3, 03.10.2026): в пути остались повар и зимний; супергерой убран,
            // пират и король — награды Золотого пути
            new StarReward(250, "hint", 6),
            new StarReward(265, "coins", 400),
            new StarReward(280, "cosmetic", 0, "halo_stars"),
            new StarReward(295, "cosmetic", 0, "scene_night_market"),
            new StarReward(310, "pack", 5),
            new StarReward(320, "coins", 500),
            new StarReward(330, "cosmetic", 0, "halo_crown"),
            // v4 (03.10.2026): звёзды режимов «Средний» и «Сложный» считаются отдельно — путь идёт до 1350.
            // Рамки режимов, ореол «Средний» и «Енот-чемпион» встанут вместо алмазов, когда придут картинки
            new StarReward(360, "coins", 300), new StarReward(390, "gems", 5), new StarReward(420, "pack", 3),
            new StarReward(450, "gems", 10),
            new StarReward(500, "coins", 400), new StarReward(550, "gems", 5), new StarReward(600, "hint", 5),
            new StarReward(650, "pack", 3), new StarReward(700, "undo", 6), new StarReward(750, "coins", 500),
            new StarReward(800, "gems", 8), new StarReward(850, "gems", 10), new StarReward(900, "gems", 10),
            new StarReward(975, "coins", 600), new StarReward(1050, "gems", 8), new StarReward(1125, "pack", 4),
            new StarReward(1200, "coins", 800), new StarReward(1275, "gems", 12), new StarReward(1350, "gems", 30),
        };

        /// <summary>
        /// Золотой путь (299 ₽, навсегда; экономика v4, 03.10.2026) — вторая строка «Звёздного пути» на тех же шагах.
        /// Купил — сразу забираешь всё, что уже пройдено. Таблица — Docs «Экономика v4», раздел 6.
        /// </summary>
        public static readonly StarReward[] GoldTrack = BuildGoldTrack();

        static StarReward[] BuildGoldTrack()
        {
            var g = new StarReward[StarTrack.Length];
            int last = g.Length - 1;
            for (int i = 0; i < g.Length; i++)
            {
                int s = StarTrack[i].Stars;
                if (i == 0) g[i] = new StarReward(s, "staff", 30);
                else if (i == 4) g[i] = new StarReward(s, "cosmetic", 0, "rac_pirate");
                else if (i == 12) g[i] = new StarReward(s, "cosmetic", 0, "rac_king");
                else if (i == 30) g[i] = new StarReward(s, "cosmetic", 0, "frame_goldcard");
                else if (i == last) g[i] = new StarReward(s, "gems", 100);
                else
                    switch (i % 4)
                    {
                        case 0: g[i] = new StarReward(s, "gems", 20); break;
                        case 1: g[i] = new StarReward(s, "coins", 250 + 10 * i); break;
                        case 2: g[i] = new StarReward(s, "hammer", 1); break;
                        default: g[i] = new StarReward(s, "boost", 2); break;
                    }
            }
            return g;
        }

        /// <summary>
        /// Косметика: ореолы вокруг енота на карте, скины енота, оформление магазина, тележка, конвейер и стеллажи
        /// в уровне. Картинки ещё нет — вещь не показываем. За монеты не продаётся (30.09.2026): награды пути,
        /// награды за ремонт районов (RenoRewards) и наборы за деньги (Products[].Cosmetics).
        /// </summary>
        public static readonly Cosmetic[] Cosmetics =
        {
            new Cosmetic("halo_leaf", "halo", "Ореол «Листья»", "halo_leaf"),
            new Cosmetic("halo_hearts", "halo", "Ореол «Сердечки»", "halo_hearts"),
            new Cosmetic("halo_gold", "halo", "Ореол «Золото»", "halo_gold"),
            new Cosmetic("halo_snow", "halo", "Ореол «Снежинки»", "halo_snow"),
            new Cosmetic("halo_stars", "halo", "Ореол «Звёзды»", "halo_stars"),
            new Cosmetic("halo_crown", "halo", "Ореол «Корона»", "halo_crown"),
            new Cosmetic("rac_chef", "raccoon", "Енот-повар", "map_avatar_chef"),
            new Cosmetic("rac_winter", "raccoon", "Зимний енот", "map_avatar_winter"),
            new Cosmetic("rac_pirate", "raccoon", "Енот-пират", "map_avatar_pirate"),
            new Cosmetic("rac_king", "raccoon", "Енот-король полок", "map_avatar_king"),
            new Cosmetic("cart_flowers", "cart", "Тележка в цветах", "brd_cart_5_flowers"),
            new Cosmetic("cart_farm", "cart", "Фермерская тележка", "brd_cart_5_farm"),
            new Cosmetic("cart_winter", "cart", "Новогодняя тележка", "brd_cart_5_winter"),
            new Cosmetic("cart_night", "cart", "Ночная тележка", "brd_cart_5_night"),
            new Cosmetic("cart_neon", "cart", "Неоновая тележка", "brd_cart_5_neon"),
            new Cosmetic("cart_gold", "cart", "Золотая тележка", "brd_cart_5_gold"),
            // конвейеры и стеллажи — пак скинов 29.09.2026 (PROMPTS_Скины_ВсёПоПолкам_29-09.txt)
            new Cosmetic("belt_candy", "belt", "Конвейер-леденец", "brd_belt_tile_candy", "brd_belt_roller_candy"),
            new Cosmetic("belt_wood", "belt", "Деревянный конвейер", "brd_belt_tile_wood", "brd_belt_roller_wood"),
            new Cosmetic("belt_winter", "belt", "Новогодний конвейер", "brd_belt_tile_winter", "brd_belt_roller_winter"),
            new Cosmetic("belt_neon", "belt", "Неоновый конвейер", "brd_belt_tile_neon", "brd_belt_roller_neon"),
            new Cosmetic("belt_gold", "belt", "Золотой конвейер", "brd_belt_tile_gold", "brd_belt_roller_gold"),
            new Cosmetic("shelf_candy", "shelf", "Кондитерские стеллажи", "brd_section_candy"),
            new Cosmetic("shelf_birch", "shelf", "Берёзовые стеллажи", "brd_section_birch"),
            new Cosmetic("shelf_winter", "shelf", "Новогодние стеллажи", "brd_section_winter"),
            new Cosmetic("shelf_metal", "shelf", "Стеллажи супермаркета", "brd_section_metal"),
            new Cosmetic("shelf_gold", "shelf", "Золотые стеллажи", "brd_section_gold"),
            // праздничный вид товаров: появится, когда нарисованы все 28 товаров (сейчас 6 — пробные)
            new Cosmetic("items_gift", "items", "Праздничные товары", "item_apple_gift"),
            // аватарки рейтинга (01.10.2026): все бесплатные, выбираются в «Гардеробе»; енот — по умолчанию
            new Cosmetic("av_raccoon", "avatar", "Енот", "map_avatar"),
            // мордочки енота в скине (03.10.2026): открываются вместе со скином, надевает игрок сам в «Гардеробе»
            new Cosmetic("av_rac_chef", "avatar", "Енот-повар", "map_head_chef"),
            new Cosmetic("av_rac_winter", "avatar", "Зимний енот", "map_head_winter"),
            new Cosmetic("av_rac_pirate", "avatar", "Енот-пират", "map_head_pirate"),
            new Cosmetic("av_rac_king", "avatar", "Енот-король", "map_head_king"),
            new Cosmetic("av_cat", "avatar", "Кошка", "av_cat"),
            new Cosmetic("av_corgi", "avatar", "Корги", "av_corgi"),
            new Cosmetic("av_bear", "avatar", "Медвежонок", "av_bear"),
            new Cosmetic("av_bunny", "avatar", "Зайчик", "av_bunny"),
            new Cosmetic("av_penguin", "avatar", "Пингвин", "av_penguin"),
            new Cosmetic("av_fox", "avatar", "Лисёнок", "av_fox"),
            new Cosmetic("av_panda", "avatar", "Панда", "av_panda"),
            new Cosmetic("av_hedgehog", "avatar", "Ёжик", "av_hedgehog"),
            new Cosmetic("av_owl", "avatar", "Совёнок", "av_owl"),
            new Cosmetic("av_frog", "avatar", "Лягушонок", "av_frog"),
            new Cosmetic("av_pig", "avatar", "Поросёнок", "av_pig"),
            // рамки аватарки — только награды недели в рейтинге (League.WeekReward), не продаются
            new Cosmetic("frame_crown", "frame", "Рамка «Золотая корона»", "frame_crown"),
            new Cosmetic("frame_goldcard", "frame", "Рамка «Золотой продавец»", "frame_goldcard"),   // Золотой путь
            new Cosmetic("frame_silver", "frame", "Рамка «Серебро»", "frame_silver"),
            new Cosmetic("frame_bronze", "frame", "Рамка «Бронза»", "frame_bronze"),
            new Cosmetic("frame_laurel", "frame", "Рамка «Лавровый венок»", "frame_laurel"),
            // рамки за собранные отделы альбома и за весь альбом (v4, 03.10.2026)
            new Cosmetic("frame_dept_fruits", "frame", "Рамка «Фрукты»", "frame_dept_fruits"),
            new Cosmetic("frame_dept_veg", "frame", "Рамка «Овощи»", "frame_dept_veg"),
            new Cosmetic("frame_dept_dairy", "frame", "Рамка «Молочка»", "frame_dept_dairy"),
            new Cosmetic("frame_dept_bakery", "frame", "Рамка «Выпечка»", "frame_dept_bakery"),
            new Cosmetic("frame_dept_cheese", "frame", "Рамка «Сыры и колбасы»", "frame_dept_cheese"),
            new Cosmetic("frame_dept_fish", "frame", "Рамка «Рыба»", "frame_dept_fish"),
            new Cosmetic("frame_dept_sweets", "frame", "Рамка «Сладости»", "frame_dept_sweets"),
            new Cosmetic("frame_dept_drinks", "frame", "Рамка «Напитки»", "frame_dept_drinks"),
            new Cosmetic("frame_dept_frozen", "frame", "Рамка «Заморозка»", "frame_dept_frozen"),
            new Cosmetic("frame_dept_seasonal", "frame", "Рамка «Бахча»", "frame_dept_seasonal"),
            new Cosmetic(AlbumMasterFrame, "frame", "Рамка «Мастер альбома»", AlbumMasterFrame),
            new Cosmetic("scene_sunset", "scene", "Магазин на закате", "bg_level_sunset"),
            new Cosmetic("scene_winter_fair", "scene", "Зимняя ярмарка", "bg_level_winter_fair"),
            new Cosmetic("scene_night_market", "scene", "Ночной рынок", "bg_level_night_market"),
            // фоны наборов оформления (раньше — «темы» магазина покупок)
            new Cosmetic("scene_farm", "scene", "Фермерский рынок", "bg_level_farm"),
            new Cosmetic("scene_winter", "scene", "Новогодний магазин", "bg_level_winter"),
            new Cosmetic("scene_night", "scene", "Ночной магазин", "bg_level_night"),
        };

        /// <summary>Награды за законченный ремонт района: скин сразу надевается (30.09.2026).</summary>
        public static readonly Dictionary<int, string> RenoRewards = new Dictionary<int, string>
        {
            { 2, "belt_candy" }, { 5, "shelf_candy" }, { 8, "items_gift" },
        };

        /// <summary>Откуда берётся вещь — для подписи в «Гардеробе».</summary>
        public enum SourceKind { None, StarTrack, Reno, Product, Leaderboard, Free, GoldPath, Album }

        /// <summary>Рамка аватарки за собранный отдел альбома (порядок — как DepartmentNames).</summary>
        public static readonly string[] DeptFrames =
        {
            "frame_dept_fruits", "frame_dept_veg", "frame_dept_dairy", "frame_dept_bakery", "frame_dept_cheese",
            "frame_dept_fish", "frame_dept_sweets", "frame_dept_drinks", "frame_dept_frozen", "frame_dept_seasonal",
        };
        public const string AlbumMasterFrame = "frame_album_master";

        public static SourceKind SourceOf(string cosmeticId, out int number, out ProductInfo product)
        {
            number = 0;
            var c = Cosmetic(cosmeticId);
            // мордочка в скине — там же, где сам скин
            if (SkinOfAvatar(cosmeticId) is string skin) return SourceOf(skin, out number, out product);
            if (c != null && c.Kind == "avatar") { product = null; return SourceKind.Free; }
            for (int i = 0; i < GoldTrack.Length; i++)
                if (GoldTrack[i].CosmeticId == cosmeticId) { product = Product("gold_path"); number = GoldTrack[i].Stars; return SourceKind.GoldPath; }
            // рамки альбома: number — отдел (−1 — весь альбом)
            int dept = System.Array.IndexOf(DeptFrames, cosmeticId);
            if (dept >= 0 || cosmeticId == AlbumMasterFrame) { product = null; number = dept; return SourceKind.Album; }
            if (c != null && c.Kind == "frame") { product = null; number = League.FramePlace(cosmeticId); return SourceKind.Leaderboard; }
            product = Products.FirstOrDefault(p => p.Cosmetics != null && p.Cosmetics.Contains(cosmeticId));
            if (product != null) return SourceKind.Product;
            for (int i = 0; i < StarTrack.Length; i++)
                if (StarTrack[i].CosmeticId == cosmeticId) { number = StarTrack[i].Stars; return SourceKind.StarTrack; }
            foreach (var kv in RenoRewards)
                if (kv.Value == cosmeticId) { number = kv.Key; return SourceKind.Reno; }
            return SourceKind.None;
        }

        public static Cosmetic Cosmetic(string id) => Cosmetics.FirstOrDefault(c => c.Id == id);

        /// <summary>Аватарка-мордочка в скине (av_rac_chef) → её скин (rac_chef); у прочих аватарок — null.</summary>
        public static string SkinOfAvatar(string id) =>
            id != null && id.StartsWith("av_rac_") ? "rac_" + id.Substring("av_rac_".Length) : null;

        public static IEnumerable<Cosmetic> CosmeticsOf(string kind) => Cosmetics.Where(c => c.Kind == kind);

        // ------------------------------------------------------------------ магазины и ремонт

        public static readonly StoreInfo[] Stores =
        {
            new StoreInfo { Id = "kiosk", Name = "Ларёк у остановки", Districts = new[] { 1, 2 } },
            new StoreInfo { Id = "corner", Name = "Магазин у дома", Districts = new[] { 3, 4 } },
            new StoreInfo { Id = "minimarket", Name = "Минимаркет", Districts = new[] { 5, 6 } },
            new StoreInfo { Id = "supermarket", Name = "Супермаркет", Districts = new[] { 7, 8, 9 } },
            new StoreInfo { Id = "hypermarket", Name = "Гипермаркет", Districts = new[] { 10, 11 } },
            // v4 (03.10.2026): уровни 111–150
            new StoreInfo { Id = "market", Name = "Фермерский рынок", Districts = new[] { 12, 13 } },
            new StoreInfo { Id = "mall", Name = "Торговый центр", Districts = new[] { 14, 15 } },
        };

        public static StoreInfo StoreOf(int district) => Stores.First(s => s.Districts.Contains(district));

        /// <summary>
        /// Стоимость этапа ремонта района (ГДД 9.3). Экономика v2 (30.09.2026, Tools/economy_sim.py): было 11 700
        /// на всю игру — любой игрок проходил без единой заминки и копил 6–14 тыс. лишних монет. Стало 21 320:
        /// без рекламы ~23 % времени уходит на гринд (упирается в ремонт в 6 районах из 10), хардкор — 2 %,
        /// с рекламой «×2» — ровно, лишние ~3,8 тыс. уходят на бустеры.
        /// </summary>
        public static readonly int[] StageCost = { 0, 300, 720, 1200, 1700, 1900, 2200, 2250, 2600, 2600, 2850, 3000,
                                                   3250, 3500, 3750, 4000 };   // районы 12–15 (экономика v4)

        // Районы 1–4 — ремонт-«лего» (23.09.2026): порядок и названия ровно те, что нарисованы
        // в сериях meta_storeN_stepNN (см. RenoLegoData). Первым покупается то, что сняли последним.
        static readonly (int d, string id, string name, string unlocks)[] RawItems =
        {
            (1, "chalkboard", "Грифельная доска", ""), (1, "fridge", "Холодильник", ""),
            (1, "flowers", "Цветочный ящик", ""), (1, "lamp", "Лампа", "Замок"),
            (1, "sign", "Вывеска-яблоко", "Коробка «?»"),
            (2, "bike_rack", "Велопарковка", ""), (2, "bin", "Урна", ""), (2, "bench", "Скамейка", "Лишний товар"),
            (2, "crates", "Ящики фруктов", "Скоропорт"), (2, "awning", "Полосатый навес", "Морозилка"),
            (2, "sign_lit", "Вывеска с огнями", "Акция"),
            (3, "banner", "Баннер акции", ""), (3, "flower_bed", "Клумба", ""), (3, "streetlamp", "Фонарь", ""),
            (3, "parking", "Парковка", "Крупный товар"), (3, "carts", "Тележки у входа", "Паллета"),
            (3, "auto_doors", "Стеклянные двери", ""), (3, "facade_sign", "Большая вывеска", "Связка"),
            (4, "flags", "Флажки", ""), (4, "benches", "Скамейки", ""),
            (4, "greens", "Зелень и клумбы", "Вторая лента"), (4, "van", "Фургон доставки", ""),
            (4, "terrace", "Фуд-корт", ""), (4, "canopy", "Козырёк входа", "Дверца морозилки"),
            (4, "sign_panel", "Вывеска-панно", ""),
            (5, "facade_sign", "Большая вывеска", ""), (5, "veg_stand", "Овощной отдел", ""), (5, "scales", "Весы", "Крупный товар"),
            (5, "produce_crates", "Ящики на паллетах", "Паллета"), (5, "auto_doors", "Автодвери", ""), (5, "trolley_bay", "Стоянка тележек", ""),
            (5, "flower_stand", "Цветочная стойка", ""), (5, "awning_green", "Зелёный навес", ""),
            (6, "oven", "Печь пекарни", ""), (6, "bread_baskets", "Корзины с хлебом", "Акционная полка"), (6, "cart_row", "Ряд тележек", ""),
            (6, "parking", "Парковка", ""), (6, "bike_rack", "Велопарковка", ""), (6, "bench_out", "Скамейка у входа", ""),
            (6, "streetlamp", "Фонарь", ""), (6, "cafe_table", "Столик кафе", ""),
            (7, "entrance_arch", "Входная арка", ""), (7, "checkout_1", "Касса №1", "Связка"), (7, "checkout_2", "Касса №2", ""),
            (7, "fish_counter", "Рыбный отдел", ""), (7, "aquarium", "Аквариум", ""), (7, "gates", "Рамки на входе", ""),
            (7, "info_desk", "Стойка информации", ""), (7, "balloon_arch", "Арка из шаров", ""), (7, "ceiling_banners", "Баннеры отделов", ""),
            (8, "racks", "Складские стеллажи", ""), (8, "forklift", "Погрузчик", "Вторая лента"), (8, "loading_belt", "Погрузочная лента", ""),
            (8, "pallet_stack", "Паллеты", ""), (8, "dock", "Разгрузочный док", ""), (8, "truck", "Грузовик", ""),
            (8, "staff_door", "Служебная дверь", ""), (8, "box_tower", "Башня коробок", ""), (8, "hand_truck", "Тележка-рохля", ""),
            (9, "cafe_counter", "Стойка кафе", ""), (9, "cafe_tables", "Столики кафе", ""), (9, "flower_corner", "Цветочный уголок", ""),
            (9, "coffee_bar", "Кофе-бар", ""), (9, "kids_ride", "Детская машинка", ""), (9, "ice_cream_stand", "Тележка мороженого", "Дверца морозилки"),
            (9, "juice_bar", "Фреш-бар", ""), (9, "plant_wall", "Зелёная стена", ""), (9, "sofa", "Диванчик", ""),
            (10, "night_sign", "Светящаяся вывеска", "Ночная смена"), (10, "street_lamps", "Фонари", ""), (10, "lit_windows", "Светлые окна", ""),
            (10, "roof_garden", "Сад на крыше", ""), (10, "fountain", "Фонтан", ""), (10, "clock_tower", "Часовая башня", ""),
            (10, "parked_cars", "Машины", ""), (10, "taxi", "Такси", ""), (10, "light_trees", "Деревья с гирляндами", ""), (10, "billboard", "Билборд", ""),
            (11, "food_stalls", "Фуд-корт", ""), (11, "food_tables", "Столы фуд-корта", ""), (11, "park_gate", "Ворота парка", ""),
            (11, "carousel", "Карусель", ""), (11, "playground", "Горка", ""), (11, "bandstand", "Сцена", ""), (11, "bike_path", "Велодорожка", ""),
            (11, "flower_beds", "Клумбы", ""), (11, "statue", "Памятник еноту", ""), (11, "fireworks", "Фейерверк", ""),
            // районы 12–15 (v4, 03.10.2026): порядок — как в нарезанных сериях meta_store12..15 (RenoLegoData)
            (12, "sign_arch", "Вывеска-арка", ""), (12, "lights", "Гирлянда", ""), (12, "pumpkins", "Ящики с тыквами", ""),
            (12, "scales", "Весы", ""), (12, "barrels", "Бочки с соленьями", ""), (12, "bread", "Корзины с хлебом", ""),
            (12, "chalkboard", "Доска цен", ""), (12, "sunflowers", "Подсолнухи", ""), (12, "cart", "Тележка у входа", ""),
            (13, "fish_stall", "Рыбный лоток", ""), (13, "lifebuoy", "Спасательный круг", ""), (13, "nets", "Сети с поплавками", ""),
            (13, "flags", "Флажки", ""), (13, "oysters", "Бочка с устрицами", ""), (13, "shells", "Корзина ракушек", ""),
            (13, "anchor", "Якорь", ""), (13, "lighthouse", "Фонарь-маяк", ""), (13, "boat_bed", "Лодка-клумба", ""),
            (14, "sign", "Большая вывеска", ""), (14, "revolving_door", "Вращающаяся дверь", ""), (14, "escalator", "Эскалатор", ""), (14, "showcase", "Витрина", ""),
            (14, "palms", "Пальмы в кадках", ""), (14, "benches", "Скамейки", ""), (14, "balloons", "Воздушные шары", ""),
            (14, "screen", "Табло", ""), (14, "fountain", "Фонтанчик", ""),
            (15, "star", "Звезда на крыше", ""), (15, "carpet", "Красная дорожка", ""), (15, "flower_arch", "Арка из цветов", ""),
            (15, "spotlights", "Прожекторы", ""), (15, "tree", "Ёлочка-гирлянда", ""), (15, "raccoon_statue", "Статуя енота", ""),
            (15, "flags", "Флаги", ""), (15, "confetti", "Конфетти-пушки", ""), (15, "stage", "Сцена", ""),
        };

        static List<RenovationItem> _items;

        public static List<RenovationItem> Items
        {
            get
            {
                if (_items != null) return _items;
                _items = new List<RenovationItem>();
                for (int d = 1; d < StageCost.Length; d++)
                {
                    var raw = RawItems.Where(r => r.d == d).ToArray();
                    int total = StageCost[d];
                    // первые дешевле, последние дороже: веса 0.7 .. 1.3
                    float wsum = 0; var w = new float[raw.Length];
                    for (int i = 0; i < raw.Length; i++) { w[i] = 0.7f + 0.6f * i / System.Math.Max(1, raw.Length - 1); wsum += w[i]; }
                    int acc = 0;
                    for (int i = 0; i < raw.Length; i++)
                    {
                        int cost = i == raw.Length - 1 ? total - acc : (int)(System.Math.Round(total * w[i] / wsum / 5f) * 5);
                        acc += cost;
                        _items.Add(new RenovationItem
                        {
                            Id = $"meta_d{d:00}_{raw[i].id}", Name = raw[i].name, Cost = cost, District = d, Unlocks = raw[i].unlocks
                        });
                    }
                }
                // первая покупка в обучении должна стоить ровно столько, сколько игрок заработает за 3 уровня
                _items[0].Cost = 30;
                return _items;
            }
        }

        public static IEnumerable<RenovationItem> ItemsOf(int district) => Items.Where(i => i.District == district);

        /// <summary>
        /// Пока не нарисованы свои иконки улучшений районов 2–4 (промты в паке 23.09) — берём
        /// похожий предмет из серии другого района. Это только картинка в облачке и на плитке:
        /// на самом магазине улучшение всё равно появляется своим слоем из кадра «лего».
        /// </summary>
        static readonly Dictionary<string, string> IconAlias = new Dictionary<string, string>
        {
            { "meta_d02_bike_rack", "meta_d06_bike_rack" },
            { "meta_d02_sign_lit", "meta_d05_facade_sign" },
            { "meta_d03_banner", "meta_d07_ceiling_banners" },
            { "meta_d03_flower_bed", "meta_d11_flower_beds" },
            { "meta_d03_streetlamp", "meta_d06_streetlamp" },
            { "meta_d03_parking", "meta_d06_parking" },
            { "meta_d03_carts", "meta_d06_cart_row" },
            { "meta_d03_auto_doors", "meta_d05_auto_doors" },
            { "meta_d03_facade_sign", "meta_d05_facade_sign" },
            { "meta_d04_benches", "meta_d06_bench_out" },
            { "meta_d04_greens", "meta_d11_flower_beds" },
            { "meta_d04_van", "meta_d08_truck" },
            { "meta_d04_terrace", "meta_d09_cafe_tables" },
            { "meta_d04_canopy", "meta_d05_awning_green" },
            { "meta_d04_sign_panel", "meta_d10_billboard" },
        };

        /// <summary>Чем заменить иконку улучшения, если своей ещё нет (null — замены нет).</summary>
        public static string IconFor(string id) => IconAlias.TryGetValue(id, out var s) ? s : null;

        // ------------------------------------------------------------------ альбом

        public static readonly string[] DepartmentNames =
            { "Фрукты", "Овощи", "Молочка", "Выпечка", "Сыры и колбасы", "Рыба", "Сладости", "Напитки", "Заморозка", "Бахча и сезонное" };

        static readonly string[][] DeptStickers =
        {
            new[] { "item_apple", "item_banana", "item_pear", "item_orange", "sticker_grapes", "sticker_strawberry", "sticker_lemon", "sticker_peach", "sticker_kiwi" },
            new[] { "item_carrot", "item_cucumber", "item_cabbage", "item_tomato", "sticker_potato", "sticker_onion", "sticker_bell_pepper", "sticker_broccoli", "sticker_corn" },
            new[] { "item_milk", "item_yogurt", "item_eggs", "sticker_butter", "sticker_kefir", "sticker_cottage_cheese", "sticker_sour_cream", "sticker_milkshake", "sticker_cream_bottle" },
            new[] { "item_bread", "item_baguette", "item_croissant", "sticker_cinnamon_bun", "sticker_pretzel", "sticker_donut", "sticker_pie", "sticker_bagel", "sticker_muffin" },
            new[] { "item_cheese", "item_sausage", "sticker_ham", "sticker_salami", "sticker_cheese_wheel", "sticker_mozzarella", "sticker_sandwich", "sticker_bacon", "sticker_blue_cheese" },
            new[] { "item_fish", "item_shrimp", "sticker_salmon", "sticker_crab", "sticker_caviar", "sticker_squid", "sticker_mussels", "sticker_fish_can", "sticker_lobster" },
            new[] { "item_candy", "item_cake", "item_chocolate", "sticker_lollipop", "sticker_cookie", "sticker_marshmallow", "sticker_jelly_jar", "sticker_cupcake", "sticker_gingerbread" },
            new[] { "item_juice", "item_water", "sticker_lemonade", "sticker_tea_box", "sticker_coffee_pack", "sticker_cocoa_can", "sticker_soda", "sticker_compote", "sticker_apple_juice" },
            new[] { "item_ice_cream", "item_dumplings", "item_pizza", "sticker_popsicle", "sticker_frozen_berries", "sticker_frozen_veg", "sticker_ice_cubes", "sticker_fish_sticks", "sticker_sorbet" },
            new[] { "item_watermelon", "item_pumpkin", "sticker_melon", "sticker_zucchini", "sticker_eggplant", "sticker_mushrooms", "sticker_honey", "sticker_jam", "sticker_pineapple" },
        };

        /// <summary>Район, с которого открывается отдел альбома.</summary>
        // район, с которого отдел есть в пачках: как появляются его товары (темп v2)
        public static readonly int[] DepartmentDistrict = { 1, 1, 1, 1, 1, 2, 2, 4, 2, 3 };

        public static string[] StickersOf(int dept) => DeptStickers[dept];
        public static string GoldId(int dept) => "gold_" + dept;
        public static int DepartmentCount => DeptStickers.Length;

        public static string StickerName(string id)
        {
            if (id.StartsWith("item_"))
            {
                int t = ItemCatalog.IndexOf(id.Substring(5));
                if (t >= 0) return ItemCatalog.Get(t).NameRu;
            }
            if (id.StartsWith("gold_")) return "Золотая наклейка";
            return StickerNamesRu.TryGetValue(id, out var n) ? n : id;
        }

        static readonly Dictionary<string, string> StickerNamesRu = new Dictionary<string, string>
        {
            { "sticker_grapes", "Виноград" }, { "sticker_strawberry", "Клубника" }, { "sticker_lemon", "Лимон" }, { "sticker_peach", "Персик" },
            { "sticker_kiwi", "Киви" }, { "sticker_potato", "Картофель" }, { "sticker_onion", "Лук" }, { "sticker_bell_pepper", "Перец" },
            { "sticker_broccoli", "Брокколи" }, { "sticker_corn", "Кукуруза" }, { "sticker_butter", "Масло" }, { "sticker_kefir", "Кефир" },
            { "sticker_cottage_cheese", "Творог" }, { "sticker_sour_cream", "Сметана" }, { "sticker_milkshake", "Коктейль" },
            { "sticker_cream_bottle", "Сливки" }, { "sticker_cinnamon_bun", "Булочка с корицей" }, { "sticker_pretzel", "Крендель" },
            { "sticker_donut", "Пончик" }, { "sticker_pie", "Пирог" }, { "sticker_bagel", "Бублик" }, { "sticker_muffin", "Маффин" },
            { "sticker_ham", "Окорок" }, { "sticker_salami", "Салями" }, { "sticker_cheese_wheel", "Головка сыра" },
            { "sticker_mozzarella", "Моцарелла" }, { "sticker_sandwich", "Сэндвич" }, { "sticker_bacon", "Бекон" },
            { "sticker_blue_cheese", "Сыр с плесенью" }, { "sticker_salmon", "Лосось" }, { "sticker_crab", "Краб" },
            { "sticker_caviar", "Икра" }, { "sticker_squid", "Кальмар" }, { "sticker_mussels", "Мидии" }, { "sticker_fish_can", "Консервы" },
            { "sticker_lobster", "Лобстер" }, { "sticker_lollipop", "Леденец" }, { "sticker_cookie", "Печенье" },
            { "sticker_marshmallow", "Зефир" }, { "sticker_jelly_jar", "Мармелад" }, { "sticker_cupcake", "Капкейк" },
            { "sticker_gingerbread", "Пряник" }, { "sticker_lemonade", "Лимонад" }, { "sticker_tea_box", "Чай" },
            { "sticker_coffee_pack", "Кофе" }, { "sticker_cocoa_can", "Какао" }, { "sticker_soda", "Газировка" },
            { "sticker_compote", "Компот" }, { "sticker_apple_juice", "Яблочный сок" }, { "sticker_popsicle", "Эскимо" },
            { "sticker_frozen_berries", "Ягоды" }, { "sticker_frozen_veg", "Овощная смесь" }, { "sticker_ice_cubes", "Лёд" },
            { "sticker_fish_sticks", "Рыбные палочки" }, { "sticker_sorbet", "Сорбет" }, { "sticker_melon", "Дыня" },
            { "sticker_zucchini", "Кабачок" }, { "sticker_eggplant", "Баклажан" }, { "sticker_mushrooms", "Грибы" },
            { "sticker_honey", "Мёд" }, { "sticker_jam", "Варенье" }, { "sticker_pineapple", "Ананас" },
        };

        // ------------------------------------------------------------------ покупки (ГДД 11.1)

        // Экономика v2 (30.09.2026): стартовый набор больше не включает «Без рекламы» (он отъедал её продажи),
        // зато в нём эксклюзивная тележка. Темы стали наборами скинов: фон + тележка + конвейер + стеллажи —
        // надеваются в «Гардеробе». «Золотой магазин» — в большой карточке после покупки стартового.
        public static readonly ProductInfo[] Products =
        {
            new ProductInfo { Id = "starter_pack", Title = "Стартовый набор", Description = "1500 монет + 10 отмен + 5 подсказок + тележка в цветах",
                              Icon = "shop_hero_starter", Type = ProductType.Permanent, Coins = 1500, Undo = 10, Hint = 5,
                              Cosmetics = new[] { "cart_flowers" }, FallbackPrice = "99 ₽" },
            new ProductInfo { Id = "no_ads", Title = "Без рекламы", Description = "Отключает рекламу между уровнями и баннер навсегда + 500 монет",
                              Icon = "shop_hero_noads", Type = ProductType.Permanent, NoAds = true, Coins = 500, FallbackPrice = "199 ₽" },
            new ProductInfo { Id = "coins_s", Title = "Горстка монет", Description = "600 монет", Icon = "shop_coin_s", Type = ProductType.Consumable, Coins = 600, FallbackPrice = "49 ₽" },
            new ProductInfo { Id = "coins_m", Title = "Мешок монет", Description = "2000 монет (+15%)", Icon = "shop_coin_m", Type = ProductType.Consumable, Coins = 2000, FallbackPrice = "129 ₽" },
            new ProductInfo { Id = "coins_l", Title = "Сундук монет", Description = "5500 монет (+30%)", Icon = "shop_coin_l", Type = ProductType.Consumable, Coins = 5500, FallbackPrice = "299 ₽" },
            new ProductInfo { Id = "coins_xl", Title = "Гора монет", Description = "13000 монет (+50%)", Icon = "shop_coin_xl", Type = ProductType.Consumable, Coins = 13000, FallbackPrice = "599 ₽" },
            // копилка (ГДД v3, п. 2.5): монеты берутся из копилки игрока, а не из товара (GameApp.GrantProduct)
            new ProductInfo { Id = Piggy.ProductId, Title = "Копилка енота", Description = "Все монеты из копилки (до 4000)",
                              Icon = "piggy_full", Type = ProductType.Consumable, FallbackPrice = "99 ₽" },
            // алмазы (v4, 03.10.2026): 6 наборов, выгода растёт с ценой; картинка самого большого — gem_pack_6 (ждём)
            new ProductInfo { Id = "gems_30", Title = "Горсть алмазов", Description = "30 алмазов", Icon = "gem_pack_1", Type = ProductType.Consumable, Gems = 30, FallbackPrice = "29 ₽" },
            new ProductInfo { Id = "gems_90", Title = "Мешочек алмазов", Description = "90 алмазов (+10 %)", Icon = "gem_pack_2", Type = ProductType.Consumable, Gems = 90, FallbackPrice = "79 ₽" },
            new ProductInfo { Id = "gems_180", Title = "Миска алмазов", Description = "180 алмазов (+17 %)", Icon = "gem_pack_3", Type = ProductType.Consumable, Gems = 180, FallbackPrice = "149 ₽" },
            new ProductInfo { Id = "gems_400", Title = "Сундучок алмазов", Description = "400 алмазов (+29 %)", Icon = "gem_pack_4", Type = ProductType.Consumable, Gems = 400, FallbackPrice = "299 ₽" },
            new ProductInfo { Id = "gems_850", Title = "Сундук алмазов", Description = "850 алмазов (+37 %)", Icon = "gem_pack_5", Type = ProductType.Consumable, Gems = 850, FallbackPrice = "599 ₽" },
            new ProductInfo { Id = "gems_1800", Title = "Тележка алмазов", Description = "1800 алмазов (+46 %)", Icon = "gem_pack_6", Type = ProductType.Consumable, Gems = 1800, FallbackPrice = "1190 ₽" },
            new ProductInfo { Id = "gold_path", Title = "Золотой путь", Description = "Вторая строка наград «Звёздного пути» навсегда: Кот-управляющий, алмазы, молотки, скины",
                              Icon = "gold_path_card", Type = ProductType.Permanent, GoldPath = true, FallbackPrice = "299 ₽" },
            new ProductInfo { Id = "house_boost", Title = "Прораб", Description = "3 молотка (стройка сразу) + 60 алмазов", Icon = "icon_hammer_gold",
                              Type = ProductType.Consumable, Hammers = 3, Gems = 60, FallbackPrice = "149 ₽" },
            new ProductInfo { Id = "helpers_pack", Title = "Набор помощника", Description = "10 отмен + 5 подсказок", Icon = "shop_helpers", Type = ProductType.Consumable, Undo = 10, Hint = 5, FallbackPrice = "79 ₽" },
            new ProductInfo { Id = "undo_24h", Title = "Отмены без лимита", Description = "Безлимитные отмены на 24 часа", Icon = "shop_undo24h", Type = ProductType.Consumable, UndoUnlimited24h = true, FallbackPrice = "49 ₽" },
            new ProductInfo { Id = "theme_night", Title = "Ночной магазин", Description = "Ночной фон, две тележки, неоновый конвейер, стеллажи супермаркета",
                              Icon = "shop_set_night", Type = ProductType.Permanent, Theme = "night",
                              Cosmetics = new[] { "scene_night", "cart_night", "cart_neon", "belt_neon", "shelf_metal" }, FallbackPrice = "149 ₽" },
            new ProductInfo { Id = "theme_winter", Title = "Новогодняя ярмарка", Description = "Зимний фон, новогодние тележка, конвейер и стеллажи",
                              Icon = "shop_set_winter", Type = ProductType.Permanent, Theme = "winter",
                              Cosmetics = new[] { "scene_winter", "cart_winter", "belt_winter", "shelf_winter" }, FallbackPrice = "149 ₽" },
            new ProductInfo { Id = "theme_farm", Title = "Фермерский рынок", Description = "Фермерский фон и тележка, деревянный конвейер, берёзовые стеллажи",
                              Icon = "shop_set_farm", Type = ProductType.Permanent, Theme = "farm",
                              Cosmetics = new[] { "scene_farm", "cart_farm", "belt_wood", "shelf_birch" }, FallbackPrice = "129 ₽" },
            new ProductInfo { Id = "set_gold", Title = "Золотой магазин", Description = "Золотые тележка, конвейер и стеллажи + 3000 монет",
                              Icon = "shop_set_gold", Type = ProductType.Permanent, Coins = 3000,
                              Cosmetics = new[] { "cart_gold", "belt_gold", "shelf_gold" }, FallbackPrice = "299 ₽" },
            // новые товары магазина (01.10.2026) — завести в консоли Яндекса с этими ID
            new ProductInfo { Id = "helpers_big", Title = "Большой набор", Description = "25 отмен + 15 подсказок",
                              Icon = "shop_helpers_big", Type = ProductType.Consumable, Undo = 25, Hint = 15, FallbackPrice = "179 ₽" },
            new ProductInfo { Id = "builder_pack", Title = "Набор строителя", Description = "4000 монет + 10 отмен + 5 подсказок",
                              Icon = "shop_pack_builder", Type = ProductType.Consumable, Coins = 4000, Undo = 10, Hint = 5, FallbackPrice = "199 ₽" },
            new ProductInfo { Id = "stickers_pack", Title = "Набор коллекционера", Description = "10 пачек наклеек + золотая наклейка",
                              Icon = "shop_pack_collector", Type = ProductType.Consumable, Packs = 10, GoldPacks = 1, FallbackPrice = "99 ₽" },
            // последним: «откуда скин» (SourceOf) должен показывать отдельный набор, а не общий
            new ProductInfo { Id = "sets_all", Title = "Все оформления", Description = "Фермерский, новогодний, ночной и золотой наборы разом",
                              Icon = "shop_set_all", Type = ProductType.Permanent,
                              Cosmetics = new[] { "scene_farm", "cart_farm", "belt_wood", "shelf_birch",
                                                  "scene_winter", "cart_winter", "belt_winter", "shelf_winter",
                                                  "scene_night", "cart_night", "cart_neon", "belt_neon", "shelf_metal",
                                                  "cart_gold", "belt_gold", "shelf_gold" }, FallbackPrice = "399 ₽" },
        };

        /// <summary>Самый маленький пакет монет, которого хватит на недостающее (иначе самый большой).</summary>
        public static ProductInfo CoinPackFor(int need)
        {
            var packs = Products.Where(p => p.Type == ProductType.Consumable && p.Coins > 0 && p.Undo == 0).OrderBy(p => p.Coins).ToList();
            return packs.FirstOrDefault(p => p.Coins >= need) ?? packs.Last();
        }

        public static ProductInfo Product(string id) => Products.FirstOrDefault(p => p.Id == id);
    }
}

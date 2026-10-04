// Сгенерировано Tools/ArtPipeline/reno_preview.py из reno_layout.json — правьте JSON и перегенерируйте.
using System.Collections.Generic;

namespace AllOnShelves.EditorTools
{
    /// <summary>Раскладка сцены ремонта: здание и предметы (низ-центр, координаты 1920×1080 от центра сцены).</summary>
    public static class RenoLayoutData
    {
        public struct Store { public float X, Base, Width; public Store(float x, float b, float w) { X = x; Base = b; Width = w; } }
        public struct Item { public bool Attached; public float X, Y, Height; public Item(bool a, float x, float y, float h) { Attached = a; X = x; Y = y; Height = h; } }

        public static readonly Dictionary<string, Store> Stores = new Dictionary<string, Store>
        {
            { "kiosk", new Store(-340f, -330f, 520f) },
            { "corner", new Store(-340f, -330f, 600f) },
            { "minimarket", new Store(-340f, -330f, 700f) },
            { "supermarket", new Store(-340f, -330f, 860f) },
            { "hypermarket", new Store(-340f, -330f, 880f) },
        };

        /// <summary>Предметы, которых нет в словаре, — только на карточке (нарисованы как весь магазин целиком).</summary>
        public static readonly Dictionary<string, Item> Items = new Dictionary<string, Item>
        {
            { "meta_d01_sign", new Item(true, -610f, -99f, 150f) },
            { "meta_d01_lamp", new Item(true, -330f, -88f, 110f) },
            { "meta_d01_flowers", new Item(false, -483f, -410f, 100f) },
            { "meta_d01_fridge", new Item(false, 30f, -310f, 250f) },
            { "meta_d01_chalkboard", new Item(false, -197f, -410f, 150f) },
            { "meta_d02_bench", new Item(false, 30f, -310f, 130f) },
            { "meta_d02_crates", new Item(false, -483f, -410f, 120f) },
            { "meta_d02_price_board", new Item(false, -197f, -410f, 170f) },
            { "meta_d02_tree_pot", new Item(false, -710f, -310f, 200f) },
            { "meta_d03_bell", new Item(true, -142f, -108f, 90f) },
            { "meta_d04_fridge_wall", new Item(false, 70f, -310f, 250f) },
            { "meta_d04_chest_freezer", new Item(false, -175f, -410f, 130f) },
            { "meta_d04_ceiling_lamps", new Item(true, -340f, -41f, 70f) },
            { "meta_d04_baskets", new Item(false, -505f, -410f, 120f) },
            { "meta_d04_coffee_machine", new Item(false, -750f, -310f, 200f) },
            { "meta_d04_wall_clock", new Item(true, -124f, 14f, 90f) },
            { "meta_d05_veg_stand", new Item(false, -789f, -330f, 190f) },
            { "meta_d05_scales", new Item(false, -147f, -410f, 130f) },
            { "meta_d05_produce_crates", new Item(false, -532f, -410f, 160f) },
            { "meta_d05_trolley_bay", new Item(false, 131f, -330f, 200f) },
            { "meta_d06_bread_baskets", new Item(false, -795f, -330f, 200f) },
            { "meta_d06_cart_row", new Item(false, 152f, -330f, 150f) },
            { "meta_d06_bike_rack", new Item(false, -800f, -310f, 150f) },
            { "meta_d06_bench_out", new Item(false, -532f, -410f, 120f) },
            { "meta_d06_streetlamp", new Item(false, 120f, -310f, 300f) },
            { "meta_d06_cafe_table", new Item(false, -147f, -410f, 130f) },
            { "meta_d07_fish_counter", new Item(false, -813f, -330f, 150f) },
            { "meta_d07_aquarium", new Item(false, 148f, -330f, 150f) },
            { "meta_d07_gates", new Item(false, -103f, -410f, 140f) },
            { "meta_d07_balloon_arch", new Item(false, -576f, -410f, 230f) },
            { "meta_d07_ceiling_banners", new Item(true, -340f, -7f, 90f) },
            { "meta_d08_racks", new Item(false, -799f, -330f, 230f) },
            { "meta_d08_forklift", new Item(false, -103f, -410f, 170f) },
            { "meta_d08_loading_belt", new Item(false, -793f, -310f, 130f) },
            { "meta_d08_pallet_stack", new Item(false, -576f, -410f, 150f) },
            { "meta_d08_truck", new Item(false, 118f, -330f, 190f) },
            { "meta_d08_box_tower", new Item(false, 181f, -310f, 220f) },
            { "meta_d08_hand_truck", new Item(false, -276f, -440f, 150f) },
            { "meta_d09_cafe_counter", new Item(false, -791f, -330f, 190f) },
            { "meta_d09_cafe_tables", new Item(false, -576f, -410f, 160f) },
            { "meta_d09_flower_corner", new Item(false, 141f, -330f, 190f) },
            { "meta_d09_coffee_bar", new Item(false, -802f, -290f, 190f) },
            { "meta_d09_kids_ride", new Item(false, -103f, -410f, 130f) },
            { "meta_d09_ice_cream_stand", new Item(false, 143f, -310f, 200f) },
            { "meta_d09_juice_bar", new Item(false, 145f, -290f, 190f) },
            { "meta_d09_plant_wall", new Item(false, -806f, -310f, 200f) },
            { "meta_d09_sofa", new Item(false, -404f, -440f, 100f) },
            { "meta_d10_night_sign", new Item(true, -340f, 134f, 150f) },
            { "meta_d10_billboard", new Item(true, -657f, 101f, 170f) },
            { "meta_d10_street_lamps", new Item(false, -788f, -310f, 280f) },
            { "meta_d10_fountain", new Item(false, -788f, -330f, 200f) },
            { "meta_d10_clock_tower", new Item(false, -810f, -290f, 320f) },
            { "meta_d10_parked_cars", new Item(false, 127f, -330f, 150f) },
            { "meta_d10_taxi", new Item(false, -98f, -410f, 130f) },
            { "meta_d10_light_trees", new Item(false, 120f, -310f, 220f) },
            { "meta_d11_food_stalls", new Item(false, -768f, -330f, 140f) },
            { "meta_d11_food_tables", new Item(false, -582f, -410f, 150f) },
            { "meta_d11_park_gate", new Item(false, -752f, -290f, 190f) },
            { "meta_d11_carousel", new Item(false, 144f, -330f, 260f) },
            { "meta_d11_playground", new Item(false, 126f, -290f, 220f) },
            { "meta_d11_bandstand", new Item(false, 129f, -310f, 200f) },
            { "meta_d11_bike_path", new Item(false, -406f, -440f, 100f) },
            { "meta_d11_flower_beds", new Item(false, -98f, -410f, 130f) },
            { "meta_d11_statue", new Item(false, -805f, -310f, 220f) },
            { "meta_d11_fireworks", new Item(false, -274f, -440f, 130f) },
        };
    }
}

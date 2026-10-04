using System.Collections.Generic;
using AllOnShelves.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>Все спрайты игры по имени файла. Заполняется меню «Всё по полкам → Обновить библиотеку арта».</summary>
    [CreateAssetMenu(menuName = "Всё по полкам/Art Library")]
    public class ArtLibrary : ScriptableObject
    {
        public Sprite[] sprites = new Sprite[0];
        public TMPro.TMP_FontAsset font;

        static ArtLibrary _i;
        Dictionary<string, Sprite> _map;

        public static ArtLibrary I
        {
            get
            {
                if (_i == null) _i = Resources.Load<ArtLibrary>("ArtLibrary");
                return _i;
            }
        }

        public Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>();
                foreach (var s in sprites) if (s != null) _map[s.name] = s;
            }
            return _map.TryGetValue(name, out var sp) ? sp : null;
        }

        public static Sprite S(string name) => I != null ? I.Get(name) : null;
        public static Sprite Item(int type)
        {
            if (type < 0) return null;
            string id = ItemCatalog.Get(type).Id;
            // надетый вид товаров («Праздничные товары»): item_apple_gift; нет картинки — обычная
            string suffix = ItemSkinSuffix();
            if (suffix.Length > 0)
            {
                var skin = S($"item_{id}_{suffix}");
                if (skin != null) return skin;
            }
            return S("item_" + id);
        }

        static string ItemSkinSuffix()
        {
            var app = GameApp.I;
            if (app == null || string.IsNullOrEmpty(app.Save.wearItems)) return "";
            var c = MetaCatalog.Cosmetic(app.Save.wearItems);
            return c != null && c.ArtReady ? c.Suffix : "";
        }

        public static Sprite Sticker(string id)
        {
            if (id.StartsWith("gold_"))
            {
                int dept = int.Parse(id.Substring(5));
                return S(MetaCatalog.StickersOf(dept)[0]);
            }
            return S(id);
        }

        static readonly string[] CustomerLooks = { "bunny", "cat", "fox", "hedgehog", "dog", "panda" };
        public static Sprite Customer(int look, bool happy) =>
            S($"chr_customer_{CustomerLooks[Mathf.Abs(look) % CustomerLooks.Length]}_{(happy ? "happy" : "wait")}");

        /// <summary>Ставит картинке родные пропорции: вписывает её в квадрат со стороной box.</summary>
        public static void Fit(Image img, float box)
        {
            if (img == null || img.sprite == null) return;
            var r = img.sprite.rect;
            float k = Mathf.Min(box / r.width, box / r.height);
            img.rectTransform.sizeDelta = new Vector2(r.width * k, r.height * k);
        }

        /// <summary>Цвета ярлыков секций по отделам.</summary>
        public static Color DepartmentColor(Department d)
        {
            switch (d)
            {
                case Department.Fruits: return new Color32(0xF2, 0x80, 0x78, 255);
                case Department.Vegetables: return new Color32(0x7C, 0xC4, 0x6A, 255);
                case Department.Dairy: return new Color32(0x86, 0xB8, 0xF0, 255);
                case Department.Bakery: return new Color32(0xF0, 0xC0, 0x7A, 255);
                case Department.Deli: return new Color32(0xF5, 0xD5, 0x4A, 255);
                case Department.Seafood: return new Color32(0x9C, 0x8C, 0xF0, 255);
                case Department.Sweets: return new Color32(0xF5, 0x9C, 0xD0, 255);
                case Department.Drinks: return new Color32(0x6C, 0xD4, 0xD0, 255);
                case Department.Frozen: return new Color32(0xB8, 0xE4, 0xF8, 255);
                default: return new Color32(0xB0, 0xD8, 0x70, 255);
            }
        }
    }
}

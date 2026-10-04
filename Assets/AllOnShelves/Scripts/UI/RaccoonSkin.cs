using System;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Скин енота целиком (ГДД v3, п. 4; 03.10.2026): енот в любой позе (обучение, победа, поражение, ремонт,
    /// загрузка) надевает надетый в «Гардеробе» костюм — картинка chr_raccoon_&lt;поза&gt;_&lt;скин&gt;. Нет такой
    /// картинки — обычный енот. Раньше скин был только головой-аватаркой и путал игроков.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RaccoonSkin : MonoBehaviour
    {
        public string pose = "point";

        public static event Action Changed;
        public static void Refresh() => Changed?.Invoke();

        void OnEnable()
        {
            Changed += Apply;
            Apply();
        }

        void OnDisable() => Changed -= Apply;

        public void Apply()
        {
            var img = GetComponent<Image>();
            var sp = Sprite(pose);
            if (sp != null && img.sprite != sp) { img.sprite = sp; img.preserveAspect = true; }
        }

        /// <summary>Картинка енота в позе с надетым скином (rac_chef → chr_raccoon_point_chef).</summary>
        public static Sprite Sprite(string pose)
        {
            string worn = GameApp.I != null ? GameApp.I.Worn("raccoon") : "";
            if (!string.IsNullOrEmpty(worn) && worn.StartsWith("rac_"))
            {
                var sp = ArtLibrary.S($"chr_raccoon_{pose}_{worn.Substring(4)}");
                if (sp != null) return sp;
            }
            return ArtLibrary.S("chr_raccoon_" + pose);
        }
    }
}

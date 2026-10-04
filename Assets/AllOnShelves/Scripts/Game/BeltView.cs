using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Лента завоза. Позиции товаров считаются по ширине ленты и видимости V.</summary>
    public class BeltView : MonoBehaviour
    {
        public RectTransform rect;        // область ленты (товары стоят на ней)
        public RectTransform hatch;       // люк справа
        public RectTransform windowFrame; // подсветка окна доступа
        public float itemSize = 120f;
        [OptionalRef] public Image body;  // картинка ленты — меняется скином конвейера

        Sprite _baseSprite;
        Vector2 _baseSize;
        Image.Type _baseType;
        float _baseMult;
        bool _baseSaved;

        /// <summary>
        /// Скин конвейера (30.09.2026). Лента скина — плитка в родных пропорциях, повторяется по длине
        /// (растягивать узор нельзя); толщина полосы — как у обычной ленты. null — обычная лента.
        /// </summary>
        public void SetSkin(Sprite skin)
        {
            if (body == null) return;
            var rt = body.rectTransform;
            if (!_baseSaved)
            {
                _baseSaved = true;
                _baseSprite = body.sprite; _baseSize = rt.sizeDelta; _baseType = body.type; _baseMult = body.pixelsPerUnitMultiplier;
            }
            if (skin == null)
            {
                body.sprite = _baseSprite; body.type = _baseType; body.pixelsPerUnitMultiplier = _baseMult; rt.sizeDelta = _baseSize;
                return;
            }
            // у обычной ленты полоса занимает ~72 % высоты картинки: той же толщины делаем и скин
            float h = _baseSize.y * 0.72f;
            body.sprite = skin;
            body.type = Image.Type.Tiled;
            body.pixelsPerUnitMultiplier = skin.rect.height / h * (100f / skin.pixelsPerUnit);
            rt.sizeDelta = new Vector2(_baseSize.x, h);
        }

        int _visible = 8;
        int _window = 3;
        float _scroll;

        public void Configure(int visible, int window)
        {
            _visible = Mathf.Max(1, visible);
            _window = window;
            UpdateWindowFrame();
        }

        // шаг — по числу мест, а не по числу видимых товаров: иначе в одном уровне 5 товаров на всю ленту,
        // а в другом 9 (03.10.2026)
        public float Spacing => rect.rect.width / (Core.LevelState.BeltSlots + 0.4f);

        public Vector3 SlotWorld(int index)
        {
            float x = rect.rect.xMin + Spacing * (0.7f + index);
            var local = new Vector3(x, 0f, 0f);
            return rect.TransformPoint(local);
        }

        public Vector3 HatchWorld => hatch != null ? hatch.position : SlotWorld(Core.LevelState.BeltSlots + 1);

        void UpdateWindowFrame()
        {
            if (windowFrame == null) return;
            float w = Spacing * _window;
            windowFrame.sizeDelta = new Vector2(w + 10f, windowFrame.sizeDelta.y);
            windowFrame.anchoredPosition = new Vector2(rect.rect.xMin + Spacing * 0.2f + w / 2f - 5f + rect.anchoredPosition.x, windowFrame.anchoredPosition.y);
        }

        /// <summary>Лёгкий «толчок» ленты при сдвиге.</summary>
        public void Step()
        {
            _scroll += 1f;
            Tween.Punch(rect, 0.015f, 0.18f);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Подложка под «книгой» страниц магазина и рейтинга (01.10.2026). На широком экране по бокам видна
    /// приглушённая копия страницы (окно, ящики с фруктами — без кнопок). На экране уже 16:9 (4:3, 16:10)
    /// полосы сверху и снизу: там копия показала бы вторые, тусклые вкладки, поэтому — ровный цвет.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class PageBackdrop : MonoBehaviour
    {
        public Sprite page;
        public Color dim = new Color(0.55f, 0.5f, 0.47f);
        public Color flat = new Color(0.33f, 0.22f, 0.15f);

        Image _img;
        bool? _wide;

        void OnEnable() { _wide = null; Apply(); }
        void OnRectTransformDimensionsChange() => Apply();

        void Apply()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            if (_img == null) _img = GetComponent<Image>();
            var size = parent.rect.size;
            bool wide = size.y <= 0f || size.x / size.y >= 16f / 9f - 0.01f;
            if (_wide == wide) return;
            _wide = wide;
            _img.sprite = wide ? page : null;
            _img.color = wide ? dim : flat;
        }
    }
}

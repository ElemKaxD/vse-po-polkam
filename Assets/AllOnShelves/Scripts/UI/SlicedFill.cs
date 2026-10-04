using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Заливка полосы без растяжения (03.10.2026). Режим Image «Filled» не умеет 9-slice: закруглённые концы
    /// ui_bar_fill тянулись вместе с серединой, и полоса выглядела кривой (жалоба игрока на полосу стройки).
    /// Здесь картинка остаётся Sliced, а заполнение задаёт ширину: код по-прежнему пишет image.fillAmount.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class SlicedFill : MonoBehaviour
    {
        public float fullWidth;

        Image _img;
        RectTransform _rt;
        float _shown = -1f;

        /// <summary>Сборка сцены: заливку — в режим Sliced, точку опоры — к левому краю (полоса растёт вправо).</summary>
        public static void Attach(Image img)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            float w = rt.sizeDelta.x;
            if (rt.pivot.x != 0f)
            {
                rt.anchoredPosition -= new Vector2(w * rt.pivot.x, 0f);
                rt.pivot = new Vector2(0f, rt.pivot.y);
            }
            var c = img.GetComponent<SlicedFill>();
            if (c == null) c = img.gameObject.AddComponent<SlicedFill>();
            c.fullWidth = w;
            img.type = Image.Type.Sliced;
        }

        void LateUpdate()
        {
            if (_img == null) { _img = GetComponent<Image>(); _rt = (RectTransform)transform; }
            float f = Mathf.Clamp01(_img.fillAmount);
            if (Mathf.Approximately(f, _shown)) return;
            _shown = f;
            // меньше высоты заливка не бывает — иначе круглые концы сплющатся; пустая — не видна
            float h = Mathf.Min(_rt.sizeDelta.y, fullWidth);
            _img.enabled = f > 0.001f;
            _rt.sizeDelta = new Vector2(h + (fullWidth - h) * f, _rt.sizeDelta.y);
        }
    }
}

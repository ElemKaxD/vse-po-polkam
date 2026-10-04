using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Праздник в интерфейсе (02.10.2026): падающее конфетти — то же, что в окне победы (его пользователь
    /// оставил как единственный эффект победы), и тёплая вспышка экрана. Рисуется на слое эффектов поверх всех экранов.
    /// </summary>
    public static class Celebration
    {
        static readonly string[] Shapes = { "fx_confetti_a", "fx_confetti_b", "fx_confetti_c" };
        static readonly Color[] Colors =
        {
            new Color(0.95f, 0.4f, 0.24f), new Color(1f, 0.78f, 0.24f), new Color(0.36f, 0.77f, 0.42f),
            new Color(0.53f, 0.72f, 0.94f), new Color(0.96f, 0.61f, 0.82f),
        };

        /// <summary>Конфетти сверху экрана. delay — задержка волны (две волны подряд выглядят пышнее).</summary>
        public static void Confetti(int count = 50, float delay = 0f)
        {
            var root = Vfx.Layer;
            if (root == null) return;
            var size = root.rect.size;
            float scale = root.lossyScale.x;
            for (int i = 0; i < count; i++)
            {
                var sp = ArtLibrary.S(Shapes[i % Shapes.Length]);
                if (sp == null) return;
                var go = new GameObject("fx_confetti", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.sprite = sp; img.raycastTarget = false; img.color = Colors[i % Colors.Length];
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                float s = 26f + Random.value * 18f;
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = new Vector2((Random.value - 0.5f) * size.x, size.y * 0.5f + 40f);
                rt.localRotation = Quaternion.Euler(0, 0, Random.value * 360f);
                var to = rt.position + new Vector3((Random.value - 0.5f) * 360f, -(size.y + 120f + Random.value * 300f), 0f) * scale;
                Tween.Move(rt, to, 1.8f + Random.value * 1.1f, Ease.InQuad, 0f, () => Object.Destroy(go), delay + Random.value * 0.5f);
            }
        }

        /// <summary>Мягкая тёплая вспышка на весь экран (без резкого белого).</summary>
        public static void Flash(float peak = 0.35f)
        {
            var root = Vfx.Layer;
            if (root == null) return;
            var go = new GameObject("fx_flash", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();   // под остальными эффектами
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.92f, 0.65f, 0f);
            Tween.Fade(img, peak, 0.15f, () => Tween.Fade(img, 0f, 0.7f, () => Object.Destroy(go)));
        }
    }
}
